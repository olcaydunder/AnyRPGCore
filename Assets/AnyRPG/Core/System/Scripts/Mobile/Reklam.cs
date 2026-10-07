using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting;

namespace AnyRPG {

    /// <summary>
    /// Ödüllü reklam (Ötüken, AdMob): oyuncu isterse reklam izler, sonuna kadar izlerse ödül alır. Zorla çıkan reklam yoktur.
    ///  - Kut Dükkânı: "Reklam izle · +5 Kut" (günde 5 kez)
    ///  - Sen Yokken: kazancı 2 katına çıkar (günde 3 kez)
    /// Köprü Assets/Plugins/Android/OtukenReklam.java (AdMob + Avrupa'daki oyuncular için Google'ın onay penceresi, UMP).
    /// Kimlikler Resources/Reklam.txt'te. Ödülü çevrimiçinde sunucu verir ("reklam-odul"), günlük sınırı karakter
    /// kaydında (OtukenVeri) tutar; tek oyunculu oyunda aynı kurallar telefonda işler.
    /// </summary>
    public static class Reklam {

        public const string Kut = "kut";
        public const string Kazanc = "kazanc";
        public const int KutOdulu = 5;

        private static readonly Dictionary<string, int> gunlukSinir = new Dictionary<string, int>() {
            { Kut, 5 },
            { Kazanc, 3 },
        };

        public static int Sinir(string amac) {
            int n;
            return gunlukSinir.TryGetValue(amac ?? string.Empty, out n) ? n : 0;
        }

        // ================================================================ kurallar (sunucuda, tek oyunculu oyunda telefonda)

        private static string Bugun {
            get { return Etkinlikler.TurkiyeSaati.ToString("yyyyMMdd", CultureInfo.InvariantCulture); }
        }

        /// <summary>bugün kalan hak</summary>
        public static int Kalan(UnitController oyuncu, string amac) {
            if (oyuncu == null) {
                return 0;
            }
            string[] p = OtukenVeri.Oku(oyuncu, "reklam-" + amac, string.Empty).Split('|');
            int kullanilan = 0;
            if (p.Length == 2 && p[0] == Bugun) {
                int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out kullanilan);
            }
            return Mathf.Max(0, Sinir(amac) - kullanilan);
        }

        /// <summary>hak varsa birini kullanır (true)</summary>
        public static bool HakKullan(UnitController oyuncu, string amac) {
            int kalan = Kalan(oyuncu, amac);
            if (kalan <= 0) {
                return false;
            }
            int kullanilan = Sinir(amac) - kalan + 1;
            OtukenVeri.Yaz(oyuncu, "reklam-" + amac, Bugun + "|" + kullanilan.ToString(CultureInfo.InvariantCulture));
            OtukenVeri.Kaydet();
            YonetimPaneli.ReklamOdulu();
            return true;
        }

        /// <summary>Kut ödülünü verir; olmazsa nedenini döndürür</summary>
        public static string KutVer(UnitController oyuncu) {
            Currency kut = KutDukkani.Kut;
            if (oyuncu == null || kut == null) {
                return "Kut bulunamadı.";
            }
            if (HakKullan(oyuncu, Kut) == false) {
                return "Bugünkü reklam ödüllerin bitti. Yarın yine gel.";
            }
            oyuncu.CharacterCurrencyManager.AddCurrency(kut, KutOdulu);
            Ticaret.Kaydet(oyuncu);
            OtukenAg.Mesaj(oyuncu, "<color=#FFD54A>Reklam ödülü: +" + KutOdulu + " Kut</color>");
            return null;
        }

        private static string Durum(UnitController oyuncu) {
            return Kalan(oyuncu, Kut).ToString(CultureInfo.InvariantCulture) + "|" + Kalan(oyuncu, Kazanc).ToString(CultureInfo.InvariantCulture);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("reklam-odul", (oyuncu, amac) => {
                string hata = amac == Kut ? KutVer(oyuncu) : "Bilinmeyen reklam ödülü.";
                Debug.Log("[Sunucu] " + oyuncu.DisplayName + " reklam ödülü " + amac + ": " + (hata ?? "verildi"));
                OtukenAg.Yanitla(oyuncu, "reklam-sonuc", (hata == null ? "1|" : "0|") + (hata ?? amac));
                OtukenAg.Yanitla(oyuncu, "reklam-durum", Durum(oyuncu));
            });
            OtukenAg.SunucuIsle("reklam-durum", (oyuncu, veri) => OtukenAg.Yanitla(oyuncu, "reklam-durum", Durum(oyuncu)));
            OtukenAg.IstemciDinle("reklam-durum", veri => {
                string[] p = veri.Split('|');
                int k, z;
                if (p.Length >= 2 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out k)
                    && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out z)) {
                    istemciKalan[Kut] = k;
                    istemciKalan[Kazanc] = z;
                }
                Bildir();
            });
            OtukenAg.IstemciDinle("reklam-sonuc", veri => {
                bool iyi = veri.StartsWith("1|");
                SonSonuc = iyi ? "+" + KutOdulu + " Kut" : veri.Substring(Math.Min(2, veri.Length));
                if (iyi) {
                    OdulSayisi++;
                    MobileFeedback.Success();
                }
                Bildir();
            });
        }

        // ================================================================ telefon

        private static readonly Dictionary<string, int> istemciKalan = new Dictionary<string, int>();
        private static readonly Queue<KeyValuePair<string, string>> olaylar = new Queue<KeyValuePair<string, string>>();
        private static bool ayarOkundu = false;
        private static string odulluBirim = string.Empty;
        private static bool baslatildi = false;
        private static Action bekleyenOdul = null;
        private static float yenidenDeneme = -1f;
        private static ReklamKoprusu kopru = null;

        /// <summary>reklam izlenebilir durumda mı (yüklü)</summary>
        public static bool Hazir { get; private set; }
        /// <summary>Avrupa'daki oyuncular için: Ayarlar'da gizlilik seçenekleri gösterilmeli</summary>
        public static bool GizlilikGerekli { get; private set; }
        public static string DurumYazisi { get; private set; } = "Reklam hazırlanıyor...";
        public static string SonSonuc { get; private set; } = "-";
        public static int OdulSayisi { get; private set; }
        /// <summary>reklam durumu ya da kalan hak değişince (pencereler yenilenir)</summary>
        public static event Action Degisti = delegate { };

        private static void Bildir() {
            try {
                Degisti();
            } catch (Exception e) {
                Debug.LogWarning("[Reklam] " + e.Message);
            }
        }

        private static void AyarOku() {
            if (ayarOkundu) {
                return;
            }
            ayarOkundu = true;
            TextAsset t = Resources.Load<TextAsset>("Reklam");
            if (t == null) {
                return;
            }
            foreach (string satir in t.text.Split('\n')) {
                string s = satir.Trim();
                if (s.StartsWith("odullu=")) {
                    odulluBirim = s.Substring("odullu=".Length).Trim();
                }
            }
        }

        public static bool Kurulu {
            get {
                AyarOku();
                return odulluBirim.StartsWith("ca-app-pub-");
            }
        }

        /// <summary>telefonda bilinen kalan hak (çevrimiçinde sunucunun son bildirdiği)</summary>
        public static int IstemciKalan(string amac) {
            if (Cevrimici.Acik) {
                int n;
                return istemciKalan.TryGetValue(amac, out n) ? n : Sinir(amac);
            }
            SystemGameManager o = OtukenAg.Oyun;
            UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            return oyuncu != null ? Kalan(oyuncu, amac) : 0;
        }

        /// <summary>çevrimiçinde kalan hakları sunucudan ister</summary>
        public static void DurumIste() {
            if (Cevrimici.Acik) {
                OtukenAg.Gonder("reklam-durum");
            }
        }

        /// <summary>oyuna girince bir kez: onay penceresi (gerekirse) ve ilk reklamın yüklenmesi</summary>
        public static void Baslat() {
            if (baslatildi || Kurulu == false) {
                return;
            }
            baslatildi = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            KopruKur();
            try {
                using (AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
                    AndroidJavaObject etkinlik = unity.GetStatic<AndroidJavaObject>("currentActivity");
                    using (AndroidJavaClass sinif = new AndroidJavaClass("com.zootopiayazilim.otuken.OtukenReklam")) {
                        sinif.CallStatic("baslat", etkinlik, new Dinleyici(), odulluBirim);
                    }
                }
            } catch (Exception e) {
                DurumYazisi = "Reklam açılamadı.";
                Debug.LogWarning("[Reklam] başlatılamadı: " + e.Message);
            }
#else
            DurumYazisi = "Reklam, Google Play'den indirilen oyunda izlenir.";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Cagir(string yontem) {
            try {
                using (AndroidJavaClass sinif = new AndroidJavaClass("com.zootopiayazilim.otuken.OtukenReklam")) {
                    sinif.CallStatic(yontem);
                }
            } catch (Exception e) {
                Debug.LogWarning("[Reklam] " + yontem + ": " + e.Message);
            }
        }
#endif

        /// <summary>
        /// ödüllü reklamı açar; sonuna kadar izlenirse odul çağrılır (ana iş parçacığında). Açamazsa nedenini döndürür.
        /// </summary>
        public static string Goster(string amac, Action odul) {
            if (Kurulu == false) {
                return "Reklam yakında.";
            }
            if (IstemciKalan(amac) <= 0) {
                return "Bugünkü reklam ödüllerin bitti. Yarın yine gel.";
            }
            Baslat();
            if (Hazir == false) {
                return DurumYazisi;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            bekleyenOdul = odul;
            Hazir = false;
            DurumYazisi = "Reklam açılıyor...";
            Cagir("goster");
            Bildir();
            return null;
#else
            return "Reklam, Google Play'den indirilen oyunda izlenir.";
#endif
        }

        /// <summary>Ayarlar > Hesap: reklam onayını değiştirme (Avrupa'da gerekli)</summary>
        public static void GizlilikSecenekleri() {
#if UNITY_ANDROID && !UNITY_EDITOR
            Baslat();
            Cagir("gizlilikSecenekleri");
#endif
        }

        /// <summary>Kut Dükkânı'ndaki düğme: reklam izlenince Kut ödülü (çevrimiçinde sunucu verir)</summary>
        public static string KutIcinIzle() {
            return Goster(Kut, () => {
                if (Cevrimici.Acik) {
                    OtukenAg.Gonder("reklam-odul", Kut);
                    return;
                }
                SystemGameManager o = OtukenAg.Oyun;
                UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
                string hata = KutVer(oyuncu);
                SonSonuc = hata ?? "+" + KutOdulu + " Kut";
                if (hata == null) {
                    OdulSayisi++;
                    MobileFeedback.Success();
                }
                Bildir();
            });
        }

        /// <summary>ReklamKoprusu her karede çağırır: Java'dan gelen olaylar ana iş parçacığında işlenir</summary>
        public static void Guncelle() {
            while (true) {
                KeyValuePair<string, string> o;
                lock (olaylar) {
                    if (olaylar.Count == 0) {
                        break;
                    }
                    o = olaylar.Dequeue();
                }
                try {
                    OlayIsle(o.Key, o.Value);
                } catch (Exception e) {
                    Debug.LogError("[Reklam] olay işlenemedi (" + o.Key + "): " + e);
                }
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (yenidenDeneme > 0f && Time.unscaledTime >= yenidenDeneme) {
                yenidenDeneme = -1f;
                Cagir("yenidenYukle");
            }
#endif
        }

        private static int yuklemeHatasi = 0;

        private static void OlayIsle(string tur, string veri) {
            switch (tur) {
                case "izin":
                    GizlilikGerekli = veri == "1";
                    break;
                case "hazir":
                    Hazir = true;
                    yuklemeHatasi = 0;
                    DurumYazisi = "Reklam hazır.";
                    break;
                case "yuklenemedi":
                    Hazir = false;
                    yuklemeHatasi++;
                    DurumYazisi = "Şu an reklam yok, biraz sonra yine dene.";
                    // 30 sn, 60 sn, ... en çok 5 dk sonra yeniden
                    yenidenDeneme = Time.unscaledTime + Mathf.Min(300f, 30f * yuklemeHatasi);
                    Debug.Log("[Reklam] yüklenemedi: " + veri);
                    break;
                case "odul":
                    DurumYazisi = "Reklam hazırlanıyor...";
                    Action odul = bekleyenOdul;
                    bekleyenOdul = null;
                    if (odul != null) {
                        odul();
                    }
                    break;
                case "kapandi":
                    bekleyenOdul = null;
                    DurumYazisi = "Reklam sonuna kadar izlenmedi; ödül verilmedi.";
                    SonSonuc = DurumYazisi;
                    break;
                case "gosterilemedi":
                case "yok":
                    bekleyenOdul = null;
                    Hazir = false;
                    DurumYazisi = "Reklam açılamadı, biraz sonra yine dene.";
                    SonSonuc = DurumYazisi;
                    Debug.Log("[Reklam] gösterilemedi: " + veri);
                    break;
                case "hata":
                    Debug.LogWarning("[Reklam] " + veri);
                    break;
            }
            Bildir();
        }

        private class Dinleyici : AndroidJavaProxy {
            public Dinleyici() : base("com.zootopiayazilim.otuken.ReklamDinleyici") {
            }

            [Preserve]
            public void olay(string tur, string veri) {
                lock (olaylar) {
                    olaylar.Enqueue(new KeyValuePair<string, string>(tur ?? string.Empty, veri ?? string.Empty));
                }
            }
        }

        private static void KopruKur() {
            if (kopru != null) {
                return;
            }
            GameObject go = new GameObject("ReklamKoprusu");
            UnityEngine.Object.DontDestroyOnLoad(go);
            kopru = go.AddComponent<ReklamKoprusu>();
        }
    }
}
