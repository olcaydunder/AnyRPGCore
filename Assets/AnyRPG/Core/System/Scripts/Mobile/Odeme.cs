using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Scripting;

namespace AnyRPG {

    /// <summary>
    /// Google Play ile Kut satın alma (Google Play Faturalandırma Kitaplığı 8, Assets/Plugins/Android/OtukenOdeme.java).
    /// Akış:
    ///  1. Kut Dükkânı açılınca Baslat: Play'e bağlanılır, paketlerin yerel fiyatları (₺) alınır, teslim edilmemiş eski
    ///     satın almalar sorulur.
    ///  2. SatinAl: Google Play'in ödeme ekranı açılır. Ödeme bitince Google'ın imzalı satın alma bilgisi gelir.
    ///  3. Çevrimiçi oyunda bilgi sunucuya gider ("odeme"): sunucu imzayı Play Console'daki genel anahtarla doğrular
    ///     (OdemeDogrulama), aynı satın almanın iki kez yüklenmesini önler (odemeler.txt), Kut'u karaktere yükler,
    ///     ödeme.log'a yazar. Tek oyunculu oyunda doğrulama telefonda yapılır.
    ///  4. Yükleme olunca satın alma "tüketilir" (aynı paket yeniden alınabilir). Yüklenemezse tüketilmez: oyun bir
    ///     sonraki açılışta yeniden dener; Google 3 gün içinde onaylanmayan satın almayı kendiliğinden iade eder.
    /// Genel anahtar ve paket adı Resources/OdemeAnahtari.txt'de ("paket=...", "anahtar=..."); dosya yoksa satın alma
    /// kapalıdır ("yakında"). Ürün kimlikleri Play Console'da aynen açılmalıdır: kut_100, kut_550, kut_1200, kut_2500, kut_6500.
    /// </summary>
    public static class Odeme {

        public class Paket {
            public string kod;
            public int kut;
        }

        public static readonly Paket[] Paketler = {
            new Paket() { kod = "kut_100", kut = 100 },
            new Paket() { kod = "kut_550", kut = 550 },
            new Paket() { kod = "kut_1200", kut = 1200 },
            new Paket() { kod = "kut_2500", kut = 2500 },
            new Paket() { kod = "kut_6500", kut = 6500 },
        };

        public static event Action Degisti = delegate { };

        public static bool Hazir { get; private set; }
        public static string DurumYazisi { get; private set; } = "Kut paketleri yakında.";
        public static string SonSonuc { get; private set; } = "-";
        public static int YuklenenSayisi { get; private set; }

        private static readonly Dictionary<string, string> fiyatlar = new Dictionary<string, string>();

        public static string Fiyat(string kod) {
            string f;
            return fiyatlar.TryGetValue(kod, out f) ? f : null;
        }

        public static Paket PaketBul(string kod) {
            return Array.Find(Paketler, p => p.kod == kod);
        }

        // ---------------------------------------------------------------- ayarlar (Resources/OdemeAnahtari.txt)

        private static bool ayarOkundu = false;
        private static string genelAnahtar = string.Empty;
        private static string paketAdi = string.Empty;

        private static void AyarOku() {
            if (ayarOkundu) {
                return;
            }
            ayarOkundu = true;
            TextAsset dosya = Resources.Load<TextAsset>("OdemeAnahtari");
            if (dosya == null) {
                return;
            }
            foreach (string satir in dosya.text.Split('\n')) {
                string s = satir.Trim();
                if (s.StartsWith("paket=")) {
                    paketAdi = s.Substring(6).Trim();
                } else if (s.StartsWith("anahtar=")) {
                    genelAnahtar = s.Substring(8).Trim();
                }
            }
        }

        public static bool Kurulu {
            get {
                AyarOku();
                return genelAnahtar.Length > 0;
            }
        }

        // ================================================================ telefon

        private static bool baslatildi = false;
        private static readonly Queue<KeyValuePair<string, string>> olaylar = new Queue<KeyValuePair<string, string>>();
        private static readonly List<string> bekleyenTeslimler = new List<string>();
        private static OdemeKoprusu kopru = null;

        /// <summary>OdemeKoprusu her karede çağırır</summary>
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
                    Debug.LogError("[Odeme] olay işlenemedi (" + o.Key + "): " + e);
                }
            }
            // oyuna girilmeden gelen satın almalar oyuna girince teslim edilir
            if (bekleyenTeslimler.Count > 0 && Time.unscaledTime >= sonrakiDeneme) {
                sonrakiDeneme = Time.unscaledTime + 10f;
                foreach (string b in bekleyenTeslimler.ToArray()) {
                    Teslim(b);
                }
            }
        }

        private static float sonrakiDeneme = 0f;

        /// <summary>Kut Dükkânı açılınca: Play'e bağlanır, fiyatları ve teslim edilmemiş satın almaları sorar</summary>
        public static void Baslat() {
            if (Kurulu == false) {
                DurumYazisi = "Kut paketleri yakında.";
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if (baslatildi) {
                Cagir("bekleyenleriSor");
                return;
            }
            baslatildi = true;
            KopruKur();
            DurumYazisi = "Google Play'e bağlanılıyor...";
            try {
                using (AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
                    AndroidJavaObject etkinlik = unity.GetStatic<AndroidJavaObject>("currentActivity");
                    List<string> kodlar = new List<string>();
                    foreach (Paket p in Paketler) {
                        kodlar.Add(p.kod);
                    }
                    using (AndroidJavaClass sinif = new AndroidJavaClass("com.zootopiayazilim.otuken.OtukenOdeme")) {
                        sinif.CallStatic("baslat", etkinlik, new Dinleyici(), string.Join(",", kodlar));
                    }
                }
            } catch (Exception e) {
                baslatildi = false;
                DurumYazisi = "Google Play ödemesi açılamadı.";
                Debug.LogWarning("[Odeme] başlatılamadı: " + e.Message);
            }
#else
            DurumYazisi = "Kut, Google Play'den indirilen oyunda satın alınır.";
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Cagir(string yontem, params object[] degerler) {
            try {
                using (AndroidJavaClass sinif = new AndroidJavaClass("com.zootopiayazilim.otuken.OtukenOdeme")) {
                    sinif.CallStatic(yontem, degerler);
                }
            } catch (Exception e) {
                Debug.LogWarning("[Odeme] " + yontem + ": " + e.Message);
            }
        }
#endif

        /// <summary>Google Play ödeme ekranını açar; açamazsa nedenini döndürür</summary>
        public static string SatinAl(string kod) {
            if (Hazir == false || Fiyat(kod) == null) {
                return DurumYazisi;
            }
            if (Cevrimici.Acik && OtukenAg.Oyun != null && OtukenAg.Oyun.PlayerManagerClient.UnitController == null) {
                return "Satın almak için oyuna gir.";
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            Cagir("satinAl", kod);
            return null;
#else
            return "Kut, Google Play'den indirilen oyunda satın alınır.";
#endif
        }

        /// <summary>Java'dan gelen olaylar (Play'in iş parçacığında) ana iş parçacığında işlenir</summary>
        private class Dinleyici : AndroidJavaProxy {
            public Dinleyici() : base("com.zootopiayazilim.otuken.OdemeDinleyici") {
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
            GameObject go = new GameObject("OdemeKoprusu");
            UnityEngine.Object.DontDestroyOnLoad(go);
            kopru = go.AddComponent<OdemeKoprusu>();
        }

        private static void OlayIsle(string tur, string veri) {
            switch (tur) {
                case "hazir":
                    Hazir = true;
                    DurumYazisi = fiyatlar.Count > 0 ? string.Empty : "Kut paketleri Google Play'de bulunamadı.";
                    break;
                case "urun": {
                    string[] p = veri.Split('\t');
                    if (p.Length >= 2) {
                        fiyatlar[p[0]] = p[1];
                    }
                    DurumYazisi = string.Empty;
                    break;
                }
                case "satin":
                    Teslim(veri);
                    break;
                case "bekliyor":
                    SonSonuc = "Ödeme onay bekliyor; onaylanınca Kut yüklenecek.";
                    Mesaj(SonSonuc);
                    break;
                case "iptal":
                    SonSonuc = "Satın alma iptal edildi.";
                    break;
                case "hata":
                    SonSonuc = "Google Play: " + veri;
                    if (Hazir == false) {
                        DurumYazisi = "Google Play ödemesine ulaşılamadı (" + veri + ").";
                    }
                    Mesaj(SonSonuc);
                    break;
                case "tuketildi":
                    break;
            }
            Degisti();
        }

        private static void Mesaj(string metin) {
            SystemGameManager o = OtukenAg.Oyun;
            UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            if (oyuncu != null) {
                OtukenAg.Mesaj(oyuncu, "<color=#FFD54A>Kut Dükkânı:</color> " + metin);
            }
        }

        /// <summary>satın alma bilgisini (json \u001F imza) yükler: çevrimiçinde sunucuya, çevrimdışında telefonda</summary>
        private static void Teslim(string veri) {
            SystemGameManager o = OtukenAg.Oyun;
            UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            if (oyuncu == null) {
                if (bekleyenTeslimler.Contains(veri) == false) {
                    bekleyenTeslimler.Add(veri);
                }
                return;
            }
            if (Cevrimici.Acik) {
                if (OtukenAg.Gonder("odeme", veri) == false && bekleyenTeslimler.Contains(veri) == false) {
                    bekleyenTeslimler.Add(veri);
                }
                return;
            }
            bekleyenTeslimler.Remove(veri);
            string[] p = veri.Split('\u001F');
            string hata;
            AyarOku();
            OdemeDogrulama.SatinAlma s = OdemeDogrulama.Dogrula(p[0], p.Length > 1 ? p[1] : string.Empty, genelAnahtar, paketAdi, out hata);
            if (s == null) {
                SonSonuc = hata;
                Mesaj(hata);
                return;
            }
            Paket paket = PaketBul(s.productId);
            if (paket == null) {
                SonSonuc = "Bilinmeyen ürün: " + s.productId;
                return;
            }
            if (YerelKullanildiMi(s.purchaseToken) == false) {
                Currency kut = KutDukkani.Kut;
                oyuncu.CharacterCurrencyManager.AddCurrency(kut, paket.kut * s.quantity);
                YerelKullanildi(s.purchaseToken);
                YuklenenSayisi++;
                Mesaj((paket.kut * s.quantity) + " Kut yüklendi. Teşekkürler!");
                MobileFeedback.Success();
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            Cagir("tuket", s.purchaseToken);
#endif
        }

        private static bool YerelKullanildiMi(string token) {
            return PlayerPrefs.GetString("odeme-tokenlar", string.Empty).Contains(Ozet(token));
        }

        private static void YerelKullanildi(string token) {
            string eski = PlayerPrefs.GetString("odeme-tokenlar", string.Empty);
            if (eski.Length > 6000) {
                eski = eski.Substring(eski.Length - 4000);
            }
            PlayerPrefs.SetString("odeme-tokenlar", eski + Ozet(token) + ";");
            PlayerPrefs.Save();
        }

        private static string Ozet(string token) {
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create()) {
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(token ?? string.Empty));
                return BitConverter.ToString(h, 0, 12).Replace("-", string.Empty);
            }
        }

        // ================================================================ sunucu

        private static HashSet<string> kullanilanlar = null;

        private static string KayitYolu {
            get { return Path.Combine(Application.persistentDataPath, "odemeler.txt"); }
        }

        private static bool SunucudaKullanildiMi(string token) {
            if (kullanilanlar == null) {
                kullanilanlar = new HashSet<string>();
                try {
                    if (File.Exists(KayitYolu)) {
                        foreach (string satir in File.ReadAllLines(KayitYolu)) {
                            string[] p = satir.Split(' ');
                            if (p.Length > 0 && p[0].Length > 0) {
                                kullanilanlar.Add(p[0]);
                            }
                        }
                    }
                } catch (Exception e) {
                    Debug.LogError("[Odeme] odemeler.txt okunamadı: " + e.Message);
                }
            }
            return kullanilanlar.Contains(Ozet(token));
        }

        private static void SunucudaKullanildi(string token, string ayrinti) {
            kullanilanlar.Add(Ozet(token));
            File.AppendAllText(KayitYolu, Ozet(token) + " " + ayrinti + "\n", Encoding.UTF8);
        }

        /// <summary>CI'daki deneme sunucusu (-testOdeme): bot "TEST:kod" ile imzasız Kut alabilir; canlı sunucuda kapalı</summary>
        private static bool TestKipi {
            get { return Array.IndexOf(Environment.GetCommandLineArgs(), "-testOdeme") >= 0; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("odeme", (oyuncu, veri) => {
                string[] p = veri.Split('\u001F');
                string json = p[0];
                OdemeDogrulama.SatinAlma s;
                string hata = null;
                if (json.StartsWith("TEST:") && TestKipi) {
                    s = new OdemeDogrulama.SatinAlma() { productId = json.Substring(5), purchaseToken = "test-" + Guid.NewGuid().ToString("N"),
                        purchaseState = 0, quantity = 1, orderId = "TEST" };
                } else {
                    AyarOku();
                    s = OdemeDogrulama.Dogrula(json, p.Length > 1 ? p[1] : string.Empty, genelAnahtar, paketAdi, out hata);
                }
                if (s == null) {
                    Debug.LogWarning("[Sunucu] " + oyuncu.DisplayName + " ödeme reddedildi: " + hata);
                    OtukenAg.Yanitla(oyuncu, "odeme-sonuc", "0\u001F\u001F" + hata);
                    return;
                }
                Paket paket = PaketBul(s.productId);
                if (paket == null) {
                    OtukenAg.Yanitla(oyuncu, "odeme-sonuc", "0\u001F" + s.purchaseToken + "\u001FBilinmeyen ürün.");
                    return;
                }
                int miktar = 0;
                if (SunucudaKullanildiMi(s.purchaseToken) == false) {
                    miktar = paket.kut * s.quantity;
                    oyuncu.CharacterCurrencyManager.AddCurrency(KutDukkani.Kut, miktar);
                    Ticaret.Kaydet(oyuncu);
                    try {
                        SunucudaKullanildi(s.purchaseToken, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + s.orderId + " "
                            + s.productId + " x" + s.quantity + " " + Ticaret.Duz(oyuncu.DisplayName).Replace(' ', '_'));
                    } catch (Exception e) {
                        Debug.LogError("[Odeme] odemeler.txt yazılamadı: " + e.Message);
                    }
                    Ticaret.Defter("ODEME " + oyuncu.DisplayName + " " + s.orderId + " " + s.productId + " x" + s.quantity + " +" + miktar + " Kut");
                    OtukenAg.Mesaj(oyuncu, "<color=#FFD54A>Kut Dükkânı:</color> " + miktar + " Kut yüklendi. Teşekkürler!");
                }
                OtukenAg.Yanitla(oyuncu, "odeme-sonuc", "1\u001F" + s.purchaseToken + "\u001F" + miktar.ToString(CultureInfo.InvariantCulture));
            });
            // CI deneme sunucusu (-testOdeme): ağ botlarına takas ve pazar denemesi için eşya (canlı sunucuda kapalı)
            OtukenAg.SunucuIsle("test-esya", (oyuncu, ad) => {
                if (TestKipi == false) {
                    return;
                }
                InstantiatedItem esya = oyuncu.CharacterInventoryManager.GetNewInstantiatedItem(ad);
                if (esya != null) {
                    oyuncu.CharacterInventoryManager.AddItem(esya, false);
                }
            });
            OtukenAg.IstemciDinle("odeme-sonuc", veri => {
                string[] p = veri.Split('\u001F');
                if (p.Length < 3) {
                    return;
                }
                string token = p[1];
                // teslim edilen (ya da kalıcı hatalı) bekleyenler listeden çıkar
                bekleyenTeslimler.RemoveAll(b => b.Contains(token) && token.Length > 0);
                if (p[0] == "1") {
                    YuklenenSayisi++;
                    SonSonuc = p[2] + " Kut yüklendi.";
                    MobileFeedback.Success();
#if UNITY_ANDROID && !UNITY_EDITOR
                    if (token.StartsWith("test-") == false) {
                        Cagir("tuket", token);
                    }
#endif
                } else {
                    SonSonuc = p[2];
                    Mesaj("Satın alma yüklenemedi: " + p[2]);
                }
                Degisti();
            });
        }

        /// <summary>ağ botu: deneme sunucusunda çantaya eşya (takas ve pazar denemesi)</summary>
        public static void TestIcinEsya(string ad) {
            OtukenAg.Gonder("test-esya", ad);
        }

        /// <summary>ağ botu: deneme sunucusunda imzasız deneme satın alması</summary>
        public static void TestIcinSatinAl(string kod) {
            OtukenAg.Gonder("odeme", "TEST:" + kod);
        }
    }
}
