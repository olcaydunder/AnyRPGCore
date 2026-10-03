using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Hata bildirimi: oyunda olan her sorunu kendiliğinden geliştiriciye gönderir.
    ///  - hatalar ve istisnalar (her farklı hata oturumda bir kez, yığın izi ve son kayıt satırlarıyla)
    ///  - çökme, donma (ANR) ve bellek yüzünden kapanma: Android 11+ sistemin kayıtlarından, daha eskilerde
    ///    "önceki oturum arka plana geçmeden kapandı" işaretinden (ErrorOverlay)
    ///  - bellek uyarısı, uzun süren düşük kare hızı
    ///  - arayüz raporu: her sürümde ve ekran boyutunda bir kez göstergelerin yerleşimi, çakışmalar ve ekran görüntüsü
    ///  - oyuncunun Seçenekler > "Sorun Bildir" ile yazdığı not ve ekran görüntüsü
    /// Raporlar ntfy.sh'deki gizli bir kanala gider (kanal adı derlemede HATA_KANALI gizli bilgisinden
    /// Resources/HataKanali.txt'ye yazılır; yoksa bildirim kapalıdır). GitHub'daki "Hata raporları" iş akışı
    /// kanalı düzenli okur ve her farklı hatayı depoda bir kayıt (issue) olarak açar, tekrarları sayar.
    /// İnternet yoksa raporlar dosyada bekler, sonra gönderilir. Kişisel bilgi gönderilmez.
    /// Seçenekler > Oyun > "Hata raporları" ile kapatılabilir.
    /// </summary>
    public class HataBildirici : MonoBehaviour {

        public const string CanvasName = "SorunBildirCanvas";
        private const int SortingOrder = 32;

        private const string Sunucu = "https://ntfy.sh";
        private const string KanalKaynagi = "HataKanali";
        private const string AcikKey = "HataRaporlari";
        private const string ArayuzKey = "HataArayuzRaporu";
        private const string CikisKey = "HataSonCikis";
        private const string KuyrukDosyasi = "hata_kuyrugu.txt";
        private const int OturumSiniri = 30;
        private const int MesajSiniri = 3800;
        private const int KuyrukSiniri = 40;
        private const float ArayuzBekleme = 15f;
        private const float DusukFps = 15f;

        [Serializable]
        private class NtfyMesaj {
            public string topic;
            public string title;
            public string message;
            public string[] tags;
            public int priority;
        }

        private class Ham {
            public string tur;
            public string mesaj;
            public string yigin;
        }

        private static HataBildirici ornek = null;
        private static string kanal = null;
        private static readonly object kilit = new object();
        private static readonly Queue<Ham> hamlar = new Queue<Ham>();
        [ThreadStatic]
        private static bool raporlaniyor;

        private readonly HashSet<string> gorulen = new HashSet<string>();
        private readonly Dictionary<string, int> tekrarlar = new Dictionary<string, int>();
        private readonly Queue<string> gonderilecek = new Queue<string>();
        private int oturumSayisi = 0;
        private bool gonderiyor = false;
        private float sonrakiGonderim = 0f;
        private float sonrakiKuyrukDenemesi = 0f;
        private bool bellekUyarisi = false;
        private bool bellekRaporlandi = false;
        private readonly HashSet<string> yavasSahneler = new HashSet<string>();
        private float fpsToplam = 0f;
        private int fpsKare = 0;
        private float fpsBaslangic = 0f;
        private float oyundaBeri = -1f;
        private bool arayuzBakildi = false;
        private SystemGameManager oyunYoneticisi = null;

        // Sorun Bildir penceresi
        private Font font = null;
        private GameObject pencereKoku = null;
        private InputField notAlani = null;
        private Text durumYazisi = null;
        private Button gonderDugmesi = null;
        private byte[] bekleyenGoruntu = null;

        /// <summary>bu derlemede hata bildirimi var mı (kanal tanımlı mı)</summary>
        public static bool Etkin { get { return ornek != null; } }

        /// <summary>oyuncu kapatmadıysa açık</summary>
        public static bool Acik {
            get { return PlayerPrefs.GetInt(AcikKey, 1) == 1; }
            set { PlayerPrefs.SetInt(AcikKey, value ? 1 : 0); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Baslat() {
            if (Application.isEditor || ornek != null) {
                return;
            }
            TextAsset kaynak = Resources.Load<TextAsset>(KanalKaynagi);
            kanal = kaynak != null ? kaynak.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(kanal)) {
                return;
            }
            Application.logMessageReceivedThreaded += KayitGeldi;
            AppDomain.CurrentDomain.UnhandledException += YakalanmamisHata;
            GameObject nesne = new GameObject("HataBildirici");
            DontDestroyOnLoad(nesne);
            ornek = nesne.AddComponent<HataBildirici>();
        }

        // ---------------------------------------------------------------- toplama (her iş parçacığından)

        private static void KayitGeldi(string mesaj, string yigin, LogType tur) {
            if (raporlaniyor || (tur != LogType.Error && tur != LogType.Exception && tur != LogType.Assert)) {
                return;
            }
            if (mesaj != null && mesaj.StartsWith("[HataBildirici]", StringComparison.Ordinal)) {
                return;
            }
            Ekle(tur == LogType.Exception ? "istisna" : "hata", mesaj, yigin);
        }

        private static void YakalanmamisHata(object gonderen, UnhandledExceptionEventArgs e) {
            Exception istisna = e.ExceptionObject as Exception;
            Ekle("istisna", istisna != null ? "[Yakalanmamış] " + istisna.GetType().Name + ": " + istisna.Message : "[Yakalanmamış] bilinmiyor",
                istisna != null ? istisna.StackTrace : string.Empty);
        }

        private static void Ekle(string tur, string mesaj, string yigin) {
            lock (kilit) {
                if (hamlar.Count < 60) {
                    hamlar.Enqueue(new Ham() { tur = tur, mesaj = mesaj ?? string.Empty, yigin = yigin ?? string.Empty });
                }
            }
        }

        // ---------------------------------------------------------------- ana döngü

        private void Start() {
            Application.lowMemory += () => bellekUyarisi = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            StartCoroutine(Acilis());
        }

        private IEnumerator Acilis() {
            // ağ ve oyun yöneticisi hazır olsun
            yield return new WaitForSecondsRealtime(8f);
            raporlaniyor = true;
            try {
                if (Acik) {
                    OncekiOturum();
                }
            } catch (Exception e) {
                Debug.LogWarning("[HataBildirici] önceki oturum okunamadı: " + e.Message);
            } finally {
                raporlaniyor = false;
            }
            sonrakiKuyrukDenemesi = Time.unscaledTime;
        }

        private void Update() {
            raporlaniyor = true;
            try {
                while (true) {
                    Ham ham;
                    lock (kilit) {
                        if (hamlar.Count == 0) {
                            break;
                        }
                        ham = hamlar.Dequeue();
                    }
                    if (Acik) {
                        Raporla(ham.tur, ham.mesaj, ham.yigin, null, null);
                    }
                }
                if (Acik) {
                    BellekDenetimi();
                    PerformansDenetimi();
                    YerlesimDenetimi();
                }
            } catch (Exception e) {
                Debug.LogWarning("[HataBildirici] " + e.Message);
            } finally {
                raporlaniyor = false;
            }

            if (gonderiyor == false && Time.unscaledTime >= sonrakiGonderim) {
                if (gonderilecek.Count > 0) {
                    StartCoroutine(Gonder(gonderilecek.Dequeue()));
                } else if (Time.unscaledTime >= sonrakiKuyrukDenemesi && sonrakiKuyrukDenemesi > 0f) {
                    sonrakiKuyrukDenemesi = Time.unscaledTime + 120f;
                    DosyaKuyrugunuYukle();
                }
            }
        }

        private SystemGameManager OyunYoneticisi {
            get {
                if (oyunYoneticisi == null && SystemGameManager.IsShuttingDown == false) {
                    oyunYoneticisi = FindAnyObjectByType<SystemGameManager>();
                }
                return oyunYoneticisi;
            }
        }

        private UnitController Oyuncu {
            get {
                SystemGameManager oyun = OyunYoneticisi;
                PlayerManagerClient oyuncular = oyun != null ? oyun.PlayerManagerClient : null;
                return oyuncular != null && oyuncular.PlayerUnitSpawned ? oyuncular.UnitController : null;
            }
        }

        // ---------------------------------------------------------------- denetimler

        private void BellekDenetimi() {
            if (bellekUyarisi == false || bellekRaporlandi) {
                return;
            }
            bellekRaporlandi = true;
            string ayrinti = "Ayrılan " + Mb(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong())
                + " | ayrılmış " + Mb(UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong())
                + " | betik " + Mb(UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong())
                + " | cihaz " + SystemInfo.systemMemorySize + " MB | ekran kartı " + SystemInfo.graphicsMemorySize + " MB";
            Raporla("bellek", "Cihazın belleği azaldı (Android bellek uyarısı)", string.Empty, ayrinti, null);
        }

        private static string Mb(long bayt) {
            return (bayt / 1048576) + " MB";
        }

        private void PerformansDenetimi() {
            if (Oyuncu == null || Time.unscaledDeltaTime <= 0f) {
                fpsKare = 0;
                return;
            }
            // arka plandan dönüş ya da yükleme takılması ortalamayı bozmasın
            if (Time.unscaledDeltaTime > 0.5f) {
                return;
            }
            if (fpsKare == 0) {
                fpsBaslangic = Time.unscaledTime;
                fpsToplam = 0f;
            }
            fpsToplam += Time.unscaledDeltaTime;
            fpsKare++;
            float sure = Time.unscaledTime - fpsBaslangic;
            if (sure < 20f) {
                return;
            }
            float fps = fpsKare / Mathf.Max(0.001f, fpsToplam);
            fpsKare = 0;
            string sahne = SceneManager.GetActiveScene().name;
            if (fps >= DusukFps || yavasSahneler.Contains(sahne)) {
                return;
            }
            yavasSahneler.Add(sahne);
            Raporla("performans", sahne + " haritasında kare hızı düşük: 20 saniyede ortalama " + fps.ToString("0.0") + " FPS",
                string.Empty, null, null);
        }

        private void YerlesimDenetimi() {
            if (Oyuncu == null) {
                oyundaBeri = -1f;
                return;
            }
            if (oyundaBeri < 0f) {
                oyundaBeri = Time.unscaledTime;
            }
            if (arayuzBakildi || Time.unscaledTime - oyundaBeri < ArayuzBekleme || MobileHud.Ornek == null
                || MobileHud.Ornek.gameObject.activeInHierarchy == false || PencereAcikMi()) {
                return;
            }
            arayuzBakildi = true;
            string anahtar = Application.version + "|" + Screen.width + "x" + Screen.height;
            if (PlayerPrefs.GetString(ArayuzKey, string.Empty) == anahtar) {
                return;
            }
            PlayerPrefs.SetString(ArayuzKey, anahtar);
            PlayerPrefs.Save();
            StartCoroutine(ArayuzRaporu());
        }

        private static bool PencereAcikMi() {
            return SeceneklerPenceresi.IsOpen || IsinlanmaPenceresi.IsOpen || GameGuide.IsOpen || GunlukArmagan.IsOpen;
        }

        private IEnumerator ArayuzRaporu() {
            MobileHud.Ornek.YerlesimiUygula();
            yield return null;
            int cakisma;
            Rect ekran = new Rect(0f, 0f, Screen.width, Screen.height);
            string ayrinti = ArayuzDenetimi.YerlesimRaporu(MobileHud.Ornek, ekran, Screen.safeArea, out cakisma);
            yield return new WaitForEndOfFrame();
            byte[] goruntu = EkranGoruntusu();
            string baslik = (cakisma == 0 ? "çakışma yok" : cakisma + " çakışma") + " (" + Screen.width + "x" + Screen.height + ")";
            Raporla("arayuz", baslik, string.Empty, ayrinti, goruntu, true);
        }

        // ---------------------------------------------------------------- önceki oturum: çökme, donma

        private void OncekiOturum() {
            bool sistemKaydi = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            sistemKaydi = AndroidCikislari();
#endif
            if (sistemKaydi == false && ErrorOverlay.PreviousSessionEndedUnexpectedly) {
                Raporla("cokme", "Oyun beklenmedik şekilde kapandı (çökme ya da sistemin kapatması)", string.Empty,
                    "Önceki oturumun kaydı:\n" + Son(ErrorOverlay.PreviousSessionReport, 1400), null);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Android 11+ (API 30): sistemin tuttuğu son kapanma nedenleri (çökme, yerel çökme, donma, bellek).
        /// Okunabildiyse true döner (o zaman "beklenmedik kapanma" tahminine gerek kalmaz).
        /// </summary>
        private bool AndroidCikislari() {
            try {
                using (AndroidJavaClass surum = new AndroidJavaClass("android.os.Build$VERSION")) {
                    if (surum.GetStatic<int>("SDK_INT") < 30) {
                        return false;
                    }
                }
                long son;
                long.TryParse(PlayerPrefs.GetString(CikisKey, "0"), out son);
                long enYeni = son;
                // ilk açılışta yalnız son üç günün kapanmaları
                long esik = son > 0 ? son : (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 3L * 24 * 3600 * 1000);
                using (AndroidJavaClass oynatici = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject etkinlik = oynatici.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject yonetici = etkinlik.Call<AndroidJavaObject>("getSystemService", "activity")) {
                    string paket = etkinlik.Call<string>("getPackageName");
                    using (AndroidJavaObject liste = yonetici.Call<AndroidJavaObject>("getHistoricalProcessExitReasons", paket, 0, 5)) {
                        int adet = liste.Call<int>("size");
                        for (int i = 0; i < adet; i++) {
                            using (AndroidJavaObject bilgi = liste.Call<AndroidJavaObject>("get", i)) {
                                long zaman = bilgi.Call<long>("getTimestamp");
                                if (zaman <= esik) {
                                    continue;
                                }
                                enYeni = Math.Max(enYeni, zaman);
                                int neden = bilgi.Call<int>("getReason");
                                string tur = CikisTuru(neden);
                                if (tur == null) {
                                    continue;
                                }
                                string aciklama = bilgi.Call<string>("getDescription");
                                long pss = bilgi.Call<long>("getPss");
                                int onem = bilgi.Call<int>("getImportance");
                                string iz = neden == 6 ? IzOku(bilgi, 70) : string.Empty;
                                string zamanYazi = DateTimeOffset.FromUnixTimeMilliseconds(zaman).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                                Raporla(tur, CikisAdi(neden) + (string.IsNullOrEmpty(aciklama) ? string.Empty : ": " + aciklama), iz,
                                    "Zaman " + zamanYazi + " | bellek (PSS) " + (pss / 1024) + " MB | önem " + onem + " (100 = ön planda)\n"
                                    + "Önceki oturumun kaydı:\n" + Son(ErrorOverlay.PreviousSessionReport, 1000), null);
                            }
                        }
                    }
                }
                PlayerPrefs.SetString(CikisKey, Math.Max(enYeni, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 1000).ToString());
                PlayerPrefs.Save();
                return true;
            } catch (Exception e) {
                Debug.LogWarning("[HataBildirici] kapanma nedenleri okunamadı: " + e.Message);
                return false;
            }
        }

        private static string IzOku(AndroidJavaObject bilgi, int satirSiniri) {
            try {
                using (AndroidJavaObject akis = bilgi.Call<AndroidJavaObject>("getTraceInputStream")) {
                    if (akis == null) {
                        return string.Empty;
                    }
                    using (AndroidJavaObject okuyucu = new AndroidJavaObject("java.io.InputStreamReader", akis))
                    using (AndroidJavaObject satirlar = new AndroidJavaObject("java.io.BufferedReader", okuyucu)) {
                        StringBuilder sb = new StringBuilder();
                        bool anaIs = false;
                        int yazilan = 0;
                        for (int i = 0; i < 4000 && yazilan < satirSiniri; i++) {
                            string satir = satirlar.Call<string>("readLine");
                            if (satir == null) {
                                break;
                            }
                            // donmada önemli olan ana iş parçacığı
                            if (satir.StartsWith("\"main\"", StringComparison.Ordinal)) {
                                anaIs = true;
                            } else if (anaIs && satir.Length == 0) {
                                break;
                            }
                            if (anaIs) {
                                sb.Append(satir).Append('\n');
                                yazilan++;
                            }
                        }
                        satirlar.Call("close");
                        return sb.ToString();
                    }
                }
            } catch (Exception) {
                return string.Empty;
            }
        }
#endif

        private static string CikisTuru(int neden) {
            switch (neden) {
                case 3: return "bellek";       // REASON_LOW_MEMORY
                case 4: return "cokme";        // REASON_CRASH (Java)
                case 5: return "cokme";        // REASON_CRASH_NATIVE
                case 6: return "anr";          // REASON_ANR
                case 7: return "cokme";        // REASON_INITIALIZATION_FAILURE
                case 9: return "bellek";       // REASON_EXCESSIVE_RESOURCE_USAGE
                default: return null;
            }
        }

        private static string CikisAdi(int neden) {
            switch (neden) {
                case 3: return "Bellek yetmedi, sistem oyunu kapattı";
                case 4: return "Çökme (Java)";
                case 5: return "Çökme (yerel kod)";
                case 6: return "Donma (ANR): oyun 5 saniyeden uzun yanıt vermedi";
                case 7: return "Başlatma hatası";
                case 9: return "Aşırı kaynak kullanımı, sistem oyunu kapattı";
                default: return "Kapanma " + neden;
            }
        }

        // ---------------------------------------------------------------- rapor

        private void Raporla(string tur, string mesaj, string yigin, string ayrinti, byte[] goruntu, bool zorla = false) {
            string imza = Imza(tur, mesaj, yigin);
            int tekrar;
            tekrarlar[imza] = tekrarlar.TryGetValue(imza, out tekrar) ? tekrar + 1 : 1;
            if (zorla == false && gorulen.Add(imza) == false) {
                return;
            }
            if (oturumSayisi >= OturumSiniri && tur != "oyuncu") {
                return;
            }
            oturumSayisi++;
            string kimlik = Guid.NewGuid().ToString("N").Substring(0, 10);

            StringBuilder ust = new StringBuilder();
            ust.Append("tur: ").Append(tur).Append('\n');
            ust.Append("imza: ").Append(imza).Append('\n');
            ust.Append("kimlik: ").Append(kimlik).Append('\n');
            ust.Append("surum: ").Append(Application.version).Append('\n');
            ust.Append("cihaz: ").Append(SystemInfo.deviceModel).Append('\n');
            ust.Append("sistem: ").Append(SystemInfo.operatingSystem).Append('\n');
            ust.Append("grafik: ").Append(SystemInfo.graphicsDeviceType).Append(" / ").Append(SystemInfo.graphicsDeviceName)
                .Append(" / bellek ").Append(SystemInfo.systemMemorySize).Append(" MB").Append('\n');
            ust.Append("ekran: ").Append(Screen.width).Append('x').Append(Screen.height).Append('\n');
            ust.Append("ayarlar: kalite ").Append(OyunAyarlari.Kalite).Append(", çözünürlük ").Append(OyunAyarlari.Cozunurluk)
                .Append(", gölge ").Append(OyunAyarlari.Golge).Append(", fps sınırı ").Append(OyunAyarlari.Fps).Append('\n');
            ust.Append("sahne: ").Append(SceneManager.GetActiveScene().name).Append('\n');
            UnitController oyuncu = Oyuncu;
            ust.Append("oyuncu: ").Append(oyuncu != null && oyuncu.CharacterStats != null ? "seviye " + oyuncu.CharacterStats.Level : "oyunda değil").Append('\n');
            ust.Append("sure: ").Append(Mathf.RoundToInt(Time.realtimeSinceStartup)).Append(" sn").Append('\n');
            if (goruntu != null) {
                ust.Append("ek: ").Append(kimlik).Append(".jpg").Append('\n');
            }

            string durum = string.Empty;
            string kayit = string.Empty;
            try {
                durum = ErrorOverlay.StatusSummary();
                kayit = ErrorOverlay.RecentLogLines(15);
            } catch (Exception) {
                // durum özeti olmadan da gönderilir
            }

            int kalan = MesajSiniri - Bayt(ust.ToString());
            StringBuilder govde = new StringBuilder(ust.ToString());
            kalan = Bolum(govde, "mesaj", mesaj, Mathf.Min(kalan, 700), kalan);
            kalan = Bolum(govde, "ayrinti", ayrinti, Mathf.Min(kalan, 1500), kalan);
            kalan = Bolum(govde, "yigin", KisaYigin(yigin, 30), Mathf.Min(kalan, 1500), kalan);
            kalan = Bolum(govde, "durum", durum, Mathf.Min(kalan, 600), kalan);
            Bolum(govde, "kayit", kayit, kalan, kalan);

            NtfyMesaj ntfy = new NtfyMesaj() {
                topic = kanal,
                title = Kes(TurAdi(tur) + ": " + IlkSatir(mesaj), 110),
                message = govde.ToString(),
                tags = new string[] { Etiket(tur) },
                priority = tur == "cokme" || tur == "anr" ? 4 : 3
            };
            gonderilecek.Enqueue(JsonUtility.ToJson(ntfy));
            if (goruntu != null) {
                StartCoroutine(EkGonder(goruntu, kimlik));
            }
        }

        private static int Bolum(StringBuilder govde, string ad, string icerik, int sinir, int kalan) {
            if (string.IsNullOrEmpty(icerik) || sinir < 40) {
                return kalan;
            }
            string baslik = "--- " + ad + "\n";
            string metin = KesBayt(icerik.TrimEnd(), sinir - Bayt(baslik) - 2);
            govde.Append(baslik).Append(metin).Append('\n');
            return kalan - Bayt(baslik) - Bayt(metin) - 1;
        }

        private static string TurAdi(string tur) {
            switch (tur) {
                case "istisna": return "İstisna";
                case "hata": return "Hata";
                case "cokme": return "Çökme";
                case "anr": return "Donma";
                case "bellek": return "Bellek";
                case "performans": return "Yavaşlık";
                case "arayuz": return "Arayüz";
                case "oyuncu": return "Oyuncu bildirimi";
                default: return tur;
            }
        }

        private static string Etiket(string tur) {
            switch (tur) {
                case "cokme": return "boom";
                case "anr": return "hourglass";
                case "bellek": return "floppy_disk";
                case "performans": return "turtle";
                case "arayuz": return "art";
                case "oyuncu": return "speech_balloon";
                default: return "warning";
            }
        }

        /// <summary>aynı hatayı her cihazda aynı imzayla tanımak için: sayılar, adresler ve dosya satırları atılır</summary>
        private static string Imza(string tur, string mesaj, string yigin) {
            StringBuilder sb = new StringBuilder(tur).Append('|');
            sb.Append(Normalle(Kes(IlkSatir(mesaj), 200))).Append('|');
            int cerceve = 0;
            if (string.IsNullOrEmpty(yigin) == false) {
                foreach (string ham in yigin.Split('\n')) {
                    string satir = ham.Trim();
                    if (satir.Length == 0 || satir.StartsWith("UnityEngine.Debug", StringComparison.Ordinal)
                        || satir.StartsWith("UnityEngine.Logger", StringComparison.Ordinal)) {
                        continue;
                    }
                    int kesme = satir.IndexOf(" (at ", StringComparison.Ordinal);
                    if (kesme > 0) {
                        satir = satir.Substring(0, kesme);
                    }
                    kesme = satir.IndexOf(" [0x", StringComparison.Ordinal);
                    if (kesme > 0) {
                        satir = satir.Substring(0, kesme);
                    }
                    sb.Append(Normalle(satir)).Append(';');
                    if (++cerceve >= 4) {
                        break;
                    }
                }
            }
            uint h = 2166136261;
            foreach (char c in sb.ToString()) {
                h ^= c;
                h *= 16777619;
            }
            return h.ToString("x8");
        }

        private static string Normalle(string s) {
            StringBuilder sb = new StringBuilder(s.Length);
            bool sayi = false;
            for (int i = 0; i < s.Length; i++) {
                char c = s[i];
                bool rakam = char.IsDigit(c) || (sayi && ((c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F') || c == 'x'));
                if (rakam) {
                    if (sayi == false) {
                        sb.Append('#');
                    }
                    sayi = true;
                } else {
                    sayi = false;
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static string KisaYigin(string yigin, int satirSiniri) {
            if (string.IsNullOrEmpty(yigin)) {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            int n = 0;
            foreach (string ham in yigin.Split('\n')) {
                string satir = ham.Trim();
                if (satir.Length == 0) {
                    continue;
                }
                sb.Append(satir).Append('\n');
                if (++n >= satirSiniri) {
                    break;
                }
            }
            return sb.ToString();
        }

        private static string IlkSatir(string metin) {
            if (string.IsNullOrEmpty(metin)) {
                return string.Empty;
            }
            int satir = metin.IndexOf('\n');
            return (satir >= 0 ? metin.Substring(0, satir) : metin).Trim();
        }

        private static string Kes(string metin, int uzunluk) {
            if (metin == null) {
                return string.Empty;
            }
            return metin.Length <= uzunluk ? metin : metin.Substring(0, uzunluk - 1) + "…";
        }

        private static string Son(string metin, int uzunluk) {
            if (string.IsNullOrEmpty(metin)) {
                return string.Empty;
            }
            return metin.Length <= uzunluk ? metin : "…" + metin.Substring(metin.Length - uzunluk);
        }

        private static int Bayt(string metin) {
            return Encoding.UTF8.GetByteCount(metin);
        }

        private static string KesBayt(string metin, int sinir) {
            if (sinir <= 0) {
                return string.Empty;
            }
            if (Bayt(metin) <= sinir) {
                return metin;
            }
            int uzunluk = Mathf.Min(metin.Length, sinir);
            while (uzunluk > 0 && Bayt(metin.Substring(0, uzunluk)) > sinir - 3) {
                uzunluk -= Mathf.Max(1, uzunluk / 20);
            }
            return metin.Substring(0, Mathf.Max(0, uzunluk)) + "…";
        }

        // ---------------------------------------------------------------- gönderme

        private IEnumerator Gonder(string govde) {
            gonderiyor = true;
            if (Application.internetReachability == NetworkReachability.NotReachable) {
                KuyrugaYaz(govde);
                sonrakiGonderim = Time.unscaledTime + 60f;
                gonderiyor = false;
                yield break;
            }
            using (UnityWebRequest istek = new UnityWebRequest(Sunucu, UnityWebRequest.kHttpVerbPOST)) {
                istek.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(govde));
                istek.downloadHandler = new DownloadHandlerBuffer();
                istek.SetRequestHeader("Content-Type", "application/json");
                istek.timeout = 20;
                yield return istek.SendWebRequest();
                if (istek.result == UnityWebRequest.Result.Success) {
                    sonrakiGonderim = Time.unscaledTime + 2f;
                } else {
                    KuyrugaYaz(govde);
                    sonrakiGonderim = Time.unscaledTime + 60f;
                    sonrakiKuyrukDenemesi = Time.unscaledTime + 120f;
                }
            }
            gonderiyor = false;
        }

        private IEnumerator EkGonder(byte[] goruntu, string kimlik) {
            if (Application.internetReachability == NetworkReachability.NotReachable) {
                yield break;
            }
            using (UnityWebRequest istek = new UnityWebRequest(Sunucu + "/" + kanal, UnityWebRequest.kHttpVerbPUT)) {
                istek.uploadHandler = new UploadHandlerRaw(goruntu);
                istek.downloadHandler = new DownloadHandlerBuffer();
                istek.SetRequestHeader("X-Filename", kimlik + ".jpg");
                istek.SetRequestHeader("X-Title", "ek " + kimlik);
                istek.SetRequestHeader("X-Tags", "frame_with_picture");
                istek.timeout = 60;
                yield return istek.SendWebRequest();
            }
        }

        private static string KuyrukYolu {
            get { return Path.Combine(Application.persistentDataPath, KuyrukDosyasi); }
        }

        private static void KuyrugaYaz(string govde) {
            try {
                string yol = KuyrukYolu;
                List<string> satirlar = File.Exists(yol) ? new List<string>(File.ReadAllLines(yol)) : new List<string>();
                // JsonUtility tek satır yazar (satır sonları kaçırılmış), dosyada her rapor bir satır
                satirlar.Add(govde.Replace("\r", string.Empty).Replace("\n", string.Empty));
                if (satirlar.Count > KuyrukSiniri) {
                    satirlar.RemoveRange(0, satirlar.Count - KuyrukSiniri);
                }
                File.WriteAllLines(yol, satirlar.ToArray());
            } catch (Exception) {
                // dosyaya yazılamazsa rapor kaybolur
            }
        }

        private void DosyaKuyrugunuYukle() {
            if (Application.internetReachability == NetworkReachability.NotReachable) {
                return;
            }
            try {
                string yol = KuyrukYolu;
                if (File.Exists(yol) == false) {
                    return;
                }
                string[] satirlar = File.ReadAllLines(yol);
                File.Delete(yol);
                foreach (string satir in satirlar) {
                    if (satir.Length > 2) {
                        gonderilecek.Enqueue(satir);
                    }
                }
            } catch (Exception) {
                // bir sonraki açılışta yeniden denenir
            }
        }

        // ---------------------------------------------------------------- ekran görüntüsü

        /// <summary>ekranın görüntüsü, en fazla 1280 piksel genişlikte JPG (kare sonunda çağrılmalı)</summary>
        private static byte[] EkranGoruntusu() {
            Texture2D tam = null;
            Texture2D kucuk = null;
            RenderTexture rt = null;
            try {
                tam = ScreenCapture.CaptureScreenshotAsTexture();
                if (tam == null) {
                    return null;
                }
                int en = tam.width;
                int boy = tam.height;
                if (en > 1280) {
                    boy = Mathf.RoundToInt(boy * 1280f / en);
                    en = 1280;
                    rt = RenderTexture.GetTemporary(en, boy, 0);
                    Graphics.Blit(tam, rt);
                    RenderTexture onceki = RenderTexture.active;
                    RenderTexture.active = rt;
                    kucuk = new Texture2D(en, boy, TextureFormat.RGB24, false);
                    kucuk.ReadPixels(new Rect(0, 0, en, boy), 0, 0);
                    kucuk.Apply();
                    RenderTexture.active = onceki;
                    return kucuk.EncodeToJPG(72);
                }
                return tam.EncodeToJPG(72);
            } catch (Exception e) {
                Debug.LogWarning("[HataBildirici] ekran görüntüsü alınamadı: " + e.Message);
                return null;
            } finally {
                if (rt != null) {
                    RenderTexture.ReleaseTemporary(rt);
                }
                if (tam != null) {
                    Destroy(tam);
                }
                if (kucuk != null) {
                    Destroy(kucuk);
                }
            }
        }

        // ---------------------------------------------------------------- Sorun Bildir penceresi

        /// <summary>Seçenekler > "Sorun Bildir": ekranın görüntüsünü alır, oyuncunun notunu sorar ve gönderir</summary>
        public static void SorunBildir() {
            if (ornek == null) {
                Debug.LogWarning("[HataBildirici] bu derlemede hata bildirimi kapalı (HATA_KANALI yok)");
                return;
            }
            ornek.StartCoroutine(ornek.SorunBildirAc());
        }

        private IEnumerator SorunBildirAc() {
            // Seçenekler penceresi kapansın, görüntüde oyun görünsün
            yield return null;
            yield return new WaitForEndOfFrame();
            bekleyenGoruntu = EkranGoruntusu();
            if (pencereKoku == null) {
                PencereKur();
            }
            notAlani.text = string.Empty;
            durumYazisi.text = "Ekran görüntüsü ve oyunun son kayıtları da gönderilir. Kişisel bilgi gönderilmez.";
            durumYazisi.color = new Color(0.75f, 0.7f, 0.62f, 1f);
            gonderDugmesi.interactable = true;
            pencereKoku.SetActive(true);
        }

        private void SorunGonder() {
            MobileFeedback.Tap();
            gonderDugmesi.interactable = false;
            string not = notAlani.text.Trim();
            raporlaniyor = true;
            try {
                Raporla("oyuncu", string.IsNullOrEmpty(not) ? "(not yazılmadı)" : not, string.Empty, null, bekleyenGoruntu, true);
            } finally {
                raporlaniyor = false;
            }
            bekleyenGoruntu = null;
            durumYazisi.text = "Gönderildi. Teşekkürler!";
            durumYazisi.color = new Color(0.55f, 0.9f, 0.5f, 1f);
            MobileFeedback.Success();
            StartCoroutine(KapatGecikmeli(1.2f));
        }

        private IEnumerator KapatGecikmeli(float sure) {
            yield return new WaitForSecondsRealtime(sure);
            pencereKoku.SetActive(false);
        }

        private void PencereKur() {
            Color altin = new Color(0.91f, 0.77f, 0.48f, 1f);
            Color yazi = new Color(0.96f, 0.92f, 0.84f, 1f);
            GameObject tuvalNesnesi = new GameObject(CanvasName);
            DontDestroyOnLoad(tuvalNesnesi);
            Canvas tuval = tuvalNesnesi.AddComponent<Canvas>();
            tuval.renderMode = RenderMode.ScreenSpaceOverlay;
            tuval.sortingOrder = SortingOrder;
            CanvasScaler olcek = tuvalNesnesi.AddComponent<CanvasScaler>();
            olcek.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olcek.referenceResolution = new Vector2(1422f, 800f);
            olcek.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            olcek.matchWidthOrHeight = 1f;
            tuvalNesnesi.AddComponent<GraphicRaycaster>();

            pencereKoku = Dikdortgen(tuvalNesnesi.transform, "SorunBildir", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pencereKoku.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            // klavye ekranın altını kapladığı için pencere üst yarıda
            GameObject panel = Dikdortgen(pencereKoku.transform, "Panel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-400f, -400f), new Vector2(400f, -20f));
            panel.AddComponent<Image>().color = new Color(0.09f, 0.07f, 0.05f, 0.97f);
            Outline cizgi = panel.AddComponent<Outline>();
            cizgi.effectColor = altin;
            cizgi.effectDistance = new Vector2(2f, -2f);

            Text baslik = Yazi(Dikdortgen(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -60f), new Vector2(-20f, -8f)),
                "SORUN BİLDİR", 32, TextAnchor.MiddleCenter, altin);
            baslik.fontStyle = FontStyle.Bold;
            Yazi(Dikdortgen(panel.transform, "Aciklama", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -96f), new Vector2(-24f, -58f)),
                "Ne oldu? Kısaca yaz (isteğe bağlı), sonra Gönder'e dokun.", 20, TextAnchor.MiddleLeft, yazi);

            GameObject alan = Dikdortgen(panel.transform, "Not", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -250f), new Vector2(-24f, -104f));
            Image alanArka = alan.AddComponent<Image>();
            alanArka.color = new Color(0f, 0f, 0f, 0.55f);
            Outline alanCizgi = alan.AddComponent<Outline>();
            alanCizgi.effectColor = new Color(altin.r, altin.g, altin.b, 0.5f);
            alanCizgi.effectDistance = new Vector2(1f, -1f);
            Text metin = Yazi(Dikdortgen(alan.transform, "Metin", Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-12f, -8f)),
                string.Empty, 22, TextAnchor.UpperLeft, yazi);
            metin.supportRichText = false;
            metin.verticalOverflow = VerticalWrapMode.Truncate;
            Text ipucu = Yazi(Dikdortgen(alan.transform, "Ipucu", Vector2.zero, Vector2.one, new Vector2(12f, 8f), new Vector2(-12f, -8f)),
                "Örnek: Işınlan düğmesi can çubuğunun üstünde kalıyor", 20, TextAnchor.UpperLeft, new Color(0.6f, 0.56f, 0.5f, 1f));
            ipucu.fontStyle = FontStyle.Italic;
            notAlani = alan.AddComponent<InputField>();
            notAlani.textComponent = metin;
            notAlani.placeholder = ipucu;
            notAlani.targetGraphic = alanArka;
            notAlani.lineType = InputField.LineType.MultiLineNewline;
            notAlani.characterLimit = 500;

            durumYazisi = Yazi(Dikdortgen(panel.transform, "Durum", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 86f), new Vector2(-24f, 122f)),
                string.Empty, 17, TextAnchor.MiddleLeft, yazi);
            gonderDugmesi = Dugme(panel.transform, "Gönder", new Vector2(-110f, 46f), new Vector2(240f, 62f), new Color(0.12f, 0.45f, 0.5f, 1f), SorunGonder);
            Dugme(panel.transform, "Vazgeç", new Vector2(150f, 46f), new Vector2(200f, 62f), new Color(0.2f, 0.15f, 0.1f, 0.95f), () => {
                MobileFeedback.Tap();
                bekleyenGoruntu = null;
                pencereKoku.SetActive(false);
            });
            pencereKoku.SetActive(false);
        }

        private GameObject Dikdortgen(Transform ust, string ad, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject nesne = new GameObject(ad);
            nesne.transform.SetParent(ust, false);
            RectTransform rt = nesne.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return nesne;
        }

        private Text Yazi(GameObject hedef, string icerik, int boyut, TextAnchor hiza, Color renk) {
            Text t = hedef.AddComponent<Text>();
            t.font = font;
            t.text = icerik;
            t.fontSize = boyut;
            t.alignment = hiza;
            t.color = renk;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        private Button Dugme(Transform ust, string etiket, Vector2 konum, Vector2 boyut, Color renk, UnityEngine.Events.UnityAction tiklama) {
            GameObject nesne = Dikdortgen(ust, etiket, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            RectTransform rt = nesne.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = konum;
            rt.sizeDelta = boyut;
            Image resim = nesne.AddComponent<Image>();
            resim.color = renk;
            Outline cizgi = nesne.AddComponent<Outline>();
            cizgi.effectColor = new Color(0.91f, 0.77f, 0.48f, 0.6f);
            cizgi.effectDistance = new Vector2(1f, -1f);
            Button dugme = nesne.AddComponent<Button>();
            dugme.targetGraphic = resim;
            dugme.onClick.AddListener(tiklama);
            Yazi(Dikdortgen(nesne.transform, "Yazi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), etiket, 26, TextAnchor.MiddleCenter,
                new Color(0.96f, 0.92f, 0.84f, 1f));
            return dugme;
        }
    }
}
