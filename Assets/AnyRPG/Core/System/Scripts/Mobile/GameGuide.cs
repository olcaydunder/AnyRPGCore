using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// "Nasıl Oynanır" rehberi: kontroller, savaş, para, ganimet, görevler, zanaatlar, bölgeler, geçit taşları ve gereksinimler.
    /// İlk kez oyuna girince kendiliğinden açılır; oyunda Menü > "Nasıl Oynanır", ana menüde "Nasıl Oynanır" düğmesiyle yeniden açılır.
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
            menuLauncher = CreateButton(transform, "Nasıl Oynanır", new Vector2(1f, 0f), new Vector2(-180f, 64f), new Vector2(300f, 76f), 28,
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
            Text titleText = CreateText(title, "ÖTÜKEN DESTANI  •  NASIL OYNANIR", 34, TextAnchor.MiddleCenter);
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
            new Section("Hoş Geldin",
                "<b>Ötüken Destanı</b>'na hoş geldin, yiğit!\n\n" +
                "Yeraltının efendisi Erlik Han'ın kara kulları yeryüzüne çıktı; Ötüken'in obaları tehlikede. " +
                "Sen, Olcayto Han'ın çağrısına uyan bir alpsın. Obanı koru, canavarları yen, ganimet topla, güçlen ve destanını yaz.\n\n" +
                H + "İlk adımların" + HE + "\n" +
                "1. Oyuna köyün güney kapısının dışında başlarsın. Kuzeye, köye doğru yürü.\n" +
                "2. Başının üstünde sarı <b>!</b> işareti olan kişiler sana görev verir. Köydeki <b>Olcayto Han</b>'ı bul ve ona dokun.\n" +
                "3. Görevi kabul et, hedefleri yap, sonra görevi veren kişiye dön. Sarı <b>?</b> işareti, teslim edebileceğin görevi gösterir.\n" +
                "4. Başladığın yerin hemen güneyindeki çayırda <b>Kara Yek</b>'ler dolaşır: ilk savaşların için en kolay düşmanlar.\n" +
                "5. Seviye atladıkça köyden uzaklaş: uzak kamplar, dağlar ve mağara daha zorlu ama daha zengindir.\n\n" +
                H + "Bilmen gerekenler" + HE + "\n" +
                "• Oyun internetsiz, tek kişilik oynanır.\n" +
                "• Oyun kendiliğinden kaydedilir: 3 dakikada bir ve uygulamadan çıktığında.\n" +
                "• Her gün oyuna girdiğinde <b>Günlük Armağan</b> seni bekler; 7 gün üst üste gelirsen en büyüğünü alırsın.\n" +
                "• Bu rehberi istediğin zaman sol kenardaki <b>Menü</b> düğmesinden <b>Nasıl Oynanır</b> ile yeniden açabilirsin.\n" +
                "• Soldaki bölümlere dokunarak konular arasında gezin; uzun metinleri parmağınla yukarı kaydır."),

            new Section("Kontroller",
                H + "Yürümek ve koşmak" + HE + "\n" +
                "• Sol alttaki <b>yuvarlak çubuğu</b> parmağınla it: kahramanın o yöne döner ve yürür. Ne kadar uzağa itersen o kadar hızlı gider.\n" +
                "• Çubuğu yukarı itmek, kameranın baktığı yöne gitmektir. Sağa itersen sağa döner, aşağı itersen kameraya doğru gelir.\n" +
                "• <b>Koş/Yürü</b>: koşmakla yürümek arasında geçiş yapar. Ekranda \"Koşuyorsun\" ya da \"Yürüyorsun\" yazar.\n" +
                "• <b>Zıpla</b>: zıplar; çitlerin, taşların ve basamakların üstünden geçmene yarar.\n\n" +
                H + "Kamerayı çevirmek" + HE + "\n" +
                "• Ekranın boş bir yerine tek parmağını koy ve <b>sağa-sola kaydır</b>: kamera kahramanın etrafında döner.\n" +
                "• Aynı şekilde <b>yukarı-aşağı kaydırırsan</b> kameranın açısı değişir.\n" +
                "• İki parmağını ekrana koyup <b>aç ya da kıstır</b>: yakınlaşır, uzaklaşırsın.\n" +
                "• Bir parmağın çubuktayken öbür parmağınla kamerayı çevirebilirsin.\n\n" +
                H + "Dokunmak" + HE + "\n" +
                "• <b>Yere dokun</b>: kahramanın oraya yürür.\n" +
                "• <b>Bir kişiye, düşmana ya da eşyaya dokun</b>: onu seçer. Yakınsan konuşursun, saldırırsın ya da toplarsın; uzaksan yanına gidersin.\n" +
                "• <b>Parmağını basılı tut (uzun bas)</b>: bilgisayardaki sağ tık gibidir. Çantadaki eşyalarda Kullan / Kuşan / Sat menüsünü açar.\n\n" +
                H + "Ekrandaki düğmeler" + HE + "\n" +
                "• <b>Saldır</b>: seçili düşmana saldırır. Seçili düşman yoksa en yakın düşmanı bulur, yanına koşar ve vurur.\n" +
                "• <b>Oto Av</b>: açınca (düğme altın rengi olur) karakter yakındaki düşmanları kendisi bulur, saldırır ve ölenlerin " +
                "ganimetini toplar. Dövüşte yeteneklerini de bekleme süreleri dolunca kendisi kullanır (Menü > Oyun'dan kapatılabilir). " +
                "Hareket çubuğuna dokununca kısa bir süre durur, sen yönetirsin. Yeniden dokununca kapanır.\n" +
                "• <b>Hedef</b>: önündeki düşmanlar arasında sırayla hedef değiştirir.\n" +
                "• <b>Harita</b>: <b>Dünya Haritası</b>; 15 diyar tek haritada. İki parmakla kıstırarak yakınlaş, sürükleyerek gez. " +
                "Yakınlaşınca mavi elmas Geçit Taşı'nı, altın noktalar komşu diyarlara kapıları, mavi ok seni gösterir. " +
                "Bir diyara dokun, <b>Işınlan</b>'a bas. Bulunduğun bölgenin ayrıntılı haritası için <b>Bölge Haritası</b>.\n" +
                "• <b>Işınlan</b> (Harita'nın yanında): aynı dünya haritası; sıralı liste için <b>Liste</b>.\n" +
                "• <b>Karakter</b>: giydiğin donanım ve değerlerin.\n" +
                "• <b>Günlük</b> (Karakter'in yanında): her gün yenilenen 3 görev (düşman yen, ganimet topla, sandık boşalt, diyar gez). " +
                "Biten görevin ödülünü al; üçü de bitince büyük ödül. Ödül bekleyince düğmede altın nokta yanar.\n" +
                "• <b>Binek</b>: 5. seviyede ejderha Evren seni seçer. Dokununca binersin (iki kat hızlı), yeniden dokununca inersin. " +
                "Zindanlarda ve savaşta binilmez.\n" +
                "• <b>Görev oku</b>: ekranda sarı ok sıradaki görev hedefini (teslim edilecek kişi, avlanacak düşman, yeni görev veren) " +
                "ve uzaklığını gösterir. Okun yanındaki yazıya (<b>[Git]</b>) dokunursan karakterin oraya kendiliğinden yürür; " +
                "varınca konuşur, toplar ya da saldırır. Menü > Oyun'dan kapatılabilir.\n" +
                "• <b>Sen yokken</b>: oyuna yarım saatten uzun ara verip dönersen yiğitlerin topladığı Gümüş Akçe ve tecrübe seni bekler " +
                "(en çok 10 saat sayılır).\n" +
                "• <b>Demirci</b> (Görevler'in yanında): silahını ve zırhını Gümüş Akçe ile, +4'ten sonra Gök Taşı Parçası da vererek " +
                "+9'a kadar güçlendir. +1 kesin tutar, sonra şans azalır (+9 %25). Tutmazsa eşyan bozulmaz, yalnız malzeme gider. " +
                "Silah hasar, zırh zırh, takılar temel değer kazanır. Üstte <b>Güç Puanı</b>'nı görürsün.\n" +
                "• <b>Seviye ödülleri</b>: 2, 3, 4, 5, 7, 10, 12, 15, 20, 25 ve 30. seviyelerde iksir, Gümüş Akçe, Gök Taşı Parçası ve Altın armağan edilir.\n" +
                "• <b>Daha iyi eşya</b>: çantana giydiğinden güçlü bir eşya girerse solda kart çıkar; <b>Kuşan</b>'a dokunman yeter.\n" +
                "• <b>Acemi koruması</b>: 5. seviyeye kadar düşmanlardan %40 daha az hasar alırsın.\n" +
                "• <b>Çanta</b>: eşyaların ve paran.\n" +
                "• <b>Görevler</b>: aldığın görevler ve kalan hedefleri.\n" +
                "• <b>Menü</b>: seçenekler (grafik, akıcılık, ses, oyun), bu rehber, ekran görüntüsü paylaşma ve <b>Sorun Bildir</b> " +
                "(bir sorun görürsen kısaca yaz; ekran görüntüsüyle geliştiriciye gider). Hatalar ve çökmeler zaten kendiliğinden bildirilir.\n" +
                "• Ekrandaki düğmeler hiçbir göstergenin üstüne binmez: telefonun ekranına göre kendiliğinden boş yere kayarlar.\n" +
                "• <b>Menü > Oyun</b>: otomatik iksir (can %30 ya da %50'nin altına inince Şifa İksiri içilir), büyük yetenek çubuğu, " +
                "ekran düğmelerinin boyutu ve saydamlığı.\n" +
                "• Alttaki sıra <b>yetenek çubuğu</b>dur: yeteneklerine dokunarak kullanırsın.\n" +
                "• Pencereleri sağ üst köşelerindeki <b>X</b> ile kapatırsın.\n" +
                "• Üstteki <b>Durum</b> düğmesi teşhis raporudur. Bir sorun yaşarsan açıp <b>Kopyala</b>'ya bas ve yapımcıya gönder.\n\n" +
                H + "Oyun takılıyorsa" + HE + "\n" +
                "• <b>Menü > Görüntü</b>: Çözünürlüğü Düşük, Gölgeleri Kapalı yap; Görüş mesafesini ve İsim mesafesini kısalt.\n" +
                "• <b>Akıcılık</b> bölümünde FPS göstergesini açarsan kare hızını sol üstte görürsün.\n" +
                "• FPS sınırını 30 yaparsan telefon daha az ısınır ve pil daha uzun gider."),

            new Section("Savaş",
                H + "Nasıl savaşılır" + HE + "\n" +
                "• Bir düşmana dokun ya da <b>Saldır</b>'a bas. Kahramanın elindeki silahla kendiliğinden vurmaya başlar.\n" +
                "• Alttaki yetenek çubuğundaki <b>yeteneklerine</b> dokunarak güçlü vuruşlar, büyüler ve iyileştirmeler yaparsın. " +
                "Kullanılan yetenek bir süre bekler; simgesi kararır ve geri sayar.\n" +
                "• Sol üstte kendi <b>can</b> (kırmızı) ve <b>mana / enerji / öfke</b> çubukların, yanında hedefinin çubukları görünür.\n" +
                "• Düşmanın uzaktaysa \"Hedef menzil dışında, yaklaş\" yazar. Manan bitmişse \"Yeterli Mana yok\" yazar: biraz bekle, mana dolar.\n\n" +
                H + "Hayatta kalmak" + HE + "\n" +
                "• Savaş bitince canın yavaş yavaş kendiliğinden dolar.\n" +
                "• Hızlı iyileşmek için çantada <b>Şifa İksiri</b>'ne uzun bas → <b>Kullan</b>. Ekmek ve peynir de can verir.\n" +
                "• Aynı anda çok düşmana saldırma: kamplarda birini uzaktan seç, o sana gelince diğerlerinden uzakta dövüş.\n" +
                "• <b>Kara Otacı</b> yanındaki düşmanları iyileştirir: kampta önce onu düşür.\n" +
                "• Okçular ve kamlar uzaktan vurur: üstlerine koşup yakından dövüş.\n" +
                "• Ölürsen ekranda <b>Yeniden Doğ</b> düğmesi çıkar; güvenli bir yerde yeniden başlarsın.\n\n" +
                H + "Düşmanların gücü" + HE + "\n" +
                "• Düşmanların seviyesi senin seviyene göre ayarlanır. Köyün çevresindekiler seninle aynı seviyededir; dağlarda ve haritanın köşelerinde 1-3 seviye daha güçlüdürler.\n" +
                "• Abası, Kan Süvarisi, Buz Bekçisi ve Yağmacı Başı sıradan düşmanlardan sağlamdır.\n" +
                "• <b>Cinler ve Körmösler</b>: Erlik'in yeraltından salıverdiği yaratıklar. Cüce <b>Çalı Cinleri</b> en kolaylarıdır, sürüyle gezerler. " +
                "Boynuzlu, gürzlü <b>Körmösler</b> daha güçlüdür; <b>Ayaz Körmösü</b> en irileridir.\n" +
                "• <b>Kemik erler</b>: Erlik'in eski kurganlardan kaldırdığı iskeletler. <b>Kemik Er</b> kılıç ve kalkanla, <b>Kemik Akıncı</b> iki kılıçla, " +
                "<b>Kemik Kam</b> asasıyla vurur; boynuzlu miğferli <b>Kemik Alp</b> en sağlamlarıdır. Yıkılınca kemik yığınına dönerler.\n" +
                "• Dağ doruklarındaki <b>Ulu Evren</b> (ejderha) ve <b>Yelbegen</b> (yedi başlı dev) en zorlu düşmanlardır. İyi donanım ve iksir olmadan gitme!\n" +
                "• Öldürdüğün düşmanların yerine 90 saniye sonra yenileri gelir; Ulu Evren ve Yelbegen 10 dakikada döner."),

            new Section("Para Kazanma",
                H + "Para birimleri" + HE + "\n" +
                "• <b>Bakır Akçe</b>, <b>Gümüş Akçe</b> ve <b>Altın</b>.\n" +
                "• 100 bakır = 1 gümüş, 100 gümüş = 1 altın. Paranı çanta penceresinin altında görürsün.\n\n" +
                H + "Para nereden kazanılır" + HE + "\n" +
                "1. <b>Düşman öldür.</b> Her düşmanın üstünden para çıkar: seviyesi kadar gümüş, güçlü düşmanlardan daha fazlası. " +
                "Ölen düşmanın üstü parlıyorsa ona dokun: ganimet <b>kendiliğinden çantana girer</b>, ekranda yeşil, mavi, mor yazıyla \"+ Kurt Dişi\" gibi görünür. " +
                "Çantan doluysa ganimet penceresi açılır; yer açıp <b>Hepsini Al</b>'a bas.\n" +
                "2. <b>Hazine sandıklarını aç.</b> Düşman kamplarının ortasında ve dağ doruklarında sandıklar var. " +
                "İçlerinden <b>Akçe Kesesi</b>, <b>Dolu Akçe Kesesi</b>, hatta <b>Altın Kese</b> çıkar. Kese alınınca para doğrudan cebine geçer. " +
                "Boşalttığın sandık birkaç dakika sonra yeniden dolar.\n" +
                "3. <b>Görev yap.</b> Görevler bitince altın, tecrübe ve eşya ödülü verir. Tekrarlanabilir görevleri (Obaya Yardım, Mağara Nöbeti, Albastı Avı) istediğin kadar yapabilirsin.\n" +
                "4. <b>Ganimet ve eşya sat.</b> Düşmanlardan düşen kurt dişi, kartal tüyü, gök taşı, eski altın sikke gibi ganimetler yalnızca satmak içindir. " +
                "İşine yaramayan silah, zırh, kolye, cevher, ot ve kereste de satılır. " +
                "Köydeki <b>Tüccar Karaçor</b>'a ya da bir malzemeciye dokun; satıcı penceresi açıkken çantadaki eşyaya uzun bas → <b>Sat</b>. " +
                "Satıcı penceresinin sol altındaki <b>Değersizleri Sat</b> düğmesi gri eşyaların hepsini tek dokunuşla satar. " +
                "Değerli (renkli) eşyalar çok daha pahalıya gider. Yanlışlıkla sattığını satıcının ilk sayfasından geri alabilirsin.\n" +
                "5. <b>Zanaatla uğraş.</b> Maden kaz, ot topla, ağaç kes, balık tut. Topladıklarını sat ya da onlardan daha değerli eşyalar yap.\n" +
                "6. <b>Günlük Armağan.</b> Her gün oyuna girince gümüş, iksir, heybe ya da altın kazanırsın. 7. gün: 1 altın ve bir Gök Taşı Parçası. " +
                "Bir gün kaçırırsan seri 1. günden yeniden başlar.\n\n" +
                H + "Para ne işe yarar" + HE + "\n" +
                "• Satıcılardan iksir, yiyecek, silah, zırh ve daha büyük <b>heybeler</b> (daha çok eşya taşırsın) alırsın. " +
                "Fiyatlar seviyene göre artar: 1. seviyede bir silah 2 gümüş, bir şifa iksiri 1 gümüş eder; renkli eşyalar çok daha pahalıdır.\n" +
                "• Bir eşyanın satış değeri, bilgi kutusunun altında <b>Satış Fiyatı</b> olarak yazar.\n" +
                "• Zanaat malzemeleri ve tarifler alırsın.\n" +
                "• Ustalardan yeni yetenek ve zanaat öğrenirsin."),

            new Section("Eşya ve Ganimet",
                H + "Eşya bulabileceğin yerler" + HE + "\n" +
                "• <b>Düşman cesetleri:</b> iksir, heybe, tomar, silah ve zırh düşer. Ağır zırhlı düşmanlardan zırh takımları, kamlardan kumaş giysiler çıkar.\n" +
                "• <b>Hazine Sandığı:</b> düşman kamplarının ortasında. Akçe kesesi, iksir, mücevher, kolye, heybe, silah.\n" +
                "• <b>Büyük Hazine Sandığı:</b> Ateş Dağı ve Buz Dağı'nda, mağarada ve haritanın uzak köşelerinde. Altın kese, destansı silahlar, değerli zırhlar.\n" +
                "• <b>Ekmek ve Peynir:</b> kampların yanında ve yol kenarlarında; dokunup alırsın.\n" +
                "• <b>Toplama:</b> çiçekler ve otlar, maden damarları ve kristaller, ağaçlar, göldeki balıklar. Bunları toplamak için önce zanaatı öğrenmelisin (Zanaatlar bölümüne bak).\n\n" +
                H + "Bozkır ganimeti" + HE + "\n" +
                "Her düşmandan, silah ve zırhın yanında satmak için ganimet de düşebilir:\n" +
                "• <color=#9A9A9A>Gri</color> (sık): Kırık Ok Ucu, Paslı Kemer Tokası, Çatlak Boncuk. Birkaç bakır eder.\n" +
                "• Beyaz ve <color=#4CE04C>yeşil</color>: Kurt Dişi, Kurt Pençesi, Kartal Tüyü. 1-4 gümüş.\n" +
                "• <color=#5A8CFF>Mavi</color> ve <color=#E040E0>mor</color> (seyrek): Gök Taşı Parçası, Eski Altın Sikke, Altın Tamga Yüzüğü. 15-60 gümüş.\n" +
                "• <color=#FF8000>Turuncu</color> (çok nadir): <b>Ergenekon Demiri</b>. 3 altın!\n" +
                "• Ulu Evren, Yelbegen ve Tepegöz her yenilişte mavi, mor ya da turuncu bir hazine ve bir kese bırakır.\n" +
                "Nadir bir ganimet aldığında telefon titrer.\n\n" +
                H + "Çantayı kullanmak" + HE + "\n" +
                "• <b>Çanta</b> düğmesine bas. Eşyanın üstüne parmağını basılı tutarsan bir menü açılır: <b>Kullan</b>, <b>Kuşan</b>, <b>Sat</b>, <b>At</b>, <b>Yok Et</b>.\n" +
                "• Bir eşyaya kısa dokunursan onu tutarsın; başka bir kutuya dokunursan oraya bırakırsın.\n" +
                "• Bulduğun silah ve zırhları <b>Kuşan</b> ile giy. Neyi giydiğini <b>Karakter</b> penceresinde görürsün.\n" +
                "• Çanta dolarsa eşya sat ya da daha büyük bir <b>heybe</b> al. Heybeler çanta penceresinin üstündeki heybe yuvalarına takılır.\n\n" +
                H + "Eşya renkleri" + HE + "\n" +
                "Gri: değersiz  •  Beyaz: sıradan  •  Yeşil: sıradışı  •  Mavi: nadir  •  Mor: destansı  •  Turuncu: efsanevi.\n" +
                "Adının önünde \"Kusurlu\", \"Güçlü\", \"Üstün\", \"Eşsiz\" ya da \"Tanrısal\" yazan silahların gücü rastgeledir."),

            new Section("Görevler",
                "• Başının üstünde sarı <b>!</b> olan kişiler görev verir; sarı <b>?</b> olanlara biten görevi teslim edersin. Bunları haritada da görürsün.\n" +
                "• <b>Görevler</b> penceresi aldığın görevleri ve kalan hedefleri gösterir. Sağdaki görev takibi ilerlemeni anlık gösterir.\n" +
                "• Görev ödülleri: tecrübe, altın ve eşya. Bazı görevlerde ödülü sen seçersin.\n\n" +
                H + "Ana görevler" + HE + "\n" +
                "• <b>Olcayto Han'ın Çağrısı</b>: her şeyin başladığı yer.\n" +
                "• <b>Alp'in Donanımı</b> ve <b>Sefere Hazırlık</b>: Tüccar Karaçor'dan hançer ve kalkan satın al (birkaç gümüş tutar; paran yoksa önce birkaç Kara Yek avla).\n" +
                "• <b>Bilgeden Öğüt</b>: Bilge Tonyukuk'u dinle.\n" +
                "• <b>Karanlığın Kapısı</b> ve <b>Tepegöz'ün Laneti</b>: Erlik'in Mağarası'na gir, Albastıları ve Tepegöz'ü yen.\n" +
                "• <b>İlk Kan</b>, <b>Ayaz Ata'nın Nefesi</b>, <b>Umay'ın Şefkati</b>: savaş ve yetenek görevleri.\n" +
                "• <b>Obanın Zanaatları</b>: toplama zanaatlarını öğren.\n" +
                "• <b>Er Meydanı</b>: arenada rakip çağır ve dövüş.\n\n" +
                H + "Tekrarlanabilir görevler" + HE + "\n" +
                "<b>Obaya Yardım</b>, <b>Mağara Nöbeti</b>, <b>Keşif Kolu</b>, <b>Albastı Avı</b>, <b>Kamın Bohçası</b>, <b>Boyların Armağanı</b>: bitirdikten sonra yeniden alınabilir; sürekli para ve tecrübe kaynağıdır."),

            new Section("Seviye ve Yetenekler",
                "• Düşman öldürmek ve görev bitirmek <b>tecrübe</b> kazandırır. Tecrübe dolunca <b>seviye atlarsın</b>: canın ve gücün artar, yeni yeteneklerin açılır.\n" +
                "• Yeni öğrendiğin yetenekler çoğunlukla yetenek çubuğuna kendiliğinden eklenir.\n" +
                "• Köydeki <b>sınıf ustaları</b> (Alp Ustası, Okçu Ustası, Kam Ustası...) sınıfının yeteneklerini öğretir.\n" +
                "• İlerledikçe <b>uzmanlık ustalarından</b> bir yol seçebilirsin: Alp için Süvari ya da Kan Süvarisi, Kam için Ateş, Ayaz ya da Kara Kam, Okçu için Nişancı ya da İzci.\n" +
                "• Daha iyi silah ve zırh kuşanmak seni en hızlı güçlendiren yoldur. Eşyanın üstüne uzun bas, açıklamasında değerlerini gör.\n\n" +
                H + "Değerler" + HE + "\n" +
                "• <b>Güç</b>: yakın dövüş hasarı.  • <b>Çeviklik</b>: ok ve hançer hasarı, kritik vuruş.\n" +
                "• <b>Zekâ</b>: büyü gücü ve mana.  • <b>Dayanıklılık</b>: can.\n" +
                "Sol kenardaki <b>Karakter</b> düğmesiyle açılan pencerede tüm değerlerini ve giydiklerini görürsün."),

            new Section("Sınıflar ve Boylar",
                H + "Sınıflar" + HE + "\n" +
                "• <b>Alp</b>: ağır plaka zırhlı, iki elli baltalı ön saf savaşçısı. Dayanıklı ve güçlü; yeni başlayanlar için en kolayı.\n" +
                "• <b>Batur</b>: pençeleriyle art arda yumruk indiren hızlı yakın dövüşçü.\n" +
                "• <b>Akıncı</b>: gizlenir, arkadan vurur, zehirli hançer kullanır. Dikkatli oynamak ister.\n" +
                "• <b>Okçu</b>: uzaktan ok yağdırır, düşmanı yavaşlatır. Düşmanı yaklaştırmadan öldürmeyi sever.\n" +
                "• <b>Kam</b>: kasırga çağıran, ileride ateş ya da buz büyüleri öğrenen şaman. Güçlü ama kırılgan.\n" +
                "• <b>Otacı</b>: kendini ve dostlarını iyileştiren, zehri temizleyen, düşmüşleri dirilten şifacı.\n\n" +
                H + "Boylar" + HE + "\n" +
                "• <b>Bozoklar</b>: Gün Han, Ay Han ve Yıldız Han'ın soyu. Armağanları <b>Mavi Kanatlar</b>.\n" +
                "• <b>Üçoklar</b>: Gök Han, Dağ Han ve Deniz Han'ın soyu. Armağanları <b>Kızıl Kanatlar</b>.\n" +
                "• Kanatlarla kısa süre uçabilirsin. İki boyun arası soğuktur; öbür boyun askerleri sana düşman davranır.\n\n" +
                "Sınıfını ve boyunu yeni oyun başlatırken seçersin. Görünüşünü köydeki <b>Ayna Kam</b>'da, adını <b>Dede Korkut</b>'ta, boyunu elçilerde değiştirebilirsin."),

            new Section("Zanaatlar",
                H + "Toplama zanaatları" + HE + "\n" +
                "• <b>Madencilik</b> (Madenci Usta): köyün kuzeyindeki taşlıkta ve dağ yamaçlarında bakır, kalay, demir, gümüş, altın ve kristal damarları.\n" +
                "• <b>Otacılık</b> (Otacı Usta): köyde, çayırlarda ve kamp yakınlarında renkli çiçekler, keten ve kenevir.\n" +
                "• <b>Odunculuk</b> (Oduncu Usta): köyün batısındaki ağaçlar ve ormanlar.\n" +
                "• <b>Balıkçılık</b> (Balıkçı Usta): köyün batısındaki gölde.\n" +
                "Önce ustaya git, ona dokun ve zanaatı öğren. Sonra toplanacak şeye dokunduğunda kahramanın onu toplar. Toplanan yer bir süre sonra yeniden dolar.\n\n" +
                H + "Yapım zanaatları" + HE + "\n" +
                "<b>Demircilik</b>, <b>Terzilik</b>, <b>Dericilik</b>, <b>Simya</b>, <b>Aşçılık</b>, <b>Kuyumculuk</b>, <b>Yazıcılık</b>, <b>Marangozluk</b>.\n" +
                "Her biri köydeki ustasından öğrenilir ve köydeki tezgâhlarda (örs, kazan, tezgâh) yapılır. " +
                "Topladığın malzemelerden silah, zırh, iksir, yemek ve takı yaparsın. Yaptıklarını kullanabilir ya da satabilirsin.\n\n" +
                "İpucu: <b>Obanın Zanaatları</b> görevi seni toplama zanaatlarına yönlendirir."),

            new Section("Bölgeler",
                "Haritada yukarısı kuzeydir. Köy haritanın ortasındadır.\n\n" +
                "• <b>Köy</b> (orta): güvenli. Olcayto Han, ustalar, tüccarlar, banka (Hazinedar Toktamış) ve zanaat tezgâhları.\n" +
                "• <b>Güney çayırı</b> (başladığın yerin hemen güneyi): Kara Yek kampları. Çok kolay; ilk durağın.\n" +
                "• <b>Cin çayırları</b> (köyün güneydoğusu ve güneybatısı, biraz uzakta): Çalı Cini sürüleri. Kolay.\n" +
                "• <b>Ağulu Bataklık</b> (güneydoğuda, uzakta): Ağulu Körmösler ve Çalı Cinleri. Orta.\n" +
                "• <b>Yağmacı Obası</b> (güneyde, uzakça): Yağmacı Okçular ve Yağmacı Başı. Kolay-orta.\n" +
                "• <b>Doğu ve güneydoğu koruları</b>: Şulmuslar ve bir Kara Otacı. Orta.\n" +
                "• <b>Kuzeydoğu ormanı</b>: Yağmacılar ve Kara Yekler. Orta.\n" +
                "• <b>Güneybatı çayırı</b>: Kara Yekler. Kolay.\n" +
                "• <b>Eski kurganlar</b> (biri kuzeydoğuda, biri güneybatıda; ikisi de çok uzakta): Kemik Erler, Kemik Akıncı, Kemik Kam ve Kemik Alp. Orta-zor.\n" +
                "• <b>Ateş Dağı</b> (köyün kuzeyindeki lavlı dağ): Kara Kamlar, Abasılar, Kan Süvarileri, Körmösler ve Kızıl Cinler. Doruğunda <b>Ulu Evren</b>. Zor.\n" +
                "• <b>Buz Dağı</b> (batıdaki karlı dağ): Kırağı Cadıları, Buz Bekçileri, Buz Cinleri ve Ayaz Körmösleri. Doruğunda <b>Yelbegen</b>. Zor.\n" +
                "• <b>Erlik'in Mağarası</b> (köyün kuzey duvarının dışındaki taş kapı): Albastılar, Şulmuslar, Kara Kamlar, Körmösler, kemik erler ve en dipte <b>Tepegöz</b>. Zindan.\n" +
                "• <b>Er Meydanı</b> (köyün doğu ucu): Er Meydanı Ağası'ndan rakip çağırıp dövüşebilirsin.\n" +
                "• <b>Dört uzak köşe</b>: haritanın köşelerinde güçlü kamplar ve büyük hazine sandıkları.\n\n" +
                "Önerilen sıra: Güney çayırı → Yağmacı Obası ve korular → mağara → Ateş Dağı ve Buz Dağı → doruklar.\n\n" +
                "Ötüken Yaylası'ndan başka diyarlara <b>Geçit Taşı</b> ile gidilir: bir sonraki bölüme bak."),

            new Section("Geçit Taşları",
                H + "Kök Taş: diyarlar arası yol" + HE + "\n" +
                "Her haritada, girdiğin yerin hemen yanında Göktürk harfleriyle yazılmış, kaplumbağa kaideli bir dikili taş durur: " +
                "<b>Geçit Taşı</b>. Harfleri gök turkuazı ışıkla parlar. Ötüken Yaylası'nda köyün güney kapısına giden yolun doğusundadır.\n" +
                "• Taşa dokun: <b>Dünya Haritası</b> açılır. Gitmek istediğin diyara dokun, <b>Işınlan</b>'a bas.\n" +
                "• Her haritadan her haritaya gidebilirsin; eve dönmek için <b>Ötüken Yaylası</b>'nı seç.\n" +
                "• Taşa yürümeden de gidebilirsin: sol kenarda Harita'nın yanındaki <b>Işınlan</b> düğmesine dokun, haritayı seç, " +
                "<b>Işınlan</b>'a bas. Haritanın girişinde, Geçit Taşı'nın yanında belirirsin. Bulunduğun haritayı seçersen " +
                "girişine dönersin (bir yere sıkışırsan işe yarar). Savaşın ortasında ve ölüyken ışınlanamazsın.\n" +
                "• Ordubalık ve çevresindeki haritalar birbirine yollarla da bağlıdır: yolun sonunda üstünde yer adı yazan " +
                "geçitten yürüyerek komşu haritaya geçersin.\n\n" +
                H + "Haritalar (kolaydan zora)" + HE + "\n" +
                "1. <b>Ötüken Yaylası</b>: köy ve çevresi. Başlangıç.\n" +
                "2. <b>Umay Tarlaları</b>: ekinler, limon bahçeleri. Çalı Cinleri, yağmacılar.\n" +
                "3. <b>Börü Tepesi</b>: meşaleli patikalar, denize inen yamaç.\n" +
                "4. <b>Ak Deniz Kıyısı</b>: bambu korulukları, kumsal. Yağmacılar ve Şulmuslar.\n" +
                "5. <b>Ordubalık Çarşısı</b>: başkentin şenlikli çarşısı.\n" +
                "6. <b>Ordubalık Kenti</b>: değirmenler, taş köprüler.\n" +
                "7. <b>Ulukayın Ormanı</b>: şelalenin ardındaki gölgeli orman.\n" +
                "8. <b>Koncolos İni</b>: kemiklerle dolu mağara; dipte <b>Kara Koncolos</b>.\n" +
                "9. <b>Kağan Ordası</b>: kentin tepesi; Erlik'in en güçlü yandaşları.\n" +
                "10. <b>Kaf Dağı Yolu</b>: sarp kayalıklar, Kızıl Cinler.\n" +
                "11. <b>Ergenekon Mağarası</b>: atalarımızın demir dağı.\n" +
                "12. <b>Kurgan Mezarlığı</b>: gece; kemik erler ve <b>Kemik Kağan</b>.\n" +
                "13. <b>Ay Dede Koyu</b>: ay ışığında ölülerin indiği koy.\n" +
                "14. <b>Erlik'in Mağarası</b>: lavlı zindan; dipte <b>Tepegöz</b>.\n" +
                "15. <b>Tamu Zindanı</b>: Erlik Han'ın yeraltı zindanı; kapıda <b>Tamu Bekçisi</b>.\n\n" +
                "Düşmanlar senin seviyene göre güçlenir; zor haritalarda birkaç seviye üstüne çıkarlar. " +
                "Kampların ortasındaki sandıklar ve bosslar en iyi ganimeti verir."),

            new Section("Gereksinimler",
                H + "Cihaz" + HE + "\n" +
                "• Android 7.1 ya da üstü.\n" +
                "• En az 3 GB bellek (RAM); rahat oynamak için 4 GB ve üstü önerilir.\n" +
                "• Yaklaşık 1,5 GB boş depolama alanı.\n" +
                "• İnternet gerekmez.\n\n" +
                H + "Kayıt" + HE + "\n" +
                "• Oyun 3 dakikada bir ve uygulamayı arka plana attığında kendiliğinden kaydeder.\n" +
                "• Ana menüdeki <b>Çevrim Dışı</b> → <b>Oyna</b> ile kayıtlı karakterini seçip kaldığın yerden devam edersin.\n\n" +
                H + "İpuçları" + HE + "\n" +
                "• Telefon ısınırsa ya da oyun yavaşlarsa: <b>Ayarlar → Görüntü → Grafik Kalitesi</b>'ni <b>Düşük</b> yap.\n" +
                "• Ses ayarları <b>Ayarlar → Ses</b>'tedir.\n" +
                "• Kamera hızı <b>Ayarlar → Kontroller → Kamera Hızı</b>'ndan değişir.\n" +
                "• Bir hata görürsen üstteki <b>Durum</b> düğmesine bas, <b>Kopyala</b> ile raporu yapımcıya gönder.\n\n" +
                H + "Yapımcı" + HE + "\n" +
                "Oyun tasarımı ve yapımcılık: <b>Olcay Yasin Dünder</b>\n" +
                "Oyun motoru: AnyRPG (açık kaynak, MIT lisansı)"),
        };
    }
}
