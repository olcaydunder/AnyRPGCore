using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Çevrimdışı kazanç (birçok mobil RPG'deki "sen yokken" ödülü): oyuna en az 30 dakika ara verip dönen oyuncuya,
    /// ara süresine göre (en çok 10 saat sayılır) Gümüş Akçe ve tecrübe. "Topla"ya dokununca verilir.
    /// Son görülme karakter başına PlayerPrefs'te tutulur: oyundayken 30 saniyede bir ve arka plana geçince yazılır;
    /// oyuna girince ve arka plandan dönünce bakılır. Günlük Armağan ve rehber kapanınca açılır. Kodla kurulur.
    /// </summary>
    public class CevrimdisiKazanc : MonoBehaviour {

        public const string CanvasName = "CevrimdisiKazancCanvas";
        // Günlük Armağan ile aynı katman
        private const int SortingOrder = 28;
        private const string Anahtar = "son-gorulme-";
        private const double EnAzDakika = 30;
        private const double EnCokSaat = 10;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color claimColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);

        private static CevrimdisiKazanc instance = null;
        private static string karakter = string.Empty;
        private static bool bakilacak = true;
        private static float sonYazma = -100f;
        private static float oyundaBeri = -1f;
        private static SystemGameManager oyun = null;

        private GameObject panelRoot = null;
        private Text metin = null;
        private int bekleyenGumus = 0;
        private int bekleyenTecrube = 0;

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        /// <summary>MobileBootstrap saniyede bir çağırır</summary>
        public static void Tick(SystemGameManager systemGameManager, bool oyunda) {
            oyun = systemGameManager;
            UnitController oyuncu = oyunda && systemGameManager != null && systemGameManager.PlayerManagerClient != null
                ? systemGameManager.PlayerManagerClient.UnitController : null;
            if (oyuncu == null) {
                oyundaBeri = -1f;
                return;
            }
            if (oyundaBeri < 0f) {
                oyundaBeri = Time.unscaledTime;
            }
            if (oyuncu.DisplayName != karakter) {
                karakter = oyuncu.DisplayName;
                bakilacak = true;
            }
            // önce Günlük Armağan ve rehber
            if (bakilacak && Time.unscaledTime - oyundaBeri > 6f && GunlukArmagan.IsOpen == false && GameGuide.IsOpen == false && IsOpen == false) {
                bakilacak = false;
                Bak(oyuncu);
            }
            if (Time.unscaledTime - sonYazma > 30f && bakilacak == false) {
                Yaz();
            }
        }

        /// <summary>arka plandan dönünce: aradaki süreye bakılır</summary>
        public static void OnPlanaGecti() {
            bakilacak = true;
            oyundaBeri = Time.unscaledTime;
        }

        /// <summary>arka plana geçerken ve kapanırken: son görülme yazılır</summary>
        public static void ArkaPlan() {
            if (bakilacak == false) {
                Yaz();
            }
        }

        private static void Yaz() {
            if (string.IsNullOrEmpty(karakter)) {
                return;
            }
            PlayerPrefs.SetString(Anahtar + karakter, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            sonYazma = Time.unscaledTime;
        }

        private static void Bak(UnitController oyuncu) {
            string kayit = PlayerPrefs.GetString(Anahtar + karakter, string.Empty);
            Yaz();
            long onceki;
            if (long.TryParse(kayit, NumberStyles.Integer, CultureInfo.InvariantCulture, out onceki) == false
                || onceki <= 0 || onceki > DateTime.UtcNow.Ticks) {
                // kayıt yok ya da saat geri alınmış
                return;
            }
            TimeSpan ara = DateTime.UtcNow - new DateTime(onceki, DateTimeKind.Utc);
            if (ara.TotalMinutes < EnAzDakika) {
                return;
            }
            double saat = Math.Min(ara.TotalHours, EnCokSaat);
            int seviye = Mathf.Max(1, oyuncu.CharacterStats.Level);
            int gumus = Mathf.Max(1, (int)Math.Round(saat * (1.0 + seviye * 0.5)));
            int tecrube = 0;
            if (oyun != null && oyun.SystemConfigurationManager != null) {
                tecrube = Mathf.Max(1, (int)Math.Round(saat * 0.04 * LevelEquations.GetXPNeededForLevel(seviye, oyun.SystemConfigurationManager)));
            }
            Ensure();
            instance.Ac(ara, saat >= EnCokSaat, gumus, tecrube);
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
            instance = canvasObject.AddComponent<CevrimdisiKazanc>();
            instance.Build();
        }

        private void Build() {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelRoot = Kutu(transform, "CevrimdisiKazanc", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            GameObject panel = Kutu(panelRoot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -170f), new Vector2(330f, 170f));
            panel.AddComponent<Image>().color = panelColor;
            Outline cizgi = panel.AddComponent<Outline>();
            cizgi.effectColor = gold;
            cizgi.effectDistance = new Vector2(2f, -2f);

            Text baslik = Yazi(Kutu(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -66f), new Vector2(-20f, -10f)),
                font, "SEN YOKKEN", 32, gold);
            baslik.fontStyle = FontStyle.Bold;
            metin = Yazi(Kutu(panel.transform, "Metin", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 100f), new Vector2(-30f, -70f)),
                font, string.Empty, 22, textColor);

            GameObject dugme = Kutu(panel.transform, "Topla", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            RectTransform drt = dugme.GetComponent<RectTransform>();
            drt.anchoredPosition = new Vector2(0f, 52f);
            drt.sizeDelta = new Vector2(260f, 66f);
            Image dresim = dugme.AddComponent<Image>();
            dresim.color = claimColor;
            Button b = dugme.AddComponent<Button>();
            b.targetGraphic = dresim;
            b.onClick.AddListener(Topla);
            Text dyazi = Yazi(Kutu(dugme.transform, "Yazi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), font, "Topla", 28, textColor);
            dyazi.fontStyle = FontStyle.Bold;
            panelRoot.SetActive(false);
        }

        private void Ac(TimeSpan ara, bool sinirda, int gumus, int tecrube) {
            bekleyenGumus = gumus;
            bekleyenTecrube = tecrube;
            string sure = ara.TotalHours >= 1 ? $"{(int)ara.TotalHours} saat {ara.Minutes} dakika" : $"{ara.Minutes} dakika";
            metin.text = $"{sure} yoktun. Bu sürede obanın yiğitleri senin adına sınırı korudu ve ganimet topladı.\n\n"
                + $"<color=#FFD54A><b>{gumus} Gümüş Akçe</b></color>   ·   <color=#9FD8FF><b>{tecrube} tecrübe</b></color>"
                + (sinirda ? "\n<size=17>(En çok 10 saat sayılır.)</size>" : string.Empty);
            panelRoot.SetActive(true);
            MobileFeedback.Light();
        }

        private void Topla() {
            MobileFeedback.Success();
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu != null && oyuncu.DisplayName == karakter) {
                Currency para = oyun.SystemDataFactory.GetResource<Currency>("Silver");
                if (para != null && bekleyenGumus > 0) {
                    oyuncu.CharacterCurrencyManager.AddCurrency(para, bekleyenGumus);
                }
                if (bekleyenTecrube > 0) {
                    oyuncu.CharacterStats.GainExperience(bekleyenTecrube);
                }
                oyuncu.WriteMessageFeedMessage($"<color=#FFD54A>Sen yokken kazandıkların: {bekleyenGumus} Gümüş Akçe, {bekleyenTecrube} tecrübe</color>");
            }
            bekleyenGumus = 0;
            bekleyenTecrube = 0;
            panelRoot.SetActive(false);
        }

        private static GameObject Kutu(Transform ust, string ad, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject go = new GameObject(ad);
            go.transform.SetParent(ust, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        private static Text Yazi(GameObject hedef, Font font, string icerik, int boyut, Color renk) {
            Text t = hedef.AddComponent<Text>();
            t.font = font;
            t.text = icerik;
            t.fontSize = boyut;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = renk;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
