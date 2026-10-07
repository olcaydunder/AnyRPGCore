using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Sunucu yönetim paneli (Ötüken; Rise of Davraz'daki gibi): http://SUNUCU:8081/yonetim
    ///  - Genel: çevrimiçi sayı, bugünkü tepe ve farklı oyuncu, hesaplar, sunucu belleği/yükü, etkinlik, ödemeler
    ///  - Oyuncular (canlı): hesap, karakter, sınıf, seviye, harita, can, ne yaptığı (savaşta/pazarda/takasta/binekte/
    ///    yürüyor/duruyor), hedefi, bu oturumdaki ve toplam süresi, Kut ve akçe, IP; At, Yasakla, herkese Duyuru
    ///  - Hesaplar: bütün hesaplar, toplam oyun süresi, son görülme, Google bağlı mı, yasak
    ///  - Oturumlar: son girişler ve süreleri
    ///  - Kayıtlar: sohbet, şikâyet, ticaret, ödeme defterleri; Günlük: sunucu iletileri ve hatalar
    ///  - Ayarlar: Google girişinin gizli anahtarı (yalnız sunucuda durur), panel şifresi
    /// İlk açılışta rastgele bir şifre üretilir: /var/lib/otuken/yonetim-sifresi.txt (yalnız VPS'ten okunur).
    /// Panel ayrı bir iş parçacığında dinler; oyun verisi ana iş parçacığında saniyede bir anlık görüntüye yazılır,
    /// panelin işleri (at, yasakla, duyuru) ana iş parçacığında yapılır.
    /// </summary>
    public static class YonetimPaneli {

        public const int Port = 8081;
        private const string Cerez = "otk_yonetim";

        // ================================================================ yollar

        private static string Ev {
            get {
                string ev = Environment.GetEnvironmentVariable("HOME");
                return string.IsNullOrEmpty(ev) ? veriKlasoru : ev;
            }
        }

        private static string veriKlasoru = string.Empty;

        private static string SifreDosyasi { get { return Path.Combine(Ev, "yonetim.json"); } }
        private static string IlkSifreDosyasi { get { return Path.Combine(Ev, "yonetim-sifresi.txt"); } }
        private static string SureDosyasiYolu { get { return Path.Combine(veriKlasoru, "oyuncu-sureleri.json"); } }
        private static string YasakDosyasi { get { return Path.Combine(veriKlasoru, "yasaklar.txt"); } }

        // ================================================================ durum

        private static Thread dinleyiciIsi = null;
        private static HttpListener dinleyici = null;
        private static bool baslatildi = false;
        private static readonly ConcurrentQueue<Action<SystemGameManager>> isler = new ConcurrentQueue<Action<SystemGameManager>>();
        private static readonly object kilit = new object();
        private static string genelJson = "{}";
        private static string oyuncularJson = "[]";
        private static string hesaplarJson = "[]";
        private static string oturumlarJson = "[]";
        private static readonly LinkedList<string> gunluk = new LinkedList<string>();
        private static readonly Dictionary<string, DateTime> jetonlar = new Dictionary<string, DateTime>();
        private static readonly Dictionary<string, KeyValuePair<int, DateTime>> denemeler = new Dictionary<string, KeyValuePair<int, DateTime>>();
        private static readonly HashSet<int> yasaklar = new HashSet<int>();
        private static float basladigi = 0f;
        private static volatile bool googleKurulu = false;
        private static volatile string googleWebIstemci = string.Empty;
        private static int reklamOdulu = 0;

        /// <summary>sunucuda verilen reklam ödülü (Reklam çağırır)</summary>
        public static void ReklamOdulu() {
            Interlocked.Increment(ref reklamOdulu);
        }

        // ---------------------------------------------------------------- oyun süreleri

        [Serializable]
        private class SureKaydi {
            public int hesap;
            public string ad = string.Empty;
            public string karakter = string.Empty;
            public long ilk;
            public long son;
            public long saniye;
            public int oturum;
            public int Dakika { get { return (int)(saniye / 60); } }
        }

        [Serializable]
        private class OturumKaydi {
            public int hesap;
            public string ad = string.Empty;
            public string karakter = string.Empty;
            public long bas;
            public long bit;
            public int dk;
            public string haritalar = string.Empty;
        }

        [Serializable]
        private class SureDosyasi {
            public List<SureKaydi> kayitlar = new List<SureKaydi>();
            public List<OturumKaydi> oturumlar = new List<OturumKaydi>();
            public string gun = string.Empty;
            public int gunTepe;
            public List<int> gunHesaplar = new List<int>();
            public int yeniHesapGun;
        }

        private class Canli {
            public long bas;
            public float sonGorulme;
            public string karakter = string.Empty;
            public readonly List<string> haritalar = new List<string>();
            public Vector3 konum;
            public float kesir;
        }

        private static SureDosyasi sureler = null;
        private static readonly Dictionary<int, SureKaydi> kayitDizini = new Dictionary<int, SureKaydi>();
        private static int googleHesapSayisi = 0;
        private static readonly Dictionary<int, Canli> cevrimici = new Dictionary<int, Canli>();
        private static float sonrakiKayit = 0f;
        private static float sonIzleme = -1f;
        private static SystemGameManager sonOyun = null;
        private static float sonrakiHesapGoruntusu = 0f;
        private static int bilinenHesapSayisi = -1;

        private static long Simdi {
            get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }
        }

        private static string Bugun {
            get { return Etkinlikler.TurkiyeSaati.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        }

        // ================================================================ ana iş parçacığı (saniyede bir, OtukenSunucu)

        public static void SunucuTick(SystemGameManager oyun) {
            if (oyun == null || oyun.PlayerManagerServer == null) {
                return;
            }
            if (baslatildi == false) {
                baslatildi = true;
                Baslat(oyun);
            }
            // panelin istediği işler
            Action<SystemGameManager> is_;
            int n = 0;
            while (n++ < 20 && isler.TryDequeue(out is_)) {
                try {
                    is_(oyun);
                } catch (Exception e) {
                    Debug.LogWarning("[Yonetim] iş yapılamadı: " + e.Message);
                }
            }
            SureleriIzle(oyun);
            GoruntuYaz(oyun);
        }

        private static void Baslat(SystemGameManager oyun) {
            veriKlasoru = Application.persistentDataPath;
            basladigi = Time.realtimeSinceStartup;
            Application.logMessageReceivedThreaded += GunlugeYaz;
            Application.quitting += Kapanis;
            SureleriOku();
            YasaklariOku();
            try {
                SifreHazirla();
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] şifre hazırlanamadı: " + e.Message);
            }
            try {
                dinleyici = new HttpListener();
                dinleyici.Prefixes.Add("http://*:" + Port + "/");
                dinleyici.Start();
                dinleyiciIsi = new Thread(Dinle) { IsBackground = true, Name = "YonetimPaneli" };
                dinleyiciIsi.Start();
                Debug.Log("[Sunucu] yönetim paneli açıldı: http://<sunucu>:" + Port + "/yonetim");
            } catch (Exception e) {
                Debug.LogWarning("[Sunucu] yönetim paneli açılamadı: " + e.Message);
            }
        }

        private static void SureleriIzle(SystemGameManager oyun) {
            long simdi = Simdi;
            float t = Time.realtimeSinceStartup;
            // süre her saniye işlenir: sunucu yeniden başlasa da oynanan süre kaybolmaz
            float gecen = sonIzleme < 0f ? 0f : Mathf.Clamp(t - sonIzleme, 0f, 5f);
            sonIzleme = t;
            sonOyun = oyun;
            string bugun = Bugun;
            if (sureler.gun != bugun) {
                sureler.gun = bugun;
                sureler.gunTepe = 0;
                sureler.gunHesaplar.Clear();
                sureler.yeniHesapGun = 0;
            }
            foreach (KeyValuePair<int, UnitController> kv in oyun.PlayerManagerServer.ActiveUnitControllers) {
                UnitController u = kv.Value;
                if (u == null) {
                    continue;
                }
                Canli c;
                if (cevrimici.TryGetValue(kv.Key, out c) == false) {
                    c = new Canli() { bas = simdi };
                    cevrimici[kv.Key] = c;
                    SureKaydi k = Kayit(kv.Key, oyun);
                    k.oturum++;
                    if (k.ilk == 0) {
                        k.ilk = simdi;
                        sureler.yeniHesapGun++;
                    }
                    if (sureler.gunHesaplar.Contains(kv.Key) == false) {
                        sureler.gunHesaplar.Add(kv.Key);
                    }
                }
                c.sonGorulme = t;
                c.karakter = u.DisplayName;
                string harita = HaritaSeviyeleri.Ad(u.gameObject.scene.name);
                if (c.haritalar.Contains(harita) == false) {
                    c.haritalar.Add(harita);
                }
                SureKaydi kayit = Kayit(kv.Key, oyun);
                kayit.karakter = u.DisplayName;
                kayit.son = simdi;
                c.kesir += gecen;
                if (c.kesir >= 1f) {
                    kayit.saniye += (long)c.kesir;
                    c.kesir -= (long)c.kesir;
                }
            }
            sureler.gunTepe = Math.Max(sureler.gunTepe, oyun.PlayerManagerServer.ActiveUnitControllers.Count);
            // harita değiştirirken birim kısa süre yoktur: 30 sn görünmeyen oyuncu çıkmış sayılır
            List<int> cikanlar = null;
            foreach (KeyValuePair<int, Canli> kv in cevrimici) {
                if (t - kv.Value.sonGorulme > 30f) {
                    (cikanlar ?? (cikanlar = new List<int>())).Add(kv.Key);
                }
            }
            if (cikanlar != null) {
                foreach (int hesap in cikanlar) {
                    OturumBitir(hesap, oyun);
                }
                SureleriKaydet();
            }
            if (t >= sonrakiKayit) {
                sonrakiKayit = t + 60f;
                SureleriKaydet();
            }
        }

        private static SureKaydi Kayit(int hesap, SystemGameManager oyun) {
            SureKaydi k;
            if (kayitDizini.TryGetValue(hesap, out k) == false) {
                k = new SureKaydi() { hesap = hesap };
                sureler.kayitlar.Add(k);
                kayitDizini[hesap] = k;
            }
            if (string.IsNullOrEmpty(k.ad)) {
                UserAccount h = oyun.UserAccountService.HesapBul(hesap);
                k.ad = h != null ? h.UserName : "#" + hesap;
            }
            return k;
        }

        private static void OturumBitir(int hesap, SystemGameManager oyun, bool kapanis = false) {
            Canli c;
            if (cevrimici.TryGetValue(hesap, out c) == false) {
                return;
            }
            cevrimici.Remove(hesap);
            long bit = kapanis ? Simdi : Simdi - 30;
            int dk = (int)Math.Max(0, (bit - c.bas) / 60);
            SureKaydi k = Kayit(hesap, oyun);
            k.son = bit;
            sureler.oturumlar.Add(new OturumKaydi() {
                hesap = hesap, ad = k.ad, karakter = c.karakter, bas = c.bas, bit = bit, dk = dk,
                haritalar = string.Join(", ", c.haritalar.ToArray())
            });
            if (sureler.oturumlar.Count > 300) {
                sureler.oturumlar.RemoveRange(0, sureler.oturumlar.Count - 300);
            }
        }

        /// <summary>sunucu kapanırken açık oturumlar kapatılır, süreler yazılır</summary>
        private static void Kapanis() {
            try {
                if (sonOyun != null) {
                    foreach (int hesap in new List<int>(cevrimici.Keys)) {
                        OturumBitir(hesap, sonOyun, true);
                    }
                }
                SureleriKaydet();
            } catch (Exception) {
            }
            try {
                if (dinleyici != null) {
                    dinleyici.Close();
                }
            } catch (Exception) {
            }
        }

        private static void SureleriOku() {
            try {
                if (File.Exists(SureDosyasiYolu)) {
                    sureler = JsonUtility.FromJson<SureDosyasi>(File.ReadAllText(SureDosyasiYolu));
                }
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] süreler okunamadı: " + e.Message);
            }
            if (sureler == null) {
                sureler = new SureDosyasi();
            }
            kayitDizini.Clear();
            foreach (SureKaydi k in sureler.kayitlar) {
                kayitDizini[k.hesap] = k;
            }
        }

        private static void SureleriKaydet() {
            try {
                string gecici = SureDosyasiYolu + ".tmp";
                File.WriteAllText(gecici, JsonUtility.ToJson(sureler));
                if (File.Exists(SureDosyasiYolu)) {
                    File.Delete(SureDosyasiYolu);
                }
                File.Move(gecici, SureDosyasiYolu);
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] süreler yazılamadı: " + e.Message);
            }
        }

        // ---------------------------------------------------------------- yasaklar

        private static void YasaklariOku() {
            try {
                if (File.Exists(YasakDosyasi)) {
                    foreach (string satir in File.ReadAllLines(YasakDosyasi)) {
                        int id;
                        string[] p = satir.Split(' ');
                        if (p.Length > 0 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out id)) {
                            yasaklar.Add(id);
                        }
                    }
                }
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] yasaklar okunamadı: " + e.Message);
            }
        }

        private static void YasaklariYaz(SystemGameManager oyun) {
            try {
                StringBuilder sb = new StringBuilder();
                List<int> liste;
                lock (kilit) {
                    liste = new List<int>(yasaklar);
                }
                foreach (int id in liste) {
                    UserAccount h = oyun.UserAccountService.HesapBul(id);
                    sb.Append(id).Append(' ').Append(h != null ? h.UserName : "?").Append('\n');
                }
                File.WriteAllText(YasakDosyasi, sb.ToString());
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] yasaklar yazılamadı: " + e.Message);
            }
        }

        /// <summary>giriş: yasaklı hesap oyuna giremez (AuthenticationService.ProcessLoginResponse)</summary>
        public static bool YasakliMi(int hesap) {
            lock (kilit) {
                return yasaklar.Contains(hesap);
            }
        }

        // ---------------------------------------------------------------- anlık görüntü

        private static float sonrakiGoruntu = 0f;
        private static float sonrakiOdemeSayimi = 0f;
        private static int odemeBugun = 0;

        private static void GoruntuYaz(SystemGameManager oyun) {
            float t = Time.realtimeSinceStartup;
            if (t < sonrakiGoruntu) {
                return;
            }
            sonrakiGoruntu = t + 2f;
            Currency gumus = oyun.SystemDataFactory.GetResource<Currency>("Silver");
            StringBuilder o = new StringBuilder("[");
            bool ilk = true;
            foreach (KeyValuePair<int, UnitController> kv in oyun.PlayerManagerServer.ActiveUnitControllers) {
                UnitController u = kv.Value;
                if (u == null || u.CharacterStats == null) {
                    continue;
                }
                UserAccount hesap = oyun.UserAccountService.HesapBul(kv.Key);
                Canli c;
                cevrimici.TryGetValue(kv.Key, out c);
                SureKaydi k;
                kayitDizini.TryGetValue(kv.Key, out k);
                string ip = string.Empty;
                LoggedInAccount giris;
                if (oyun.AuthenticationService.LoggedInAccounts.TryGetValue(kv.Key, out giris) && giris != null) {
                    ip = giris.ipAddress ?? string.Empty;
                }
                int canYuzde = u.CharacterStats.MaxPrimaryResource > 0
                    ? Mathf.RoundToInt(100f * u.CharacterStats.CurrentPrimaryResource / u.CharacterStats.MaxPrimaryResource) : 0;
                bool yuruyor = c != null && (u.transform.position - c.konum).sqrMagnitude > 0.25f;
                if (c != null) {
                    c.konum = u.transform.position;
                }
                string hedef = u.Target != null ? u.Target.DisplayName : string.Empty;
                string durum;
                if (u.CharacterStats.IsAlive == false) {
                    durum = "Ölü";
                } else if (Takas.SunucudaAcik(u)) {
                    durum = "Takasta";
                } else if (Pazar.SunucudaAcik(u)) {
                    durum = "Pazar kurmuş";
                } else if (u.CharacterCombat != null && u.CharacterCombat.GetInCombat()) {
                    durum = "Savaşta" + (hedef.Length > 0 ? " → " + hedef : string.Empty);
                } else if (u.IsMounted) {
                    durum = yuruyor ? "Binekle gidiyor" : "Binekte";
                } else {
                    durum = yuruyor ? "Yürüyor" : "Duruyor";
                }
                int oturumDk = c != null ? (int)((Simdi - c.bas) / 60) : 0;
                int bakir = gumus != null ? u.CharacterCurrencyManager.GetBaseCurrencyValue(gumus) : 0;
                if (ilk == false) {
                    o.Append(',');
                }
                ilk = false;
                o.Append('{');
                Alan(o, "hesapNo", kv.Key);
                Alan(o, "hesap", hesap != null ? hesap.UserName : "#" + kv.Key);
                Alan(o, "karakter", u.DisplayName);
                Alan(o, "sinif", u.BaseCharacter != null && u.BaseCharacter.CharacterClass != null ? u.BaseCharacter.CharacterClass.DisplayName : string.Empty);
                Alan(o, "seviye", u.CharacterStats.Level);
                Alan(o, "harita", HaritaSeviyeleri.Ad(u.gameObject.scene.name));
                Alan(o, "can", canYuzde);
                Alan(o, "durum", durum);
                Alan(o, "oturumDk", oturumDk);
                Alan(o, "toplamDk", k != null ? k.Dakika : oturumDk);
                Alan(o, "kut", KutDukkani.KutMiktari(u));
                Alan(o, "para", OtukenPencere.ParaYazisi(bakir));
                Alan(o, "ip", ip);
                Alan(o, "google", hesap != null && string.IsNullOrEmpty(hesap.GoogleId) == false, true);
                o.Append('}');
            }
            o.Append(']');

            StringBuilder g = new StringBuilder("{");
            Alan(g, "surum", oyun.SystemConfigurationManager.ClientVersion);
            Alan(g, "calisma", (int)(t - basladigi));
            Alan(g, "cevrimici", oyun.PlayerManagerServer.ActiveUnitControllers.Count);
            Alan(g, "gunTepe", sureler.gunTepe);
            Alan(g, "gunOyuncu", sureler.gunHesaplar.Count);
            Alan(g, "gunYeni", sureler.yeniHesapGun);
            Alan(g, "hesap", bilinenHesapSayisi);
            Alan(g, "googleHesap", googleHesapSayisi);
            Alan(g, "etkinlik", Etkinlikler.AfisYazisi());
            Alan(g, "reklamOdulu", reklamOdulu);
            Alan(g, "yerdekiGanimet", YerdekiGanimet.DusenSayisi + " düştü, " + YerdekiGanimet.AlinanSayisi + " alındı, " + YerdekiGanimet.KaybolanSayisi + " kayboldu");
            if (t >= sonrakiOdemeSayimi) {
                sonrakiOdemeSayimi = t + 30f;
                odemeBugun = BugunkuOdemeler();
            }
            Alan(g, "odemeBugun", odemeBugun);
            using (System.Diagnostics.Process surec = System.Diagnostics.Process.GetCurrentProcess()) {
                Alan(g, "bellekMb", (int)(surec.WorkingSet64 / 1048576));
            }
            Alan(g, "yuk", DosyaSatiri("/proc/loadavg"));
            Alan(g, "makineBellek", MakineBellegi());
            googleKurulu = GoogleGiris.Kurulu;
            googleWebIstemci = GoogleGiris.WebIstemci;
            Alan(g, "google", googleKurulu ? (GoogleGiris.SinamaSonucu ?? "sınanmadı") : "kapalı (web istemci kimliği yok)");
            Alan(g, "saat", Etkinlikler.TurkiyeSaati.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture), true);
            g.Append('}');

            if (t >= sonrakiHesapGoruntusu) {
                sonrakiHesapGoruntusu = t + 15f;
                HesapGoruntusu(oyun);
            }
            lock (kilit) {
                oyuncularJson = o.ToString();
                genelJson = g.ToString();
            }
        }

        private static void HesapGoruntusu(SystemGameManager oyun) {
            List<UserAccount> hepsi = oyun.UserAccountService.TumHesaplar();
            bilinenHesapSayisi = hepsi.Count;
            googleHesapSayisi = 0;
            foreach (UserAccount a in hepsi) {
                if (string.IsNullOrEmpty(a.GoogleId) == false) {
                    googleHesapSayisi++;
                }
            }
            hepsi.Sort((a, b) => {
                SureKaydi ka, kb;
                kayitDizini.TryGetValue(a.Id, out ka);
                kayitDizini.TryGetValue(b.Id, out kb);
                return (kb != null ? kb.son : 0).CompareTo(ka != null ? ka.son : 0);
            });
            StringBuilder h = new StringBuilder("[");
            int sayi = 0;
            foreach (UserAccount a in hepsi) {
                if (sayi++ >= 1000) {
                    break;
                }
                SureKaydi k;
                kayitDizini.TryGetValue(a.Id, out k);
                if (sayi > 1) {
                    h.Append(',');
                }
                h.Append('{');
                Alan(h, "no", a.Id);
                Alan(h, "ad", a.UserName);
                Alan(h, "karakter", k != null ? k.karakter : string.Empty);
                Alan(h, "dakika", k != null ? k.Dakika : 0);
                Alan(h, "oturum", k != null ? k.oturum : 0);
                Alan(h, "ilk", k != null ? Zaman(k.ilk) : string.Empty);
                Alan(h, "son", k != null ? Zaman(k.son) : string.Empty);
                Alan(h, "cevrimici", cevrimici.ContainsKey(a.Id));
                Alan(h, "google", string.IsNullOrEmpty(a.GoogleId) == false);
                Alan(h, "yasak", YasakliMi(a.Id), true);
                h.Append('}');
            }
            h.Append(']');
            StringBuilder s = new StringBuilder("[");
            for (int i = sureler.oturumlar.Count - 1, j = 0; i >= 0 && j < 200; i--, j++) {
                OturumKaydi k = sureler.oturumlar[i];
                if (j > 0) {
                    s.Append(',');
                }
                s.Append('{');
                Alan(s, "ad", k.ad);
                Alan(s, "karakter", k.karakter);
                Alan(s, "bas", Zaman(k.bas));
                Alan(s, "dk", k.dk);
                Alan(s, "haritalar", k.haritalar, true);
                s.Append('}');
            }
            s.Append(']');
            lock (kilit) {
                hesaplarJson = h.ToString();
                oturumlarJson = s.ToString();
            }
        }

        private static string Zaman(long unix) {
            if (unix <= 0) {
                return string.Empty;
            }
            return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime.AddHours(3).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture);
        }

        private static string DosyaSatiri(string yol) {
            try {
                return File.Exists(yol) ? File.ReadAllText(yol).Trim() : string.Empty;
            } catch (Exception) {
                return string.Empty;
            }
        }

        private static string MakineBellegi() {
            try {
                if (File.Exists("/proc/meminfo") == false) {
                    return string.Empty;
                }
                long toplam = 0, bos = 0;
                foreach (string satir in File.ReadAllLines("/proc/meminfo")) {
                    string[] p = satir.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 2 && p[0] == "MemTotal") {
                        long.TryParse(p[1], out toplam);
                    } else if (p.Length >= 2 && p[0] == "MemAvailable") {
                        long.TryParse(p[1], out bos);
                    }
                }
                return ((toplam - bos) / 1024) + " / " + (toplam / 1024) + " MB";
            } catch (Exception) {
                return string.Empty;
            }
        }

        /// <summary>odemeler.txt satırları UTC zamanla başlar ("2026-10-07T17:12:31.0000000Z ..."); Türkiye gününe göre sayılır</summary>
        private static int BugunkuOdemeler() {
            try {
                string yol = Path.Combine(veriKlasoru, "odemeler.txt");
                if (File.Exists(yol) == false) {
                    return 0;
                }
                DateTime bugun = Etkinlikler.TurkiyeSaati.Date;
                int n = 0;
                foreach (string satir in SonSatirlar(yol, 2000)) {
                    int bosluk = satir.IndexOf(' ');
                    DateTime zaman;
                    if (bosluk > 0 && DateTime.TryParse(satir.Substring(0, bosluk), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out zaman) && zaman.AddHours(3).Date == bugun) {
                        n++;
                    }
                }
                return n;
            } catch (Exception) {
                return 0;
            }
        }

        // ---------------------------------------------------------------- günlük

        private static void GunlugeYaz(string ileti, string yigin, LogType tur) {
            if (string.IsNullOrEmpty(ileti)) {
                return;
            }
            bool onemli = tur == LogType.Error || tur == LogType.Exception || tur == LogType.Assert || tur == LogType.Warning;
            if (onemli == false && ileti.StartsWith("[") == false) {
                return;
            }
            string satir = DateTime.UtcNow.AddHours(3).ToString("dd.MM HH:mm:ss", CultureInfo.InvariantCulture)
                + (tur == LogType.Log ? " " : " [" + tur + "] ") + (ileti.Length > 400 ? ileti.Substring(0, 400) : ileti);
            if (tur == LogType.Exception && string.IsNullOrEmpty(yigin) == false) {
                string[] y = yigin.Split('\n');
                satir += " @ " + y[0];
            }
            lock (kilit) {
                gunluk.AddLast(satir);
                while (gunluk.Count > 500) {
                    gunluk.RemoveFirst();
                }
            }
        }

        // ================================================================ şifre ve oturum

        [Serializable]
        private class SifreKaydi {
            public string tuz = string.Empty;
            public string ozet = string.Empty;
        }

        /// <summary>PBKDF2-HMAC-SHA256, 100.000 tur (her çalışma zamanında aynı sonucu versin diye elle)</summary>
        private static string Ozet(string sifre, string tuz) {
            byte[] tuzB = Hex(tuz);
            using (HMACSHA256 h = new HMACSHA256(Encoding.UTF8.GetBytes(sifre ?? string.Empty))) {
                byte[] giris = new byte[tuzB.Length + 4];
                Buffer.BlockCopy(tuzB, 0, giris, 0, tuzB.Length);
                giris[tuzB.Length + 3] = 1;
                byte[] u = h.ComputeHash(giris);
                byte[] t = (byte[])u.Clone();
                for (int i = 1; i < 100000; i++) {
                    u = h.ComputeHash(u);
                    for (int j = 0; j < t.Length; j++) {
                        t[j] ^= u[j];
                    }
                }
                return HexYaz(t);
            }
        }

        private static byte[] Rastgele(int n) {
            byte[] b = new byte[n];
            using (RandomNumberGenerator r = RandomNumberGenerator.Create()) {
                r.GetBytes(b);
            }
            return b;
        }

        private static string HexYaz(byte[] b) {
            StringBuilder sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) {
                sb.Append(x.ToString("x2"));
            }
            return sb.ToString();
        }

        private static byte[] Hex(string s) {
            byte[] b = new byte[s.Length / 2];
            for (int i = 0; i < b.Length; i++) {
                b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            }
            return b;
        }

        private static void SifreKaydet(string sifre) {
            SifreKaydi k = new SifreKaydi() { tuz = HexYaz(Rastgele(16)) };
            k.ozet = Ozet(sifre, k.tuz);
            File.WriteAllText(SifreDosyasi, JsonUtility.ToJson(k));
            Kisitla(SifreDosyasi);
        }

        private static void SifreHazirla() {
            if (File.Exists(SifreDosyasi)) {
                return;
            }
            const string harfler = "abcdefghjkmnpqrstuvwxyz23456789";
            byte[] b = Rastgele(12);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 12; i++) {
                if (i > 0 && i % 4 == 0) {
                    sb.Append('-');
                }
                sb.Append(harfler[b[i] % harfler.Length]);
            }
            SifreKaydet(sb.ToString());
            File.WriteAllText(IlkSifreDosyasi, sb + "\n");
            Kisitla(IlkSifreDosyasi);
            Debug.Log("[Sunucu] yönetim paneli ilk şifresi yazıldı: " + IlkSifreDosyasi);
        }

        /// <summary>dosyayı yalnız sunucunun kullanıcısı okuyabilsin (chmod 600)</summary>
        private static void Kisitla(string yol) {
            try {
                if (Application.platform == RuntimePlatform.LinuxServer || Application.platform == RuntimePlatform.LinuxPlayer) {
                    System.Diagnostics.Process p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("chmod", "600 \"" + yol + "\"") {
                        UseShellExecute = false, CreateNoWindow = true
                    });
                    if (p != null) {
                        p.WaitForExit(3000);
                    }
                }
            } catch (Exception) {
            }
        }

        private static bool SifreDogru(string sifre) {
            try {
                SifreKaydi k = JsonUtility.FromJson<SifreKaydi>(File.ReadAllText(SifreDosyasi));
                if (k == null || string.IsNullOrEmpty(k.tuz)) {
                    return false;
                }
                string a = Ozet(sifre, k.tuz);
                int fark = a.Length ^ k.ozet.Length;
                for (int i = 0; i < Math.Min(a.Length, k.ozet.Length); i++) {
                    fark |= a[i] ^ k.ozet[i];
                }
                return fark == 0;
            } catch (Exception) {
                return false;
            }
        }

        private static bool OturumVar(HttpListenerRequest istek) {
            Cookie c = istek.Cookies[Cerez];
            if (c == null || string.IsNullOrEmpty(c.Value)) {
                return false;
            }
            lock (kilit) {
                DateTime bitis;
                return jetonlar.TryGetValue(c.Value, out bitis) && bitis > DateTime.UtcNow;
            }
        }

        // ================================================================ HTTP (ayrı iş parçacığı)

        private static void Dinle() {
            while (dinleyici != null && dinleyici.IsListening) {
                HttpListenerContext ctx;
                try {
                    ctx = dinleyici.GetContext();
                } catch (Exception) {
                    break;
                }
                ThreadPool.QueueUserWorkItem(_ => {
                    try {
                        Isle(ctx);
                    } catch (Exception e) {
                        try {
                            Yanit(ctx, 500, "{\"hata\":" + Esc(e.Message) + "}", "application/json");
                        } catch (Exception) {
                        }
                    }
                });
            }
        }

        [Serializable] private class GirisIstegi { public string sifre = string.Empty; }
        [Serializable] private class HesapIstegi { public int hesap; }
        [Serializable] private class MetinIstegi { public string metin = string.Empty; }
        [Serializable] private class AnahtarIstegi { public string anahtar = string.Empty; public bool sina; }
        [Serializable] private class SifreIstegi { public string eski = string.Empty; public string yeni = string.Empty; }

        private static void Isle(HttpListenerContext ctx) {
            HttpListenerRequest istek = ctx.Request;
            string yol = istek.Url.AbsolutePath.TrimEnd('/');
            bool post = istek.HttpMethod == "POST";
            string govde = string.Empty;
            if (post) {
                using (StreamReader r = new StreamReader(istek.InputStream, Encoding.UTF8)) {
                    char[] tampon = new char[8192];
                    int n = r.ReadBlock(tampon, 0, tampon.Length);
                    govde = new string(tampon, 0, n);
                }
            }
            if (yol == string.Empty) {
                ctx.Response.Redirect("/yonetim");
                Yanit(ctx, 302, string.Empty, "text/plain");
                return;
            }
            if (yol == "/yonetim") {
                Yanit(ctx, 200, YonetimSayfasi.Html, "text/html; charset=utf-8");
                return;
            }
            if (yol == "/yonetim/giris" && post) {
                string ip = istek.RemoteEndPoint != null ? istek.RemoteEndPoint.Address.ToString() : "?";
                lock (kilit) {
                    KeyValuePair<int, DateTime> d;
                    if (denemeler.TryGetValue(ip, out d) && d.Value > DateTime.UtcNow && d.Key >= 8) {
                        Yanit(ctx, 429, "{\"hata\":\"Çok fazla deneme; 10 dakika sonra yine dene.\"}", "application/json");
                        return;
                    }
                }
                GirisIstegi g = Oku<GirisIstegi>(govde);
                if (g == null || SifreDogru(g.sifre) == false) {
                    lock (kilit) {
                        KeyValuePair<int, DateTime> d;
                        if (denemeler.TryGetValue(ip, out d) == false || d.Value < DateTime.UtcNow) {
                            d = new KeyValuePair<int, DateTime>(0, DateTime.UtcNow.AddMinutes(10));
                        }
                        denemeler[ip] = new KeyValuePair<int, DateTime>(d.Key + 1, d.Value);
                    }
                    Debug.Log("[Sunucu] yönetim paneline yanlış şifre (" + ip + ")");
                    Yanit(ctx, 401, "{\"hata\":\"Şifre yanlış.\"}", "application/json");
                    return;
                }
                string jeton = HexYaz(Rastgele(32));
                lock (kilit) {
                    jetonlar[jeton] = DateTime.UtcNow.AddHours(12);
                }
                ctx.Response.AppendHeader("Set-Cookie", Cerez + "=" + jeton + "; HttpOnly; SameSite=Strict; Path=/yonetim; Max-Age=43200");
                Debug.Log("[Sunucu] yönetim paneline girildi (" + ip + ")");
                Yanit(ctx, 200, "{\"tamam\":true}", "application/json");
                return;
            }
            if (OturumVar(istek) == false) {
                Yanit(ctx, 401, "{\"hata\":\"giriş gerekli\"}", "application/json");
                return;
            }
            string json;
            switch (yol) {
                case "/yonetim/cikis":
                    Cookie c = istek.Cookies[Cerez];
                    lock (kilit) {
                        if (c != null) {
                            jetonlar.Remove(c.Value);
                        }
                    }
                    ctx.Response.AppendHeader("Set-Cookie", Cerez + "=; Path=/yonetim; Max-Age=0");
                    json = "{\"tamam\":true}";
                    break;
                case "/yonetim/api/genel":
                    lock (kilit) {
                        json = genelJson;
                    }
                    break;
                case "/yonetim/api/oyuncular":
                    lock (kilit) {
                        json = oyuncularJson;
                    }
                    break;
                case "/yonetim/api/hesaplar":
                    lock (kilit) {
                        json = hesaplarJson;
                    }
                    break;
                case "/yonetim/api/oturumlar":
                    lock (kilit) {
                        json = oturumlarJson;
                    }
                    break;
                case "/yonetim/api/gunluk":
                    StringBuilder gb = new StringBuilder("[");
                    lock (kilit) {
                        bool ilk = true;
                        for (LinkedListNode<string> d = gunluk.Last; d != null; d = d.Previous) {
                            if (ilk == false) {
                                gb.Append(',');
                            }
                            ilk = false;
                            gb.Append(Esc(d.Value));
                        }
                    }
                    json = gb.Append(']').ToString();
                    break;
                case "/yonetim/api/kayit":
                    json = KayitOku(istek.QueryString["tur"]);
                    break;
                case "/yonetim/api/at":
                    HesapIstegi at = Oku<HesapIstegi>(govde);
                    if (at != null) {
                        isler.Enqueue(o => {
                            o.NetworkManagerServer.KickPlayer(at.hesap);
                            Debug.Log("[Sunucu] yönetim: #" + at.hesap + " oyundan atıldı");
                        });
                    }
                    json = "{\"tamam\":true}";
                    break;
                case "/yonetim/api/yasakla":
                case "/yonetim/api/yasak-kaldir":
                    HesapIstegi y = Oku<HesapIstegi>(govde);
                    bool yasakla = yol.EndsWith("yasakla");
                    if (y != null) {
                        lock (kilit) {
                            if (yasakla) {
                                yasaklar.Add(y.hesap);
                            } else {
                                yasaklar.Remove(y.hesap);
                            }
                        }
                        isler.Enqueue(o => {
                            YasaklariYaz(o);
                            if (yasakla && o.PlayerManagerServer.ActiveUnitControllers.ContainsKey(y.hesap)) {
                                o.NetworkManagerServer.KickPlayer(y.hesap);
                            }
                            Debug.Log("[Sunucu] yönetim: #" + y.hesap + (yasakla ? " yasaklandı" : " yasağı kaldırıldı"));
                            sonrakiHesapGoruntusu = 0f;
                        });
                    }
                    json = "{\"tamam\":true}";
                    break;
                case "/yonetim/api/duyuru":
                    MetinIstegi m = Oku<MetinIstegi>(govde);
                    if (m != null && string.IsNullOrEmpty(m.metin) == false) {
                        string metin = m.metin.Length > 300 ? m.metin.Substring(0, 300) : m.metin;
                        metin = metin.Replace("<", "‹").Replace(">", "›");
                        isler.Enqueue(o => {
                            foreach (UnitController u in o.PlayerManagerServer.ActiveUnitControllers.Values) {
                                if (u != null) {
                                    OtukenAg.Mesaj(u, "<color=#FFD54A><b>[Duyuru]</b> " + metin + "</color>");
                                }
                            }
                            Debug.Log("[Sunucu] yönetim duyurusu: " + metin);
                        });
                    }
                    json = "{\"tamam\":true}";
                    break;
                case "/yonetim/api/google":
                    AnahtarIstegi a = post ? Oku<AnahtarIstegi>(govde) : null;
                    if (a != null && a.sina) {
                        GoogleGiris.SinamaSonucu = "sınanıyor...";
                        isler.Enqueue(o => o.NetworkManagerServer.StartCoroutine(GoogleGiris.AnahtariSina()));
                    } else if (a != null) {
                        string anahtar = a != null ? (a.anahtar ?? string.Empty).Trim() : string.Empty;
                        if (anahtar.Length > 0 && (anahtar.Length > 200 || anahtar.IndexOfAny(new[] { ' ', '\n', '\r', '"' }) >= 0)) {
                            Yanit(ctx, 400, "{\"hata\":\"Anahtar geçersiz görünüyor.\"}", "application/json");
                            return;
                        }
                        if (anahtar.Length == 0) {
                            if (File.Exists(GoogleGiris.AnahtarYolu)) {
                                File.Delete(GoogleGiris.AnahtarYolu);
                            }
                        } else {
                            File.WriteAllText(GoogleGiris.AnahtarYolu, anahtar);
                            Kisitla(GoogleGiris.AnahtarYolu);
                        }
                        GoogleGiris.SinamaSonucu = anahtar.Length > 0 ? "sınanıyor..." : "gizli anahtar girilmedi";
                        isler.Enqueue(o => o.NetworkManagerServer.StartCoroutine(GoogleGiris.AnahtariSina()));
                        Debug.Log("[Sunucu] yönetim: Google gizli anahtarı " + (anahtar.Length > 0 ? "kaydedildi" : "silindi"));
                    }
                    StringBuilder gs = new StringBuilder("{");
                    Alan(gs, "kurulu", googleKurulu);
                    Alan(gs, "webIstemci", googleWebIstemci);
                    Alan(gs, "anahtarVar", File.Exists(GoogleGiris.AnahtarYolu) && new FileInfo(GoogleGiris.AnahtarYolu).Length > 0);
                    Alan(gs, "sinama", GoogleGiris.SinamaSonucu ?? "sınanmadı", true);
                    json = gs.Append('}').ToString();
                    break;
                case "/yonetim/api/sifre":
                    SifreIstegi s = Oku<SifreIstegi>(govde);
                    if (s == null || SifreDogru(s.eski) == false) {
                        Yanit(ctx, 400, "{\"hata\":\"Eski şifre yanlış.\"}", "application/json");
                        return;
                    }
                    if (string.IsNullOrEmpty(s.yeni) || s.yeni.Length < 10) {
                        Yanit(ctx, 400, "{\"hata\":\"Yeni şifre en az 10 karakter olmalı.\"}", "application/json");
                        return;
                    }
                    SifreKaydet(s.yeni);
                    if (File.Exists(IlkSifreDosyasi)) {
                        File.Delete(IlkSifreDosyasi);
                    }
                    lock (kilit) {
                        jetonlar.Clear();
                    }
                    Debug.Log("[Sunucu] yönetim paneli şifresi değiştirildi");
                    json = "{\"tamam\":true}";
                    break;
                default:
                    Yanit(ctx, 404, "{\"hata\":\"yok\"}", "application/json");
                    return;
            }
            Yanit(ctx, 200, json, "application/json");
        }

        private static string KayitOku(string tur) {
            string dosya;
            switch (tur) {
                case "sohbet": dosya = "sohbet.log"; break;
                case "sikayet": dosya = "sikayetler.log"; break;
                case "ticaret": dosya = "ticaret.log"; break;
                case "odeme": dosya = "odemeler.txt"; break;
                default: return "[]";
            }
            string yol = Path.Combine(veriKlasoru, dosya);
            if (File.Exists(yol) == false) {
                return "[]";
            }
            List<string> satirlar = SonSatirlar(yol, 300);
            StringBuilder sb = new StringBuilder("[");
            for (int i = satirlar.Count - 1; i >= 0; i--) {
                if (i < satirlar.Count - 1) {
                    sb.Append(',');
                }
                sb.Append(Esc(satirlar[i]));
            }
            return sb.Append(']').ToString();
        }

        private static List<string> SonSatirlar(string yol, int adet) {
            LinkedList<string> son = new LinkedList<string>();
            using (FileStream fs = new FileStream(yol, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (StreamReader r = new StreamReader(fs, Encoding.UTF8)) {
                string satir;
                while ((satir = r.ReadLine()) != null) {
                    son.AddLast(satir);
                    if (son.Count > adet) {
                        son.RemoveFirst();
                    }
                }
            }
            return new List<string>(son);
        }

        private static T Oku<T>(string json) where T : class {
            try {
                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json);
            } catch (Exception) {
                return null;
            }
        }

        private static void Yanit(HttpListenerContext ctx, int kod, string icerik, string tur) {
            byte[] b = Encoding.UTF8.GetBytes(icerik ?? string.Empty);
            HttpListenerResponse r = ctx.Response;
            r.StatusCode = kod;
            r.ContentType = tur;
            r.AppendHeader("Cache-Control", "no-store");
            r.AppendHeader("X-Frame-Options", "DENY");
            r.ContentLength64 = b.Length;
            r.OutputStream.Write(b, 0, b.Length);
            r.OutputStream.Close();
        }

        // ---------------------------------------------------------------- JSON

        private static string Esc(string s) {
            if (s == null) {
                return "\"\"";
            }
            StringBuilder sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            foreach (char c in s) {
                switch (c) {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        } else {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        private static void Alan(StringBuilder sb, string ad, string deger, bool son = false) {
            sb.Append('"').Append(ad).Append("\":").Append(Esc(deger));
            if (son == false) {
                sb.Append(',');
            }
        }

        private static void Alan(StringBuilder sb, string ad, int deger, bool son = false) {
            sb.Append('"').Append(ad).Append("\":").Append(deger.ToString(CultureInfo.InvariantCulture));
            if (son == false) {
                sb.Append(',');
            }
        }

        private static void Alan(StringBuilder sb, string ad, bool deger, bool son = false) {
            sb.Append('"').Append(ad).Append("\":").Append(deger ? "true" : "false");
            if (son == false) {
                sb.Append(',');
            }
        }
    }
}
