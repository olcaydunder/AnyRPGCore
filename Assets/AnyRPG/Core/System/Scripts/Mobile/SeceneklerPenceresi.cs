using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Metin2 tarzı "Seçenekler" penceresi: Görüntü, Akıcılık, Ses ve Oyun bölümleri.
    /// Oyunda sol sütundaki "Menü" düğmesiyle açılır. Değerler OyunAyarlari'nda saklanır ve uygulanır.
    /// Alttaki düğmeler: Oyun Kılavuzu ve ekran görüntüsü paylaşma (NativeShare, yasirkula, MIT).
    /// Kodla kurulur, hiçbir prefab ya da resim dosyası gerektirmez.
    /// </summary>
    public class SeceneklerPenceresi : MonoBehaviour {

        public const string CanvasName = "SeceneklerCanvas";
        // rehberin (30) üstünde, Durum katmanının (1000) altında
        private const int SortingOrder = 31;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color tabColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color tabSelectedColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color optionColor = new Color(0.16f, 0.12f, 0.08f, 0.95f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color hintColor = new Color(0.75f, 0.7f, 0.62f, 1f);

        private static SeceneklerPenceresi instance = null;

        private Font font = null;
        private Canvas canvas = null;
        private GameObject panelRoot = null;
        private SystemGameManager systemGameManager = null;
        private readonly List<Image> tabImages = new List<Image>();
        private readonly List<GameObject> sectionContents = new List<GameObject>();
        private readonly List<Action> refreshers = new List<Action>();
        private ScrollRect bodyScroll = null;
        private int currentSection = 0;

        private string[] sectionTitles = { "Görüntü", "Akıcılık", "Ses", "Oyun", "Hesap" };

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        public static void Show() {
            Ensure();
            instance.Open();
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
            instance = canvasObject.AddComponent<SeceneklerPenceresi>();
            instance.canvas = canvas;
            instance.Build();
        }

        // ---------------------------------------------------------------- pencere

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panelRoot = CreateRect(transform, "Secenekler", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimmer = panelRoot.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.7f);

            GameObject panel = CreateRect(panelRoot.transform, "Panel", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), Vector2.zero, Vector2.zero);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = panelColor;
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = gold;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            GameObject title = CreateRect(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -66f), new Vector2(-20f, -8f));
            Text titleText = CreateText(title, "SEÇENEKLER", 34, TextAnchor.MiddleCenter, textColor);
            titleText.color = gold;
            titleText.fontStyle = FontStyle.Bold;

            // bölümler solda
            GameObject tabArea = CreateRect(panel.transform, "Bolumler", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 92f), new Vector2(276f, -76f));
            const float tabHeight = 64f;
            const float tabSpacing = 10f;
            for (int i = 0; i < sectionTitles.Length; i++) {
                int sectionIndex = i;
                GameObject tab = CreateButton(tabArea.transform, sectionTitles[i], new Vector2(0.5f, 1f), new Vector2(0f, -(tabHeight / 2f) - i * (tabHeight + tabSpacing)),
                    new Vector2(260f, tabHeight), 26, tabColor, () => { SelectSection(sectionIndex); MobileFeedback.Tap(); });
                tabImages.Add(tab.GetComponent<Image>());
            }

            // seçenekler sağda, kaydırılabilir
            GameObject bodyArea = CreateRect(panel.transform, "Ayarlar", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(296f, 92f), new Vector2(-20f, -76f));
            Image bodyBackground = bodyArea.AddComponent<Image>();
            bodyBackground.color = new Color(0f, 0f, 0f, 0.25f);
            GameObject viewport = CreateRect(bodyArea.transform, "Gorunum", Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            viewport.AddComponent<RectMask2D>();
            Image catcher = viewport.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            bodyScroll = bodyArea.AddComponent<ScrollRect>();
            bodyScroll.viewport = viewport.GetComponent<RectTransform>();
            bodyScroll.horizontal = false;
            bodyScroll.vertical = true;
            bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            bodyScroll.scrollSensitivity = 30f;

            BuildGoruntu(CreateSection(viewport.transform, "Goruntu"));
            BuildAkicilik(CreateSection(viewport.transform, "Akicilik"));
            BuildSes(CreateSection(viewport.transform, "Ses"));
            BuildOyun(CreateSection(viewport.transform, "Oyun"));
            BuildHesap(CreateSection(viewport.transform, "Hesap"));

            // alt düğmeler
            // dört düğme 4:3 tablette de sığsın diye toplam 920 birim
            CreateButton(panel.transform, "Oyun Kılavuzu", new Vector2(0.5f, 0f), new Vector2(-350f, 46f), new Vector2(220f, 64f), 23,
                tabColor, () => { Close(); GameGuide.Show(false); MobileFeedback.Tap(); });
            CreateButton(panel.transform, "Sorun Bildir", new Vector2(0.5f, 0f), new Vector2(-114f, 46f), new Vector2(220f, 64f), 23,
                new Color(0.12f, 0.45f, 0.5f, 1f), () => { MobileFeedback.Tap(); Close(); HataBildirici.SorunBildir(); });
            CreateButton(panel.transform, "Görüntü Paylaş", new Vector2(0.5f, 0f), new Vector2(122f, 46f), new Vector2(220f, 64f), 23,
                tabColor, () => { MobileFeedback.Tap(); StartCoroutine(ShareScreenshot()); });
            CreateButton(panel.transform, "Kapat", new Vector2(0.5f, 0f), new Vector2(348f, 46f), new Vector2(200f, 64f), 25,
                tabSelectedColor, () => { Close(); MobileFeedback.Tap(); });

            panelRoot.SetActive(false);
        }

        private void Open() {
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
            RefreshAll();
            SelectSection(currentSection);
        }

        private void Close() {
            panelRoot.SetActive(false);
            PlayerPrefs.Save();
        }

        private void SelectSection(int index) {
            currentSection = Mathf.Clamp(index, 0, sectionContents.Count - 1);
            for (int i = 0; i < sectionContents.Count; i++) {
                sectionContents[i].SetActive(i == currentSection);
                tabImages[i].color = i == currentSection ? tabSelectedColor : tabColor;
            }
            bodyScroll.content = sectionContents[currentSection].GetComponent<RectTransform>();
            bodyScroll.verticalNormalizedPosition = 1f;
        }

        private void RefreshAll() {
            foreach (Action refresh in refreshers) {
                refresh();
            }
        }

        private SystemGameManager GameManager {
            get {
                if (systemGameManager == null) {
                    systemGameManager = FindAnyObjectByType<SystemGameManager>();
                }
                return systemGameManager;
            }
        }

        // ---------------------------------------------------------------- bölümler

        private void BuildGoruntu(SectionBuilder s) {
            s.Choice("Grafik kalitesi", "Düşük en akıcısıdır. 4K en keskin ve canlı görüntüdür; güçlü telefon ister, ısıtır.",
                new[] { "Düşük", "Orta", "Yüksek", "4K" }, () => OyunAyarlari.Kalite, v => OyunAyarlari.Kalite = v);
            s.Choice("Çözünürlük", "Oyunun çizildiği netlik. Düşük en akıcısıdır; Otomatik, 4K kalitede 4K çizer.",
                new[] { "Otomatik", "Düşük", "Orta", "Yüksek", "4K" }, () => OyunAyarlari.Cozunurluk, v => OyunAyarlari.Cozunurluk = v);
            s.Choice("Gölgeler", "Gölgeleri kapatmak ya da yakına almak akıcılığı artırır.",
                new[] { "Otomatik", "Kapalı", "Yakın", "Uzak" }, () => OyunAyarlari.Golge, v => OyunAyarlari.Golge = v);
            s.Choice("Görüş mesafesi", "Uzaktaki nesneler, ağaçlar ve canavarlar çizilmez.",
                new[] { "Yakın", "Orta", "Uzak" }, () => OyunAyarlari.Gorus, v => OyunAyarlari.Gorus = v);
            s.Choice("Bitki yoğunluğu", "Yerdeki ot ve çiçeklerin sıklığı.",
                new[] { "Az", "Orta", "Çok" }, () => OyunAyarlari.Bitki, v => OyunAyarlari.Bitki = v);
            s.Choice("İsim mesafesi", "Bu mesafeden uzaktaki isim ve can çubukları gizlenir; kalabalıkta oyunu hızlandırır.",
                new[] { "20 m", "30 m", "40 m", "60 m" }, () => OyunAyarlari.IsimMesafesiSecimi, v => OyunAyarlari.IsimMesafesiSecimi = v);
            s.Finish();
        }

        private void BuildAkicilik(SectionBuilder s) {
            s.Choice("FPS sınırı", "30: pil dostu, telefon ısınmaz. 60: daha akıcı.",
                new[] { "30", "60" }, () => OyunAyarlari.Fps, v => OyunAyarlari.Fps = v);
            s.Choice("Ekran dışı canavarlar", "Durdur: görünmeyen canavarların hareketi hesaplanmaz (önerilir).",
                new[] { "Hareketli", "Durdur" }, () => OyunAyarlari.EkranDisiAnimasyonuDurdur ? 1 : 0, v => OyunAyarlari.EkranDisiAnimasyonuDurdur = v == 1);
            s.Choice("FPS göstergesi", "Sol üstte kare hızı grafiği ve bellek kullanımı.",
                new[] { "Kapalı", "Açık" }, () => OyunAyarlari.FpsGostergesi ? 1 : 0, v => OyunAyarlari.FpsGostergesi = v == 1);
            s.Choice("Hata konsolu", "Test için: üstte Durum düğmesi ve hata sayacı çıkar, dokununca oyun günlüğü açılır.",
                new[] { "Kapalı", "Açık" }, () => OyunAyarlari.HataKonsolu ? 1 : 0, v => OyunAyarlari.HataKonsolu = v == 1);
            s.Finish();
        }

        private void BuildSes(SectionBuilder s) {
            s.Kaydirici("Müzik", () => Volume("MusicVolume"), v => GameManager?.AudioManager?.SetMusicVolume(v));
            s.Kaydirici("Efektler", () => Volume("EffectsVolume"), v => GameManager?.AudioManager?.SetEffectsVolume(v));
            s.Kaydirici("Ortam sesleri", () => Volume("AmbientVolume"), v => GameManager?.AudioManager?.SetAmbientVolume(v));
            s.Kaydirici("Arayüz sesleri", () => Volume("UIVolume"), v => GameManager?.AudioManager?.SetUIVolume(v));
            s.Finish();
        }

        private static float Volume(string key) {
            return PlayerPrefs.GetFloat(key, 1f);
        }

        private void BuildHesap(SectionBuilder s) {
            s.Eylem("Kullanım şartları", "Oyunu kullanma koşulları (web sayfası).", "Aç",
                () => Application.OpenURL(Sartlar.KullanimSartlariAdresi));
            s.Eylem("Gizlilik politikası", "Hangi verilerin neden toplandığı ve nasıl silineceği (web sayfası).", "Aç",
                () => Application.OpenURL(Sartlar.GizlilikAdresi));
            s.Eylem("Oyun kuralları", "Sohbet, ticaret ve adlar için kurallar; yaptırımlar (web sayfası).", "Aç",
                () => Application.OpenURL(Sartlar.OyunKurallariAdresi));
            s.Eylem("Reklam gizlilik seçenekleri", "Ödüllü reklamlar için Google'ın onay ayarları (Avrupa'da gerekli).", "Aç",
                Reklam.GizlilikSecenekleri);
            s.Eylem("Engellenen oyuncular", "Engellediğin oyuncuların listesi ve engel kaldırma.", "Göster",
                () => { Close(); TicaretPenceresi.EngellenenleriGoster(); });
            s.Eylem("Açık kaynak lisansları", "Oyunda kullanılan açık kaynak yazılımlar ve ücretsiz içerikler.", "Göster",
                () => { Close(); Lisanslar.Goster(); });
            s.Eylem("Hesabımı sil", "Çevrimiçi hesabını, karakterlerini ve verilerini kalıcı olarak siler.", "Sil...",
                () => { Close(); HesapSilme.Goster(); });
            s.Finish();
        }

        private void BuildOyun(SectionBuilder s) {
            s.Choice("Kendi adım", "Karakterinin adı başının üstünde görünsün mü?",
                new[] { "Gizle", "Göster" }, () => PlayerPrefs.GetInt("ShowPlayerName", 1), v => {
                    PlayerPrefs.SetInt("ShowPlayerName", v);
                    UnitController player = GameManager?.PlayerManagerClient?.UnitController;
                    if (player != null) {
                        GameManager.SystemEventManager.NotifyOnReputationChange(player);
                    }
                });
            s.Choice("Hasar yazıları", "Vuruşlarda uçuşan hasar ve iyileşme sayıları.",
                new[] { "Gizle", "Göster" }, () => PlayerPrefs.GetInt("UseFloatingCombatText", 1), v => {
                    PlayerPrefs.SetInt("UseFloatingCombatText", v);
                    GameManager?.UIManager?.UpdateFloatingCombatText();
                });
            s.Choice("Mini harita", "Sağ üstteki küçük harita.",
                new[] { "Gizle", "Göster" }, () => PlayerPrefs.GetInt("UseMiniMap", 1), v => {
                    PlayerPrefs.SetInt("UseMiniMap", v);
                    GameManager?.UIManager?.UpdateMiniMap();
                });
            s.Choice("Otomatik ganimet", "Ölen düşmanın ganimeti dokununca kendiliğinden çantaya girer.",
                new[] { "Kapalı", "Açık" }, () => Ganimet.AutoLoot ? 1 : 0, v => Ganimet.AutoLoot = v == 1);
            s.Choice("Titreşim", "Düğmelere ve vuruşlara kısa titreşim.",
                new[] { "Kapalı", "Açık" }, () => MobileFeedback.Enabled ? 1 : 0, v => MobileFeedback.Enabled = v == 1);
            s.Choice("Otomatik iksir", "Can bu oranın altına inince çantadaki can iksiri kendiliğinden içilir.",
                new[] { "Kapalı", "%30", "%50" }, () => OtomatikAv.IksirSecimi, v => OtomatikAv.IksirSecimi = v);
            s.Choice("Yetenek çubuğu", "Büyük: yetenek kutuları parmakla kolay seçilir, menü simgeleri ekranın üstüne geçer.",
                new[] { "Normal", "Büyük" }, () => MobilArayuzDuzeni.CubukSecimi, v => MobilArayuzDuzeni.CubukSecimi = v);
            s.Choice("Ekran düğmeleri", "Saldır, Zıpla, Harita gibi yuvarlak düğmelerin boyutu.",
                new[] { "Küçük", "Normal", "Büyük" }, () => MobilArayuzDuzeni.DugmeBoyutuSecimi, v => MobilArayuzDuzeni.DugmeBoyutuSecimi = v);
            s.Choice("Düğme saydamlığı", "Düğmelerin arkasındaki dünya daha çok görünsün mü?",
                new[] { "%50", "%75", "%100" }, () => MobilArayuzDuzeni.SaydamlikSecimi, v => MobilArayuzDuzeni.SaydamlikSecimi = v);
            s.Choice("Oto av becerileri", "Oto av açıkken dövüşte yeteneklerin bekleme süreleri dolunca kendiliğinden kullanılır.",
                new[] { "Kapalı", "Açık" }, () => OtomatikAv.BeceriKullan ? 1 : 0, v => OtomatikAv.BeceriKullan = v == 1);
            s.Choice("Görev oku", "Ekranda sıradaki görev hedefini ve uzaklığını gösteren sarı ok.",
                new[] { "Kapalı", "Açık" }, () => GorevOku.Acik ? 1 : 0, v => GorevOku.Acik = v == 1);
            s.Choice("Bildirimler", "Günlük armağan hazır olunca ve uzun süre girmeyince telefona hatırlatma gelsin mi?",
                new[] { "Kapalı", "Açık" }, () => Bildirimler.Acik ? 1 : 0, v => Bildirimler.Acik = v == 1);
            s.Choice("Hata raporları", "Hata, çökme ve donmalar geliştiriciye kendiliğinden gönderilir. Kişisel bilgi gönderilmez.",
                new[] { "Kapalı", "Açık" }, () => HataBildirici.Acik ? 1 : 0, v => HataBildirici.Acik = v == 1);
            s.Finish();
        }

        // ---------------------------------------------------------------- ekran görüntüsü

        private IEnumerator ShareScreenshot() {
            // pencere görüntüye girmesin
            canvas.enabled = false;
            yield return new WaitForEndOfFrame();
            Texture2D screenshot = null;
            try {
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                new NativeShare()
                    .AddFile(screenshot, "otuken-destani.png")
                    .SetSubject("Ötüken Destanı")
                    .SetText("Ötüken Destanı'nda maceram sürüyor!")
                    .Share();
            } catch (Exception exception) {
                Debug.LogWarning($"SeceneklerPenceresi.ShareScreenshot(): {exception.Message}");
            } finally {
                if (screenshot != null) {
                    Destroy(screenshot);
                }
                canvas.enabled = true;
            }
        }

        // ---------------------------------------------------------------- yapı taşları

        private SectionBuilder CreateSection(Transform viewport, string name) {
            GameObject content = CreateRect(viewport, name, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.pivot = new Vector2(0.5f, 1f);
            sectionContents.Add(content);
            content.SetActive(false);
            return new SectionBuilder(this, contentRect);
        }

        /// <summary>
        /// bir bölümün satırlarını yukarıdan aşağı dizer
        /// </summary>
        private class SectionBuilder {
            private const float RowHeight = 96f;
            private const float RowSpacing = 8f;
            private readonly SeceneklerPenceresi window;
            private readonly RectTransform content;
            private float y = 12f;

            public SectionBuilder(SeceneklerPenceresi window, RectTransform content) {
                this.window = window;
                this.content = content;
            }

            private GameObject Row(string label, string hint) {
                GameObject row = window.CreateRect(content, label, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(12f, -(y + RowHeight)), new Vector2(-12f, -y));
                Image background = row.AddComponent<Image>();
                background.color = new Color(1f, 1f, 1f, 0.04f);
                GameObject labelObject = window.CreateRect(row.transform, "Ad", new Vector2(0f, 0.5f), new Vector2(0.38f, 1f), new Vector2(14f, 0f), new Vector2(-6f, -6f));
                Text labelText = window.CreateText(labelObject, label, 25, TextAnchor.LowerLeft, textColor);
                labelText.fontStyle = FontStyle.Bold;
                if (string.IsNullOrEmpty(hint) == false) {
                    GameObject hintObject = window.CreateRect(row.transform, "Aciklama", new Vector2(0f, 0f), new Vector2(0.38f, 0.5f), new Vector2(14f, 4f), new Vector2(-6f, -2f));
                    Text hintText = window.CreateText(hintObject, hint, 16, TextAnchor.UpperLeft, hintColor);
                    hintText.resizeTextForBestFit = true;
                    hintText.resizeTextMinSize = 12;
                    hintText.resizeTextMaxSize = 16;
                }
                y += RowHeight + RowSpacing;
                return row;
            }

            /// <summary>tek düğmeli satır (sayfa aç, pencere göster)</summary>
            public void Eylem(string label, string hint, string buttonLabel, Action action) {
                GameObject row = Row(label, hint);
                GameObject buttonObject = window.CreateRect(row.transform, buttonLabel, new Vector2(0.62f, 0.14f), new Vector2(0.98f, 0.86f), Vector2.zero, Vector2.zero);
                Image image = buttonObject.AddComponent<Image>();
                image.color = optionColor;
                Outline outline = buttonObject.AddComponent<Outline>();
                outline.effectColor = new Color(gold.r, gold.g, gold.b, 0.5f);
                outline.effectDistance = new Vector2(1f, -1f);
                Button button = buttonObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => {
                    MobileFeedback.Tap();
                    try {
                        action();
                    } catch (Exception exception) {
                        Debug.LogWarning($"SeceneklerPenceresi: {label}: {exception.Message}");
                    }
                });
                GameObject textObject = window.CreateRect(buttonObject.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
                window.CreateText(textObject, buttonLabel, 22, TextAnchor.MiddleCenter, textColor);
            }

            public void Choice(string label, string hint, string[] options, Func<int> get, Action<int> set) {
                GameObject row = Row(label, hint);
                List<Image> buttons = new List<Image>();
                float width = 1f / options.Length;
                for (int i = 0; i < options.Length; i++) {
                    int option = i;
                    GameObject buttonObject = window.CreateRect(row.transform, options[i], new Vector2(0.39f + 0.61f * width * i, 0.14f),
                        new Vector2(0.39f + 0.61f * width * (i + 1), 0.86f), new Vector2(4f, 0f), new Vector2(-4f, 0f));
                    Image image = buttonObject.AddComponent<Image>();
                    image.color = optionColor;
                    Outline outline = buttonObject.AddComponent<Outline>();
                    outline.effectColor = new Color(gold.r, gold.g, gold.b, 0.5f);
                    outline.effectDistance = new Vector2(1f, -1f);
                    Button button = buttonObject.AddComponent<Button>();
                    button.targetGraphic = image;
                    button.onClick.AddListener(() => {
                        MobileFeedback.Tap();
                        try {
                            set(option);
                        } catch (Exception exception) {
                            Debug.LogWarning($"SeceneklerPenceresi: {label}: {exception.Message}");
                        }
                        PlayerPrefs.Save();
                        window.RefreshAll();
                    });
                    GameObject textObject = window.CreateRect(buttonObject.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f));
                    Text text = window.CreateText(textObject, options[i], 22, TextAnchor.MiddleCenter, textColor);
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = 14;
                    text.resizeTextMaxSize = 22;
                    buttons.Add(image);
                }
                window.refreshers.Add(() => {
                    int selected = get();
                    for (int i = 0; i < buttons.Count; i++) {
                        buttons[i].color = i == selected ? tabSelectedColor : optionColor;
                    }
                });
            }

            public void Kaydirici(string label, Func<float> get, Action<float> set) {
                GameObject row = Row(label, null);
                GameObject sliderObject = window.CreateRect(row.transform, "Kaydirici", new Vector2(0.39f, 0.3f), new Vector2(0.88f, 0.7f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
                GameObject background = window.CreateRect(sliderObject.transform, "Arka", new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), Vector2.zero, Vector2.zero);
                background.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
                GameObject fillArea = window.CreateRect(sliderObject.transform, "DolguAlani", new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), Vector2.zero, Vector2.zero);
                GameObject fill = window.CreateRect(fillArea.transform, "Dolgu", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                fill.AddComponent<Image>().color = tabSelectedColor;
                GameObject handleArea = window.CreateRect(sliderObject.transform, "TutamakAlani", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
                GameObject handle = window.CreateRect(handleArea.transform, "Tutamak", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                RectTransform handleRect = handle.GetComponent<RectTransform>();
                handleRect.sizeDelta = new Vector2(44f, 6f);
                Image handleImage = handle.AddComponent<Image>();
                handleImage.sprite = MobileShapes.Circle;
                handleImage.color = gold;

                GameObject valueObject = window.CreateRect(row.transform, "Deger", new Vector2(0.88f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-10f, 0f));
                Text valueText = window.CreateText(valueObject, string.Empty, 24, TextAnchor.MiddleRight, textColor);

                Slider slider = sliderObject.AddComponent<Slider>();
                slider.fillRect = fill.GetComponent<RectTransform>();
                slider.handleRect = handleRect;
                slider.targetGraphic = handleImage;
                slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
                slider.minValue = 0f;
                slider.maxValue = 1f;
                bool refreshing = false;
                slider.onValueChanged.AddListener(value => {
                    valueText.text = "%" + Mathf.RoundToInt(value * 100f);
                    if (refreshing) {
                        return;
                    }
                    try {
                        set(value);
                    } catch (Exception exception) {
                        Debug.LogWarning($"SeceneklerPenceresi: {label}: {exception.Message}");
                    }
                });
                window.refreshers.Add(() => {
                    refreshing = true;
                    slider.value = Mathf.Clamp01(get());
                    valueText.text = "%" + Mathf.RoundToInt(slider.value * 100f);
                    refreshing = false;
                });
            }

            public void Finish() {
                content.sizeDelta = new Vector2(content.sizeDelta.x, y + 12f);
            }
        }

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
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private GameObject CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize,
            Color color, UnityEngine.Events.UnityAction onClick) {
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
            button.onClick.AddListener(onClick);
            GameObject textObject = CreateRect(buttonObject.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            CreateText(textObject, label, fontSize, TextAnchor.MiddleCenter, textColor);
            return buttonObject;
        }
    }
}
