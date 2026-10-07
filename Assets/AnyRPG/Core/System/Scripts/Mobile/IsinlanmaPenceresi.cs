using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// "Işınlan" penceresi: oyunun 15 haritası kolaydan zora dizilir; oyuncu birini seçip Işınlan'a dokununca
    /// o haritanın giriş noktasına (Geçit Taşı'nın yanı, DefaultSpawnLocation) geçer.
    /// Geçit Taşı'nın (LoadSceneComponent) yaptığının aynısını yapar: doğma isteği + sahne yükleme.
    /// Bulunduğun harita seçilirse oyuncu o haritanın giriş noktasına geri döner (takılıp kalınca işe yarar).
    /// Savaşta ve ölüyken ışınlanılamaz. Sol sütundaki "Işınlan" düğmesiyle açılır; kodla kurulur.
    /// </summary>
    public class IsinlanmaPenceresi : MonoBehaviour {

        public const string CanvasName = "IsinlanmaCanvas";
        // Seçenekler penceresiyle aynı katman: HUD'un ve oyun pencerelerinin üstünde, Durum katmanının (1000) altında
        private const int SortingOrder = 31;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color cardColor = new Color(0.16f, 0.12f, 0.08f, 0.95f);
        private static readonly Color cardSelectedColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color buttonColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color teleportColor = new Color(0.12f, 0.45f, 0.5f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color hintColor = new Color(0.75f, 0.7f, 0.62f, 1f);
        private static readonly Color errorColor = new Color(1f, 0.45f, 0.35f, 1f);

        private struct Harita {
            public string sahne;
            public string ad;
            public string aciklama;

            public Harita(string sahne, string ad, string aciklama) {
                this.sahne = sahne;
                this.ad = ad;
                this.aciklama = aciklama;
            }
        }

        // Geçit Taşı'ndaki sıra (Tools~/dunya/haritalar.py YOLCULUK_SIRASI): kolaydan zora
        private static readonly Harita[] haritalar = {
            new Harita("FeaturesDemoZone", "Ötüken Yaylası", "Köy ve çevresi. Başlangıç."),
            new Harita("UmayTarlalari", "Umay Tarlaları", "Ekinler, limon bahçeleri. Çalı Cinleri, yağmacılar."),
            new Harita("BoruTepesi", "Börü Tepesi", "Meşaleli patikalar, denize inen yamaç."),
            new Harita("AkDenizKiyisi", "Ak Deniz Kıyısı", "Bambu korulukları, kumsal. Yağmacılar, Şulmuslar."),
            new Harita("OrdubalikCarsisi", "Ordubalık Çarşısı", "Başkentin şenlikli çarşısı."),
            new Harita("OrdubalikKenti", "Ordubalık Kenti", "Değirmenler, taş köprüler, dar sokaklar."),
            new Harita("UlukayinOrmani", "Ulukayın Ormanı", "Şelalenin ardındaki gölgeli orman."),
            new Harita("KoncolosIni", "Koncolos İni", "Kemiklerle dolu mağara; dipte Kara Koncolos."),
            new Harita("KaganOrdasi", "Kağan Ordası", "Kentin tepesi; Erlik'in en güçlü yandaşları."),
            new Harita("KafDagiYolu", "Kaf Dağı Yolu", "Sarp kayalıklar, Kızıl Cinler."),
            new Harita("ErgenekonMagarasi", "Ergenekon Mağarası", "Atalarımızın eritip çıktığı demir dağ."),
            new Harita("KurganMezarligi", "Kurgan Mezarlığı", "Gece; kemik erler ve Kemik Kağan."),
            new Harita("AyDedeKoyu", "Ay Dede Koyu", "Ay ışığında ölülerin kıyıya indiği koy."),
            new Harita("FeaturesDemoDungeon", "Erlik'in Mağarası", "Lavlı zindan; dipte Tepegöz."),
            new Harita("TamuZindani", "Tamu Zindanı", "Erlik Han'ın yeraltı zindanı; Tamu Bekçisi."),
        };

        private static IsinlanmaPenceresi instance = null;

        private Font font = null;
        private GameObject panelRoot = null;
        private SystemGameManager systemGameManager = null;
        private readonly List<Image> cardImages = new List<Image>();
        private readonly List<Text> cardTags = new List<Text>();
        private readonly List<Text> cardLevels = new List<Text>();
        private readonly List<Outline> cardOutlines = new List<Outline>();
        private Text statusText = null;
        private Button teleportButton = null;
        private Text teleportButtonText = null;
        private int selected = -1;
        private int current = -1;
        private float busyUntil = 0f;

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
            instance = canvasObject.AddComponent<IsinlanmaPenceresi>();
            instance.Build();
        }

        private SystemGameManager GameManager {
            get {
                if (systemGameManager == null) {
                    systemGameManager = FindAnyObjectByType<SystemGameManager>();
                }
                return systemGameManager;
            }
        }

        // ---------------------------------------------------------------- pencere

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panelRoot = CreateRect(transform, "Isinlanma", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimmer = panelRoot.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.7f);

            GameObject panel = CreateRect(panelRoot.transform, "Panel", new Vector2(0.03f, 0.04f), new Vector2(0.97f, 0.96f), Vector2.zero, Vector2.zero);
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = panelColor;
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = gold;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            GameObject title = CreateRect(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -58f), new Vector2(-20f, -6f));
            Text titleText = CreateText(title, "IŞINLAN", 34, TextAnchor.MiddleCenter, gold);
            titleText.fontStyle = FontStyle.Bold;
            GameObject subtitle = CreateRect(panel.transform, "Aciklama", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -86f), new Vector2(-20f, -56f));
            CreateText(subtitle, "Gitmek istediğin diyarı seç, sonra Işınlan'a dokun. Haritalar kolaydan zora dizilidir.", 18, TextAnchor.MiddleCenter, hintColor);

            // 15 harita: 3 sütun, 5 satır
            GameObject grid = CreateRect(panel.transform, "Haritalar", Vector2.zero, Vector2.one, new Vector2(16f, 96f), new Vector2(-16f, -92f));
            const int columns = 3;
            int rows = Mathf.CeilToInt(haritalar.Length / (float)columns);
            for (int i = 0; i < haritalar.Length; i++) {
                int column = i % columns;
                int row = i / columns;
                Vector2 anchorMin = new Vector2(column / (float)columns, 1f - (row + 1) / (float)rows);
                Vector2 anchorMax = new Vector2((column + 1) / (float)columns, 1f - row / (float)rows);
                CreateCard(grid.transform, i, anchorMin, anchorMax);
            }

            // alt satır: durum yazısı, Işınlan ve Kapat
            GameObject status = CreateRect(panel.transform, "Durum", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 14f), new Vector2(-520f, 82f));
            statusText = CreateText(status, string.Empty, 20, TextAnchor.MiddleLeft, textColor);
            statusText.resizeTextForBestFit = true;
            statusText.resizeTextMinSize = 14;
            statusText.resizeTextMaxSize = 20;

            GameObject teleport = CreateButton(panel.transform, "Işınlan", new Vector2(1f, 0f), new Vector2(-370f, 48f), new Vector2(280f, 66f), 28,
                teleportColor, () => { MobileFeedback.Tap(); Teleport(); });
            teleportButton = teleport.GetComponent<Button>();
            teleportButtonText = teleport.GetComponentInChildren<Text>();
            teleportButtonText.fontStyle = FontStyle.Bold;
            CreateButton(panel.transform, "Kapat", new Vector2(1f, 0f), new Vector2(-130f, 48f), new Vector2(180f, 66f), 26,
                buttonColor, () => { MobileFeedback.Tap(); Close(); });

            panelRoot.SetActive(false);
        }

        private void CreateCard(Transform parent, int index, Vector2 anchorMin, Vector2 anchorMax) {
            Harita harita = haritalar[index];
            GameObject card = CreateRect(parent, harita.sahne, anchorMin, anchorMax, new Vector2(5f, 5f), new Vector2(-5f, -5f));
            Image image = card.AddComponent<Image>();
            image.color = cardColor;
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(gold.r, gold.g, gold.b, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);
            Button button = card.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => { MobileFeedback.Tap(); Select(index); });

            // solda zorluk rengi şeridi
            GameObject stripe = CreateRect(card.transform, "Zorluk", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(7f, 0f));
            Image stripeImage = stripe.AddComponent<Image>();
            stripeImage.color = DifficultyColor(index);
            stripeImage.raycastTarget = false;

            GameObject nameObject = CreateRect(card.transform, "Ad", new Vector2(0f, 0.56f), new Vector2(1f, 1f), new Vector2(18f, 0f), new Vector2(-110f, -4f));
            Text nameText = CreateText(nameObject, (index + 1) + ". " + harita.ad, 24, TextAnchor.LowerLeft, textColor);
            nameText.fontStyle = FontStyle.Bold;
            nameText.resizeTextForBestFit = true;
            nameText.resizeTextMinSize = 16;
            nameText.resizeTextMaxSize = 24;

            GameObject tagObject = CreateRect(card.transform, "Etiket", new Vector2(1f, 0.56f), new Vector2(1f, 1f), new Vector2(-108f, 0f), new Vector2(-10f, -4f));
            Text tagText = CreateText(tagObject, string.Empty, 16, TextAnchor.LowerRight, DifficultyColor(index));
            tagText.fontStyle = FontStyle.Bold;
            // tek satırda kalsın, gerekirse sola taşsın
            tagText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // adın altında haritanın seviyesi (HaritaSeviyeleri), oyuncunun seviyesine göre renkli
            GameObject levelObject = CreateRect(card.transform, "Seviye", new Vector2(0f, 0.32f), new Vector2(1f, 0.56f), new Vector2(18f, 0f), new Vector2(-10f, 0f));
            Text levelText = CreateText(levelObject, HaritaSeviyeleri.Yazi(harita.sahne), 18, TextAnchor.MiddleLeft, gold);
            levelText.fontStyle = FontStyle.Bold;
            levelText.horizontalOverflow = HorizontalWrapMode.Overflow;

            GameObject descriptionObject = CreateRect(card.transform, "Aciklama", new Vector2(0f, 0f), new Vector2(1f, 0.32f), new Vector2(18f, 3f), new Vector2(-10f, 0f));
            Text descriptionText = CreateText(descriptionObject, harita.aciklama, 15, TextAnchor.UpperLeft, hintColor);
            descriptionText.resizeTextForBestFit = true;
            descriptionText.resizeTextMinSize = 11;
            descriptionText.resizeTextMaxSize = 15;

            cardImages.Add(image);
            cardTags.Add(tagText);
            cardLevels.Add(levelText);
            cardOutlines.Add(outline);
        }

        private static string DifficultyName(int index) {
            if (index <= 3) {
                return "Kolay";
            }
            if (index <= 8) {
                return "Orta";
            }
            if (index <= 12) {
                return "Zor";
            }
            return "Çok zor";
        }

        private static Color DifficultyColor(int index) {
            if (index <= 3) {
                return new Color(0.5f, 0.85f, 0.42f, 1f);
            }
            if (index <= 8) {
                return new Color(0.98f, 0.82f, 0.32f, 1f);
            }
            if (index <= 12) {
                return new Color(0.98f, 0.55f, 0.22f, 1f);
            }
            return new Color(0.95f, 0.3f, 0.25f, 1f);
        }

        private void Open() {
            string activeScene = SceneManager.GetActiveScene().name;
            current = -1;
            for (int i = 0; i < haritalar.Length; i++) {
                if (haritalar[i].sahne == activeScene) {
                    current = i;
                }
            }
            selected = -1;
            panelRoot.SetActive(true);
            panelRoot.transform.SetAsLastSibling();
            Refresh();
            SetStatus(current >= 0 ? "Şu an <b>" + haritalar[current].ad + "</b> haritasındasın. Gitmek istediğin diyarı seç." : "Gitmek istediğin diyarı seç.", textColor);
        }

        private void Close() {
            panelRoot.SetActive(false);
        }

        private void Select(int index) {
            selected = index;
            Refresh();
            if (index == current) {
                SetStatus("Zaten buradasın. Işınlan'a dokunursan haritanın giriş noktasına, Geçit Taşı'nın yanına dönersin.", textColor);
            } else {
                SetStatus("<b>" + haritalar[index].ad + "</b> seçildi. Girişte, Geçit Taşı'nın yanında belirirsin.", textColor);
            }
        }

        private int OyuncuSeviyesi {
            get {
                UnitController oyuncu = GameManager != null && GameManager.PlayerManagerClient != null ? GameManager.PlayerManagerClient.UnitController : null;
                return oyuncu != null && oyuncu.CharacterStats != null ? oyuncu.CharacterStats.Level : 0;
            }
        }

        private void Refresh() {
            int oyuncuSeviyesi = OyuncuSeviyesi;
            for (int i = 0; i < cardImages.Count; i++) {
                cardImages[i].color = i == selected ? cardSelectedColor : cardColor;
                bool here = i == current;
                Color levelColor;
                string yorum = HaritaSeviyeleri.Yorum(haritalar[i].sahne, oyuncuSeviyesi, out levelColor);
                int gereken = GerekenSeviye(haritalar[i].sahne);
                bool kilitli = oyuncuSeviyesi > 0 && oyuncuSeviyesi < gereken;
                cardLevels[i].text = kilitli ? "Kilitli · " + gereken + ". seviyede açılır"
                    : HaritaSeviyeleri.Yazi(haritalar[i].sahne) + (yorum.Length > 0 ? "  ·  " + yorum : string.Empty);
                cardLevels[i].color = kilitli ? new Color(0.62f, 0.58f, 0.52f, 1f) : levelColor;
                cardTags[i].text = here ? "Buradasın" : DifficultyName(i);
                cardTags[i].color = here ? gold : DifficultyColor(i);
                cardOutlines[i].effectColor = here || i == selected ? gold : new Color(gold.r, gold.g, gold.b, 0.45f);
                cardOutlines[i].effectDistance = here || i == selected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
            bool canTeleport = selected >= 0 && (oyuncuSeviyesi <= 0 || oyuncuSeviyesi >= GerekenSeviye(haritalar[selected].sahne));
            teleportButton.interactable = canTeleport;
            teleportButton.GetComponent<Image>().color = canTeleport ? teleportColor : new Color(teleportColor.r, teleportColor.g, teleportColor.b, 0.35f);
            teleportButtonText.text = selected >= 0 && selected == current ? "Girişe Dön" : "Işınlan";
            teleportButtonText.color = canTeleport ? textColor : new Color(textColor.r, textColor.g, textColor.b, 0.45f);
        }

        private void SetStatus(string message, Color color) {
            statusText.text = message;
            statusText.color = color;
        }

        // ---------------------------------------------------------------- ışınlanma

        private void Teleport() {
            if (selected < 0 || selected >= haritalar.Length || Time.unscaledTime < busyUntil) {
                return;
            }
            string error = Teleport(haritalar[selected].sahne);
            if (error != null) {
                SetStatus(error, errorColor);
                return;
            }
            busyUntil = Time.unscaledTime + 3f;
            Close();
        }

        /// <summary>haritaların sahne adları, kolaydan zora (otomatik oyun testi de bu sırayla gezer)</summary>
        public static string[] SahneAdlari {
            get {
                string[] adlar = new string[haritalar.Length];
                for (int i = 0; i < haritalar.Length; i++) {
                    adlar[i] = haritalar[i].sahne;
                }
                return adlar;
            }
        }

        /// <summary>haritanın oyunda görünen adı</summary>
        public static string GorunenAd(string sahne) {
            foreach (Harita h in haritalar) {
                if (h.sahne == sahne) {
                    return h.ad;
                }
            }
            return sahne;
        }

        /// <summary>haritanın kısa tanıtımı (dünya haritasındaki bilgi şeridi)</summary>
        public static string Aciklama(string sahne) {
            foreach (Harita h in haritalar) {
                if (h.sahne == sahne) {
                    return h.aciklama;
                }
            }
            return string.Empty;
        }

        /// <summary>haritanın zorluğu: Kolay, Orta, Zor, Çok zor (yolculuk sırasına göre)</summary>
        public static string ZorlukAdi(string sahne) {
            int i = System.Array.IndexOf(SahneAdlari, sahne);
            return i >= 0 ? DifficultyName(i) : string.Empty;
        }

        public static Color ZorlukRengi(string sahne) {
            int i = System.Array.IndexOf(SahneAdlari, sahne);
            return i >= 0 ? DifficultyColor(i) : Color.white;
        }

        /// <summary>
        /// oyuncuyu haritanın giriş noktasına ışınlar (Işınlan penceresi ve otomatik oyun testi);
        /// olmazsa nedenini döndürür, olursa null
        /// </summary>
        public static string Teleport(string sceneName) {
            return Teleport(sceneName, false);
        }

        // ---------------------------------------------------------------- seviyeye göre ışınlanma (Ötüken)

        /// <summary>haritaya ışınlanmak için gereken seviye: haritanın en düşük seviyesinin bir altı</summary>
        public static int GerekenSeviye(string sahne) {
            int en, ust;
            if (HaritaSeviyeleri.Aralik(sahne, out en, out ust) == false) {
                return 1;
            }
            return Mathf.Max(1, en - 1);
        }

        /// <summary>ışınlanmaya engel (yoksa null): ölü, savaşta, seviye yetersiz, bilinmeyen harita</summary>
        public static string Engel(UnitController oyuncu, string sahne, bool force) {
            if (oyuncu == null) {
                return "Işınlanmak için önce oyuna girmelisin.";
            }
            if (System.Array.IndexOf(SahneAdlari, sahne) < 0) {
                return "Bu diyar bilinmiyor.";
            }
            if (oyuncu.CharacterStats != null && oyuncu.CharacterStats.IsAlive == false) {
                return "Ölüyken ışınlanamazsın. Önce yeniden doğ.";
            }
            if (force == false && oyuncu.CharacterCombat != null && oyuncu.CharacterCombat.GetInCombat()) {
                return "Savaşın ortasında ışınlanamazsın. Düşmanlardan uzaklaş ya da savaşı bitir.";
            }
            int gereken = GerekenSeviye(sahne);
            if (force == false && oyuncu.CharacterStats != null && oyuncu.CharacterStats.Level < gereken) {
                return GorunenAd(sahne) + " için en az " + gereken + ". seviye olmalısın.";
            }
            return null;
        }

        /// <summary>sunucu: çevrimiçi ışınlanma isteği ("isinlan" + sahne adı); her yerden, seviyeye göre</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("isinlan", (oyuncu, sahne) => {
                string hata = Engel(oyuncu, sahne, false);
                SystemGameManager o = OtukenAg.Oyun;
                if (hata == null && (o == null || o.PlayerManagerServer == null)) {
                    hata = "Sunucu hazır değil.";
                }
                if (hata != null) {
                    OtukenAg.Yanitla(oyuncu, "isinlan-sonuc", hata);
                    return;
                }
                SunucudaIsinla(o, oyuncu, sahne);
                OtukenAg.Yanitla(oyuncu, "isinlan-sonuc", string.Empty);
            });
            OtukenAg.IstemciDinle("isinlan-sonuc", hata => {
                SonCevrimiciSonuc = hata;
                if (string.IsNullOrEmpty(hata) == false) {
                    SystemGameManager o = OtukenAg.Oyun;
                    UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
                    OtukenAg.Mesaj(oyuncu, hata);
                }
            });
        }

        public static string SonCevrimiciSonuc { get; private set; } = "-";

        /// <summary>sunucu: oyuncuyu bir diyarın girişine ışınlar (aynı diyardaysa girişe götürür). Yönetim paneli de kullanır.</summary>
        public static void SunucudaIsinla(SystemGameManager o, UnitController oyuncu, string sahne) {
            if (oyuncu.gameObject.scene.name == sahne) {
                TeleportEffectProperties ayni = new TeleportEffectProperties();
                ayni.levelName = sahne;
                o.PlayerManagerServer.Teleport(oyuncu, ayni);
                return;
            }
            Debug.Log("[Sunucu] " + oyuncu.DisplayName + " ışınlanıyor: " + sahne);
            o.PlayerManagerServer.AddSpawnRequest(oyuncu, new SpawnPlayerRequest());
            oyuncu.StartCoroutine(SunucudaYukle(o.PlayerManagerServer, sahne, oyuncu));
        }

        private static IEnumerator SunucudaYukle(PlayerManagerServer sunucu, string sahne, UnitController oyuncu) {
            // ekransız sunucuda WaitForEndOfFrame gelmez: bir kare bekle
            yield return null;
            if (oyuncu != null) {
                sunucu.LoadScene(sahne, oyuncu);
            }
        }

        /// <param name="force">otomatik oyun testi için: savaşta da ışınla</param>
        public static string Teleport(string sceneName, bool force) {
            if (Cevrimici.Acik) {
                SystemGameManager o = OtukenAg.Oyun;
                UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
                string engel = Engel(oyuncu, sceneName, force);
                if (engel != null) {
                    return engel;
                }
                return OtukenAg.Gonder("isinlan", sceneName) ? null : "Sunucuya ulaşılamadı, biraz sonra yeniden dene.";
            }
            Ensure();
            SystemGameManager gameManager = instance.GameManager;
            PlayerManagerClient playerManagerClient = gameManager != null ? gameManager.PlayerManagerClient : null;
            PlayerManagerServer playerManagerServer = gameManager != null ? gameManager.PlayerManagerServer : null;
            UnitController player = playerManagerClient != null && playerManagerClient.PlayerUnitSpawned ? playerManagerClient.UnitController : null;
            if (player == null || playerManagerServer == null || SystemGameManager.IsShuttingDown) {
                return "Işınlanmak için önce oyuna girmelisin.";
            }
            string yerelEngel = Engel(player, sceneName, force);
            if (yerelEngel != null) {
                return yerelEngel;
            }
            try {
                if (player.gameObject.scene.name == sceneName) {
                    // aynı harita: oyuncu haritanın giriş noktasında yeniden belirir
                    TeleportEffectProperties teleportProperties = new TeleportEffectProperties();
                    teleportProperties.levelName = sceneName;
                    playerManagerServer.Teleport(player, teleportProperties);
                    return null;
                }
                // Geçit Taşı ile aynı yol (LoadSceneComponent): varsayılan giriş noktasında doğma isteği, sonra sahne yükleme
                playerManagerServer.AddSpawnRequest(player, new SpawnPlayerRequest());
                instance.StartCoroutine(instance.LoadSceneNextFrame(playerManagerServer, sceneName, player));
                return null;
            } catch (System.Exception exception) {
                Debug.LogWarning($"IsinlanmaPenceresi.Teleport({sceneName}): {exception.Message}");
                return "Işınlanılamadı: " + exception.Message;
            }
        }

        private IEnumerator LoadSceneNextFrame(PlayerManagerServer playerManagerServer, string sceneName, UnitController player) {
            // bir kare bekle (WaitForEndOfFrame ekransız çalışan otomatik testte hiç gelmiyor)
            yield return null;
            if (player != null) {
                playerManagerServer.LoadScene(sceneName, player);
            }
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
