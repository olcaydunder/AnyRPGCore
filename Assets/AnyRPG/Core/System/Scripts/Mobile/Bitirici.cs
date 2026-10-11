using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Bitirici vuruş (Ötüken): oyuncu düşmana vurdukça öfkesi dolar (vuruş başına 10, kritikte 18; 100'de dolu).
    /// Dolunca ekrandaki "Bitir" düğmesiyle kurulur: 8 saniye içindeki ilk vuruş 3 kat vurur ve kritik gibi görünür,
    /// öfke sıfırlanır. Kurulup kullanılmazsa öfke dolu kalır, yeniden kurulabilir. Oto Av açıkken kendiliğinden kullanılır.
    /// Hesap yetkili tarafta yapılır (tek oyunculu oyunda telefonda, çevrimiçinde sunucuda:
    /// CharacterCombat.TakeDamageCommon → Vurus). Çevrimiçinde sunucu öfkeyi oyuncunun telefonuna bildirir ("ofke"),
    /// kurma isteği sunucuya gider ("bitirici").
    /// </summary>
    public static class Bitirici {

        public const int Dolu = 100;
        public const float Carpan = 3f;
        public const float KuruluSuresi = 8f;
        private const int VurusPayi = 10;
        private const int KritikPayi = 18;

        private class Durum {
            public int ofke = 0;
            public float kuruluBitis = -1f;
        }

        // yetkili taraf: oyuncu başına
        private static readonly Dictionary<UnitController, Durum> durumlar = new Dictionary<UnitController, Durum>();
        // çevrimiçi istemci: sunucunun bildirdiği
        private static int istemciOfke = 0;
        private static float istemciKuruluBitis = -1f;

        /// <summary>bu oturumda atılan bitirici vuruş sayısı (oyun testi okur)</summary>
        public static int KullanilanSayisi = 0;

        private static Durum Al(UnitController oyuncu) {
            Durum d;
            if (durumlar.TryGetValue(oyuncu, out d) == false) {
                if (durumlar.Count > 300) {
                    // uzun süre açık sunucuda çıkan oyuncuların kayıtları birikmesin
                    durumlar.Clear();
                }
                d = new Durum();
                durumlar[oyuncu] = d;
            }
            return d;
        }

        private static bool Istemci() {
            SystemGameManager oyun = OtukenAg.Oyun;
            return oyun != null && oyun.GameMode == GameMode.Network && OtukenAg.Sunucuda == false;
        }

        private static UnitController YerelOyuncu() {
            SystemGameManager oyun = OtukenAg.Oyun;
            return oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
        }

        /// <summary>öfkenin doluluğu, 0–1 (ekrandaki düğme)</summary>
        public static float Doluluk(UnitController oyuncu) {
            if (oyuncu == null) {
                return 0f;
            }
            if (Istemci()) {
                return Mathf.Clamp01(istemciOfke / (float)Dolu);
            }
            Durum d;
            return durumlar.TryGetValue(oyuncu, out d) ? Mathf.Clamp01(d.ofke / (float)Dolu) : 0f;
        }

        /// <summary>bitirici kuruldu mu (sıradaki vuruş bitirici olacak)</summary>
        public static bool Kurulu(UnitController oyuncu) {
            if (oyuncu == null) {
                return false;
            }
            if (Istemci()) {
                return Time.time < istemciKuruluBitis;
            }
            Durum d;
            return durumlar.TryGetValue(oyuncu, out d) && Time.time < d.kuruluBitis;
        }

        /// <summary>
        /// yetkili taraf, oyuncunun bir canavara her hasarlı vuruşunda: bitirici kuruluysa true döner (hasar çarpılır),
        /// değilse öfkeyi doldurur
        /// </summary>
        public static bool Vurus(UnitController vuran, UnitController hedef, bool kritik) {
            if (vuran == null || hedef == null || vuran.UnitControllerMode != UnitControllerMode.Player
                || hedef.UnitControllerMode != UnitControllerMode.AI) {
                return false;
            }
            Durum d = Al(vuran);
            if (Time.time < d.kuruluBitis) {
                d.kuruluBitis = -1f;
                d.ofke = 0;
                KullanilanSayisi++;
                Bildir(vuran, d);
                return true;
            }
            if (d.ofke < Dolu) {
                d.ofke = Mathf.Min(Dolu, d.ofke + (kritik ? KritikPayi : VurusPayi));
                Bildir(vuran, d);
            }
            return false;
        }

        private static void Bildir(UnitController oyuncu, Durum d) {
            if (OtukenAg.Sunucuda) {
                OtukenAg.Yanitla(oyuncu, "ofke", d.ofke + "|" + (Time.time < d.kuruluBitis ? "1" : "0"));
            }
        }

        /// <summary>yetkili taraf: bitiriciyi kurar; olmazsa nedenini döndürür (olursa null)</summary>
        private static string Kur(UnitController oyuncu) {
            if (oyuncu == null) {
                return "Karakter bulunamadı.";
            }
            Durum d = Al(oyuncu);
            if (Time.time < d.kuruluBitis) {
                return null;
            }
            if (d.ofke < Dolu) {
                return "Öfken dolmadı: düşmana vurdukça dolar.";
            }
            d.kuruluBitis = Time.time + KuruluSuresi;
            Bildir(oyuncu, d);
            return null;
        }

        /// <summary>ekrandaki Bitir düğmesi (ve Oto Av)</summary>
        public static void Kullan() {
            UnitController oyuncu = YerelOyuncu();
            if (oyuncu == null || Kurulu(oyuncu)) {
                return;
            }
            if (Istemci()) {
                if (istemciOfke < Dolu) {
                    OtukenAg.Mesaj(oyuncu, "Öfken dolmadı: düşmana vurdukça dolar.");
                } else if (OtukenAg.Gonder("bitirici") == false) {
                    OtukenAg.Mesaj(oyuncu, "Sunucuya ulaşılamadı, biraz sonra yeniden dene.");
                }
                return;
            }
            string hata = Kur(oyuncu);
            if (hata != null) {
                OtukenAg.Mesaj(oyuncu, hata);
                return;
            }
            Kuruldu(oyuncu);
        }

        private static void Kuruldu(UnitController oyuncu) {
            OtukenAg.Mesaj(oyuncu, "<color=#FF9A2E>Bitirici hazır: sıradaki vuruşun 3 kat vurur!</color>");
            Efektler.BitiriciHazir(oyuncu);
            MobileFeedback.Medium();
        }

        private static float sonOtoDeneme = -10f;

        /// <summary>MobileBootstrap saniyede bir çağırır: Oto Av açıkken (becerileri kullanıyorsa) dolan bitiriciyi savaşta kendisi kurar</summary>
        public static void Tick(SystemGameManager oyun, bool oyunda) {
            if (oyunda == false || OtomatikAv.Acik == false || OtomatikAv.BeceriKullan == false) {
                return;
            }
            UnitController oyuncu = YerelOyuncu();
            if (oyuncu == null || oyuncu.CharacterCombat == null || oyuncu.CharacterCombat.GetInCombat() == false) {
                return;
            }
            if (Doluluk(oyuncu) >= 1f && Kurulu(oyuncu) == false && Time.unscaledTime - sonOtoDeneme > 3f) {
                sonOtoDeneme = Time.unscaledTime;
                Kullan();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("bitirici", (oyuncu, veri) => {
                string hata = Kur(oyuncu);
                OtukenAg.Yanitla(oyuncu, "bitirici-sonuc", hata ?? string.Empty);
            });
            OtukenAg.IstemciDinle("bitirici-sonuc", veri => {
                UnitController oyuncu = YerelOyuncu();
                if (veri.Length == 0) {
                    istemciKuruluBitis = Time.time + KuruluSuresi;
                    Kuruldu(oyuncu);
                } else {
                    OtukenAg.Mesaj(oyuncu, veri);
                }
            });
            OtukenAg.IstemciDinle("ofke", veri => {
                string[] p = veri.Split('|');
                int ofke;
                if (p.Length < 2 || int.TryParse(p[0], out ofke) == false) {
                    return;
                }
                istemciOfke = Mathf.Clamp(ofke, 0, Dolu);
                if (p[1] != "1") {
                    istemciKuruluBitis = -1f;
                } else if (Time.time >= istemciKuruluBitis) {
                    istemciKuruluBitis = Time.time + KuruluSuresi;
                }
            });
        }
    }
}
