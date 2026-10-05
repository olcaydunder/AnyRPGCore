using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnyRPG {

    public enum GunlukGorevTuru { Oldurme, Ganimet, Sandik, Harita, Tas }

    /// <summary>
    /// Günlük Görevler: her gün karakter başına 3 görev çıkar; ertesi gün yenilenir. Tek oyunculu oyunda telefonda sayılır
    /// (cihazın tarihi); çevrimiçi oyunda sunucuda sayılır ve ödülü sunucu verir (Türkiye saati, OtukenAg).
    /// Görevler 5 türden seçilir: düşman yen, ganimet topla, hazine sandığı boşalt, farklı haritalara git,
    /// Ötüken Taşı kır.
    /// Her biri tamamlanınca Gümüş Akçe ve tecrübe, üçü de tamamlanınca büyük ödül (Gök Taşı Parçası) alınır.
    /// İlerleme oyunun kendi olaylarından sayılır:
    ///  - düşman: oyuncunun UnitEventController.OnKillEvent'i (pay alınan her öldürme)
    ///  - ganimet: Ganimet.OnLooted (oyuncunun aldığı her ganimet)
    ///  - sandık: LootableNodeComponent.CheckDropListSize (içi tamamen boşaltılan hazine sandığı)
    ///  - harita: o gün girilen farklı haritalar (Işınlan penceresindeki 15 harita)
    ///  - taş: kırılan Ötüken Taşları (öldürme olayından; taşlar "düşman yen"e sayılmaz)
    /// Sol sütundaki "Günlük" düğmesiyle açılır; ödül bekleyince düğmede altın nokta yanar. Kodla kurulur.
    /// </summary>
    public class GunlukGorevler : MonoBehaviour {

        public const string CanvasName = "GunlukGorevlerCanvas";
        // Işınlan ve Seçenekler pencereleriyle aynı katman
        private const int SortingOrder = 31;
        private const int GorevSayisi = 3;
        private const string BuyukOdulEsyasi = "Gok Tasi Parcasi";
        private const string BuyukOdulEsyaAdi = "Gök Taşı Parçası";
        private const int BuyukOdulGumus = 20;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color rowColor = new Color(0.16f, 0.12f, 0.08f, 0.95f);
        private static readonly Color rowDoneColor = new Color(0.3f, 0.23f, 0.1f, 0.98f);
        private static readonly Color buttonColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color claimColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color barBackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color barColor = new Color(0.85f, 0.65f, 0.25f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color hintColor = new Color(0.75f, 0.7f, 0.62f, 1f);
        private static readonly Color errorColor = new Color(1f, 0.55f, 0.45f, 1f);

        private struct Tanim {
            public GunlukGorevTuru tur;
            public string baslik;
            public string aciklama;
            public int[] adetler;

            public Tanim(GunlukGorevTuru tur, string baslik, string aciklama, int[] adetler) {
                this.tur = tur;
                this.baslik = baslik;
                this.aciklama = aciklama;
                this.adetler = adetler;
            }
        }

        private static readonly Tanim[] tanimlar = {
            new Tanim(GunlukGorevTuru.Oldurme, "Erlik'in yandaşları", "{0} düşman yen", new int[] { 12, 18, 25 }),
            new Tanim(GunlukGorevTuru.Ganimet, "Ganimet avcısı", "{0} ganimet topla", new int[] { 8, 12, 16 }),
            new Tanim(GunlukGorevTuru.Sandik, "Hazine avı", "{0} hazine sandığını boşalt", new int[] { 1, 2, 3 }),
            new Tanim(GunlukGorevTuru.Harita, "Yolculuk", "{0} farklı diyara ayak bas", new int[] { 2, 3, 4 }),
            new Tanim(GunlukGorevTuru.Tas, "Ötüken Taşları", "{0} Ötüken Taşı kır", new int[] { 5, 8, 12 }),
        };

        // ---------------------------------------------------------------- günün durumu

        private class Gorev {
            public Tanim tanim;
            public int hedef;
            public int ilerleme;
            public bool alindi;

            public bool Tamam { get { return ilerleme >= hedef; } }
            public string Yazi { get { return string.Format(tanim.aciklama, hedef); } }
        }

        /// <summary>
        /// bir karakterin günü: tek oyunculu oyunda telefondaki oyuncunun, sunucuda her oyuncunun ayrı ayrı;
        /// çevrimiçi istemcide sunucudan gelen kopya (pencere ve HUD bunu gösterir)
        /// </summary>
        private class Durum {
            public string karakter = string.Empty;
            public string gun = string.Empty;
            public readonly List<Gorev> gorevler = new List<Gorev>();
            public readonly HashSet<string> gezilenler = new HashSet<string>();
            public bool buyukOdulAlindi = false;
            public bool kirli = false;
            public float sonKayit = 0f;
            // ilerlemenin sahibi (tek oyunculu oyunda ve sunucuda)
            public UnitController oyuncu = null;
            // sunucu: telefona yeni kopya gidecek
            public bool gonderilecek = false;
            public float sonGonderim = -100f;
            public System.Action<UnitController, UnitController, float> oldurme = null;
        }

        private static readonly Durum yerel = new Durum();
        private static readonly Dictionary<UnitController, Durum> sunucuDurumlari = new Dictionary<UnitController, Durum>();
        private static UnitController takipEdilen = null;
        private static SystemGameManager oyun = null;
        private static float sonIstek = -100f;

        private static SystemGameManager Oyun {
            get { return oyun != null ? oyun : OtukenAg.Oyun; }
        }

        private static bool OdulVarMi(Durum d) {
            if (d.gorevler.Count == 0) {
                return false;
            }
            bool hepsi = true;
            foreach (Gorev g in d.gorevler) {
                if (g.Tamam && g.alindi == false) {
                    return true;
                }
                hepsi &= g.alindi;
            }
            return hepsi && d.buyukOdulAlindi == false;
        }

        /// <summary>alınmayı bekleyen ödül var mı (HUD düğmesindeki altın nokta)</summary>
        public static bool OdulVar {
            get { return OdulVarMi(yerel); }
        }

        /// <summary>bugünün sandık görevi sürüyor mu (görev oku sıradaki hedef yoksa en yakın sandığı gösterir)</summary>
        public static bool SandikGoreviSuruyor {
            get {
                foreach (Gorev g in yerel.gorevler) {
                    if (g.tanim.tur == GunlukGorevTuru.Sandik && g.Tamam == false) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>MobileBootstrap saniyede bir çağırır</summary>
        public static void Tick(SystemGameManager systemGameManager, bool oyunda) {
            oyun = systemGameManager;
            bool cevrimici = systemGameManager != null && systemGameManager.GameMode == GameMode.Network;
            UnitController oyuncu = oyunda && systemGameManager != null && systemGameManager.PlayerManagerClient != null
                ? systemGameManager.PlayerManagerClient.UnitController : null;
            // tek oyunculu oyunda öldürmeler telefonda sayılır; çevrimiçi oyunda sunucuda (SunucuTick)
            UnitController izlenecek = cevrimici ? null : oyuncu;
            if (izlenecek != takipEdilen) {
                if (takipEdilen != null && takipEdilen.UnitEventController != null) {
                    takipEdilen.UnitEventController.OnKillEvent -= YerelOldurme;
                }
                takipEdilen = izlenecek;
                if (takipEdilen != null && takipEdilen.UnitEventController != null) {
                    takipEdilen.UnitEventController.OnKillEvent += YerelOldurme;
                }
            }
            if (oyuncu == null) {
                Kaydet(yerel, true);
                if (IsOpen) {
                    instance.Kapat();
                }
                return;
            }
            if (cevrimici) {
                // durum sunucudan gelir: karakter değişince (ve gün dönünce) istenir
                bool gunDondu = yerel.gun.Length > 0 && yerel.gun != OtukenVeri.GunAnahtari(DateTime.UtcNow.AddHours(3));
                if ((yerel.karakter != oyuncu.DisplayName || gunDondu) && Time.unscaledTime - sonIstek > 10f) {
                    sonIstek = Time.unscaledTime;
                    OtukenAg.Gonder("gorev-iste");
                }
                return;
            }
            yerel.oyuncu = oyuncu;
            Hazirla(yerel, oyuncu, OtukenVeri.Bugun);
            HaritaSay(yerel, SceneManager.GetActiveScene().name);
            if (yerel.kirli && Time.unscaledTime - yerel.sonKayit > 10f) {
                Kaydet(yerel, false);
            }
        }

        private static void HaritaSay(Durum d, string sahne) {
            // bugün girilen haritalar
            if (Array.IndexOf(IsinlanmaPenceresi.SahneAdlari, sahne) >= 0 && d.gezilenler.Add(sahne)) {
                d.kirli = true;
                Ilerlet(d, GunlukGorevTuru.Harita, 0, d.gezilenler.Count);
            }
        }

        /// <summary>oyunun olaylarından: ganimet ve sandık (tek oyunculu oyunda; düşman öldürme kendi olayından sayılır)</summary>
        public static void Bildir(GunlukGorevTuru tur, int adet = 1) {
            if (takipEdilen == null || yerel.gorevler.Count == 0) {
                return;
            }
            Ilerlet(yerel, tur, adet, -1);
        }

        /// <summary>çevrimiçi sunucu: bir oyuncunun ganimeti ya da boşalttığı sandık</summary>
        public static void SunucuBildir(UnitController oyuncu, GunlukGorevTuru tur, int adet = 1) {
            if (oyuncu == null || OtukenAg.Sunucuda == false) {
                return;
            }
            Durum d;
            if (sunucuDurumlari.TryGetValue(oyuncu, out d) && d.gorevler.Count > 0) {
                Ilerlet(d, tur, adet, -1);
            }
        }

        private static void YerelOldurme(UnitController olduren, UnitController olen, float pay) {
            Oldurme(yerel, olen, pay);
        }

        private static void Oldurme(Durum d, UnitController olen, float pay) {
            if (pay <= 0f || olen == null || olen == d.oyuncu || d.gorevler.Count == 0) {
                return;
            }
            if (OtukenTasi.TasMi(olen)) {
                Ilerlet(d, GunlukGorevTuru.Tas, 1, -1);
                return;
            }
            Ilerlet(d, GunlukGorevTuru.Oldurme, 1, -1);
        }

        /// <param name="kesin">0 veya üstüyse ilerleme bu değere çekilir (harita sayısı), değilse adet eklenir</param>
        private static void Ilerlet(Durum d, GunlukGorevTuru tur, int adet, int kesin) {
            foreach (Gorev g in d.gorevler) {
                if (g.tanim.tur != tur || g.Tamam) {
                    continue;
                }
                g.ilerleme = Mathf.Min(g.hedef, kesin >= 0 ? kesin : g.ilerleme + adet);
                d.kirli = true;
                d.gonderilecek = true;
                if (g.Tamam && d.oyuncu != null) {
                    OtukenAg.Mesaj(d.oyuncu, $"<color=#FFD54A>Günlük görev tamamlandı: {g.Yazi}. Ödülün için Günlük'e dokun.</color>");
                    if (OtukenAg.Sunucuda == false) {
                        MobileFeedback.Success();
                    }
                }
            }
            if (d == yerel && IsOpen) {
                instance.Yenile();
            }
        }

        // ---------------------------------------------------------------- sunucu (çevrimiçi oyun)

        /// <summary>sunucu her oyuncu için saniyede bir çağırır (OtukenSunucu)</summary>
        public static void SunucuTick(UnitController oyuncu) {
            Durum d = SunucuDurumu(oyuncu);
            Hazirla(d, oyuncu, OtukenVeri.Bugun);
            if (oyuncu.gameObject != null) {
                HaritaSay(d, oyuncu.gameObject.scene.name);
            }
            if (d.kirli) {
                Kaydet(d, true);
            }
            if (d.gonderilecek && Time.realtimeSinceStartup - d.sonGonderim > 1f) {
                SunucuGonder(d);
            }
        }

        /// <summary>oyuncu oyundan çıktı ya da harita değiştirdi (sunucudaki birimi gitti)</summary>
        public static void SunucuCikti(UnitController oyuncu) {
            Durum d;
            if (oyuncu == null || sunucuDurumlari.TryGetValue(oyuncu, out d) == false) {
                return;
            }
            if (d.oldurme != null && oyuncu.UnitEventController != null) {
                oyuncu.UnitEventController.OnKillEvent -= d.oldurme;
            }
            sunucuDurumlari.Remove(oyuncu);
        }

        private static Durum SunucuDurumu(UnitController oyuncu) {
            Durum d;
            if (sunucuDurumlari.TryGetValue(oyuncu, out d)) {
                return d;
            }
            d = new Durum();
            d.oyuncu = oyuncu;
            Durum yakalanan = d;
            d.oldurme = (olduren, olen, pay) => Oldurme(yakalanan, olen, pay);
            oyuncu.UnitEventController.OnKillEvent += d.oldurme;
            d.gonderilecek = true;
            sunucuDurumlari[oyuncu] = d;
            return d;
        }

        private static void SunucuGonder(Durum d) {
            d.gonderilecek = false;
            d.sonGonderim = Time.realtimeSinceStartup;
            OtukenAg.Yanitla(d.oyuncu, "gorev", KayitYazisi(d));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("gorev-iste", (oyuncu, veri) => {
                Durum d = SunucuDurumu(oyuncu);
                Hazirla(d, oyuncu, OtukenVeri.Bugun);
                SunucuGonder(d);
            });
            OtukenAg.SunucuIsle("gorev-al", (oyuncu, veri) => {
                Durum d = SunucuDurumu(oyuncu);
                Hazirla(d, oyuncu, OtukenVeri.Bugun);
                int index;
                string hata = int.TryParse(veri, out index) ? OdulVer(d, index) : "Geçersiz görev.";
                Kaydet(d, true);
                SunucuGonder(d);
                if (hata != null) {
                    OtukenAg.Yanitla(oyuncu, "gorev-hata", hata);
                }
            });
            OtukenAg.SunucuIsle("gorev-buyuk", (oyuncu, veri) => {
                Durum d = SunucuDurumu(oyuncu);
                Hazirla(d, oyuncu, OtukenVeri.Bugun);
                string hata = BuyukOdulVer(d);
                Kaydet(d, true);
                SunucuGonder(d);
                if (hata != null) {
                    OtukenAg.Yanitla(oyuncu, "gorev-hata", hata);
                }
            });
            // istemci: sunucudan günün kopyası
            OtukenAg.IstemciDinle("gorev", veri => {
                SystemGameManager o = Oyun;
                UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
                if (oyuncu == null) {
                    return;
                }
                string gunu = veri.Split('|')[0];
                Kur(yerel, oyuncu.DisplayName, gunu);
                Yukle(yerel, veri);
                yerel.oyuncu = null;
                if (IsOpen) {
                    instance.Yenile();
                }
            });
            OtukenAg.IstemciDinle("gorev-hata", veri => {
                if (instance != null) {
                    instance.durumYazisi.text = veri;
                }
            });
        }

        // ---------------------------------------------------------------- kayıt (karakter başına: telefonda PlayerPrefs, sunucuda karakter kaydı)

        private const string KayitAnahtari = "gunluk-gorevler-";

        /// <summary>günün görevlerini kurar (gerekirse) ve kayıtlı ilerlemeyi yükler</summary>
        private static void Hazirla(Durum d, UnitController oyuncu, string bugun) {
            string ad = oyuncu.DisplayName;
            if (ad == d.karakter && bugun == d.gun && d.gorevler.Count > 0) {
                return;
            }
            Kaydet(d, true);
            d.oyuncu = oyuncu;
            Kur(d, ad, bugun);
            string kayit = OtukenVeri.Oku(oyuncu, KayitAnahtari, string.Empty);
            if (kayit.Split('|')[0] == bugun) {
                Yukle(d, kayit);
            }
            d.kirli = false;
            d.gonderilecek = true;
        }

        /// <summary>günün görevleri: tarih ve karakterden türeyen sayıyla karıştırılan türlerden ilk üçü (sırası korunur)</summary>
        private static void Kur(Durum d, string ad, string bugun) {
            d.karakter = ad;
            d.gun = bugun;
            d.gorevler.Clear();
            d.gezilenler.Clear();
            d.buyukOdulAlindi = false;
            uint tohum = Ozet32(bugun + "|" + ad);
            List<int> turler = new List<int>();
            for (int i = 0; i < tanimlar.Length; i++) {
                turler.Add(i);
            }
            uint karistir = tohum;
            for (int i = turler.Count - 1; i > 0; i--) {
                karistir = karistir * 1664525u + 1013904223u;
                int j = (int)((karistir >> 8) % (uint)(i + 1));
                int gecici = turler[i];
                turler[i] = turler[j];
                turler[j] = gecici;
            }
            List<int> secilen = turler.GetRange(0, Mathf.Min(GorevSayisi, turler.Count));
            secilen.Sort();
            int sira = 0;
            foreach (int i in secilen) {
                Tanim t = tanimlar[i];
                int zorluk = (int)((tohum / 7u + (uint)sira * 3u) % (uint)t.adetler.Length);
                d.gorevler.Add(new Gorev() { tanim = t, hedef = t.adetler[zorluk] });
                sira++;
            }
        }

        /// <summary>kayıt: gün|ilerleme,ilerleme,ilerleme|alındı bitleri|büyük ödül|gezilen;haritalar</summary>
        private static void Yukle(Durum d, string kayit) {
            string[] parcalar = kayit.Split('|');
            if (parcalar.Length < 5 || parcalar[0] != d.gun) {
                return;
            }
            string[] ilerlemeler = parcalar[1].Split(',');
            for (int i = 0; i < d.gorevler.Count && i < ilerlemeler.Length; i++) {
                int deger;
                if (int.TryParse(ilerlemeler[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out deger)) {
                    d.gorevler[i].ilerleme = Mathf.Clamp(deger, 0, d.gorevler[i].hedef);
                }
                d.gorevler[i].alindi = i < parcalar[2].Length && parcalar[2][i] == '1';
            }
            d.buyukOdulAlindi = parcalar[3] == "1";
            d.gezilenler.Clear();
            foreach (string s in parcalar[4].Split(';')) {
                if (s.Length > 0) {
                    d.gezilenler.Add(s);
                }
            }
        }

        private static string KayitYazisi(Durum d) {
            StringBuilder sb = new StringBuilder();
            sb.Append(d.gun).Append('|');
            for (int i = 0; i < d.gorevler.Count; i++) {
                sb.Append(i > 0 ? "," : string.Empty).Append(d.gorevler[i].ilerleme.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('|');
            foreach (Gorev g in d.gorevler) {
                sb.Append(g.alindi ? '1' : '0');
            }
            sb.Append('|').Append(d.buyukOdulAlindi ? '1' : '0').Append('|').Append(string.Join(";", d.gezilenler));
            return sb.ToString();
        }

        private static void Kaydet(Durum d, bool zorla) {
            if (d.kirli == false || d.oyuncu == null || string.IsNullOrEmpty(d.karakter) || d.gorevler.Count == 0) {
                return;
            }
            if (d.oyuncu.DisplayName != d.karakter) {
                return;
            }
            OtukenVeri.Yaz(d.oyuncu, KayitAnahtari, KayitYazisi(d));
            OtukenVeri.Kaydet();
            d.kirli = false;
            d.sonKayit = Time.unscaledTime;
        }

        private static uint Ozet32(string metin) {
            uint h = 2166136261u;
            foreach (char c in metin) {
                h ^= c;
                h *= 16777619u;
            }
            return h;
        }

        /// <summary>oyun testi ve ağ botu raporu için kısa özet</summary>
        public static string Ozet() {
            if (yerel.gorevler.Count == 0) {
                return "günlük görev yok";
            }
            List<string> satirlar = new List<string>();
            foreach (Gorev g in yerel.gorevler) {
                satirlar.Add(g.Yazi + " " + g.ilerleme + "/" + g.hedef + (g.Tamam ? " (tamam" + (g.alindi ? ", alındı)" : ")") : string.Empty));
            }
            return string.Join(", ", satirlar);
        }

        // ---------------------------------------------------------------- ödüller

        private static int GumusOdulu(int seviye) {
            return 6 + 2 * Mathf.Max(1, seviye);
        }

        private static int TecrubeOdulu(int seviye) {
            SystemConfigurationManager ayarlar = Oyun != null ? Oyun.SystemConfigurationManager : null;
            if (ayarlar == null) {
                // derleme önizlemesi: oyun yok
                return 25 * Mathf.Max(1, seviye);
            }
            return Mathf.Max(10, Mathf.RoundToInt(LevelEquations.GetXPNeededForLevel(Mathf.Max(1, seviye), ayarlar) * 0.12f));
        }

        /// <summary>tamamlanan görevin ödülü; olmazsa nedeni (olursa null)</summary>
        private static string OdulVer(Durum d, int index) {
            UnitController oyuncu = d.oyuncu;
            if (oyuncu == null || index < 0 || index >= d.gorevler.Count) {
                return "Görev bulunamadı.";
            }
            Gorev g = d.gorevler[index];
            if (g.Tamam == false || g.alindi) {
                return g.alindi ? "Bu görevin ödülünü zaten aldın." : "Görev henüz bitmedi.";
            }
            int seviye = oyuncu.CharacterStats.Level;
            int gumus = GumusOdulu(seviye);
            int tecrube = TecrubeOdulu(seviye);
            Currency para = Oyun.SystemDataFactory.GetResource<Currency>("Silver");
            if (para != null) {
                oyuncu.CharacterCurrencyManager.AddCurrency(para, gumus);
            }
            g.alindi = true;
            d.kirli = true;
            d.gonderilecek = true;
            Kaydet(d, true);
            if (tecrube > 0) {
                oyuncu.CharacterStats.GainExperience(tecrube);
            }
            OtukenAg.Mesaj(oyuncu, $"<color=#FFD54A>Günlük görev ödülü: {gumus} Gümüş Akçe, {tecrube} tecrübe</color>");
            OdulSayisi++;
            return null;
        }

        private static string BuyukOdulVer(Durum d) {
            UnitController oyuncu = d.oyuncu;
            if (oyuncu == null || d.buyukOdulAlindi) {
                return d.buyukOdulAlindi ? "Bugünün büyük ödülünü zaten aldın." : "Karakter bulunamadı.";
            }
            foreach (Gorev g in d.gorevler) {
                if (g.alindi == false) {
                    return "Önce üç görevin ödülünü de al.";
                }
            }
            if (oyuncu.CharacterInventoryManager.EmptySlotCount() == 0) {
                return "Çantan dolu! Biraz yer aç, sonra büyük ödülü al.";
            }
            InstantiatedItem esya = oyuncu.CharacterInventoryManager.GetNewInstantiatedItem(BuyukOdulEsyasi);
            if (esya != null) {
                oyuncu.CharacterInventoryManager.AddItem(esya, false);
            }
            Currency para = Oyun.SystemDataFactory.GetResource<Currency>("Silver");
            if (para != null) {
                oyuncu.CharacterCurrencyManager.AddCurrency(para, BuyukOdulGumus);
            }
            d.buyukOdulAlindi = true;
            d.kirli = true;
            d.gonderilecek = true;
            Kaydet(d, true);
            OtukenAg.Mesaj(oyuncu, $"<color=#FFD54A>Günün büyük ödülü: {BuyukOdulEsyaAdi} ve {BuyukOdulGumus} Gümüş Akçe!</color>");
            OdulSayisi++;
            return null;
        }

        /// <summary>bu süreçte verilen günlük görev ödülü sayısı (oyun testi, ağ botu)</summary>
        public static int OdulSayisi { get; private set; }

        /// <summary>pencerenin "Ödülü Al" düğmesi: tek oyunculu oyunda burada, çevrimiçinde sunucuda verilir</summary>
        private void OdulDugmesi(int index) {
            if (Cevrimici.Acik) {
                if (OtukenAg.Gonder("gorev-al", index.ToString(CultureInfo.InvariantCulture)) == false) {
                    durumYazisi.text = "Sunucuya ulaşılamadı, biraz sonra yeniden dene.";
                }
                return;
            }
            string hata = OdulVer(yerel, index);
            if (hata != null) {
                durumYazisi.text = hata;
            } else {
                MobileFeedback.Success();
            }
            Yenile();
        }

        private void BuyukOdulDugmesi() {
            if (Cevrimici.Acik) {
                if (OtukenAg.Gonder("gorev-buyuk") == false) {
                    durumYazisi.text = "Sunucuya ulaşılamadı, biraz sonra yeniden dene.";
                }
                return;
            }
            string hata = BuyukOdulVer(yerel);
            if (hata != null) {
                durumYazisi.text = hata;
            } else {
                MobileFeedback.Success();
            }
            Yenile();
        }

        /// <summary>ağ botu: tamamlanan ilk görevin ödülünü ister (yoksa false)</summary>
        public static bool TestIcinOdulIste() {
            for (int i = 0; i < yerel.gorevler.Count; i++) {
                if (yerel.gorevler[i].Tamam && yerel.gorevler[i].alindi == false) {
                    Ensure();
                    instance.OdulDugmesi(i);
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- pencere

        private static GunlukGorevler instance = null;

        private Font font = null;
        private GameObject panelRoot = null;
        private Text altBaslik = null;
        private Text durumYazisi = null;
        private readonly List<Image> satirResimleri = new List<Image>();
        private readonly List<Text> satirBasliklari = new List<Text>();
        private readonly List<Text> satirYazilari = new List<Text>();
        private readonly List<RectTransform> cubuklar = new List<RectTransform>();
        private readonly List<Text> sayilar = new List<Text>();
        private readonly List<Text> odulYazilari = new List<Text>();
        private readonly List<Button> alDugmeleri = new List<Button>();
        private readonly List<Text> alYazilari = new List<Text>();
        private Button buyukDugme = null;
        private Text buyukYazi = null;
        private Text buyukDugmeYazisi = null;
        private float sonrakiYenileme = 0f;

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        public static void Goster() {
            SystemGameManager o = Oyun;
            UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            if (oyuncu == null) {
                return;
            }
            if (Cevrimici.Acik) {
                // güncel durumu sunucudan iste (pencere gelen kopyayı gösterir)
                sonIstek = Time.unscaledTime;
                OtukenAg.Gonder("gorev-iste");
            } else {
                yerel.oyuncu = oyuncu;
                Hazirla(yerel, oyuncu, OtukenVeri.Bugun);
            }
            Ensure();
            instance.panelRoot.SetActive(true);
            instance.durumYazisi.text = string.Empty;
            instance.Yenile();
            MobileFeedback.Light();
        }

        private static void Ensure() {
            if (instance != null) {
                return;
            }
            GameObject canvasObject = new GameObject(CanvasName);
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1422f, 800f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            instance = canvasObject.AddComponent<GunlukGorevler>();
            instance.Build();
        }

        private void Kapat() {
            panelRoot.SetActive(false);
        }

        /// <summary>derleme önizlemesi (HaritaHazirlik, tani/arayuz_gunluk.jpg): örnek görevlerle pencere</summary>
        private void Onizleme() {
            yerel.gorevler.Clear();
            int[] ilerlemeler = { 18, 5, 1 };
            for (int i = 0; i < GorevSayisi; i++) {
                Tanim t = tanimlar[i];
                yerel.gorevler.Add(new Gorev() { tanim = t, hedef = t.adetler[1], ilerleme = Mathf.Min(ilerlemeler[i], t.adetler[1]), alindi = false });
            }
            yerel.buyukOdulAlindi = false;
            panelRoot.SetActive(true);
            durumYazisi.text = string.Empty;
            Yenile();
        }

        /// <summary>önizlemeden sonra örnek görevleri sil</summary>
        private void OnizlemeBitti() {
            yerel.gorevler.Clear();
            yerel.karakter = string.Empty;
            yerel.gun = string.Empty;
        }

        private void Update() {
            if (IsOpen && Time.unscaledTime >= sonrakiYenileme) {
                Yenile();
            }
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panelRoot = CreateRect(transform, "GunlukGorevler", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimmer = panelRoot.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.65f);

            GameObject panel = CreateRect(panelRoot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-540f, -330f), new Vector2(540f, 330f));
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = panelColor;
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = gold;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            GameObject title = CreateRect(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -62f), new Vector2(-20f, -8f));
            Text titleText = CreateText(title, "GÜNLÜK GÖREVLER", 36, TextAnchor.MiddleCenter, gold);
            titleText.fontStyle = FontStyle.Bold;
            GameObject subtitle = CreateRect(panel.transform, "Aciklama", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -96f), new Vector2(-20f, -60f));
            altBaslik = CreateText(subtitle, string.Empty, 19, TextAnchor.MiddleCenter, hintColor);

            const float satirYuksekligi = 118f;
            const float satirAraligi = 10f;
            float ust = -104f;
            for (int i = 0; i < GorevSayisi; i++) {
                int index = i;
                GameObject satir = CreateRect(panel.transform, "Gorev" + (i + 1), new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(24f, ust - satirYuksekligi), new Vector2(-24f, ust));
                Image satirResmi = satir.AddComponent<Image>();
                satirResmi.color = rowColor;
                satirResimleri.Add(satirResmi);
                ust -= satirYuksekligi + satirAraligi;

                GameObject baslik = CreateRect(satir.transform, "Baslik", new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(18f, -40f), new Vector2(0f, -8f));
                Text baslikYazisi = CreateText(baslik, string.Empty, 22, TextAnchor.MiddleLeft, gold);
                baslikYazisi.fontStyle = FontStyle.Bold;
                satirBasliklari.Add(baslikYazisi);

                GameObject yazi = CreateRect(satir.transform, "Yazi", new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(18f, -72f), new Vector2(0f, -40f));
                satirYazilari.Add(CreateText(yazi, string.Empty, 21, TextAnchor.MiddleLeft, textColor));

                // ilerleme çubuğu
                GameObject cubukArka = CreateRect(satir.transform, "Cubuk", new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(18f, 14f), new Vector2(-90f, 34f));
                cubukArka.AddComponent<Image>().color = barBackColor;
                GameObject cubuk = CreateRect(cubukArka.transform, "Dolu", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                cubuk.AddComponent<Image>().color = barColor;
                cubuklar.Add(cubuk.GetComponent<RectTransform>());
                GameObject sayi = CreateRect(satir.transform, "Sayi", new Vector2(0.62f, 0f), new Vector2(0.62f, 0f), new Vector2(-84f, 8f), new Vector2(0f, 40f));
                sayilar.Add(CreateText(sayi, string.Empty, 20, TextAnchor.MiddleLeft, textColor));

                GameObject odul = CreateRect(satir.transform, "Odul", new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-190f, -10f));
                odulYazilari.Add(CreateText(odul, string.Empty, 19, TextAnchor.MiddleCenter, hintColor));

                Text alYazisi;
                Button al = CreateButton(satir.transform, "Al", new Vector2(1f, 0.5f), new Vector2(-92f, 0f), new Vector2(160f, 64f), 24, claimColor,
                    () => { MobileFeedback.Tap(); OdulDugmesi(index); }, out alYazisi);
                alDugmeleri.Add(al);
                alYazilari.Add(alYazisi);
            }

            // büyük ödül
            GameObject buyuk = CreateRect(panel.transform, "BuyukOdul", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 92f), new Vector2(-24f, 150f));
            buyuk.AddComponent<Image>().color = new Color(0.13f, 0.1f, 0.07f, 0.95f);
            GameObject buyukMetin = CreateRect(buyuk.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-230f, 0f));
            buyukYazi = CreateText(buyukMetin, string.Empty, 20, TextAnchor.MiddleLeft, textColor);
            buyukDugme = CreateButton(buyuk.transform, "Büyük Ödülü Al", new Vector2(1f, 0.5f), new Vector2(-112f, 0f), new Vector2(210f, 48f), 21, claimColor,
                () => { MobileFeedback.Tap(); BuyukOdulDugmesi(); }, out buyukDugmeYazisi);

            GameObject durum = CreateRect(panel.transform, "Durum", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 64f), new Vector2(-24f, 90f));
            durumYazisi = CreateText(durum, string.Empty, 19, TextAnchor.MiddleCenter, errorColor);

            Text kapatYazisi;
            CreateButton(panel.transform, "Kapat", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(220f, 52f), 24, buttonColor,
                () => { MobileFeedback.Tap(); Kapat(); }, out kapatYazisi);

            panelRoot.SetActive(false);
        }

        private void Yenile() {
            sonrakiYenileme = Time.unscaledTime + 0.5f;
            if (panelRoot == null) {
                return;
            }
            // çevrimiçi oyunda günler sunucuda Türkiye saatiyle döner
            DateTime simdi = Cevrimici.Acik ? DateTime.UtcNow.AddHours(3) : DateTime.Now;
            TimeSpan kalan = simdi.Date.AddDays(1) - simdi;
            altBaslik.text = $"Her gün 3 yeni görev. Hepsini bitirene büyük ödül! Yenilenmesine {(int)kalan.TotalHours} sa {kalan.Minutes} dk var.";
            SystemGameManager o = Oyun;
            UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            int seviye = oyuncu != null && oyuncu.CharacterStats != null ? oyuncu.CharacterStats.Level : 1;
            List<Gorev> gorevler = yerel.gorevler;
            bool hepsiAlindi = gorevler.Count > 0;
            for (int i = 0; i < GorevSayisi; i++) {
                bool mevcut = i < gorevler.Count;
                satirResimleri[i].gameObject.SetActive(mevcut);
                if (mevcut == false) {
                    hepsiAlindi = false;
                    continue;
                }
                Gorev g = gorevler[i];
                satirBasliklari[i].text = g.tanim.baslik;
                satirYazilari[i].text = g.Yazi;
                float oran = g.hedef > 0 ? Mathf.Clamp01(g.ilerleme / (float)g.hedef) : 0f;
                cubuklar[i].anchorMax = new Vector2(oran, 1f);
                sayilar[i].text = g.ilerleme + "/" + g.hedef;
                odulYazilari[i].text = $"{GumusOdulu(seviye)} Gümüş Akçe\n{TecrubeOdulu(seviye)} tecrübe";
                satirResimleri[i].color = g.Tamam ? rowDoneColor : rowColor;
                alDugmeleri[i].interactable = g.Tamam && g.alindi == false;
                alYazilari[i].text = g.alindi ? "Alındı" : (g.Tamam ? "Ödülü Al" : "Sürüyor");
                hepsiAlindi &= g.alindi;
            }
            buyukYazi.text = yerel.buyukOdulAlindi
                ? "Bugünün büyük ödülünü aldın. Yarın yeni görevler seni bekliyor."
                : $"Üç görevin ödülünü de alınca: {BuyukOdulEsyaAdi} + {BuyukOdulGumus} Gümüş Akçe";
            buyukDugme.interactable = hepsiAlindi && yerel.buyukOdulAlindi == false;
            buyukDugmeYazisi.text = yerel.buyukOdulAlindi ? "Alındı" : "Büyük Ödülü Al";
        }

        // ---------------------------------------------------------------- yapı taşları

        private GameObject CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject rectObject = new GameObject(name);
            rectObject.transform.SetParent(parent, false);
            RectTransform rectTransform = rectObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            return rectObject;
        }

        private Text CreateText(GameObject target, string content, int fontSize, TextAnchor alignment, Color color) {
            Text text = target.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize,
            Color color, UnityEngine.Events.UnityAction onClick, out Text labelText) {
            GameObject buttonObject = CreateRect(parent, label, anchor, anchor, Vector2.zero, Vector2.zero);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(gold.r, gold.g, gold.b, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            GameObject textObject = CreateRect(buttonObject.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            labelText = CreateText(textObject, label, fontSize, TextAnchor.MiddleCenter, textColor);
            return button;
        }
    }
}
