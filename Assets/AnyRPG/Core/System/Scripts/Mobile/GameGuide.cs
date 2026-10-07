using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Oyun Kılavuzu: oyunun mantığı, ekran ve kontroller, savaş, sınıflar ve silahlar, çanta ve kuşanma, ganimet, demirci,
    /// depo, görevler, etkinlikler, diyarlar ve ışınlanma, para ve Kut, ticaret, çevrim içi, ayarlar.
    /// İlk kez oyuna girince kendiliğinden açılır; oyunda Menü > "Oyun Kılavuzu", ana menüde ve Ayarlar'da "Oyun Kılavuzu" düğmesiyle yeniden açılır.
    /// Kodla kurulur, hiçbir prefab ya da resim dosyası gerektirmez.
    /// </summary>
    public class GameGuide : MonoBehaviour {

        public const string CanvasName = "OyunRehberiCanvas";
        // above windows and menus (12-20 after MobileBootstrap.ScaleCanvases), below the Durum overlay (1000)
        private const int SortingOrder = 30;
        private const string ShownKey = "oyun-rehberi-gosterildi";

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color tabColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color tabSelectedColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);

        private static GameGuide instance = null;

        private Font font = null;
        private GameObject panelRoot = null;
        private GameObject menuLauncher = null;
        private Text bodyText = null;
        private Text headerText = null;
        private Text closeLabel = null;
        private ScrollRect bodyScroll = null;
        private RectTransform bodyRect = null;
        private RectTransform bodyContentRect = null;
        private readonly List<Image> tabImages = new List<Image>();
        private int currentSection = 0;

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        public static void Show(bool firstTime) {
            Ensure();
            instance.Open(firstTime);
        }

        /// <summary>
        /// open the guide the first time a character enters the world
        /// </summary>
        /// <summary>ağ botu: açıksa kapatır (ilk girişte kendiliğinden açılır)</summary>
        public static void Kapat() {
            if (IsOpen) {
                instance.Close();
            }
        }

        public static void ShowFirstTimeIfNeeded() {
            if (PlayerPrefs.GetInt(ShownKey, 0) != 0) {
                return;
            }
            PlayerPrefs.SetInt(ShownKey, 1);
            PlayerPrefs.Save();
            Show(true);
        }

        /// <summary>
        /// the "Nasıl Oynanır" button on the main menu screen
        /// </summary>
        public static void SetMenuLauncherVisible(bool visible) {
            if (visible == false && instance == null) {
                return;
            }
            Ensure();
            if (instance.menuLauncher.activeSelf != visible) {
                instance.menuLauncher.SetActive(visible);
            }
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
            instance = canvasObject.AddComponent<GameGuide>();
            instance.Build();
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // main menu launcher, bottom right
            menuLauncher = CreateButton(transform, "Oyun Kılavuzu", new Vector2(1f, 0f), new Vector2(-180f, 64f), new Vector2(300f, 76f), 28,
                tabSelectedColor, () => { Open(false); MobileFeedback.Tap(); }, out _);
            menuLauncher.SetActive(false);

            // full screen dimmer that also blocks taps on the game behind the guide
            panelRoot = CreateRect(transform, "Rehber", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimmer = panelRoot.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.7f);

            GameObject panel = CreateRect(panelRoot.transform, "Panel", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), Vector2.zero, Vector2.zero);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = panelColor;
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = gold;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            // title
            GameObject title = CreateRect(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -66f), new Vector2(-20f, -8f));
            Text titleText = CreateText(title, "ÖTÜKEN DESTANI  •  OYUN KILAVUZU", 34, TextAnchor.MiddleCenter);
            titleText.color = gold;
            titleText.fontStyle = FontStyle.Bold;

            // section list on the left
            GameObject tabArea = CreateRect(panel.transform, "Bolumler", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(16f, 92f), new Vector2(316f, -76f));
            RectTransform tabContent = CreateScroll(tabArea, out _);
            const float tabHeight = 50f;
            const float tabSpacing = 6f;
            for (int i = 0; i < sections.Length; i++) {
                int sectionIndex = i;
                GameObject tab = CreateButton(tabContent, sections[i].title, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, 22,
                    tabColor, () => { SelectSection(sectionIndex); MobileFeedback.Tap(); }, out _);
                RectTransform tabRect = tab.GetComponent<RectTransform>();
                tabRect.anchorMin = new Vector2(0f, 1f);
                tabRect.anchorMax = new Vector2(1f, 1f);
                tabRect.pivot = new Vector2(0.5f, 1f);
                tabRect.offsetMin = new Vector2(0f, -(i * (tabHeight + tabSpacing)) - tabHeight);
                tabRect.offsetMax = new Vector2(0f, -(i * (tabHeight + tabSpacing)));
                tabImages.Add(tab.GetComponent<Image>());
            }
            tabContent.sizeDelta = new Vector2(0f, sections.Length * (tabHeight + tabSpacing));

            // section text on the right
            GameObject bodyArea = CreateRect(panel.transform, "Metin", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(336f, 92f), new Vector2(-20f, -76f));
            Image bodyBackground = bodyArea.AddComponent<Image>();
            bodyBackground.color = new Color(0f, 0f, 0f, 0.25f);
            RectTransform bodyContent = CreateScroll(bodyArea, out bodyScroll);
            bodyContentRect = bodyContent;
            GameObject header = CreateRect(bodyContent, "BolumBasligi", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -52f), new Vector2(-18f, -8f));
            headerText = CreateText(header, string.Empty, 30, TextAnchor.MiddleLeft);
            headerText.color = gold;
            headerText.fontStyle = FontStyle.Bold;
            GameObject body = CreateRect(bodyContent, "BolumMetni", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -60f), new Vector2(-18f, -60f));
            bodyRect = body.GetComponent<RectTransform>();
            bodyRect.pivot = new Vector2(0.5f, 1f);
            bodyText = CreateText(body, string.Empty, 23, TextAnchor.UpperLeft);
            bodyText.lineSpacing = 1.12f;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;

            // bottom buttons
            CreateButton(panel.transform, "< Önceki", new Vector2(0.5f, 0f), new Vector2(-330f, 46f), new Vector2(220f, 64f), 24,
                tabColor, () => { SelectSection(currentSection - 1); MobileFeedback.Tap(); }, out _);
            CreateButton(panel.transform, "Sonraki >", new Vector2(0.5f, 0f), new Vector2(-90f, 46f), new Vector2(220f, 64f), 24,
                tabColor, () => { SelectSection(currentSection + 1); MobileFeedback.Tap(); }, out _);
            CreateButton(panel.transform, "Kapat", new Vector2(0.5f, 0f), new Vector2(230f, 46f), new Vector2(340f, 64f), 26,
                tabSelectedColor, () => { Close(); MobileFeedback.Tap(); }, out closeLabel);

            panelRoot.SetActive(false);
        }

        private void Open(bool firstTime) {
            closeLabel.text = firstTime ? "Anladım, oyuna başla!" : "Kapat";
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
            SelectSection(firstTime ? 0 : currentSection);
        }

        private void Close() {
            panelRoot.SetActive(false);
        }

        private void SelectSection(int index) {
            currentSection = Mathf.Clamp(index, 0, sections.Length - 1);
            for (int i = 0; i < tabImages.Count; i++) {
                tabImages[i].color = i == currentSection ? tabSelectedColor : tabColor;
            }
            headerText.text = sections[currentSection].title;
            bodyText.text = sections[currentSection].body;
            // size the text and the scroll content to the text, so long sections scroll
            Canvas.ForceUpdateCanvases();
            float bodyHeight = bodyText.preferredHeight;
            bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, bodyHeight);
            bodyContentRect.sizeDelta = new Vector2(bodyContentRect.sizeDelta.x, 60f + bodyHeight + 24f);
            Canvas.ForceUpdateCanvases();
            bodyScroll.verticalNormalizedPosition = 1f;
        }

        // ---- building blocks ----

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

        private Text CreateText(GameObject target, string content, int fontSize, TextAnchor alignment) {
            Text text = target.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = textColor;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// a vertical scroll area filling the target; returns the content rect (anchored to the top, grows downward)
        /// </summary>
        private RectTransform CreateScroll(GameObject target, out ScrollRect scrollRect) {
            GameObject viewport = CreateRect(target.transform, "Gorunum", Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            viewport.AddComponent<RectMask2D>();
            // an invisible graphic so a drag anywhere in the area scrolls it
            Image catcher = viewport.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            GameObject content = CreateRect(viewport.transform, "Icerik", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.pivot = new Vector2(0.5f, 1f);
            scrollRect = target.AddComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
            return contentRect;
        }

        private GameObject CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize,
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
            button.onClick.AddListener(onClick);
            GameObject textObject = CreateRect(buttonObject.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(8f, 0f), new Vector2(-8f, 0f));
            labelText = CreateText(textObject, label, fontSize, TextAnchor.MiddleCenter);
            return buttonObject;
        }

        // ---- content ----

        private struct Section {
            public string title;
            public string body;
            public Section(string title, string body) {
                this.title = title;
                this.body = body;
            }
        }

        private const string H = "<color=#E8C47A><b>";
        private const string HE = "</b></color>";

        private static readonly Section[] sections = new Section[] {
            new Section("Oyunun Mantığı",
                "<b>Ötüken Destanı</b>'na hoş geldin, alp!\n\n" +
                "Yeraltının efendisi <b>Erlik Han</b> Tamu'nun mührünü kırdı; kara ruhları on beş diyara saçıldı. " +
                "Sen, <b>Olcayto Han</b>'ın çağrısına uyan bir alpsın. Diyar diyar ilerleyip Erlik'in başbuğlarını yenecek, " +
                "sonunda Tamu Zindanı'nın dibinde Erlik Han'la yüzleşeceksin.\n\n" +
                H + "Oyunun döngüsü" + HE + "\n" +
                "1. <b>Görev al.</b> Başının üstünde sarı <b>!</b> olan kişiler görev verir. Her diyarda bir hikâye yardımcısı ve üç yan görev veren vardır.\n" +
                "2. <b>Avlan.</b> Canavarları yen, Ötüken Taşlarını kır. Tecrübe kazanır, seviye atlarsın.\n" +
                "3. <b>Ganimet topla.</b> Düşenler yere saçılır; yanından geçince kendiliğinden çantana girer.\n" +
                "4. <b>Güçlen.</b> Sınıfına uygun silah ve zırhı kuşan, <b>Demirci</b>'de +9'a kadar yükselt.\n" +
                "5. <b>İlerle.</b> Seviyen yetince <b>Işınlan</b> ile bir sonraki diyara geç. Her diyar bir öncekinden zor ve zengindir.\n\n" +
                H + "İlk adımların" + HE + "\n" +
                "• Ötüken Yaylası'nda köyün güney kapısının dışında başlarsın. Köydeki <b>Olcayto Han</b>'a dokun, <b>Kutun Çağrısı</b>'nı al.\n" +
                "• Girişin yanındaki <b>Avcı Batur</b>, <b>Otacı Ay Hatun</b> ve <b>Yılkıcı Erdem</b> de sana görev verir.\n" +
                "• İlk savaşların için en kolay düşmanlar <b>Kara Yek</b> ve <b>Çalı Cini</b>'dir.\n" +
                "• Sağ alttaki <b>Oto Av</b>'ı açarsan karakterin kendisi avlanır, ganimeti toplar.\n\n" +
                H + "Bilmen gerekenler" + HE + "\n" +
                "• Oyun tek kişilik (internetsiz) ya da çevrim içi oynanır. İkisinin kayıtları ayrıdır.\n" +
                "• Oyun kendiliğinden kaydedilir; çevrim içinde karakterin sunucuda saklanır.\n" +
                "• Bu kılavuzu istediğin zaman sol kenardaki <b>Menü</b> simgesinden <b>Oyun Kılavuzu</b> ile açarsın. " +
                "Soldaki bölümlere dokunarak gezin, uzun metni parmağınla kaydır."),

            new Section("Ekran ve Kontroller",
                H + "Yürümek ve koşmak" + HE + "\n" +
                "• Sol alttaki <b>yuvarlak çubuğu</b> parmağınla it: karakterin o yöne gider. <b>Az itersen yürür</b>, ileri ittikçe hızlanır, " +
                "sonuna kadar itersen <b>koşar</b>.\n" +
                "• <b>Oto koşu</b>: çubuğu en uca it ve bir an (yaklaşık 1 saniye) tut: çubukta <b>OTO KOŞU</b> yazar. Artık parmağını kaldırsan da " +
                "karakterin koşmaya devam eder. Çubuğu ortaya doğru geri çekince ya da yeniden dokununca yavaşlar ve durur.\n" +
                "• Zıplama yoktur; yollar ve basamaklar yürüyerek geçilir.\n\n" +
                H + "Kamera" + HE + "\n" +
                "• Ekranın boş yerinde tek parmakla <b>sağa-sola kaydır</b>: kamera döner; <b>yukarı-aşağı</b> kaydırınca açı değişir.\n" +
                "• İki parmakla <b>aç-kıstır</b>: yakınlaş, uzaklaş. Bir parmağın çubuktayken öbürüyle kamerayı çevirebilirsin.\n\n" +
                H + "Dokunmak" + HE + "\n" +
                "• <b>Yere dokun</b>: karakterin oraya yürür.\n" +
                "• <b>Kişiye, düşmana, eşyaya dokun</b>: seçer; yakınsan konuşur, saldırır ya da toplarsın, uzaksan yanına gidersin.\n" +
                "• <b>Uzun bas</b>: çantadaki eşyada Kullan / Kuşan / Sat menüsünü açar.\n\n" +
                H + "Ekranın düzeni" + HE + "\n" +
                "• <b>Sol alt</b>: <b>Çanta</b> simgesi (çantan), yanında <b>Ayarlar</b> çarkı ve <b>Işınlan</b> simgesi.\n" +
                "• <b>Sol kenar</b>: <b>Menü</b> simgesi. Ekran dolmasın diye öteki her şey onun içindedir: Karakter, Görevler, Günlük Görevler, " +
                "Demirci, Ticaret, Depo, Dünya Haritası, Etkinlikler, Sohbet (çevrim içi) ve Oyun Kılavuzu. Ödül bekleyince Menü'de altın nokta yanar.\n" +
                "• <b>Sağ alt</b>: büyük <b>Saldır</b> düğmesi ve çevresinde sınıfının <b>yetenekleri</b> (beceri halkası). " +
                "Yanında <b>Oto Av</b>, <b>Binek</b> ve <b>Hedef</b> (önündeki düşmanlar arasında sırayla hedef değiştirir).\n" +
                "• <b>Sağ üst</b>: küçük harita. Üstündeki <b>HARİTA</b>'ya dokununca büyük Dünya Haritası açılır. " +
                "Altında süren bölge etkinliğinin afişi görünür; dokununca etkinlik penceresi açılır.\n" +
                "• <b>Sol üst</b>: canın (kırmızı), manan / enerjin ve hedefinin çubukları.\n" +
                "• <b>Görev oku</b>: sarı ok sıradaki görev hedefini gösterir; yanındaki <b>[Git]</b>'e dokunursan karakterin oraya kendisi yürür.\n" +
                "• Pencereleri sağ üst köşelerindeki <b>X</b> ile kapatırsın."),

            new Section("Savaş",
                H + "Nasıl savaşılır" + HE + "\n" +
                "• Bir düşmana dokun ya da <b>Saldır</b>'a bas. Seçili düşman yoksa en yakınını bulur, yanına koşar ve vurur.\n" +
                "• Karakterin vururken yüzünü hedefe döner; her vuruşta düşman sarsılır, vurduğun belli olur.\n" +
                "• <b>Saldırı hızı silahına göredir</b>: pençe ve hançer en hızlı (0,8–0,9 sn), tek elli kılıç 1 sn, balta, gürz ve asa 1,1–1,3 sn, " +
                "yay 1,2 sn, iki elli silahlar 1,4–1,5 sn. Yavaş silah daha sert vurur.\n" +
                "• <b>Beceri halkası</b>: Saldır düğmesinin çevresindeki simgeler sınıfının yetenekleridir. Dokununca kullanılır; " +
                "kullanılan yetenek bir süre bekler, simgesi kararır. Yeni öğrendiğin yetenekler halkaya kendiliğinden eklenir.\n\n" +
                H + "Oto Av" + HE + "\n" +
                "• Açınca (düğme altın rengi olur) karakterin yakındaki düşmanları bulur, menziline girip saldırır, yeteneklerini bekleme süreleri " +
                "dolunca kendisi kullanır ve 15 m içindeki yerdeki ganimete yürüyüp toplar. Ötüken Taşlarını da kırar.\n" +
                "• Çubuğa dokununca kısa bir süre sen yönetirsin; düğmeye yeniden dokununca kapanır.\n" +
                "• <b>Otomatik iksir</b>: Ayarlar > Oyun'dan açılır; can %30 ya da %50'nin altına inince Şifa İksiri içilir.\n\n" +
                H + "Hayatta kalmak" + HE + "\n" +
                "• Savaş bitince canın yavaş yavaş dolar. Hızlı iyileşmek için çantada Şifa İksiri'ne uzun bas → Kullan.\n" +
                "• Kamplara toptan dalma: birini uzaktan seç, o sana gelince ötekilerden uzakta dövüş.\n" +
                "• <b>Kara Otacı</b> yanındakileri iyileştirir: önce onu düşür. Okçular ve kamlar uzaktan vurur: üstlerine koş.\n" +
                "• 5. seviyeye kadar <b>acemi koruması</b>: düşmanlardan %40 daha az hasar alırsın.\n" +
                "• Ölürsen <b>Yeniden Doğ</b> düğmesi çıkar; güvenli bir yerde yeniden başlarsın.\n\n" +
                H + "Düşmanların gücü" + HE + "\n" +
                "• Her diyarın bir seviye aralığı vardır. Canavarlar girişe yakın yerde aralığın altında, derinlerde üstünde doğar.\n" +
                "• <b>Seçkin</b> canavarlar (Abası, Kan Süvarisi, Yağmacı Başı, Buz Bekçisi, Kemik Alp) bir seviye yukarıdadır, daha sağlamdır ve daha iyi ganimet bırakır.\n" +
                "• <b>Boss</b>'lar diyarın en uzak ucundadır, aralığın tepesindedir; 10 dakikada bir yeniden doğar.\n" +
                "• Öldürdüğün düşmanların yerine 90 saniye sonra yenileri gelir."),

            new Section("Sınıflar ve Silahlar",
                "Her sınıfın kendine ait silahları ve zırhı vardır. Başka sınıfın eşyası <b>kuşanılamaz</b>: denersen nedeni yazar " +
                "(ör. \"... senin sınıfın bu türü kullanamaz\"). Canavarlar sınıfına uygun eşyayı <b>2,5 kat daha sık</b> düşürür.\n\n" +
                "• <b>Alp</b> — kılıç, gürz ve balta (tek ya da iki elli), kalkan · <b>plaka zırh</b>. Ağır zırhlı ön saf savaşçısı; yeni başlayana en kolayı.\n" +
                "• <b>Batur</b> — pençe (yumruk silahı), mızrak · <b>deri zırh</b>. Art arda vuran hızlı yakın dövüşçü.\n" +
                "• <b>Akıncı</b> — hançer · <b>deri zırh</b>. Gizlenir, arkadan vurur, zehir kullanır.\n" +
                "• <b>Okçu</b> — yay, arbalet · <b>deri zırh</b>. Uzaktan vurur, düşmanı yavaşlatır.\n" +
                "• <b>Kam</b> — asa, değnek, büyü kitabı · <b>kumaş giysi</b>. Kasırga, ateş ve buz büyüleri; güçlü ama kırılgan.\n" +
                "• <b>Otacı</b> — asa, değnek, büyü kitabı · <b>kumaş giysi</b>. İyileştirir, zehri temizler, düşenleri diriltir.\n\n" +
                H + "Uzmanlık" + HE + "\n" +
                "İlerledikçe köydeki uzmanlık ustalarından bir yol seçersin: Alp için Süvari ya da Kan Süvarisi, Kam için Ateş, Ayaz ya da Kara Kam, " +
                "Okçu için Nişancı ya da İzci. Köydeki <b>sınıf ustaları</b> yeni yetenekler öğretir.\n\n" +
                H + "Değerler" + HE + "\n" +
                "• <b>Güç</b>: yakın dövüş hasarı  • <b>Çeviklik</b>: ok ve hançer hasarı, kritik vuruş\n" +
                "• <b>Zekâ</b>: büyü gücü ve mana  • <b>Dayanıklılık</b>: can\n\n" +
                H + "Boylar" + HE + "\n" +
                "• <b>Bozoklar</b> (Gün, Ay, Yıldız Han'ın soyu): armağanları <b>Mavi Kanatlar</b>. <b>Üçoklar</b> (Gök, Dağ, Deniz Han'ın soyu): <b>Kızıl Kanatlar</b>.\n" +
                "• Kanatlarla kısa süre uçarsın. İki boyun arası soğuktur; öbür boyun askerleri sana düşman davranır.\n" +
                "• Görünüşünü köydeki <b>Ayna Kam</b>'da, adını <b>Dede Korkut</b>'ta, boyunu elçilerde değiştirebilirsin."),

            new Section("Çanta ve Kuşanma",
                H + "Çanta" + HE + "\n" +
                "• Sol alttaki <b>Çanta</b> simgesine dokun. Her sayfada 40 göz görünür; çantan büyüdükçe alttaki <b>‹ ›</b> ve sayfa numaralarıyla " +
                "sayfalar arasında geçersin (en çok 20 sayfa).\n" +
                "• <b>Menü > Karakter</b> açılınca çanta da yanında açılır: giydiklerin solda, kuşanabileceklerin sağda.\n" +
                "• <b>Sırala</b>: eşyaları türe ve kaliteye göre dizer, yığınları birleştirir. <b>Toplu Sat</b>: satıcıya gitmeden gri eşyaları ve " +
                "giydiğinden zayıf ya da kullanamadığın sıradan donanımı satar (tutmak istediğine dokun).\n" +
                "• Çanta dolarsa daha büyük <b>heybe</b> al ya da eşyalarını <b>Depo</b>'ya koy.\n\n" +
                H + "Eşyanın köşesindeki işaretler" + HE + "\n" +
                "• <color=#E8C47A><b>▲</b></color> altın: şu an kuşandığından <b>güçlü</b> — hemen kuşan!\n" +
                "• <color=#7CDC6A><b>●</b></color> yeşil: sınıfına uygun, kuşanabilirsin.\n" +
                "• <color=#FF7359><b>×</b></color> kırmızı: başka sınıfın eşyası, kuşanamazsın (satabilir ya da takas edebilirsin).\n\n" +
                H + "Karşılaştırma" + HE + "\n" +
                "Eşyaya dokununca açılan bilgi kutusunda yazar:\n" +
                "• \"Sınıfına uygun\" ya da \"Sınıfına uygun değil\" (kimin kullandığıyla) ve gereken seviye,\n" +
                "• silahta <b>Saldırı gücü</b>, zırhta <b>Zırh</b> değeri,\n" +
                "• şu an kuşandığın eşyayla <b>farkı</b>: <color=#7CDC6A>yeşil +</color> daha iyi, <color=#FF7359>kırmızı −</color> daha kötü.\n\n" +
                H + "Kuşanmak" + HE + "\n" +
                "• Eşyaya uzun bas → <b>Kuşan</b>. Çantana giydiğinden güçlü bir eşya girerse solda kart çıkar; <b>Kuşan</b>'a dokunman yeter.\n" +
                "• Kuşanamazsan nedeni ekranda yazar: seviyen yetmiyor ya da eşya başka sınıfın.\n\n" +
                H + "Eşya renkleri" + HE + "\n" +
                "Gri: değersiz • Beyaz: sıradan • <color=#4CE04C>Yeşil</color>: sıradışı • <color=#5A8CFF>Mavi</color>: nadir • " +
                "<color=#E040E0>Mor</color>: destansı • <color=#FF8000>Turuncu</color>: efsanevi. " +
                "Adının önünde \"Kusurlu\", \"Güçlü\", \"Üstün\", \"Eşsiz\" ya da \"Tanrısal\" yazan silahların gücü rastgeledir."),

            new Section("Ganimet",
                H + "Yere düşen ganimet" + HE + "\n" +
                "• Ölen canavarın ganimeti cesedin çevresine, <b>yere saçılır</b>.\n" +
                "• Yanından geçince (2–3 m) <b>kendiliğinden çantana girer</b>; Oto Av açıksa karakterin 15 m içindekilere kendisi yürür.\n" +
                "• Yerdeki eşya <b>3 dakika</b> durur, sonra kaybolur. Bu sürede <b>başka oyuncular da alabilir</b>: ganimetini bekletme!\n" +
                "• Akçe ve görev eşyaları doğrudan sana gelir, yere düşmez.\n" +
                "• Çantan doluysa eşya yerde kalır; yer açınca gidip al.\n\n" +
                H + "Nereden ne düşer" + HE + "\n" +
                "• <b>Canavarlar</b>: iksir, heybe, tomar, silah, zırh, kolye. Sınıfına uygun donanım daha sık düşer.\n" +
                "• <b>Hazine sandıkları</b>: kampların ortasında ve dağ doruklarında; akçe kesesi, mücevher, silah. Boşalan sandık birkaç dakikada dolar.\n" +
                "• <b>Ötüken Taşları</b>: akçe kesesi, Gök Taşı Parçası, iksir (Ötüken Taşları bölümüne bak).\n" +
                "• <b>Bozkır ganimeti</b> (yalnız satmak için): gri Kırık Ok Ucu'ndan turuncu <b>Ergenekon Demiri</b>'ne (3 altın) kadar.\n" +
                "• Boss'lar her yenilişte değerli bir hazine ve kese bırakır. Nadir bir şey aldığında telefon titrer.\n\n" +
                H + "Cevherler (Demirci için)" + HE + "\n" +
                "• <b>Demir Cevheri</b>: Börü Tepesi'nden itibaren (canavar başına %4)\n" +
                "• <b>Gümüş Cevheri</b>: Ordubalık Kenti'nden itibaren (%2,5)\n" +
                "• <b>Altın Cevheri</b>: Kağan Ordası'ndan itibaren (%1,5)\n" +
                "• <b>Gök Demiri</b>: Kurgan Mezarlığı'ndan itibaren (%0,8)\n" +
                "Seçkin canavarlarda şans 2,5 kat. Boss diyarın en iyi cevherinden bir tane bırakır (bazen bir tane daha). " +
                "Yan görev zincirlerinin son görevi de bir cevher verir. Cevherler takas ve pazarda satılabilir."),

            new Section("Demirci",
                "<b>Menü > Demirci</b>: silahını, zırhını ve takılarını <b>+9</b>'a kadar güçlendir. Silah hasar, zırh zırh, takı temel değer kazanır; " +
                "üstte <b>Güç Puanı</b>'nı görürsün.\n\n" +
                H + "Bedel ve şans" + HE + "\n" +
                "• Her basamak <b>Gümüş Akçe</b> ister (basamak büyüdükçe artar: +1 6 gümüş, +5 150, +9 486).\n" +
                "• +4, +5, +6 için 1, +7, +8, +9 için 2 <b>Gök Taşı Parçası</b> gerekir.\n" +
                "• Şans: +1 kesin, +2 %95, +3 %90, +4 %80, +5 %70, +6 %60, +7 %45, +8 %35, +9 %25.\n" +
                "• Tutmazsa <b>eşyan bozulmaz</b>, yalnız malzeme gider.\n" +
                "• <b>Demirci Kutsaması</b> (Kut Dükkânı) şansı 15 puan artırır.\n\n" +
                H + "Cevher yuvası (+6 ve üstü)" + HE + "\n" +
                "+5'ten sonra Demirci penceresinde bir <b>cevher yuvası</b> açılır. Yuvaya cevheri koymadan <b>Yükselt</b> çalışmaz:\n" +
                "• +6 için <b>Demir Cevheri</b>  • +7 için <b>Gümüş Cevheri</b>  • +8 için <b>Altın Cevheri</b>  • +9 için <b>Gök Demiri</b>\n" +
                "<b>Cevher Ekle</b>'ye dokun, sonra <b>Yükselt</b>. Cevher yükseltmeyle birlikte harcanır (tutmasa da).\n\n" +
                H + "Değer" + HE + "\n" +
                "Demirciden geçmiş eşya daha değerlidir: satıcı fiyatı +1'de 1,3 kat, +5'te 4 kat, +9'da 14 kat olur."),

            new Section("Depo",
                "<b>Depo</b> çantana sığmayanları sakladığın yerdir. <b>Menü > Depo</b> ile her yerden açılır (köydeki hazinedar da açar).\n\n" +
                "• <b>10 göz ücretsizdir.</b>\n" +
                "• Depo penceresindeki <b>10 Göz Al</b> düğmesiyle Google Play'den her alışta <b>10 göz</b> eklenir.\n" +
                "• Depoda her sayfa <b>50 göz</b>dur; en çok <b>20 sayfa</b> (1000 göz). Eklenen gözler sayfaları sırayla doldurur: " +
                "4 alışta ilk sayfa (50 göz) dolar, sonrakiler ikinci sayfaya geçer.\n" +
                "• Pencerenin solu depo, sağı çantandır. <b>Eşyaya dokun</b>: öbür tarafa geçer. Alttaki ‹ › ile sayfa değiştir.\n" +
                "• Depo gözleri hesabına bağlıdır: karakterinle birlikte saklanır, takas edilemez.\n" +
                "• Çevrim içinde depo sunucuda durur; aldığın gözler hemen gelir."),

            new Section("Görevler",
                "• Sarı <b>!</b>: görev verir. Sarı <b>?</b>: biten görevi teslim et. Bunları haritada da görürsün.\n" +
                "• <b>Menü > Görevler</b>: aldığın görevler ve kalan hedefler. Aynı anda 25 görev taşıyabilirsin.\n" +
                "• Ödüller: tecrübe, Gümüş Akçe, bazen eşya ya da cevher.\n\n" +
                H + "Ana hikâye: Ötüken Destanı (45 görev)" + HE + "\n" +
                "Olcayto Han'dan <b>Kutun Çağrısı</b>'nı al. Her diyarın girişinde bir <b>yardımcı</b> bekler (Ak Ana, Börü Alp Tonga, Kıyı Beyi Aybars...). " +
                "Önce diyarın düşmanlarını dağıtırsın, sonra Erlik'in o diyardaki başbuğunu (boss) yenersin; yardımcı seni bir sonraki diyara gönderir. " +
                "Sonunda Tamu'nun dibinde <b>Erlik Han</b>'ı yen ve Ötüken'e dön.\n\n" +
                H + "Yan görevler (405 görev)" + HE + "\n" +
                "• Her diyarın girişinin çevresinde <b>üç görev veren</b> durur: avcılar, otacılar, ustalar, bekçiler, ozanlar...\n" +
                "• Her biri <b>9 görevlik bir zincir</b> verir; biri bitince sıradaki açılır. Görev başlığındaki (3/9) kaçıncıda olduğunu gösterir.\n" +
                "• Zincirde canavar avı, iki türü birden avlama, Ötüken Taşı kırma, seçkin canavarlar ve diyarın boss'u vardır.\n" +
                "• Zincirin son görevi daha çok tecrübe ve akçe verir; Börü Tepesi'nden itibaren bir de <b>cevher</b>.\n\n" +
                H + "Günlük görevler" + HE + "\n" +
                "<b>Menü > Günlük Görevler</b>: her gün yenilenen 3 görev (düşman yen, ganimet topla, sandık boşalt, diyar gez). " +
                "Biten görevin ödülünü al; üçü de bitince büyük ödül.\n\n" +
                H + "Tekrarlanabilir görevler" + HE + "\n" +
                "Ötüken Yaylası'nda <b>Obaya Yardım</b>, <b>Mağara Nöbeti</b>, <b>Keşif Kolu</b>, <b>Albastı Avı</b>, <b>Kamın Bohçası</b>, " +
                "<b>Boyların Armağanı</b>: bitirdikten sonra yeniden alınır."),

            new Section("Etkinlikler",
                "Diyarlarda her gün Türkiye saatiyle etkinlikler olur. Süren etkinlik küçük haritanın altında afiş olarak görünür; " +
                "<b>Menü > Etkinlikler</b> günün takvimini gösterir.\n\n" +
                H + "Bölge etkinlikleri" + HE + "\n" +
                "Her 2 saatte bir (00:00, 02:00 ... 22:00) bir diyarda <b>45 dakika</b> sürer:\n" +
                "• <b>Çifte Tecrübe</b>: o diyarda canavarlardan 2 kat tecrübe.\n" +
                "• <b>Ganimet Bereketi</b>: eşya düşme şansı 2 kat.\n" +
                "• <b>Canavar İstilası</b>: canavarlar 2,5 kat hızlı yeniden doğar, tecrübe 1,5 kat.\n" +
                "• <b>Boss Avı</b>: diyarın boss'u 10 dakika yerine 2 dakikada yeniden doğar, ganimeti 1,5 kat.\n\n" +
                H + "Bozkır Şöleni" + HE + "\n" +
                "Cumartesi ve pazar 20:00–23:00: <b>bütün diyarlarda</b> tecrübe 1,5 kat.\n\n" +
                "İpucu: etkinlik başlayınca ekranda duyurulur. <b>Işınlan</b> ile o diyara geçip fırsatı kaçırma."),

            new Section("Diyarlar ve Işınlanma",
                H + "Işınlanma" + HE + "\n" +
                "• Sol alttaki <b>Işınlan</b> simgesi: her yerden, diyar listesinden seçip ışınlanırsın. Diyarın girişinde, Geçit Taşı'nın yanında belirirsin.\n" +
                "• Diyarlar <b>seviyene göre</b> açılır: bir diyar, en düşük seviyesinin bir altına geldiğinde açılır. Kilitli diyarın kartında " +
                "\"Kilitli · N. seviyede açılır\" yazar.\n" +
                "• Bulunduğun diyarı seçersen girişine dönersin (bir yere sıkışırsan işe yarar). Savaşta ve ölüyken ışınlanamazsın.\n" +
                "• <b>Geçit Taşı</b>: her diyarın girişindeki Göktürk harfli dikili taş. Dokununca Dünya Haritası açılır.\n" +
                "• Ordubalık ve çevresindeki diyarlar yollarla da bağlıdır: üstünde yer adı yazan geçitten yürüyerek geçersin.\n" +
                "• <b>Dünya Haritası</b> (sağ üstteki HARİTA ya da Menü): 15 diyar tek haritada; iki parmakla yakınlaş, sürükleyerek gez.\n\n" +
                H + "Diyarlar (kolaydan zora)" + HE + "\n" +
                "1. <color=#8FE07A>Sv 1–4</color> <b>Ötüken Yaylası</b>: köy ve çevresi; başlangıç.\n" +
                "2. <color=#8FE07A>Sv 3–6</color> <b>Umay Tarlaları</b>: ekinler; Çalı Cinleri, yağmacılar.\n" +
                "3. <color=#8FE07A>Sv 5–8</color> <b>Börü Tepesi</b>: meşaleli patikalar; ilk cevherler.\n" +
                "4. <color=#8FE07A>Sv 7–10</color> <b>Ak Deniz Kıyısı</b>: kumsal; yağmacılar ve Şulmuslar.\n" +
                "5. <color=#8FE07A>Sv 9–12</color> <b>Ordubalık Çarşısı</b>: başkentin çarşısı.\n" +
                "6. <color=#8FE07A>Sv 11–14</color> <b>Ordubalık Kenti</b>: değirmenler, taş köprüler.\n" +
                "7. <color=#8FE07A>Sv 13–16</color> <b>Ulukayın Ormanı</b>: şelalenin ardındaki orman.\n" +
                "8. <color=#8FE07A>Sv 15–18</color> <b>Koncolos İni</b>: dipte <b>Kara Koncolos</b>.\n" +
                "9. <color=#8FE07A>Sv 17–20</color> <b>Kağan Ordası</b>: Erlik'in en güçlü yandaşları.\n" +
                "10. <color=#8FE07A>Sv 19–22</color> <b>Kaf Dağı Yolu</b>: sarp kayalıklar, Kızıl Cinler.\n" +
                "11. <color=#8FE07A>Sv 21–24</color> <b>Ergenekon Mağarası</b>: atalarımızın demir dağı.\n" +
                "12. <color=#8FE07A>Sv 23–26</color> <b>Kurgan Mezarlığı</b>: kemik erler ve <b>Kemik Kağan</b>.\n" +
                "13. <color=#8FE07A>Sv 25–28</color> <b>Ay Dede Köyü</b>: ay ışığında ölülerin indiği köy.\n" +
                "14. <color=#8FE07A>Sv 27–30</color> <b>Erlik'in Mağarası</b>: lavlı zindan; dipte <b>Tepegöz</b>.\n" +
                "15. <color=#8FE07A>Sv 29–32</color> <b>Tamu Zindanı</b>: kapıda <b>Tamu Bekçisi</b>, dipte <b>Erlik Han</b>.\n\n" +
                "Diyarın seviyesi adının altında yazar: <color=#8FE07A>yeşil</color> sana göre, <color=#FF7359>kırmızı</color> henüz zor, gri artık kolay. " +
                "Çok üstündeysen canavarlar az tecrübe verir; sonraki diyara geç."),

            new Section("Ötüken Yaylası",
                "Haritada yukarısı kuzeydir. Köy haritanın ortasındadır.\n\n" +
                "• <b>Köy</b> (orta): güvenli. Olcayto Han, ustalar, tüccarlar, hazinedar ve zanaat tezgâhları.\n" +
                "• <b>Güney çayırı</b> (başladığın yerin güneyi): Kara Yek kampları. Çok kolay; ilk durağın.\n" +
                "• <b>Cin çayırları</b> (güneydoğu ve güneybatı): Çalı Cini sürüleri. Kolay.\n" +
                "• <b>Ağulu Bataklık</b> (güneydoğu, uzakta): Ağulu Körmösler. Orta.\n" +
                "• <b>Yağmacı Obası</b> (güney, uzakça): Yağmacı Okçular ve Yağmacı Başı. Kolay-orta.\n" +
                "• <b>Doğu korular</b>: Şulmuslar ve Kara Otacı. <b>Kuzeydoğu ormanı</b>: yağmacılar ve Kara Yekler. Orta.\n" +
                "• <b>Eski kurganlar</b> (kuzeydoğu ve güneybatı, çok uzakta): kemik erler. Orta-zor.\n" +
                "• <b>Ateş Dağı</b> (kuzey): Kara Kamlar, Abasılar, Kan Süvarileri, Kızıl Cinler; doruğunda <b>Ulu Evren</b>. Zor.\n" +
                "• <b>Buz Dağı</b> (batı): Kırağı Cadıları, Buz Bekçileri, Buz Cinleri; doruğunda <b>Yelbegen</b>. Zor.\n" +
                "• <b>Er Meydanı</b> (köyün doğu ucu): Er Meydanı Ağası'ndan rakip çağırıp dövüşürsün.\n\n" +
                H + "Ötüken Yaylası'nın görevleri" + HE + "\n" +
                "<b>Olcayto Han'ın Çağrısı</b>, <b>Alp'in Donanımı</b>, <b>Sefere Hazırlık</b> (Tüccar Karaçor), <b>Bilgeden Öğüt</b> (Bilge Tonyukuk), " +
                "<b>Karanlığın Kapısı</b> ve <b>Tepegöz'ün Laneti</b> (Erlik'in Mağarası), <b>Obanın Zanaatları</b>, <b>Er Meydanı</b>."),

            new Section("Ötüken Taşları",
                "Her diyara saçılmış, gök turkuazı kristalleri olan, üstünde Göktürk harfleri parlayan kayalar: <b>Ötüken Taşları</b>.\n\n" +
                "• Taşa dokun: karakterin vurur. Canı bitince taş <b>parçalanır</b> ve içinden ödül çıkar.\n" +
                "• Ödül: her seferinde <b>akçe kesesi</b>; çoğu zaman <b>Gök Taşı Parçası</b> (Demirci için); sık sık iksir; " +
                "bazen değerli taş, silah, gerdanlık ya da heybe; çok nadiren efsanevi <b>Ergenekon Demiri</b>.\n" +
                "• Taşlar karşılık vermez ama seviyeleri diyarın seviyesidir: zor diyardaki taş daha çok vuruşta kırılır.\n" +
                "• Kırılan taşın yerinde birkaç dakika sonra yenisi biter. Oto Av taşları da kırar.\n" +
                "• Ana hikâyede, yan görevlerde ve günlük görevlerde \"Ötüken Taşı kır\" hedefi çıkar."),

            new Section("Para ve Kut",
                H + "Para birimleri" + HE + "\n" +
                "<b>Bakır Akçe</b>, <b>Gümüş Akçe</b> ve <b>Altın</b>: 100 bakır = 1 gümüş, 100 gümüş = 1 altın.\n\n" +
                H + "Para nereden kazanılır" + HE + "\n" +
                "1. <b>Düşman öldür</b>: her düşmandan akçe çıkar, güçlülerden daha çok.\n" +
                "2. <b>Sandık aç</b> ve <b>Ötüken Taşı kır</b>: akçe keseleri.\n" +
                "3. <b>Görev yap</b>: her görev akçe verir; zincirlerin son görevi üç katını.\n" +
                "4. <b>Ganimet sat</b>: satıcıya dokun, çantadaki eşyaya uzun bas → <b>Sat</b>. <b>Değersizleri Sat</b> gri eşyaların hepsini satar. " +
                "Yanlışlıkla sattığını satıcının ilk sayfasından geri alırsın.\n" +
                "5. <b>Zanaat</b>: topladıklarını sat ya da daha değerli eşyalar yap.\n" +
                "6. <b>Günlük Armağan</b>: her gün oyuna girince; 7 gün üst üste gelirsen en büyüğünü alırsın.\n" +
                "7. <b>Seviye ödülleri</b>: 2, 3, 4, 5, 7, 10, 12, 15, 20, 25 ve 30. seviyelerde iksir, akçe, Gök Taşı Parçası ve altın.\n" +
                "8. <b>Sen yokken</b>: yarım saatten uzun ara verip dönersen yiğitlerinin topladığı akçe ve tecrübe seni bekler (en çok 10 saat). " +
                "İstersen kısa bir reklam izleyip <b>2 katını</b> alırsın (günde 3 kez).\n\n" +
                H + "Kut Dükkânı" + HE + "\n" +
                "<b>Kut</b>, Google Play'den alınan oyun parasıdır. <b>Menü > Ticaret > Kut Dükkânı</b> (ya da Demirci penceresi) ile açılır.\n" +
                "• <b>Demirci Kutsaması</b>: yükseltme şansı +15 puan.\n" +
                "• <b>Deneyim Muskası</b>: 1 saat %50 fazla tecrübe.\n" +
                "• <b>Ulu Heybe</b>: 24 gözlü çanta.\n" +
                "• <b>Depo gözü</b>: Depo penceresinden, 10'ar göz.\n" +
                "• <b>Reklam izle · +5 Kut</b>: Kut Dükkânı'nın en üstünde; günde 5 kez. Reklam yalnız sen istersen açılır, " +
                "sonuna kadar izlersen ödül gelir. Avrupa'daysan reklam onayını Ayarlar > Hesap > Reklam gizlilik seçenekleri'nden değiştirirsin.\n" +
                "Rastgele ödül satılmaz. Kut ve Kut ile alınanlar hesabına bağlıdır: takas edilemez, satılamaz, gerçek paraya çevrilemez."),

            new Section("Ticaret",
                H + "Takas" + HE + "\n" +
                "• <b>Menü > Ticaret</b>: yakındaki oyuncular listelenir. 15 m yakınındaki oyuncuya <b>Takas</b> teklif edersin.\n" +
                "• İkiniz de en çok 9 yuva eşya ve para koyarsınız. Bir şey değişince onaylar düşer; ikiniz de onaylayınca takas olur ve <b>geri alınamaz</b>.\n\n" +
                H + "Pazar kurmak" + HE + "\n" +
                "• <b>Ticaret > Pazar Kur</b>: çantandaki en çok 8 eşyaya fiyat yaz (ör. <b>5a 20g</b> = 5 Altın 20 Gümüş; çıplak sayı gümüştür), pazarına ad ver ve aç.\n" +
                "• Pazarının adı başının üstünde görünür; yakındakiler dokunup alır, para sana gelir. Yürürsen pazar kapanır.\n\n" +
                H + "Kurallar" + HE + "\n" +
                "• Küfür, hakaret, taciz, dolandırıcılık, hile ve <b>gerçek parayla eşya, para ya da hesap satmak yasaktır</b>.\n" +
                "• Rahatsız eden oyuncuyu Ticaret penceresinden <b>Şikâyet Et</b> ve <b>Engelle</b>.\n" +
                "• İksirler, malzemeler ve ganimetler bir yuvada 200'e kadar yığılır."),

            new Section("Seviye, Binek, Zanaat",
                H + "Seviye" + HE + "\n" +
                "• Düşman öldürmek ve görev bitirmek <b>tecrübe</b> kazandırır; dolunca seviye atlarsın: canın ve gücün artar, yeni yeteneklerin açılır.\n" +
                "• En hızlı güçlenme yolu: sınıfına uygun, ▲ işaretli eşyaları kuşanmak ve Demirci'de yükseltmek.\n\n" +
                H + "Binek" + HE + "\n" +
                "5. seviyede ejderha <b>Evren</b> seni seçer. Sağdaki <b>Binek</b> düğmesine dokununca binersin (iki kat hızlı), yeniden dokununca inersin. " +
                "Zindanlarda ve savaşta binilmez.\n\n" +
                H + "Toplama zanaatları" + HE + "\n" +
                "<b>Madencilik</b>, <b>Otacılık</b>, <b>Odunculuk</b>, <b>Balıkçılık</b>: önce köydeki ustasından öğren, sonra damara, çiçeğe, ağaca ya da göle dokun.\n\n" +
                H + "Yapım zanaatları" + HE + "\n" +
                "<b>Demircilik</b>, <b>Terzilik</b>, <b>Dericilik</b>, <b>Simya</b>, <b>Aşçılık</b>, <b>Kuyumculuk</b>, <b>Yazıcılık</b>, <b>Marangozluk</b>: " +
                "köydeki ustalardan öğrenilir, tezgâhlarda yapılır. <b>Obanın Zanaatları</b> görevi seni yönlendirir."),

            new Section("Çevrim İçi",
                "• Ana menüde <b>Çevrim İçi Oyna</b>: kullanıcı adı ve şifre yaz, <b>Giriş</b>'e bas. İlk girişte hesabın açılır; şifreni unutma.\n" +
                "• Aynı diyardaki öteki oyuncuları görür, birlikte savaşırsın. Düşmanları, ganimeti ve kayıtları sunucu yönetir.\n" +
                "• Yere düşen ganimeti herkes görür; 3 dakika içinde ilk alan alır.\n" +
                "• <b>Menü > Sohbet</b>: aynı diyardakiler yazdıklarını görür.\n" +
                "• Günlük armağan ve görevler, demirci, depo, etkinlikler, seviye ödülleri ve binek çevrim içinde de vardır; hepsini sunucu verir. " +
                "Günler Türkiye saatiyle gece yarısı yenilenir.\n" +
                "• Oyunun yeni sürümü çıktığında giriş ekranı seni Google Play'e yönlendirir; güncelleyip yeniden gir."),

            new Section("Ayarlar ve Sorunlar",
                H + "Ayarlar (sol alttaki çark)" + HE + "\n" +
                "• <b>Görüntü</b>: grafik kalitesi, çözünürlük, gölgeler, görüş ve isim mesafesi.\n" +
                "• <b>Akıcılık</b>: FPS sınırı ve FPS göstergesi. 30 FPS telefonu daha az ısıtır, pili uzatır.\n" +
                "• <b>Ses</b>: müzik ve efekt sesleri.\n" +
                "• <b>Oyun</b>: otomatik iksir, Oto Av becerileri, görev oku, mini harita, titreşim, bildirimler, ekran düğmelerinin boyutu ve saydamlığı.\n" +
                "• <b>Hesap</b>: şartlar, gizlilik ve hesap silme.\n\n" +
                H + "Oyun takılıyorsa" + HE + "\n" +
                "Grafik kalitesini düşür, çözünürlüğü Düşük, gölgeleri Kapalı yap, görüş mesafesini kısalt.\n\n" +
                H + "Sorun bildirmek" + HE + "\n" +
                "Ayarlar penceresindeki <b>Sorun Bildir</b>: kısaca yaz, ekran görüntüsüyle geliştiriciye gider. Hatalar ve çökmeler zaten kendiliğinden bildirilir.\n\n" +
                H + "Gereksinimler" + HE + "\n" +
                "Android 7.1 ya da üstü, en az 3 GB bellek (4 GB önerilir), yaklaşık 1,5 GB boş alan. Çevrim içi oyun için internet.\n\n" +
                H + "Yapımcı" + HE + "\n" +
                "Oyun tasarımı ve yapımcılık: <b>Olcay Yasin Dünder</b> · zootopiayazilim.com\n" +
                "Oyun motoru: AnyRPG (açık kaynak, MIT lisansı)"),
        };
    }
}
