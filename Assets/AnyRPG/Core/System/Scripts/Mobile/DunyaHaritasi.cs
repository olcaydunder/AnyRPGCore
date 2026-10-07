using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Dünya Haritası: oyunun 15 diyarı tek bir haritada. Her diyar, kendi kuşbakışı görüntüsünü taşıyan yuvarlak bir
    /// madalyon; aralarında yürüyerek geçilen kapı yolları (dolu çizgi) ve yolculuk sırası (kesik çizgi) var.
    ///  - İki parmakla kıstırarak (bilgisayarda fare tekerleği, ya da +/− düğmeleri) yakınlaşılır, sürükleyerek gezilir.
    ///  - Yakınlaşınca diyarın içindeki Geçit Taşı (mavi), komşu diyarlara kapılar (altın) ve bulunduğun diyarda sen
    ///    (yön oku) görünür.
    ///  - Bir diyara dokununca alttaki şeritte adı, zorluğu, tanıtımı ve "Işınlan" düğmesi çıkar.
    /// Sol sütundaki Harita ve Işınlan düğmeleri ve haritalardaki Geçit Taşları bu haritayı açar (InteractionManagerClient).
    /// Görüntüler ve işaretlerin yeri derlemede üretilir (Otuken.EditorAraclari.HaritaHazirlik.DunyaHaritasiCek):
    /// Resources/DunyaHaritasi/{sahne}.jpg ve {sahne}_veri.json. Görüntüsü olmayan diyar düz renkle çizilir.
    /// </summary>
    public class DunyaHaritasi : MonoBehaviour {

        public const string CanvasName = "DunyaHaritasiCanvas";
        public const string KaynakKlasoru = "DunyaHaritasi";
        // Işınlan ve Seçenekler pencereleriyle aynı katman
        private const int SortingOrder = 31;
        private static readonly Vector2 DunyaBoyutu = new Vector2(2400f, 1500f);
        private const float BolgeCapi = 300f;
        private const float EnCokYakinlik = 4f;
        private const float AyrintiYakinligi = 1.5f;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.07f, 0.055f, 0.04f, 0.98f);
        private static readonly Color parsomen = new Color(0.6f, 0.5f, 0.35f, 1f);
        private static readonly Color buttonColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color teleportColor = new Color(0.12f, 0.45f, 0.5f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color hintColor = new Color(0.75f, 0.7f, 0.62f, 1f);
        private static readonly Color errorColor = new Color(1f, 0.45f, 0.35f, 1f);
        private static readonly Color tasRengi = new Color(0.3f, 0.95f, 1f, 1f);
        private static readonly Color kapiRengi = new Color(1f, 0.8f, 0.3f, 1f);
        private static readonly Color oyuncuRengi = new Color(0.35f, 0.7f, 1f, 1f);
        private static readonly Color halkaRengi = new Color(0.36f, 0.27f, 0.15f, 1f);

        // diyarların dünya üzerindeki yeri (sol alt köşeden). Kapı komşuları yan yana, yolculuk soldan sağa zorlaşır.
        private static readonly Dictionary<string, Vector2> yerler = new Dictionary<string, Vector2>() {
            { "FeaturesDemoZone", new Vector2(330f, 780f) },
            { "UmayTarlalari", new Vector2(740f, 680f) },
            { "BoruTepesi", new Vector2(600f, 280f) },
            { "AkDenizKiyisi", new Vector2(1030f, 250f) },
            { "AyDedeKoyu", new Vector2(1420f, 230f) },
            { "OrdubalikCarsisi", new Vector2(1080f, 700f) },
            { "OrdubalikKenti", new Vector2(1060f, 1130f) },
            { "KaganOrdasi", new Vector2(1440f, 1230f) },
            { "UlukayinOrmani", new Vector2(680f, 1160f) },
            { "KoncolosIni", new Vector2(300f, 1260f) },
            { "KafDagiYolu", new Vector2(1460f, 760f) },
            { "ErgenekonMagarasi", new Vector2(1820f, 990f) },
            { "KurganMezarligi", new Vector2(1830f, 450f) },
            { "FeaturesDemoDungeon", new Vector2(2180f, 640f) },
            { "TamuZindani", new Vector2(2140f, 1240f) },
        };

        // kapıdan yürüyerek geçilen komşular (Chop Chop çıkışları)
        private static readonly string[][] kapiYollari = {
            new[] { "UmayTarlalari", "OrdubalikCarsisi" }, new[] { "UmayTarlalari", "BoruTepesi" },
            new[] { "BoruTepesi", "AkDenizKiyisi" }, new[] { "OrdubalikCarsisi", "OrdubalikKenti" },
            new[] { "OrdubalikCarsisi", "KafDagiYolu" }, new[] { "OrdubalikKenti", "UlukayinOrmani" },
            new[] { "OrdubalikKenti", "KaganOrdasi" }, new[] { "KafDagiYolu", "ErgenekonMagarasi" },
        };

        // yalnız Geçit Taşı ile gidilen diyarların yolculuk sırası
        private static readonly string[][] yolculukCizgileri = {
            new[] { "FeaturesDemoZone", "UmayTarlalari" }, new[] { "UlukayinOrmani", "KoncolosIni" },
            new[] { "AkDenizKiyisi", "AyDedeKoyu" }, new[] { "ErgenekonMagarasi", "KurganMezarligi" },
            new[] { "KurganMezarligi", "FeaturesDemoDungeon" }, new[] { "FeaturesDemoDungeon", "TamuZindani" },
            new[] { "KaganOrdasi", "TamuZindani" },
        };

        [Serializable]
        private class KapiVerisi {
            public string hedef = string.Empty;
            public float u;
            public float v;
        }

        /// <summary>derlemede üretilen işaret verisi: kuşbakışı kameranın yeri ve görüntüdeki (0-1) konumlar</summary>
        [Serializable]
        private class BolgeVerisi {
            public string sahne = string.Empty;
            public float[] kamera = new float[0];
            public float fov = 30f;
            public float[] tas = new float[0];
            public float[] giris = new float[0];
            public List<KapiVerisi> kapilar = new List<KapiVerisi>();
        }

        private class Bolge {
            public string sahne;
            public RectTransform kok;
            public Image halka;
            public Image maske;
            public RectTransform etiket;
            public RectTransform buradasin;
            public RectTransform oyuncu;
            public BolgeVerisi veri;
            public bool resimli;
            public readonly List<GameObject> ayrintilar = new List<GameObject>();
            public readonly List<RectTransform> isaretler = new List<RectTransform>();
        }

        private static DunyaHaritasi instance = null;

        private Font font = null;
        private GameObject panelRoot = null;
        private RectTransform viewport = null;
        private RectTransform content = null;
        private Transform bolgeKatmani = null;
        private Transform etiketKatmani = null;
        private ScrollRect kaydirma = null;
        private Text altBaslik = null;
        private Text bilgiAdi = null;
        private Text bilgiZorluk = null;
        private Text bilgiAciklama = null;
        private Text durumYazisi = null;
        private Button isinlanDugmesi = null;
        private Text isinlanYazisi = null;
        private readonly List<Bolge> bolgeler = new List<Bolge>();
        private readonly Dictionary<string, Bolge> bolgeSozlugu = new Dictionary<string, Bolge>();

        private float yakinlik = 1f;
        private string mevcut = string.Empty;
        private string secili = string.Empty;
        private bool kistirma = false;
        private float oncekiMesafe = 1f;
        private bool animasyon = false;
        private float hedefYakinlik = 1f;
        private Vector2 hedefKonum = Vector2.zero;
        private float mesgulKadar = 0f;
        private SystemGameManager systemGameManager = null;

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        /// <summary>HUD'daki Harita ve Işınlan düğmeleri</summary>
        public static void Goster() {
            Goster(false);
        }

        /// <param name="gecitTasindan">Geçit Taşı'na dokunularak açıldı</param>
        public static void Goster(bool gecitTasindan) {
            Goster(gecitTasindan, null);
        }

        /// <summary>
        /// çevrimiçi oyunda ışınlanma Geçit Taşı'nın kapı seçeneğiyle (LoadSceneComponent) sunucuya istenir: dokunulan taş saklanır
        /// </summary>
        private static InteractableBase sonGecitTasi = null;

        public static void Goster(bool gecitTasindan, InteractableBase gecitTasi) {
            Ensure();
            sonGecitTasi = gecitTasindan ? gecitTasi : null;
            instance.Ac(SceneManager.GetActiveScene().name, gecitTasindan);
        }

        /// <summary>çevrimiçi: dokunulan Geçit Taşı'nın hedef diyara giden kapı seçeneğini sunucuya ister; olmazsa nedeni</summary>
        private string CevrimiciIsinla(string sahne) {
            SystemGameManager oyun = OyunYoneticisi;
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu == null) {
                return "Işınlanmak için önce oyuna girmelisin.";
            }
            if (sonGecitTasi == null) {
                // Ötüken: her yerden ışınlanma (seviyeye göre; sunucu denetler)
                return IsinlanmaPenceresi.Teleport(sahne);
            }
            string engel = IsinlanmaPenceresi.Engel(oyuncu, sahne, false);
            if (engel != null) {
                return engel;
            }
            if (oyuncu.CharacterCombat != null && oyuncu.CharacterCombat.GetInCombat()) {
                return "Savaşın ortasında ışınlanamazsın. Düşmanlardan uzaklaş ya da savaşı bitir.";
            }
            foreach (KeyValuePair<int, InteractableOptionComponent> secenek in sonGecitTasi.Interactables) {
                LoadSceneComponent kapi = secenek.Value as LoadSceneComponent;
                if (kapi != null && kapi.LoadSceneProps != null && kapi.LoadSceneProps.SceneName == sahne) {
                    oyun.InteractionManagerClient.InteractWithOption(oyuncu, sonGecitTasi, kapi, secenek.Key, 0);
                    return null;
                }
            }
            return sahne == mevcut ? "Zaten buradasın." : "Bu Geçit Taşı oraya kapı açmıyor.";
        }

        /// <summary>etkileşilen nesne bir Geçit Taşı mı (InteractionManagerClient seçenek listesi yerine bu haritayı açar)</summary>
        public static bool GecitTasiMi(InteractableBase etkilesim) {
            return etkilesim != null && (etkilesim.gameObject.name == "GecitTasi" || etkilesim.DisplayName == "Geçit Taşı");
        }

        /// <summary>oyun testi: haritayı açar, diyarı seçer ve Işınlan'a basar; olmazsa nedeni</summary>
        public static string TestIcinIsinla(string sahne) {
            Goster(false);
            instance.Sec(sahne);
            return instance.Isinla();
        }

        /// <summary>oyun testi: kuşbakışı görüntüsü bulunan diyar sayısı</summary>
        public static int ResimliBolgeSayisi {
            get {
                if (instance == null) {
                    return 0;
                }
                int n = 0;
                foreach (Bolge b in instance.bolgeler) {
                    if (b.resimli) {
                        n++;
                    }
                }
                return n;
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
            instance = canvasObject.AddComponent<DunyaHaritasi>();
            instance.Build();
        }

        // ---------------------------------------------------------------- kurulum

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panelRoot = CreateRect(transform, "DunyaHaritasi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelRoot.AddComponent<Image>().color = panelColor;

            // başlık şeridi
            GameObject baslik = CreateRect(panelRoot.transform, "Baslik", new Vector2(0f, 1f), new Vector2(0.4f, 1f), new Vector2(24f, -64f), new Vector2(0f, -6f));
            Text baslikYazisi = CreateText(baslik, "DÜNYA HARİTASI", 30, TextAnchor.MiddleLeft, gold);
            baslikYazisi.fontStyle = FontStyle.Bold;
            GameObject alt = CreateRect(panelRoot.transform, "Aciklama", new Vector2(0.24f, 1f), new Vector2(1f, 1f), new Vector2(0f, -64f), new Vector2(-470f, -6f));
            altBaslik = CreateText(alt, string.Empty, 17, TextAnchor.MiddleLeft, hintColor);
            CreateButton(panelRoot.transform, "Bölge Haritası", new Vector2(1f, 1f), new Vector2(-370f, -34f), new Vector2(190f, 48f), 19, buttonColor, () => {
                MobileFeedback.Tap();
                Kapat();
                MobileInput.PressVirtualKey("MAINMAP");
            });
            CreateButton(panelRoot.transform, "Liste", new Vector2(1f, 1f), new Vector2(-200f, -34f), new Vector2(130f, 48f), 19, buttonColor, () => {
                MobileFeedback.Tap();
                Kapat();
                IsinlanmaPenceresi.Show();
            });
            CreateButton(panelRoot.transform, "X", new Vector2(1f, 1f), new Vector2(-70f, -34f), new Vector2(84f, 48f), 24, buttonColor, () => {
                MobileFeedback.Tap();
                Kapat();
            });

            // harita görünümü
            GameObject vp = CreateRect(panelRoot.transform, "Gorunum", Vector2.zero, Vector2.one, new Vector2(12f, 116f), new Vector2(-12f, -68f));
            viewport = vp.GetComponent<RectTransform>();
            Image vpResim = vp.AddComponent<Image>();
            vpResim.color = new Color(0.16f, 0.26f, 0.32f, 1f);
            vp.AddComponent<RectMask2D>();
            GameObject icerik = CreateRect(vp.transform, "Dunya", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            content = icerik.GetComponent<RectTransform>();
            content.pivot = new Vector2(0.5f, 0.5f);
            content.sizeDelta = DunyaBoyutu;
            kaydirma = vp.AddComponent<ScrollRect>();
            kaydirma.content = content;
            kaydirma.viewport = viewport;
            kaydirma.horizontal = true;
            kaydirma.vertical = true;
            kaydirma.movementType = ScrollRect.MovementType.Clamped;
            kaydirma.inertia = true;
            kaydirma.decelerationRate = 0.1f;
            kaydirma.scrollSensitivity = 0f;

            Image zemin = icerik.AddComponent<Image>();
            zemin.color = parsomen;
            zemin.raycastTarget = false;
            Sus();

            foreach (string[] yol in yolculukCizgileri) {
                CizgiEkle(yol[0], yol[1], new Color(0.3f, 0.22f, 0.12f, 0.55f), 7f, true);
            }
            foreach (string[] yol in kapiYollari) {
                CizgiEkle(yol[0], yol[1], new Color(0.45f, 0.3f, 0.12f, 0.95f), 12f, false);
            }
            // madalyonlar, üstlerinde adlar (adlar komşu madalyonun altında kalmasın)
            bolgeKatmani = CreateRect(content, "Bolgeler", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).transform;
            etiketKatmani = CreateRect(content, "Etiketler", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).transform;
            string[] sahneler = IsinlanmaPenceresi.SahneAdlari;
            for (int i = 0; i < sahneler.Length; i++) {
                if (yerler.ContainsKey(sahneler[i])) {
                    BolgeKur(sahneler[i], i);
                }
            }

            // yakınlaştırma düğmeleri
            CreateButton(panelRoot.transform, "+", new Vector2(1f, 0f), new Vector2(-52f, 250f), new Vector2(64f, 64f), 34, buttonColor, () => {
                MobileFeedback.Tap();
                YakinlikAyarla(yakinlik * 1.5f, EkranMerkezi(), true);
            });
            CreateButton(panelRoot.transform, "−", new Vector2(1f, 0f), new Vector2(-52f, 176f), new Vector2(64f, 64f), 34, buttonColor, () => {
                MobileFeedback.Tap();
                YakinlikAyarla(yakinlik / 1.5f, EkranMerkezi(), true);
            });

            // alt şerit: seçili diyar
            GameObject serit = CreateRect(panelRoot.transform, "Bilgi", Vector2.zero, new Vector2(1f, 0f), new Vector2(12f, 8f), new Vector2(-12f, 108f));
            serit.AddComponent<Image>().color = new Color(0.12f, 0.09f, 0.06f, 0.98f);
            GameObject ad = CreateRect(serit.transform, "Ad", new Vector2(0f, 0.55f), new Vector2(0.62f, 1f), new Vector2(18f, 0f), new Vector2(0f, -4f));
            bilgiAdi = CreateText(ad, string.Empty, 26, TextAnchor.MiddleLeft, gold);
            bilgiAdi.fontStyle = FontStyle.Bold;
            bilgiAdi.horizontalOverflow = HorizontalWrapMode.Overflow;
            GameObject zorluk = CreateRect(serit.transform, "Zorluk", new Vector2(0.3f, 0.55f), new Vector2(0.62f, 1f), new Vector2(0f, 0f), new Vector2(0f, -4f));
            bilgiZorluk = CreateText(zorluk, string.Empty, 20, TextAnchor.MiddleRight, textColor);
            bilgiZorluk.horizontalOverflow = HorizontalWrapMode.Overflow;
            bilgiZorluk.fontStyle = FontStyle.Bold;
            GameObject aciklama = CreateRect(serit.transform, "Aciklama", new Vector2(0f, 0f), new Vector2(0.62f, 0.55f), new Vector2(18f, 4f), new Vector2(0f, 0f));
            bilgiAciklama = CreateText(aciklama, string.Empty, 18, TextAnchor.UpperLeft, hintColor);
            GameObject durum = CreateRect(serit.transform, "Durum", new Vector2(0f, 0f), new Vector2(0.62f, 0.55f), new Vector2(18f, 4f), new Vector2(0f, 0f));
            durumYazisi = CreateText(durum, string.Empty, 18, TextAnchor.LowerLeft, errorColor);
            CreateButton(serit.transform, "Yakınlaş", new Vector2(1f, 0.5f), new Vector2(-400f, 0f), new Vector2(170f, 64f), 22, buttonColor, () => {
                MobileFeedback.Tap();
                SecileneYakinlas();
            });
            GameObject isinlan = CreateButton(serit.transform, "Işınlan", new Vector2(1f, 0.5f), new Vector2(-160f, 0f), new Vector2(280f, 70f), 28, teleportColor, () => {
                MobileFeedback.Tap();
                Isinla();
            });
            isinlanDugmesi = isinlan.GetComponent<Button>();
            isinlanYazisi = isinlan.GetComponentInChildren<Text>();
            isinlanYazisi.fontStyle = FontStyle.Bold;

            panelRoot.SetActive(false);
        }

        /// <summary>parşömen üstünde bölge renkleri: güneyde deniz, kuzeybatıda orman, doğuda dağlar, en doğuda Erlik'in ülkesi</summary>
        private void Sus() {
            Leke(new Vector2(1200f, 60f), 900f, new Color(0.2f, 0.45f, 0.6f, 0.55f));
            Leke(new Vector2(1650f, 40f), 700f, new Color(0.2f, 0.45f, 0.6f, 0.5f));
            Leke(new Vector2(700f, 20f), 600f, new Color(0.2f, 0.45f, 0.6f, 0.45f));
            Leke(new Vector2(560f, 1220f), 700f, new Color(0.25f, 0.45f, 0.2f, 0.35f));
            Leke(new Vector2(450f, 700f), 760f, new Color(0.55f, 0.65f, 0.3f, 0.3f));
            Leke(new Vector2(1650f, 900f), 800f, new Color(0.45f, 0.4f, 0.38f, 0.35f));
            Leke(new Vector2(2250f, 900f), 900f, new Color(0.45f, 0.12f, 0.08f, 0.4f));
            Leke(new Vector2(1250f, 1180f), 600f, new Color(0.7f, 0.55f, 0.3f, 0.3f));
        }

        private void Leke(Vector2 yer, float cap, Color renk) {
            GameObject leke = CreateRect(content, "Leke", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform rt = leke.GetComponent<RectTransform>();
            rt.anchoredPosition = yer - DunyaBoyutu * 0.5f;
            rt.sizeDelta = new Vector2(cap, cap);
            Image resim = leke.AddComponent<Image>();
            resim.sprite = MobileShapes.Circle;
            resim.color = renk;
            resim.raycastTarget = false;
        }

        private void CizgiEkle(string a, string b, Color renk, float kalinlik, bool kesikli) {
            Vector2 pa, pb;
            if (yerler.TryGetValue(a, out pa) == false || yerler.TryGetValue(b, out pb) == false) {
                return;
            }
            Vector2 fark = pb - pa;
            float uzunluk = fark.magnitude;
            float aci = Mathf.Atan2(fark.y, fark.x) * Mathf.Rad2Deg;
            Vector2 yon = fark / Mathf.Max(uzunluk, 0.01f);
            float parca = kesikli ? 22f : uzunluk;
            float bosluk = kesikli ? 16f : 0f;
            for (float t = 0f; t < uzunluk; t += parca + bosluk) {
                float l = Mathf.Min(parca, uzunluk - t);
                GameObject cizgi = CreateRect(content, "Yol", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                RectTransform rt = cizgi.GetComponent<RectTransform>();
                rt.anchoredPosition = pa + yon * (t + l * 0.5f) - DunyaBoyutu * 0.5f;
                rt.sizeDelta = new Vector2(l, kalinlik);
                rt.localEulerAngles = new Vector3(0f, 0f, aci);
                Image resim = cizgi.AddComponent<Image>();
                resim.color = renk;
                resim.raycastTarget = false;
            }
        }

        private void BolgeKur(string sahne, int sira) {
            Bolge b = new Bolge() { sahne = sahne };
            GameObject kok = CreateRect(bolgeKatmani, "Bolge_" + sahne, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            b.kok = kok.GetComponent<RectTransform>();
            b.kok.anchoredPosition = yerler[sahne] - DunyaBoyutu * 0.5f;
            b.kok.sizeDelta = new Vector2(BolgeCapi + 24f, BolgeCapi + 24f);

            // dokunulan halka (seçim)
            b.halka = kok.AddComponent<Image>();
            b.halka.sprite = MobileShapes.Circle;
            b.halka.color = halkaRengi;
            Button dugme = kok.AddComponent<Button>();
            dugme.targetGraphic = b.halka;
            dugme.transition = Selectable.Transition.None;
            dugme.onClick.AddListener(() => { MobileFeedback.Tap(); Sec(sahne); });

            // yuvarlak görüntü
            GameObject maske = CreateRect(kok.transform, "Maske", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform maskeRt = maske.GetComponent<RectTransform>();
            maskeRt.sizeDelta = new Vector2(BolgeCapi, BolgeCapi);
            b.maske = maske.AddComponent<Image>();
            b.maske.sprite = MobileShapes.Circle;
            b.maske.color = IsinlanmaPenceresi.ZorlukRengi(sahne) * 0.55f + new Color(0f, 0f, 0f, 0.45f);
            b.maske.raycastTarget = false;
            maske.AddComponent<Mask>().showMaskGraphic = true;

            Texture2D doku = Resources.Load<Texture2D>(KaynakKlasoru + "/" + sahne);
            if (doku != null) {
                GameObject resim = CreateRect(maske.transform, "Kusbakisi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                RawImage ham = resim.AddComponent<RawImage>();
                ham.texture = doku;
                ham.raycastTarget = false;
                b.resimli = true;
            } else {
                // görüntü yok (hızlı derleme): diyarın baş harfi
                GameObject harf = CreateRect(maske.transform, "Harf", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Text harfYazisi = CreateText(harf, IsinlanmaPenceresi.GorunenAd(sahne).Substring(0, 1), 150, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.35f));
                harfYazisi.fontStyle = FontStyle.Bold;
            }

            TextAsset veriDosyasi = Resources.Load<TextAsset>(KaynakKlasoru + "/" + sahne + "_veri");
            if (veriDosyasi != null) {
                try {
                    b.veri = JsonUtility.FromJson<BolgeVerisi>(veriDosyasi.text);
                } catch (Exception e) {
                    Debug.LogWarning("DunyaHaritasi: " + sahne + " verisi okunamadı: " + e.Message);
                }
            }
            if (b.veri != null) {
                if (b.veri.kapilar != null) {
                    foreach (KapiVerisi kapi in b.veri.kapilar) {
                        IsaretEkle(b, maske.transform, kapi.u, kapi.v, kapiRengi, 20f, false, "→ " + IsinlanmaPenceresi.GorunenAd(kapi.hedef));
                    }
                }
                if (b.veri.tas != null && b.veri.tas.Length >= 2) {
                    IsaretEkle(b, maske.transform, b.veri.tas[0], b.veri.tas[1], tasRengi, 30f, true, "Geçit Taşı");
                }
                if (b.veri.kamera != null && b.veri.kamera.Length >= 3) {
                    // bulunduğun diyarda oyuncu (Update'te yeri ve yönü güncellenir)
                    GameObject oyuncu = CreateRect(maske.transform, "Oyuncu", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    b.oyuncu = oyuncu.GetComponent<RectTransform>();
                    b.oyuncu.sizeDelta = new Vector2(30f, 30f);
                    Image ok = oyuncu.AddComponent<Image>();
                    ok.sprite = GorevOku.Ucgen();
                    ok.color = oyuncuRengi;
                    ok.raycastTarget = false;
                    Outline cizgi = oyuncu.AddComponent<Outline>();
                    cizgi.effectColor = Color.white;
                    cizgi.effectDistance = new Vector2(2f, -2f);
                    b.isaretler.Add(b.oyuncu);
                    oyuncu.SetActive(false);
                }
            }

            // ad ve zorluk (yakınlıktan bağımsız boyda)
            GameObject etiket = CreateRect(etiketKatmani, "Etiket_" + sahne, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            b.etiket = etiket.GetComponent<RectTransform>();
            b.etiket.pivot = new Vector2(0.5f, 1f);
            b.etiket.anchoredPosition = b.kok.anchoredPosition + new Vector2(0f, -BolgeCapi * 0.5f - 14f);
            b.etiket.sizeDelta = new Vector2(280f, 58f);
            Text adYazisi = CreateText(etiket, (sira + 1) + ". " + IsinlanmaPenceresi.GorunenAd(sahne) + "\n<size=17><color=#"
                + ColorUtility.ToHtmlStringRGB(IsinlanmaPenceresi.ZorlukRengi(sahne)) + ">" + HaritaSeviyeleri.Yazi(sahne) + " · " + IsinlanmaPenceresi.ZorlukAdi(sahne) + "</color></size>",
                22, TextAnchor.UpperCenter, textColor);
            adYazisi.fontStyle = FontStyle.Bold;
            adYazisi.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline adCizgisi = etiket.AddComponent<Outline>();
            adCizgisi.effectColor = new Color(0f, 0f, 0f, 0.9f);
            adCizgisi.effectDistance = new Vector2(1.5f, -1.5f);

            // "Buradasın"
            GameObject burada = CreateRect(etiketKatmani, "Buradasin_" + sahne, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            b.buradasin = burada.GetComponent<RectTransform>();
            // madalyonun üst kenarına oturur
            b.buradasin.pivot = new Vector2(0.5f, 0f);
            b.buradasin.anchoredPosition = b.kok.anchoredPosition + new Vector2(0f, BolgeCapi * 0.5f - 10f);
            b.buradasin.sizeDelta = new Vector2(150f, 34f);
            Image buradaResmi = burada.AddComponent<Image>();
            buradaResmi.color = gold;
            buradaResmi.raycastTarget = false;
            GameObject buradaYazi = CreateRect(burada.transform, "Yazi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Text buradaText = CreateText(buradaYazi, "Buradasın", 20, TextAnchor.MiddleCenter, new Color(0.15f, 0.1f, 0.05f, 1f));
            buradaText.fontStyle = FontStyle.Bold;
            burada.SetActive(false);

            bolgeler.Add(b);
            bolgeSozlugu[sahne] = b;
        }

        private void IsaretEkle(Bolge b, Transform ust, float u, float v, Color renk, float boyut, bool elmas, string ad) {
            GameObject isaret = CreateRect(ust, "Isaret", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform rt = isaret.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2((u - 0.5f) * BolgeCapi, (v - 0.5f) * BolgeCapi);
            rt.sizeDelta = new Vector2(boyut, boyut);
            GameObject sekil = CreateRect(isaret.transform, "Sekil", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform sekilRt = sekil.GetComponent<RectTransform>();
            sekilRt.sizeDelta = new Vector2(boyut, boyut);
            Image resim = sekil.AddComponent<Image>();
            resim.color = renk;
            resim.raycastTarget = false;
            if (elmas) {
                sekilRt.localEulerAngles = new Vector3(0f, 0f, 45f);
                sekilRt.sizeDelta = new Vector2(boyut * 0.72f, boyut * 0.72f);
            } else {
                resim.sprite = MobileShapes.Circle;
            }
            Outline cizgi = sekil.AddComponent<Outline>();
            cizgi.effectColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);
            cizgi.effectDistance = new Vector2(2f, -2f);
            b.isaretler.Add(rt);

            GameObject yazi = CreateRect(isaret.transform, "Ad", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            RectTransform yaziRt = yazi.GetComponent<RectTransform>();
            yaziRt.pivot = new Vector2(0.5f, 0f);
            yaziRt.anchoredPosition = new Vector2(0f, boyut * 0.5f + 2f);
            yaziRt.sizeDelta = new Vector2(220f, 30f);
            Text yaziText = CreateText(yazi, ad, 20, TextAnchor.LowerCenter, renk);
            yaziText.fontStyle = FontStyle.Bold;
            yaziText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline yaziCizgisi = yazi.AddComponent<Outline>();
            yaziCizgisi.effectColor = new Color(0f, 0f, 0f, 0.95f);
            yaziCizgisi.effectDistance = new Vector2(1.5f, -1.5f);
            b.ayrintilar.Add(yazi);
        }

        // ---------------------------------------------------------------- açma, seçme

        private SystemGameManager OyunYoneticisi {
            get {
                if (systemGameManager == null) {
                    systemGameManager = FindAnyObjectByType<SystemGameManager>();
                }
                return systemGameManager;
            }
        }

        private void Ac(string bulunulan, bool gecitTasindan) {
            mevcut = bulunulan;
            panelRoot.SetActive(true);
            Canvas.ForceUpdateCanvases();
            altBaslik.text = gecitTasindan
                ? "Geçit Taşı seni bekliyor: gitmek istediğin diyara dokun, sonra Işınlan."
                : "Kıstırarak yakınlaş, sürükleyerek gez. Mavi: Geçit Taşı, altın: komşu diyara kapı.";
            foreach (Bolge b in bolgeler) {
                bool burada = b.sahne == mevcut;
                b.buradasin.gameObject.SetActive(burada);
                if (b.oyuncu != null) {
                    b.oyuncu.gameObject.SetActive(false);
                }
            }
            animasyon = false;
            yakinlik = Mathf.Max(1f, EnAzYakinlik());
            content.localScale = new Vector3(yakinlik, yakinlik, 1f);
            Bolge simdiki;
            content.anchoredPosition = bolgeSozlugu.TryGetValue(mevcut, out simdiki) ? -simdiki.kok.anchoredPosition * yakinlik : Vector2.zero;
            Sinirla();
            OlcekleriYenile();
            Sec(bolgeSozlugu.ContainsKey(mevcut) ? mevcut : IsinlanmaPenceresi.SahneAdlari[0]);
            MobileFeedback.Light();
        }

        private void Kapat() {
            panelRoot.SetActive(false);
            kistirma = false;
            if (kaydirma != null) {
                kaydirma.enabled = true;
            }
        }

        private void Sec(string sahne) {
            Bolge b;
            if (bolgeSozlugu.TryGetValue(sahne, out b) == false) {
                return;
            }
            secili = sahne;
            foreach (Bolge diger in bolgeler) {
                diger.halka.color = diger.sahne == secili ? gold : (diger.sahne == mevcut ? new Color(0.62f, 0.5f, 0.28f, 1f) : halkaRengi);
            }
            bilgiAdi.text = IsinlanmaPenceresi.GorunenAd(sahne);
            // haritanın seviyesi ve oyuncuya göre zorluğu ("Seviye 3–6 · sana göre")
            Color seviyeRengi;
            string yorum = HaritaSeviyeleri.Yorum(sahne, HaritaSeviyeleri.OyuncuSeviyesi(), out seviyeRengi);
            bilgiZorluk.text = HaritaSeviyeleri.Yazi(sahne) + (yorum.Length > 0 ? " · " + yorum : " · " + IsinlanmaPenceresi.ZorlukAdi(sahne));
            bilgiZorluk.color = yorum.Length > 0 ? seviyeRengi : IsinlanmaPenceresi.ZorlukRengi(sahne);
            bilgiAciklama.text = IsinlanmaPenceresi.Aciklama(sahne);
            durumYazisi.text = string.Empty;
            isinlanYazisi.text = sahne == mevcut ? "Girişe Dön" : "Işınlan";
        }

        private string Isinla() {
            if (string.IsNullOrEmpty(secili) || Time.unscaledTime < mesgulKadar) {
                return "meşgul";
            }
            string hata = Cevrimici.Acik ? CevrimiciIsinla(secili) : IsinlanmaPenceresi.Teleport(secili);
            if (hata != null) {
                durumYazisi.text = hata;
                bilgiAciklama.text = string.Empty;
                return hata;
            }
            mesgulKadar = Time.unscaledTime + 3f;
            Kapat();
            return null;
        }

        private void SecileneYakinlas() {
            Bolge b;
            if (bolgeSozlugu.TryGetValue(secili, out b) == false) {
                return;
            }
            hedefYakinlik = Mathf.Clamp(Mathf.Max(yakinlik * 1.8f, 2.6f), EnAzYakinlik(), EnCokYakinlik);
            if (yakinlik >= EnCokYakinlik * 0.95f) {
                hedefYakinlik = Mathf.Max(EnAzYakinlik(), 1f);
            }
            hedefKonum = -b.kok.anchoredPosition * hedefYakinlik;
            animasyon = true;
        }

        // ---------------------------------------------------------------- yakınlaştırma ve gezinme

        private float EnAzYakinlik() {
            Rect r = viewport.rect;
            if (r.width < 1f || r.height < 1f) {
                return 0.5f;
            }
            return Mathf.Max(r.width / DunyaBoyutu.x, r.height / DunyaBoyutu.y);
        }

        private Vector2 EkranMerkezi() {
            Vector3[] koseler = new Vector3[4];
            viewport.GetWorldCorners(koseler);
            Vector3 orta = (koseler[0] + koseler[2]) * 0.5f;
            return RectTransformUtility.WorldToScreenPoint(null, orta);
        }

        /// <param name="ekranNoktasi">yakınlaşırken yerinde kalan nokta (parmakların ortası)</param>
        private void YakinlikAyarla(float yeni, Vector2 ekranNoktasi, bool yumusak) {
            yeni = Mathf.Clamp(yeni, EnAzYakinlik(), EnCokYakinlik);
            Vector2 m;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, ekranNoktasi, null, out m) == false) {
                m = Vector2.zero;
            }
            // görünümün ortası (0,0); içeriğin ortası anchoredPosition
            m -= viewport.rect.center;
            Vector2 a = content.anchoredPosition;
            Vector2 yeniKonum = m - (m - a) * (yeni / yakinlik);
            if (yumusak) {
                hedefYakinlik = yeni;
                hedefKonum = yeniKonum;
                animasyon = true;
                return;
            }
            animasyon = false;
            yakinlik = yeni;
            content.localScale = new Vector3(yakinlik, yakinlik, 1f);
            content.anchoredPosition = yeniKonum;
            kaydirma.StopMovement();
            Sinirla();
            OlcekleriYenile();
        }

        private void Sinirla() {
            Rect r = viewport.rect;
            float yx = Mathf.Max(0f, (DunyaBoyutu.x * yakinlik - r.width) * 0.5f);
            float yy = Mathf.Max(0f, (DunyaBoyutu.y * yakinlik - r.height) * 0.5f);
            Vector2 a = content.anchoredPosition;
            content.anchoredPosition = new Vector2(Mathf.Clamp(a.x, -yx, yx), Mathf.Clamp(a.y, -yy, yy));
        }

        /// <summary>adlar ve işaretler yakınlıktan bağımsız okunur boyda kalır; ayrıntı yazıları yakınlaşınca çıkar</summary>
        private void OlcekleriYenile() {
            float etiketOlcegi = Mathf.Clamp(1f / yakinlik, 0.8f, 1.7f);
            float isaretOlcegi = Mathf.Clamp(1f / yakinlik, 0.42f, 1.5f);
            bool ayrinti = yakinlik >= AyrintiYakinligi;
            foreach (Bolge b in bolgeler) {
                b.etiket.localScale = new Vector3(etiketOlcegi, etiketOlcegi, 1f);
                b.buradasin.localScale = new Vector3(etiketOlcegi, etiketOlcegi, 1f);
                foreach (RectTransform isaret in b.isaretler) {
                    isaret.localScale = new Vector3(isaretOlcegi, isaretOlcegi, 1f);
                }
                foreach (GameObject a in b.ayrintilar) {
                    if (a.activeSelf != ayrinti) {
                        a.SetActive(ayrinti);
                    }
                }
            }
        }

        private void Update() {
            if (IsOpen == false) {
                return;
            }
            Kistirma();
            Tekerlek();
            if (animasyon) {
                float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f);
                yakinlik = Mathf.Lerp(yakinlik, hedefYakinlik, t);
                content.localScale = new Vector3(yakinlik, yakinlik, 1f);
                content.anchoredPosition = Vector2.Lerp(content.anchoredPosition, hedefKonum, t);
                kaydirma.StopMovement();
                Sinirla();
                OlcekleriYenile();
                if (Mathf.Abs(yakinlik - hedefYakinlik) < 0.002f && (content.anchoredPosition - hedefKonum).sqrMagnitude < 1f) {
                    animasyon = false;
                }
            }
            OyuncuyuGoster();
        }

        private void Kistirma() {
            Touchscreen ekran = Touchscreen.current;
            int adet = 0;
            Vector2 a = Vector2.zero, b = Vector2.zero;
            if (ekran != null) {
                foreach (UnityEngine.InputSystem.Controls.TouchControl dokunus in ekran.touches) {
                    if (dokunus.press.isPressed == false) {
                        continue;
                    }
                    if (adet == 0) {
                        a = dokunus.position.ReadValue();
                    } else if (adet == 1) {
                        b = dokunus.position.ReadValue();
                    }
                    adet++;
                }
            }
            if (adet >= 2) {
                float mesafe = Mathf.Max(Vector2.Distance(a, b), 1f);
                if (kistirma) {
                    YakinlikAyarla(yakinlik * mesafe / oncekiMesafe, (a + b) * 0.5f, false);
                } else {
                    kistirma = true;
                    animasyon = false;
                    // iki parmak kıstırırken tek parmak sürüklemesi haritayı kaydırmasın
                    kaydirma.enabled = false;
                }
                oncekiMesafe = mesafe;
            } else if (kistirma) {
                kistirma = false;
                kaydirma.enabled = true;
            }
        }

        private void Tekerlek() {
            Mouse fare = Mouse.current;
            if (fare == null) {
                return;
            }
            float tekerlek = fare.scroll.ReadValue().y;
            if (Mathf.Abs(tekerlek) > 0.01f) {
                YakinlikAyarla(yakinlik * (tekerlek > 0f ? 1.15f : 1f / 1.15f), fare.position.ReadValue(), false);
            }
        }

        /// <summary>bulunduğun diyarın görüntüsünde oyuncunun yeri ve baktığı yön</summary>
        private void OyuncuyuGoster() {
            Bolge b;
            if (bolgeSozlugu.TryGetValue(mevcut, out b) == false || b.oyuncu == null || b.veri == null) {
                return;
            }
            SystemGameManager oyun = OyunYoneticisi;
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu == null) {
                b.oyuncu.gameObject.SetActive(false);
                return;
            }
            Vector3 p = oyuncu.transform.position;
            float[] k = b.veri.kamera;
            float derinlik = Mathf.Max(1f, k[1] - p.y);
            float yari = derinlik * Mathf.Tan(b.veri.fov * 0.5f * Mathf.Deg2Rad);
            float u = 0.5f + (p.x - k[0]) / (2f * yari);
            float v = 0.5f + (p.z - k[2]) / (2f * yari);
            bool icinde = u > 0.02f && u < 0.98f && v > 0.02f && v < 0.98f;
            if (b.oyuncu.gameObject.activeSelf != icinde) {
                b.oyuncu.gameObject.SetActive(icinde);
            }
            if (icinde == false) {
                return;
            }
            b.oyuncu.anchoredPosition = new Vector2((u - 0.5f) * BolgeCapi, (v - 0.5f) * BolgeCapi);
            Vector3 ileri = oyuncu.transform.forward;
            b.oyuncu.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(ileri.x, ileri.z) * Mathf.Rad2Deg);
        }

        // ---------------------------------------------------------------- derleme önizlemesi

        /// <summary>HaritaHazirlik.ArayuzCiz: oyuncu Börü Tepesi'ndeymiş gibi, biraz yakınlaşmış dünya haritası</summary>
        private void Onizleme() {
            Ac("BoruTepesi", false);
            Canvas.ForceUpdateCanvases();
            YakinlikAyarla(0.75f, EkranMerkezi(), false);
            content.anchoredPosition = Vector2.zero;
            Sinirla();
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
            text.verticalOverflow = VerticalWrapMode.Overflow;
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
            GameObject textObject = CreateRect(buttonObject.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            CreateText(textObject, label, fontSize, TextAnchor.MiddleCenter, textColor);
            return buttonObject;
        }
    }
}
