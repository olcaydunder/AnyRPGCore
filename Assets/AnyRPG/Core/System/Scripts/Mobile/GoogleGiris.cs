using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Google Play Oyun Hizmetleri ile giriş (Ötüken; Rise of Davraz'daki gibi):
    ///  - Telefon Play Games'e girer (Play Games açılışta kendiliğinden dener). Ana menüdeki "Google Play ile Gir" düğmesi
    ///    Google'dan tek kullanımlık bir "sunucu yetki kodu" ister ve çevrimiçi oyuna kullanıcı adı "@google", şifre
    ///    yerine bu kodla girer.
    ///  - Sunucu kodu Google'da doğrular (web istemcisinin gizli anahtarıyla; anahtar yalnız sunucuda,
    ///    /var/lib/otuken/google-istemci.txt), Google'ın oyuncu kimliğini (playerId) alır. Bu kimliğe bağlı hesap varsa
    ///    ona girer (başka telefon, yeniden kurulum); yoksa Play Games adından yeni bir hesap açar ve bağlar.
    ///  - Kullanıcı adı/şifreyle açılmış hesap, Ayarlar > Hesap > "Google Play Games'e bağla" ile bağlanır.
    /// Kimlikler (proje kimliği, web istemci kimliği) Resources/Google.txt'te; boşsa özellik kapalıdır.
    /// Köprü: Assets/Plugins/Android/OtukenGiris.java. Gradle ve manifest: Otuken/Editor/OdemeGradle.
    /// </summary>
    public static class GoogleGiris {

        public const string KullaniciAdi = "@google";

        // ================================================================ ayarlar

        private static bool ayarOkundu = false;
        private static string proje = string.Empty;
        private static string webIstemci = string.Empty;

        private static void AyarOku() {
            if (ayarOkundu) {
                return;
            }
            ayarOkundu = true;
            TextAsset t = Resources.Load<TextAsset>("Google");
            if (t == null) {
                return;
            }
            foreach (string satir in t.text.Split('\n')) {
                string s = satir.Trim();
                if (s.StartsWith("proje=")) {
                    proje = s.Substring("proje=".Length).Trim();
                } else if (s.StartsWith("web_istemci=")) {
                    webIstemci = s.Substring("web_istemci=".Length).Trim();
                }
            }
        }

        /// <summary>proje kimliği ve web istemci kimliği girilmiş mi</summary>
        public static bool Kurulu {
            get {
                AyarOku();
                return proje.Length >= 6 && webIstemci.EndsWith(".apps.googleusercontent.com");
            }
        }

        public static string WebIstemci {
            get {
                AyarOku();
                return webIstemci;
            }
        }

        // ================================================================ sunucu

        /// <summary>web istemcisinin gizli anahtarı: yalnız sunucuda, oyun kullanıcısının ev klasöründe</summary>
        public static string AnahtarYolu {
            get {
                string ev = Environment.GetEnvironmentVariable("HOME");
                return Path.Combine(string.IsNullOrEmpty(ev) ? Application.persistentDataPath : ev, "google-istemci.txt");
            }
        }

        private static string Anahtar() {
            try {
                return File.Exists(AnahtarYolu) ? File.ReadAllText(AnahtarYolu).Trim() : string.Empty;
            } catch (Exception e) {
                Debug.LogWarning("[GoogleGiris] anahtar okunamadı: " + e.Message);
                return string.Empty;
            }
        }

        [Serializable]
        private class Jeton {
            public string access_token = string.Empty;
            public string error = string.Empty;
        }

        [Serializable]
        private class Oyuncu {
            public string playerId = string.Empty;
            public string displayName = string.Empty;
        }

        private static UnityWebRequest JetonIstegi(string kod, string anahtar) {
            WWWForm form = new WWWForm();
            form.AddField("code", kod);
            form.AddField("client_id", WebIstemci);
            form.AddField("client_secret", anahtar);
            form.AddField("grant_type", "authorization_code");
            form.AddField("redirect_uri", string.Empty);
            UnityWebRequest istek = UnityWebRequest.Post("https://oauth2.googleapis.com/token", form);
            istek.timeout = 15;
            return istek;
        }

        /// <summary>
        /// sunucu: yetki kodunu Google'da doğrular. Bitince sonuc(oyuncuKimligi, ad, hata) ana iş parçacığında çağrılır
        /// (başarılıysa hata null).
        /// </summary>
        public static IEnumerator Dogrula(string kod, Action<string, string, string> sonuc) {
            string anahtar = Anahtar();
            if (Kurulu == false || anahtar.Length == 0) {
                sonuc(null, null, "Google girişi sunucuda henüz ayarlanmadı");
                yield break;
            }
            string jeton = null;
            using (UnityWebRequest istek = JetonIstegi(kod, anahtar)) {
                yield return istek.SendWebRequest();
                if (istek.result == UnityWebRequest.Result.Success) {
                    Jeton j = JsonUtility.FromJson<Jeton>(istek.downloadHandler.text);
                    jeton = j != null ? j.access_token : null;
                } else {
                    Debug.LogWarning("[Sunucu] Google girişi: jeton alınamadı (" + istek.responseCode + ") " + Kisalt(istek.downloadHandler != null ? istek.downloadHandler.text : istek.error));
                }
            }
            if (string.IsNullOrEmpty(jeton)) {
                sonuc(null, null, "Google girişi doğrulanamadı");
                yield break;
            }
            using (UnityWebRequest istek = UnityWebRequest.Get("https://games.googleapis.com/games/v1/players/me")) {
                istek.SetRequestHeader("Authorization", "Bearer " + jeton);
                istek.timeout = 15;
                yield return istek.SendWebRequest();
                if (istek.result != UnityWebRequest.Result.Success) {
                    Debug.LogWarning("[Sunucu] Google girişi: oyuncu alınamadı (" + istek.responseCode + ") " + Kisalt(istek.downloadHandler != null ? istek.downloadHandler.text : istek.error));
                    sonuc(null, null, "Google girişi doğrulanamadı");
                    yield break;
                }
                Oyuncu o = JsonUtility.FromJson<Oyuncu>(istek.downloadHandler.text);
                if (o == null || string.IsNullOrEmpty(o.playerId)) {
                    sonuc(null, null, "Google girişi doğrulanamadı");
                    yield break;
                }
                sonuc(o.playerId.Length > 64 ? o.playerId.Substring(0, 64) : o.playerId, o.displayName ?? string.Empty, null);
            }
        }

        private static string Kisalt(string s) {
            if (string.IsNullOrEmpty(s)) {
                return string.Empty;
            }
            s = s.Replace("\n", " ");
            return s.Length > 200 ? s.Substring(0, 200) : s;
        }

        /// <summary>
        /// sunucu: gizli anahtarı oyuncu olmadan sınar. Uydurma bir kod her durumda reddedilir; anahtar doğruysa
        /// "invalid_grant", yanlışsa "invalid_client" döner. Sonuç sunucu günlüğüne yazılır.
        /// </summary>
        public static IEnumerator AnahtariSina() {
            if (Kurulu == false) {
                Debug.Log("[Sunucu] Google girişi: Google.txt'te kimlikler yok, kapalı");
                yield break;
            }
            string anahtar = Anahtar();
            if (anahtar.Length == 0) {
                Debug.Log("[Sunucu] Google girişi: gizli anahtar girilmedi (" + AnahtarYolu + ")");
                yield break;
            }
            using (UnityWebRequest istek = JetonIstegi("otuken-anahtar-sinama", anahtar)) {
                yield return istek.SendWebRequest();
                string hata = string.Empty;
                try {
                    Jeton j = JsonUtility.FromJson<Jeton>(istek.downloadHandler.text);
                    hata = j != null ? j.error : string.Empty;
                } catch (Exception) {
                }
                if (hata == "invalid_grant") {
                    Debug.Log("[Sunucu] Google girişi: anahtar DOĞRU (Google istemciyi tanıdı)");
                } else if (hata == "invalid_client" || hata == "unauthorized_client") {
                    Debug.LogWarning("[Sunucu] Google girişi: anahtar YANLIŞ (" + hata + ")");
                } else {
                    Debug.LogWarning("[Sunucu] Google girişi: Google beklenmeyen cevap verdi (" + istek.responseCode + " " + hata + ")");
                }
            }
        }

        private static DateTime anahtarZamani = DateTime.MinValue;
        private static float sonrakiBakis = 0f;

        /// <summary>sunucu, saniyede bir: açılışta ve anahtar dosyası değişince anahtarı sınar</summary>
        public static void SunucuTick(SystemGameManager oyun) {
            if (Time.realtimeSinceStartup < sonrakiBakis || oyun == null || oyun.NetworkManagerServer == null) {
                return;
            }
            sonrakiBakis = Time.realtimeSinceStartup + 30f;
            DateTime zaman = File.Exists(AnahtarYolu) ? File.GetLastWriteTimeUtc(AnahtarYolu) : DateTime.MinValue.AddTicks(1);
            if (zaman == anahtarZamani) {
                return;
            }
            anahtarZamani = zaman;
            oyun.NetworkManagerServer.StartCoroutine(AnahtariSina());
        }

        /// <summary>sunucu: Play Games adından boş bir kullanıcı adı ("Batur", "Batur2"...)</summary>
        public static string YeniKullaniciAdi(UserAccountService hesaplar, string ad) {
            StringBuilder sb = new StringBuilder();
            foreach (char c in ad ?? string.Empty) {
                if (char.IsLetterOrDigit(c) && sb.Length < 14) {
                    sb.Append(c);
                }
            }
            string temel = sb.Length >= 3 ? sb.ToString() : "Alp";
            string aday = temel;
            for (int i = 2; hesaplar.AccountExists(aday); i++) {
                aday = temel + i;
            }
            return aday;
        }

        /// <summary>sunucu: rastgele şifre (Google hesabı şifreyle girilmez)</summary>
        public static string RastgeleSifre() {
            byte[] b = new byte[24];
            using (System.Security.Cryptography.RandomNumberGenerator r = System.Security.Cryptography.RandomNumberGenerator.Create()) {
                r.GetBytes(b);
            }
            return Convert.ToBase64String(b);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            // giriş yapmış oyuncunun hesabını Google'a bağlar
            OtukenAg.SunucuIsle("google-bagla", (oyuncu, kod) => {
                SystemGameManager o = OtukenAg.Oyun;
                int hesapNo = o.PlayerManagerServer.GetAccountIdFromUnitController(oyuncu);
                UserAccount hesap = hesapNo >= 0 ? o.UserAccountService.HesapBul(hesapNo) : null;
                if (hesap == null) {
                    OtukenAg.Yanitla(oyuncu, "google-bagla", "0|Hesap bulunamadı.");
                    return;
                }
                if (string.IsNullOrEmpty(hesap.GoogleId) == false) {
                    OtukenAg.Yanitla(oyuncu, "google-bagla", "1|Hesabın zaten Google Play Games'e bağlı.");
                    return;
                }
                o.NetworkManagerServer.StartCoroutine(Dogrula(kod, (pid, ad, hata) => {
                    if (hata != null) {
                        OtukenAg.Yanitla(oyuncu, "google-bagla", "0|" + hata);
                        return;
                    }
                    UserAccount sahibi = o.UserAccountService.GoogleHesabi(pid);
                    if (sahibi != null && sahibi.Id != hesap.Id) {
                        OtukenAg.Yanitla(oyuncu, "google-bagla", "0|Bu Google hesabı başka bir oyun hesabına bağlı (" + sahibi.UserName + ").");
                        return;
                    }
                    hesap.GoogleId = pid;
                    o.ServerDataService.SaveAccount(hesap);
                    Debug.Log("[Sunucu] " + hesap.UserName + " (#" + hesap.Id + ") Google Play Games'e bağlandı");
                    OtukenAg.Yanitla(oyuncu, "google-bagla", "1|Hesabın Google Play Games'e bağlandı. Başka telefonda \"Google Play ile Gir\" ile girersin.");
                }));
            });
            OtukenAg.IstemciDinle("google-bagla", veri => {
                bool iyi = veri.StartsWith("1|");
                SonSonuc = veri.Substring(Math.Min(2, veri.Length));
                if (iyi) {
                    Bagli = true;
                    MobileFeedback.Success();
                }
                SystemGameManager o = OtukenAg.Oyun;
                UnitController ben = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
                if (ben != null) {
                    ben.WriteMessageFeedMessage((iyi ? "<color=#9BE37A>" : "<color=#FF8A6A>") + SonSonuc + "</color>");
                }
                Bildir();
            });
        }

        // ================================================================ telefon

        private static readonly Queue<KeyValuePair<string, string>> olaylar = new Queue<KeyValuePair<string, string>>();
        private static bool baslatildi = false;
        private static Action<string> bekleyenKod = null;
        private static GirisKoprusu kopru = null;

        /// <summary>telefon Play Games'e girmiş mi</summary>
        public static bool Girdi { get; private set; }
        public static string OyuncuAdi { get; private set; } = string.Empty;
        /// <summary>bu oturumda hesap Google'a bağlandı mı (Ayarlar'daki satır)</summary>
        public static bool Bagli { get; private set; }
        public static string SonSonuc { get; private set; } = string.Empty;
        public static event Action Degisti = delegate { };

        private static void Bildir() {
            try {
                Degisti();
            } catch (Exception e) {
                Debug.LogWarning("[GoogleGiris] " + e.Message);
            }
        }

        /// <summary>uygulama açılınca bir kez</summary>
        public static void Baslat() {
            if (baslatildi || Kurulu == false) {
                return;
            }
            baslatildi = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (kopru == null) {
                GameObject go = new GameObject("GirisKoprusu");
                UnityEngine.Object.DontDestroyOnLoad(go);
                kopru = go.AddComponent<GirisKoprusu>();
            }
            try {
                using (AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
                    AndroidJavaObject etkinlik = unity.GetStatic<AndroidJavaObject>("currentActivity");
                    using (AndroidJavaClass sinif = new AndroidJavaClass("com.zootopiayazilim.otuken.OtukenGiris")) {
                        sinif.CallStatic("baslat", etkinlik, new Dinleyici());
                    }
                }
            } catch (Exception e) {
                Debug.LogWarning("[GoogleGiris] başlatılamadı: " + e.Message);
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void Cagir(string yontem, params object[] degerler) {
            try {
                using (AndroidJavaClass sinif = new AndroidJavaClass("com.zootopiayazilim.otuken.OtukenGiris")) {
                    sinif.CallStatic(yontem, degerler);
                }
            } catch (Exception e) {
                Debug.LogWarning("[GoogleGiris] " + yontem + ": " + e.Message);
            }
        }
#endif

        /// <summary>
        /// sunucu için tek kullanımlık kod ister; gelince kodGeldi(kod) ana iş parçacığında çağrılır (alınamazsa null).
        /// Play Games'e girilmemişse önce giriş penceresi açılır.
        /// </summary>
        public static void KodIste(Action<string> kodGeldi) {
            if (Kurulu == false) {
                kodGeldi(null);
                return;
            }
            Baslat();
#if UNITY_ANDROID && !UNITY_EDITOR
            bekleyenKod = kodGeldi;
            if (Girdi == false) {
                kodBekliyor = true;
                Cagir("girisYap");
                return;
            }
            Cagir("kodIste", WebIstemci);
#else
            SonSonuc = "Google girişi, Google Play'den indirilen oyunda çalışır.";
            kodGeldi(null);
#endif
        }

        private static bool kodBekliyor = false;

        /// <summary>GirisKoprusu her karede çağırır</summary>
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
                    Debug.LogError("[GoogleGiris] olay işlenemedi (" + o.Key + "): " + e);
                }
            }
        }

        private static void OlayIsle(string tur, string veri) {
            switch (tur) {
                case "durum":
                    Girdi = veri == "1";
                    if (kodBekliyor) {
                        kodBekliyor = false;
                        if (Girdi) {
#if UNITY_ANDROID && !UNITY_EDITOR
                            Cagir("kodIste", WebIstemci);
#endif
                        } else {
                            SonSonuc = "Google Play Games'e girilmedi.";
                            KodTeslim(null);
                        }
                    }
                    break;
                case "oyuncu":
                    string[] p = veri.Split('\t');
                    OyuncuAdi = p.Length > 0 ? p[0] : string.Empty;
                    break;
                case "kod":
                    KodTeslim(veri);
                    break;
                case "kodHata":
                    SonSonuc = "Google'dan giriş kodu alınamadı.";
                    Debug.LogWarning("[GoogleGiris] kod alınamadı: " + veri);
                    KodTeslim(null);
                    break;
                case "hata":
                    Debug.LogWarning("[GoogleGiris] " + veri);
                    break;
            }
            Bildir();
        }

        private static void KodTeslim(string kod) {
            Action<string> a = bekleyenKod;
            bekleyenKod = null;
            if (a != null) {
                a(kod);
            }
        }

        /// <summary>ana menüdeki düğme: Google koduyla çevrimiçi oyuna girer</summary>
        public static void CevrimiciGir() {
            SystemGameManager oyun = OtukenAg.Oyun;
            if (oyun == null) {
                return;
            }
            MobileFeedback.Tap();
            KodIste(kod => {
                if (string.IsNullOrEmpty(kod)) {
                    if (menuYazisi != null) {
                        menuYazisi.text = string.IsNullOrEmpty(SonSonuc) ? "Google girişi olmadı." : SonSonuc;
                    }
                    return;
                }
                string sunucu = oyun.SystemConfigurationManager.GameServerAddress;
                if (PlayerPrefs.GetInt("NetworkLoginPanel.rememberServer", 0) == 1 && PlayerPrefs.HasKey("NetworkLoginPanel.server")) {
                    sunucu = PlayerPrefs.GetString("NetworkLoginPanel.server");
                }
                oyun.UIManager.loginInProgressWindow.OpenWindow();
                oyun.NetworkManagerClient.Login(KullaniciAdi, kod, sunucu);
            });
        }

        /// <summary>Ayarlar > Hesap: çevrimiçi hesabı Google'a bağlar</summary>
        public static void HesabiBagla() {
            SystemGameManager o = OtukenAg.Oyun;
            UnitController ben = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            if (Cevrimici.Acik == false || ben == null) {
                SonSonuc = "Önce çevrimiçi oyuna gir; sonra hesabını Google'a bağla.";
                if (ben != null) {
                    ben.WriteMessageFeedMessage(SonSonuc);
                }
                MobileFeedback.Medium();
                Bildir();
                return;
            }
            KodIste(kod => {
                if (string.IsNullOrEmpty(kod) == false) {
                    OtukenAg.Gonder("google-bagla", kod);
                } else if (ben != null) {
                    ben.WriteMessageFeedMessage(string.IsNullOrEmpty(SonSonuc) ? "Google girişi olmadı." : SonSonuc);
                }
            });
        }

        // ---------------------------------------------------------------- ana menü düğmesi

        private static GameObject menuDugmesi = null;
        private static Text menuYazisi = null;

        /// <summary>MobileSupport her karede: ana menü açıkken "Google Play ile Gir" görünür</summary>
        public static void MenuDugmesiGoster(bool gorunsun) {
            if (Kurulu == false) {
                return;
            }
            if (gorunsun == false) {
                if (menuDugmesi != null && menuDugmesi.activeSelf) {
                    menuDugmesi.SetActive(false);
                }
                return;
            }
            Baslat();
            if (menuDugmesi == null) {
                MenuKur();
            }
            if (menuDugmesi.activeSelf == false) {
                menuDugmesi.SetActive(true);
                menuYazisi.text = Girdi && OyuncuAdi.Length > 0 ? "Play Games: " + OyuncuAdi : "Çevrimiçi oyuna Google hesabınla gir";
            }
        }

        private static void MenuKur() {
            GameObject tuval = new GameObject("GoogleGirisCanvas");
            UnityEngine.Object.DontDestroyOnLoad(tuval);
            Canvas canvas = tuval.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 29;
            CanvasScaler olcek = tuval.AddComponent<CanvasScaler>();
            olcek.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olcek.referenceResolution = new Vector2(1422f, 800f);
            olcek.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            olcek.matchWidthOrHeight = 1f;
            tuval.AddComponent<GraphicRaycaster>();
            Font yazi = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            menuDugmesi = OtukenPencere.Kutu(tuval.transform, "GoogleGir", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 40f), new Vector2(370f, 116f));
            Image arka = menuDugmesi.AddComponent<Image>();
            arka.color = new Color(0.16f, 0.38f, 0.22f, 0.96f);
            Outline cizgi = menuDugmesi.AddComponent<Outline>();
            cizgi.effectColor = new Color(0.91f, 0.77f, 0.48f, 0.7f);
            cizgi.effectDistance = new Vector2(1f, -1f);
            Button b = menuDugmesi.AddComponent<Button>();
            b.targetGraphic = arka;
            b.onClick.AddListener(CevrimiciGir);
            Text baslik = OtukenPencere.Kutu(menuDugmesi.transform, "Yazi", new Vector2(0f, 0.42f), new Vector2(1f, 1f), new Vector2(10f, 0f), new Vector2(-10f, -4f))
                .AddComponent<Text>();
            baslik.font = yazi;
            baslik.text = "<b>Google Play ile Gir</b>";
            baslik.fontSize = 26;
            baslik.alignment = TextAnchor.MiddleCenter;
            baslik.color = new Color(0.96f, 0.92f, 0.84f, 1f);
            baslik.supportRichText = true;
            baslik.raycastTarget = false;
            menuYazisi = OtukenPencere.Kutu(menuDugmesi.transform, "Alt", new Vector2(0f, 0f), new Vector2(1f, 0.42f), new Vector2(8f, 4f), new Vector2(-8f, 0f))
                .AddComponent<Text>();
            menuYazisi.font = yazi;
            menuYazisi.fontSize = 15;
            menuYazisi.alignment = TextAnchor.MiddleCenter;
            menuYazisi.color = new Color(0.85f, 0.9f, 0.8f, 1f);
            menuYazisi.raycastTarget = false;
            menuYazisi.horizontalOverflow = HorizontalWrapMode.Wrap;
            Degisti += () => {
                if (menuYazisi != null && menuDugmesi != null && menuDugmesi.activeSelf && string.IsNullOrEmpty(SonSonuc)) {
                    menuYazisi.text = Girdi && OyuncuAdi.Length > 0 ? "Play Games: " + OyuncuAdi : "Çevrimiçi oyuna Google hesabınla gir";
                }
            };
            menuDugmesi.SetActive(false);
        }

        private class Dinleyici : AndroidJavaProxy {
            public Dinleyici() : base("com.zootopiayazilim.otuken.GirisDinleyici") {
            }

            [Preserve]
            public void olay(string tur, string veri) {
                lock (olaylar) {
                    olaylar.Enqueue(new KeyValuePair<string, string>(tur ?? string.Empty, veri ?? string.Empty));
                }
            }
        }
    }
}
