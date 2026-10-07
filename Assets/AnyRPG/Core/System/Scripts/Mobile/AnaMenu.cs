using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Ötüken: ana menünün düğme sütunu. AnyRPG'nin ana menü düğmeleri gizlenir; yerine tek sütunda, aynı biçimde
    /// Çevrim İçi Oyna, Google Play ile Gir, Çevrim Dışı Oyna, Oyun Kılavuzu, Ayarlar, Jenerik, Çıkış.
    /// Sütun yalnız ana menü tek başına açıkken görünür: karakter seçme/oluşturma, giriş, ayarlar gibi pencereler
    /// açılınca kaybolur (eskiden Oyun Kılavuzu ve Google düğmeleri bu pencerelerin düğmelerini örtüyordu).
    /// Düğmeler açılışta sırayla kayarak gelir; basma hissi DugmeHissi'dendir.
    /// </summary>
    public static class AnaMenu {

        private const float Genislik = 410f;
        private const float Yukseklik = 60f;
        private const float Aralik = 9f;

        private class Satir {
            public string ad;
            public RectTransform rt;
            public CanvasGroup grup;
            public TextMeshProUGUI altYazi;
            public Func<bool> gorunur;
        }

        private static GameObject kok = null;
        private static RectTransform sutun = null;
        private static readonly List<Satir> satirlar = new List<Satir>();
        private static MainMenuPanel panel = null;
        private static CanvasGroup eskiGrup = null;
        private static float acilis = -1f;
        private static bool gorunuyor = false;
        private static Sprite altinPlaka = null;
        private static Sprite koyuPlaka = null;
        private static Sprite yesilPlaka = null;

        private static float sonrakiDenetim = 0f;
        private static bool sonGoster = false;
        private static UIManager ustlerUi = null;
        private static CloseableWindow[] ustler = null;

        /// <summary>MobileSupport, her karede (görünürlük saniyede 10 kez denetlenir; kayma canlandırması her karede)</summary>
        public static void Tick(SystemGameManager oyun, bool oyunda) {
            if (Time.unscaledTime >= sonrakiDenetim) {
                sonrakiDenetim = Time.unscaledTime + 0.1f;
                sonGoster = oyunda == false && YalnizAnaMenu(oyun);
            }
            bool goster = sonGoster;
            if (goster) {
                if (kok == null || panel == null) {
                    Kur(oyun);
                }
                if (kok == null) {
                    return;
                }
                EskiDugmeleriGizle();
            }
            if (kok == null) {
                return;
            }
            if (goster != gorunuyor) {
                gorunuyor = goster;
                kok.SetActive(goster);
                if (goster) {
                    acilis = Time.unscaledTime;
                    Yerlestir();
                }
            }
            if (goster && Time.unscaledTime - acilis < 1.2f) {
                Canlandir();
            }
        }

        /// <summary>ana menü açık ve üstünde başka hiçbir pencere yok</summary>
        public static bool YalnizAnaMenu(SystemGameManager oyun) {
            if (oyun == null || SystemGameManager.IsShuttingDown || oyun.UIManager == null) {
                return false;
            }
            UIManager ui = oyun.UIManager;
            if (ui.mainMenuWindow == null || ui.mainMenuWindow.IsOpen == false) {
                return false;
            }
            if (ustler == null || ustlerUi != ui) {
                ustlerUi = ui;
                ustler = new CloseableWindow[] {
                    ui.loadGameWindow, ui.newGameWindow, ui.characterCreatorWindow, ui.playMenuWindow, ui.playOnlineMenuWindow,
                    ui.networkLoginWindow, ui.settingsMenuWindow, ui.creditsWindow, ui.exitMenuWindow, ui.loginInProgressWindow,
                    ui.loginFailedWindow, ui.wrongClientVersionWindow, ui.disconnectedWindow, ui.deleteGameMenuWindow,
                    ui.copyGameMenuWindow, ui.hostServerWindow, ui.clientLobbyWindow, ui.clientLobbyGameWindow,
                    ui.createLobbyGameWindow, ui.playerOptionsMenuWindow, ui.nameChangeWindow, ui.confirmLogoutWindow
                };
            }
            foreach (CloseableWindow w in ustler) {
                if (w != null && w.IsOpen) {
                    return false;
                }
            }
            if (Sartlar.KabulEdildi == false && Application.isEditor == false && Application.isBatchMode == false && AgBotu.Etkin == false) {
                return false;
            }
            return GameGuide.IsOpen == false && SeceneklerPenceresi.IsOpen == false;
        }

        // ================================================================ kurulum

        private static void Kur(SystemGameManager oyun) {
            MainMenuPanel bulunan = UnityEngine.Object.FindAnyObjectByType<MainMenuPanel>(FindObjectsInactive.Include);
            if (bulunan == null) {
                return;
            }
            if (bulunan != panel) {
                panel = bulunan;
                eskiGrup = null;
            }
            if (kok != null) {
                return;
            }
            TMP_FontAsset yazi = null;
            foreach (HighlightButton h in panel.GetComponentsInChildren<HighlightButton>(true)) {
                if (h.Text != null && h.Text.font != null) {
                    yazi = h.Text.font;
                    break;
                }
            }
            if (yazi == null) {
                yazi = TMP_Settings.defaultFontAsset;
            }
            altinPlaka = Plaka(new Color32(0xF6, 0xD7, 0x86, 255), new Color32(0xB8, 0x78, 0x1E, 255), new Color32(0xFF, 0xEC, 0xB0, 255), new Color32(0x6A, 0x43, 0x10, 255));
            koyuPlaka = Plaka(new Color32(0x3B, 0x2D, 0x1E, 242), new Color32(0x17, 0x11, 0x0B, 242), new Color32(0x6B, 0x55, 0x38, 255), new Color32(0xD9, 0xA9, 0x4A, 255));
            yesilPlaka = Plaka(new Color32(0x2E, 0x6B, 0x3D, 245), new Color32(0x14, 0x37, 0x1E, 245), new Color32(0x5F, 0xA8, 0x6E, 255), new Color32(0xD9, 0xA9, 0x4A, 255));

            kok = new GameObject("OtukenAnaMenuCanvas");
            UnityEngine.Object.DontDestroyOnLoad(kok);
            Canvas canvas = kok.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 28;
            CanvasScaler olcek = kok.AddComponent<CanvasScaler>();
            olcek.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olcek.referenceResolution = new Vector2(1422f, 800f);
            olcek.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            olcek.matchWidthOrHeight = 1f;
            kok.AddComponent<GraphicRaycaster>();

            sutun = new GameObject("Sutun", typeof(RectTransform)).GetComponent<RectTransform>();
            sutun.SetParent(kok.transform, false);
            sutun.anchorMin = new Vector2(0f, 0f);
            sutun.anchorMax = new Vector2(0f, 0f);
            sutun.pivot = new Vector2(0f, 0f);

            SystemConfigurationManager ayar = oyun.SystemConfigurationManager;
            Ekle(yazi, "Çevrim İçi Oyna", altinPlaka, new Color32(0x2A, 0x18, 0x06, 255), () => panel.PlayOnlineMenu(),
                () => ayar == null || ayar.AllowOnlinePlay, false);
            Ekle(yazi, "Google Play ile Gir", yesilPlaka, new Color32(0xF5, 0xEC, 0xD6, 255), GoogleGiris.CevrimiciGir,
                () => GoogleGiris.Kurulu && Application.platform == RuntimePlatform.Android && (ayar == null || ayar.AllowOnlinePlay), true);
            Ekle(yazi, "Çevrim Dışı Oyna", koyuPlaka, new Color32(0xF2, 0xE6, 0xCC, 255), () => panel.PlayMenu(),
                () => ayar == null || ayar.AllowOfflinePlay, false);
            Ekle(yazi, "Oyun Kılavuzu", koyuPlaka, new Color32(0xF2, 0xE6, 0xCC, 255), () => GameGuide.Show(false), () => true, false);
            Ekle(yazi, "Ayarlar", koyuPlaka, new Color32(0xF2, 0xE6, 0xCC, 255), () => panel.SettingsMenu(), () => true, false);
            Ekle(yazi, "Jenerik", koyuPlaka, new Color32(0xF2, 0xE6, 0xCC, 255), () => panel.CreditsMenu(), () => true, false);
            Ekle(yazi, "Çıkış", koyuPlaka, new Color32(0xE9, 0xB8, 0xA8, 255), () => panel.ExitMenu(), () => true, false);
            GoogleGiris.Degisti += GoogleYazisi;
            kok.SetActive(false);
            gorunuyor = false;
        }

        private static void Ekle(TMP_FontAsset yazi, string ad, Sprite plaka, Color yaziRengi, Action tik, Func<bool> gorunur, bool altYaziVar) {
            GameObject go = new GameObject(ad, typeof(RectTransform));
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.SetParent(sutun, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Genislik, Yukseklik);
            CanvasGroup grup = go.AddComponent<CanvasGroup>();
            Image arka = go.AddComponent<Image>();
            arka.sprite = plaka;
            arka.type = Image.Type.Simple;
            arka.color = Color.white;
            Shadow golge = go.AddComponent<Shadow>();
            golge.effectColor = new Color(0f, 0f, 0f, 0.55f);
            golge.effectDistance = new Vector2(0f, -4f);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = arka;
            ColorBlock renkler = b.colors;
            renkler.normalColor = Color.white;
            renkler.highlightedColor = new Color(1.06f, 1.06f, 1.06f, 1f);
            renkler.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            renkler.selectedColor = Color.white;
            renkler.fadeDuration = 0.06f;
            b.colors = renkler;
            b.navigation = new Navigation() { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => {
                try {
                    tik();
                } catch (Exception e) {
                    Debug.LogWarning("[AnaMenu] " + ad + ": " + e.Message);
                }
            });

            TextMeshProUGUI baslik = Yazi(go.transform, "Yazi", yazi, ad, altYaziVar ? 25f : 28f, yaziRengi, FontStyles.Bold);
            RectTransform brt = baslik.rectTransform;
            brt.anchorMin = new Vector2(0f, altYaziVar ? 0.36f : 0f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.offsetMin = new Vector2(16f, 0f);
            brt.offsetMax = new Vector2(-16f, altYaziVar ? -2f : 0f);
            TextMeshProUGUI alt = null;
            if (altYaziVar) {
                alt = Yazi(go.transform, "Alt", yazi, string.Empty, 14f, new Color(0.86f, 0.93f, 0.84f, 0.95f), FontStyles.Normal);
                RectTransform art = alt.rectTransform;
                art.anchorMin = new Vector2(0f, 0f);
                art.anchorMax = new Vector2(1f, 0.4f);
                art.offsetMin = new Vector2(16f, 4f);
                art.offsetMax = new Vector2(-16f, 0f);
            }
            satirlar.Add(new Satir() { ad = ad, rt = rt, grup = grup, altYazi = alt, gorunur = gorunur });
        }

        private static TextMeshProUGUI Yazi(Transform ust, string ad, TMP_FontAsset yazi, string metin, float boy, Color renk, FontStyles stil) {
            GameObject go = new GameObject(ad, typeof(RectTransform));
            go.transform.SetParent(ust, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            if (yazi != null) {
                t.font = yazi;
            }
            t.text = metin;
            t.fontSize = boy;
            t.fontStyle = stil;
            t.color = renk;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.characterSpacing = 1.5f;
            return t;
        }

        private static void GoogleYazisi() {
            foreach (Satir s in satirlar) {
                if (s.altYazi != null) {
                    if (string.IsNullOrEmpty(GoogleGiris.SonSonuc) == false) {
                        s.altYazi.text = GoogleGiris.SonSonuc;
                    } else {
                        s.altYazi.text = GoogleGiris.Girdi && GoogleGiris.OyuncuAdi.Length > 0
                            ? "Play Games: " + GoogleGiris.OyuncuAdi : "Google hesabınla tek dokunuşla";
                    }
                }
            }
        }

        /// <summary>görünen düğmeleri alttan yukarı dizer (Google kurulu değilse araya boşluk girmez)</summary>
        private static void Yerlestir() {
            GoogleYazisi();
            List<Satir> gorunen = new List<Satir>();
            foreach (Satir s in satirlar) {
                bool g = false;
                try {
                    g = s.gorunur();
                } catch (Exception) {
                }
                s.rt.gameObject.SetActive(g);
                if (g) {
                    gorunen.Add(s);
                }
            }
            float yukseklik = gorunen.Count * Yukseklik + Mathf.Max(0, gorunen.Count - 1) * Aralik;
            sutun.sizeDelta = new Vector2(Genislik, yukseklik);
            sutun.anchoredPosition = new Vector2(36f, 30f);
            for (int i = 0; i < gorunen.Count; i++) {
                float y = yukseklik - i * (Yukseklik + Aralik) - Yukseklik * 0.5f;
                gorunen[i].rt.anchoredPosition = new Vector2(Genislik * 0.5f, y);
            }
        }

        /// <summary>açılışta düğmeler soldan sırayla kayarak ve belirerek gelir</summary>
        private static void Canlandir() {
            float t = Time.unscaledTime - acilis;
            int sira = 0;
            foreach (Satir s in satirlar) {
                if (s.rt.gameObject.activeSelf == false) {
                    continue;
                }
                float k = Mathf.Clamp01((t - sira * 0.05f) / 0.28f);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                s.grup.alpha = e;
                s.grup.interactable = k >= 1f;
                s.grup.blocksRaycasts = k > 0.3f;
                float x = Genislik * 0.5f - (1f - e) * 40f;
                s.rt.anchoredPosition = new Vector2(x, s.rt.anchoredPosition.y);
                sira++;
            }
        }

        /// <summary>AnyRPG'nin ana menü düğmeleri ve koyu arka planı görünmez, dokunulmaz olur (pencere yine açık sayılır)</summary>
        private static void EskiDugmeleriGizle() {
            if (panel == null) {
                return;
            }
            if (eskiGrup == null) {
                eskiGrup = panel.GetComponent<CanvasGroup>();
                if (eskiGrup == null) {
                    eskiGrup = panel.gameObject.AddComponent<CanvasGroup>();
                }
            }
            if (eskiGrup.alpha != 0f || eskiGrup.blocksRaycasts) {
                eskiGrup.alpha = 0f;
                eskiGrup.blocksRaycasts = false;
                eskiGrup.interactable = false;
            }
        }

        // ================================================================ düğme görüntüsü

        /// <summary>
        /// yuvarlak köşeli plaka: dikey renk geçişli dolgu, ince altın kenar, üstte ince parlak çizgi. Düğmenin iki katı
        /// çözünürlükte çizilir (keskin görünsün), kenarları yumuşatılmıştır.
        /// </summary>
        private static Sprite Plaka(Color ust, Color alt, Color parlak, Color kenar) {
            int w = Mathf.RoundToInt(Genislik * 2f), h = Mathf.RoundToInt(Yukseklik * 2f);
            float r = 22f, kalinlik = 3.2f;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color32[] p = new Color32[w * h];
            for (int y = 0; y < h; y++) {
                float v = y / (float)(h - 1);
                Color dolgu = Color.Lerp(alt, ust, Mathf.SmoothStep(0f, 1f, v));
                for (int x = 0; x < w; x++) {
                    // yuvarlak dikdörtgene işaretli uzaklık (içeride negatif)
                    float qx = Mathf.Abs(x + 0.5f - w * 0.5f) - (w * 0.5f - r);
                    float qy = Mathf.Abs(y + 0.5f - h * 0.5f) - (h * 0.5f - r);
                    float dis = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
                    float kaplama = Mathf.Clamp01(0.5f - dis);
                    if (kaplama <= 0f) {
                        p[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }
                    Color c = dolgu;
                    // üst kenarın hemen altında ince parlak çizgi
                    float ustUzak = (h - 1 - y);
                    if (ustUzak > kalinlik + 1f && ustUzak < kalinlik + 4f && Mathf.Abs(x - w * 0.5f) < w * 0.5f - r) {
                        c = Color.Lerp(c, parlak, 0.55f);
                    }
                    // kenar çizgisi
                    float kenarK = Mathf.Clamp01((dis + kalinlik) + 0.5f);
                    c = Color.Lerp(c, kenar, kenarK);
                    c.a *= kaplama;
                    p[y * w + x] = c;
                }
            }
            tex.SetPixels32(p);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }
    }
}
