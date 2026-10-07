using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// On-screen controls for phones: a movement stick on the left and action buttons on the right.
    /// The stick feeds the same input as a gamepad's left stick, and the buttons press the game's own key bind actions,
    /// so all movement and actions go through the normal game logic.
    /// </summary>
    public class MobileHud : MonoBehaviour {

        public const string CanvasName = "MobileHudCanvas";
        // above the HUD (0-1), below windows and menus (moved to 12 and up by MobileBootstrap.ScaleCanvases)
        public const int SortingOrder = 5;

        // yerleşim koruması: düğmeler oyunun göstergelerinin (can çubuğu, yetenek çubuğu, mini harita...) ve
        // birbirlerinin üstüne binerse en yakın boş yere kaydırılır; ekran boyutu, çentik (güvenli alan) ya da
        // göstergeler değişince yeniden bakılır
        private const float DenetimAraligi = 1f;
        private const float EngelPayi = 6f;
        // sol sütundaki düğmeler arasında 6 birim boşluk var; bu paydan büyük olursa sütun boşuna kayar
        private const float DugmeAraligi = 4f;
        private const float KenarPayi = 4f;
        private const float AramaYaricapi = 330f;
        private const float AramaAdimi = 10f;

        private class Kontrol {
            public string ad;
            public RectTransform rt;
            public Vector2 ev;
            // güvenli alana göre kaydırılmış ev
            public Vector2 hedef;
            public float yaricap;
            public float tabanYaricap;
            public bool kaydirildi;
        }

        public static MobileHud Ornek { get; private set; }

        private Font font = null;
        private Canvas canvas = null;
        private readonly List<Kontrol> kontroller = new List<Kontrol>();
        private readonly List<ArayuzDenetimi.Parca> engeller = new List<ArayuzDenetimi.Parca>();
        private readonly List<Rect> engelTuval = new List<Rect>();
        private readonly List<Rect> yakinEngeller = new List<Rect>();
        private readonly List<Vector3> yerlesenler = new List<Vector3>();
        private static Vector2[] adaylar = null;
        private float sonrakiDenetim = 0f;
        private int sonImza = 0;
        private bool imzaVar = false;
        private readonly Dictionary<string, Rect> grupAlanlari = new Dictionary<string, Rect>();
        private SystemGameManager oyunYoneticisi = null;
        private float uygulananOlcek = 1f;
        private CanvasGroup saydamlik = null;

        public static MobileHud Create() {
            GameObject canvasObject = new GameObject(CanvasName);
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvasObject.AddComponent<GraphicRaycaster>();
            // the canvas scaler is added by MobileBootstrap.ScaleCanvases (in-game HUD size)
            MobileHud hud = canvasObject.AddComponent<MobileHud>();
            hud.Build();
            return hud;
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = GetComponent<Canvas>();
            Ornek = this;

            // hareket çubuğu, sol alt (altında simge sırası). İtildikçe hızlanır, sonuna itilip tutulunca kendi koşar
            GameObject stickBase = CreateCircle(transform, "MoveStick", new Vector2(0f, 0f), new Vector2(200f, 236f), 230f, new Color(0f, 0f, 0f, 0.28f), new Color(0.85f, 0.7f, 0.4f, 0.55f));
            GameObject stickKnob = CreateCircle(stickBase.transform, "MoveStickKnob", new Vector2(0.5f, 0.5f), Vector2.zero, 100f, new Color(0.85f, 0.7f, 0.4f, 0.6f), new Color(1f, 0.9f, 0.7f, 0.8f));
            stickKnob.GetComponent<Image>().raycastTarget = false;
            Text otoKosu = Etiket(stickBase.transform, "OtoKosu", "OTO KOŞU", 17, new Vector2(0.5f, 1f), new Vector2(0f, 18f), new Vector2(200f, 26f));
            otoKosu.color = new Color(1f, 0.85f, 0.4f, 1f);
            otoKosu.gameObject.SetActive(false);
            VirtualJoystick joystick = stickBase.AddComponent<VirtualJoystick>();
            joystick.Configure(stickKnob.GetComponent<RectTransform>(), 115f, otoKosu.gameObject);
            KontrolEkle("Hareket", stickBase, 230f);

            // sol alt köşe: çanta, ayarlar çarkı, ışınlanma
            Vector2 solAlt = new Vector2(0f, 0f);
            SimgeDugmesi("Çanta", "canta", () => MobileInput.PressVirtualKey("INVENTORY"), solAlt, new Vector2(50f, 50f), 78f);
            SimgeDugmesi("Ayarlar", "ayarlar", SeceneklerPenceresi.Show, solAlt, new Vector2(136f, 50f), 78f);
            SimgeDugmesi("Işınlan", "isinlan", IsinlanmaPenceresi.Show, solAlt, new Vector2(222f, 50f), 78f);

            // öteki pencereler tek bir menü simgesinin içinde (ekran dolmasın)
            menuDugmesi = SimgeDugmesi("Menü", "menu", MenuyuAcKapa, new Vector2(0f, 0.5f), new Vector2(54f, 150f), 84f);
            gunlukRozeti = CreateCircle(menuDugmesi.transform, "OdulRozeti", new Vector2(1f, 1f), new Vector2(-12f, -12f), 22f,
                new Color(1f, 0.78f, 0.25f, 1f), new Color(0.35f, 0.2f, 0.05f, 1f));
            gunlukRozeti.GetComponent<Image>().raycastTarget = false;
            gunlukRozeti.SetActive(false);
            MenuPaneliKur();

            // sağ alt: büyük Saldır, çevresinde sınıf becerileri; üstünde Oto Av ve Binek, solunda Hedef
            Vector2 bottomRight = new Vector2(1f, 0f);
            CreateActionButton("Saldır", MobileInput.RequestAttack, bottomRight, SaldiriMerkezi, 150f, 26);
            HalkaKur(bottomRight);
            // Metin2 tarzı otomatik av: açıkken altın renkli (OtomatikAv, PlayerController.HandleAutoHunt)
            otoAvDugmesi = SimgeDugmesi("Oto Av", "otoav", OtomatikAv.Degistir, bottomRight, new Vector2(-62f, 322f), 88f).GetComponent<Image>();
            OtomatikAv.Degisti += OtoAvGuncelle;
            OtoAvGuncelle();
            binekDugmesi = SimgeDugmesi("Binek", "binek", Binek.Degistir, bottomRight, new Vector2(-62f, 430f), 80f);
            binekDugmesi.SetActive(false);
            SimgeDugmesi("Hedef", "hedef", () => MobileInput.PressVirtualKey("NEXTTARGET"), bottomRight, new Vector2(-398f, 72f), 84f);

            // mini haritaya dokununca büyük harita; altında bölge etkinliği afişi
            HaritaKatmaniKur();
        }

        // ---------------------------------------------------------------- simgeli düğme, etiket

        private static readonly Dictionary<string, Sprite> simgeler = new Dictionary<string, Sprite>();

        public static Sprite Simge(string ad) {
            Sprite s;
            if (simgeler.TryGetValue(ad, out s) == false) {
                s = Resources.Load<Sprite>("Arayuz/" + ad);
                simgeler[ad] = s;
            }
            return s;
        }

        private Text Etiket(Transform ust, string ad, string metin, int boyut, Vector2 capa, Vector2 konum, Vector2 olcu) {
            GameObject go = new GameObject(ad);
            go.transform.SetParent(ust, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = capa;
            rt.anchorMax = capa;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = konum;
            rt.sizeDelta = olcu;
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.text = metin;
            t.fontSize = boyut;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.97f, 0.92f, 0.82f, 1f);
            t.raycastTarget = false;
            Outline o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.8f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        /// <summary>yuvarlak düğme: ortada simge, altında küçük ad</summary>
        private GameObject SimgeDugmesi(string label, string simge, System.Action onPress, Vector2 anchor, Vector2 position, float size) {
            GameObject buttonObject = CreateActionButton(label, onPress, anchor, position, size, 12);
            Text yazi = buttonObject.GetComponentInChildren<Text>();
            Sprite sp = Simge(simge);
            if (sp != null && yazi != null) {
                RectTransform yrt = yazi.rectTransform;
                yrt.anchorMin = new Vector2(0f, 0f);
                yrt.anchorMax = new Vector2(1f, 0f);
                yrt.pivot = new Vector2(0.5f, 0f);
                yrt.anchoredPosition = new Vector2(0f, size * 0.07f);
                yrt.sizeDelta = new Vector2(0f, size * 0.24f);
                yazi.fontSize = Mathf.RoundToInt(size * 0.16f);
                GameObject resim = new GameObject("Simge");
                resim.transform.SetParent(buttonObject.transform, false);
                RectTransform rrt = resim.AddComponent<RectTransform>();
                rrt.anchorMin = new Vector2(0.5f, 0.5f);
                rrt.anchorMax = new Vector2(0.5f, 0.5f);
                rrt.pivot = new Vector2(0.5f, 0.5f);
                rrt.anchoredPosition = new Vector2(0f, size * 0.08f);
                rrt.sizeDelta = new Vector2(size * 0.56f, size * 0.56f);
                Image im = resim.AddComponent<Image>();
                im.sprite = sp;
                im.preserveAspect = true;
                im.raycastTarget = false;
            }
            return buttonObject;
        }

        // ---------------------------------------------------------------- menü (öteki pencereler)

        private GameObject menuDugmesi = null;
        private GameObject menuPaneli = null;
        private GameObject menuPerdesi = null;
        private GameObject menuSohbet = null;

        private void MenuyuAcKapa() {
            if (menuPaneli == null) {
                return;
            }
            bool ac = menuPaneli.activeSelf == false;
            menuPaneli.SetActive(ac);
            menuPerdesi.SetActive(ac);
            if (ac) {
                menuSohbet.SetActive(Cevrimici.Acik);
                menuPaneli.transform.SetAsLastSibling();
            }
        }

        private void MenuyuKapat() {
            if (menuPaneli != null) {
                menuPaneli.SetActive(false);
                menuPerdesi.SetActive(false);
            }
        }

        private void MenuPaneliKur() {
            // dışına dokununca kapanır
            menuPerdesi = new GameObject("MenuPerdesi");
            menuPerdesi.transform.SetParent(transform, false);
            RectTransform prt = menuPerdesi.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            Image perde = menuPerdesi.AddComponent<Image>();
            perde.color = new Color(0f, 0f, 0f, 0.25f);
            Button kapat = menuPerdesi.AddComponent<Button>();
            kapat.transition = Selectable.Transition.None;
            kapat.onClick.AddListener(MenuyuKapat);
            menuPerdesi.SetActive(false);

            menuPaneli = new GameObject("MenuPaneli");
            menuPaneli.transform.SetParent(transform, false);
            RectTransform rt = menuPaneli.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(104f, 110f);
            Image zemin = menuPaneli.AddComponent<Image>();
            zemin.color = new Color(0.09f, 0.07f, 0.05f, 0.94f);
            Outline cerceve = menuPaneli.AddComponent<Outline>();
            cerceve.effectColor = new Color(0.85f, 0.7f, 0.4f, 0.9f);
            cerceve.effectDistance = new Vector2(2f, -2f);

            List<KeyValuePair<string, System.Action>> ogeler = new List<KeyValuePair<string, System.Action>>() {
                new KeyValuePair<string, System.Action>("Karakter", () => CantaSayfalari.KarakterleAc(this)),
                new KeyValuePair<string, System.Action>("Görevler", () => MobileInput.PressVirtualKey("QUESTLOG")),
                new KeyValuePair<string, System.Action>("Günlük Görevler", GunlukGorevler.Goster),
                new KeyValuePair<string, System.Action>("Demirci", Demirci.Goster),
                new KeyValuePair<string, System.Action>("Ticaret", TicaretPenceresi.Goster),
                new KeyValuePair<string, System.Action>("Depo", Depo.Goster),
                new KeyValuePair<string, System.Action>("Dünya Haritası", DunyaHaritasi.Goster),
                new KeyValuePair<string, System.Action>("Etkinlikler", EtkinlikPenceresi.Goster),
                new KeyValuePair<string, System.Action>("Sohbet", Sohbet),
                new KeyValuePair<string, System.Action>("Oyun Kılavuzu", () => GameGuide.Show(false)),
            };
            const float en = 230f;
            const float boy = 62f;
            const float bosluk = 8f;
            int sutun = 2;
            int satir = Mathf.CeilToInt(ogeler.Count / (float)sutun);
            rt.sizeDelta = new Vector2(sutun * en + (sutun + 1) * bosluk, satir * boy + (satir + 1) * bosluk);
            for (int i = 0; i < ogeler.Count; i++) {
                System.Action eylem = ogeler[i].Value;
                GameObject d = new GameObject(ogeler[i].Key);
                d.transform.SetParent(menuPaneli.transform, false);
                RectTransform drt = d.AddComponent<RectTransform>();
                drt.anchorMin = new Vector2(0f, 1f);
                drt.anchorMax = new Vector2(0f, 1f);
                drt.pivot = new Vector2(0f, 1f);
                drt.anchoredPosition = new Vector2(bosluk + (i % sutun) * (en + bosluk), -bosluk - (i / sutun) * (boy + bosluk));
                drt.sizeDelta = new Vector2(en, boy);
                Image im = d.AddComponent<Image>();
                im.color = new Color(0.22f, 0.16f, 0.1f, 0.95f);
                Button b = d.AddComponent<Button>();
                b.targetGraphic = im;
                b.onClick.AddListener(() => {
                    MobileFeedback.Tap();
                    MenuyuKapat();
                    eylem();
                });
                Text t = Etiket(d.transform, "Ad", ogeler[i].Key, 22, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(en - 10f, boy));
                if (ogeler[i].Key == "Günlük Görevler") {
                    menuGunlukYazisi = t;
                }
                if (ogeler[i].Key == "Sohbet") {
                    menuSohbet = d;
                }
            }
            menuPaneli.SetActive(false);
        }

        private Text menuGunlukYazisi = null;

        // ---------------------------------------------------------------- beceri halkası (Saldır'ın çevresinde)

        public static readonly Vector2 SaldiriMerkezi = new Vector2(-150f, 150f);

        private class HalkaYuvasi {
            public GameObject go;
            public Image simge;
            public Image bekleme;
            public Text sayi;
            public ActionButton kaynak;
        }

        private readonly List<HalkaYuvasi> halka = new List<HalkaYuvasi>();
        private CanvasGroup gizlenenCubuk = null;

        private void HalkaKur(Vector2 capa) {
            // iç halka 4, dış halka 4 beceri: Saldır'ın üstünden soluna doğru
            float[] icAcilar = { 95f, 125f, 155f, 185f };
            float[] disAcilar = { 100f, 123f, 146f, 169f };
            for (int i = 0; i < 8; i++) {
                bool ic = i < 4;
                float aci = (ic ? icAcilar[i] : disAcilar[i - 4]) * Mathf.Deg2Rad;
                float r = ic ? 148f : 236f;
                float boyut = ic ? 84f : 76f;
                Vector2 konum = SaldiriMerkezi + new Vector2(Mathf.Cos(aci), Mathf.Sin(aci)) * r;
                int sira = i;
                GameObject go = CreateActionButton("Beceri" + (i + 1), () => HalkaBas(sira), capa, konum, boyut, 1);
                go.GetComponentInChildren<Text>().text = string.Empty;
                HalkaYuvasi y = new HalkaYuvasi() { go = go };
                GameObject sg = new GameObject("Simge");
                sg.transform.SetParent(go.transform, false);
                RectTransform srt = sg.AddComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.14f, 0.14f);
                srt.anchorMax = new Vector2(0.86f, 0.86f);
                srt.offsetMin = Vector2.zero;
                srt.offsetMax = Vector2.zero;
                y.simge = sg.AddComponent<Image>();
                y.simge.raycastTarget = false;
                y.simge.preserveAspect = true;
                GameObject bg = new GameObject("Bekleme");
                bg.transform.SetParent(go.transform, false);
                RectTransform brt = bg.AddComponent<RectTransform>();
                brt.anchorMin = Vector2.zero;
                brt.anchorMax = Vector2.one;
                brt.offsetMin = Vector2.zero;
                brt.offsetMax = Vector2.zero;
                y.bekleme = bg.AddComponent<Image>();
                y.bekleme.sprite = MobileShapes.Circle;
                y.bekleme.type = Image.Type.Filled;
                y.bekleme.fillMethod = Image.FillMethod.Radial360;
                y.bekleme.fillOrigin = (int)Image.Origin360.Top;
                y.bekleme.fillClockwise = false;
                y.bekleme.color = new Color(0f, 0f, 0f, 0.62f);
                y.bekleme.raycastTarget = false;
                y.bekleme.fillAmount = 0f;
                y.sayi = Etiket(go.transform, "Sayi", string.Empty, 16, new Vector2(1f, 0f), new Vector2(-14f, 12f), new Vector2(40f, 22f));
                go.SetActive(false);
                halka.Add(y);
            }
        }

        private void HalkaBas(int sira) {
            if (sira < halka.Count && halka[sira].kaynak != null) {
                halka[sira].kaynak.OnClick(true);
            }
        }

        /// <summary>
        /// yetenek çubuğunun dolu kutuları (normal saldırı hariç) halkada gösterilir; çubuk "Saldırı çevresinde"
        /// seçiliyken gizlenir. Simge ve bekleme süresi her karede çubuktaki düğmeden kopyalanır.
        /// </summary>
        private void HalkayiGuncelle() {
            bool halkaAcik = MobilArayuzDuzeni.CubukSecimi == 1;
            if (oyunYoneticisi == null) {
                oyunYoneticisi = FindAnyObjectByType<SystemGameManager>();
            }
            ActionBarManager cubuklar = oyunYoneticisi != null && oyunYoneticisi.UIManager != null ? oyunYoneticisi.UIManager.ActionBarManager : null;
            ActionBarController cubuk = cubuklar != null && cubuklar.ActionBarControllers != null && cubuklar.ActionBarControllers.Count > 0
                ? cubuklar.ActionBarControllers[0] : null;
            if (cubuk != null) {
                if (gizlenenCubuk == null || gizlenenCubuk.gameObject != cubuk.gameObject) {
                    gizlenenCubuk = cubuk.GetComponent<CanvasGroup>();
                    if (gizlenenCubuk == null) {
                        gizlenenCubuk = cubuk.gameObject.AddComponent<CanvasGroup>();
                    }
                }
                gizlenenCubuk.alpha = halkaAcik ? 0f : 1f;
                gizlenenCubuk.blocksRaycasts = halkaAcik == false;
                gizlenenCubuk.interactable = halkaAcik == false;
            }
            int n = 0;
            if (halkaAcik && cubuk != null) {
                foreach (ActionButton ab in cubuk.ActionButtons) {
                    if (n >= halka.Count) {
                        break;
                    }
                    if (ab == null || ab.Useable == null) {
                        continue;
                    }
                    AbilityProperties yetenek = ab.Useable as AbilityProperties;
                    if (yetenek != null && yetenek.IsAutoAttack) {
                        continue;
                    }
                    HalkaYuvasi y = halka[n++];
                    y.kaynak = ab;
                    if (y.go.activeSelf == false) {
                        y.go.SetActive(true);
                    }
                    Sprite sp = ab.Icon != null ? ab.Icon.sprite : null;
                    if (y.simge.sprite != sp) {
                        y.simge.sprite = sp;
                    }
                    y.simge.enabled = sp != null;
                    Image bekle = ab.CoolDownIcon;
                    y.bekleme.fillAmount = bekle != null && bekle.enabled ? bekle.fillAmount : 0f;
                    string sayi = ab.StackSizeText != null && ab.StackSizeText.gameObject.activeInHierarchy ? ab.StackSizeText.text : string.Empty;
                    if (y.sayi.text != sayi) {
                        y.sayi.text = sayi;
                    }
                }
            }
            for (int i = n; i < halka.Count; i++) {
                halka[i].kaynak = null;
                if (halka[i].go.activeSelf) {
                    halka[i].go.SetActive(false);
                }
            }
        }

        private void LateUpdate() {
            try {
                HalkayiGuncelle();
            } catch (System.Exception e) {
                if (halkaHatasiYazildi == false) {
                    halkaHatasiYazildi = true;
                    Debug.LogWarning("MobileHud beceri halkası: " + e.Message);
                }
            }
        }

        private bool halkaHatasiYazildi = false;

        // ---------------------------------------------------------------- mini harita ve etkinlik afişi

        private RectTransform haritaKatmani = null;
        private RectTransform afis = null;
        private Text afisYazisi = null;

        private void HaritaKatmaniKur() {
            GameObject go = new GameObject("MiniHaritaDokunusu");
            go.transform.SetParent(transform, false);
            haritaKatmani = go.AddComponent<RectTransform>();
            haritaKatmani.anchorMin = Vector2.zero;
            haritaKatmani.anchorMax = Vector2.zero;
            haritaKatmani.pivot = Vector2.zero;
            Image im = go.AddComponent<Image>();
            im.color = new Color(0f, 0f, 0f, 0f);
            Button b = go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => {
                MobileFeedback.Tap();
                DunyaHaritasi.Goster();
            });
            Text t = Etiket(go.transform, "Etiket", "HARİTA", 16, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(120f, 24f));
            t.color = new Color(1f, 0.88f, 0.55f, 1f);
            go.SetActive(false);

            GameObject a = new GameObject("EtkinlikAfisi");
            a.transform.SetParent(transform, false);
            afis = a.AddComponent<RectTransform>();
            afis.anchorMin = Vector2.zero;
            afis.anchorMax = Vector2.zero;
            afis.pivot = new Vector2(1f, 1f);
            Image az = a.AddComponent<Image>();
            az.color = new Color(0.1f, 0.07f, 0.04f, 0.78f);
            Button ab = a.AddComponent<Button>();
            ab.targetGraphic = az;
            ab.onClick.AddListener(() => {
                MobileFeedback.Tap();
                EtkinlikPenceresi.Goster();
            });
            afisYazisi = Etiket(a.transform, "Yazi", string.Empty, 16, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f));
            afisYazisi.rectTransform.anchorMin = Vector2.zero;
            afisYazisi.rectTransform.anchorMax = Vector2.one;
            afisYazisi.rectTransform.offsetMin = new Vector2(8f, 0f);
            afisYazisi.rectTransform.offsetMax = new Vector2(-8f, 0f);
            a.SetActive(false);
        }

        /// <summary>mini haritanın üstüne görünmez dokunma alanı ve altına etkinlik afişi (saniyede bir)</summary>
        private void HaritaKatmaniniGuncelle() {
            if (haritaKatmani == null) {
                return;
            }
            UIManager ui = oyunYoneticisi != null ? oyunYoneticisi.UIManager : null;
            CloseableWindow pencere = ui != null ? ui.MiniMapWindow : null;
            Rect r = new Rect();
            bool gorunur = false;
            if (pencere != null && pencere.RectTransform != null && pencere.gameObject.activeInHierarchy) {
                Canvas tuval = pencere.GetComponentInParent<Canvas>();
                if (tuval != null) {
                    r = ArayuzDenetimi.Dikdortgen(pencere.RectTransform, tuval);
                    gorunur = r.width > 20f && r.height > 20f;
                }
            }
            float olcek = canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
            Rect ekran = ArayuzDenetimi.TuvalEkrani(canvas, new Rect(0f, 0f, Screen.width, Screen.height));
            if (haritaKatmani.gameObject.activeSelf != gorunur) {
                haritaKatmani.gameObject.SetActive(gorunur);
            }
            Vector2 sagUst;
            if (gorunur) {
                haritaKatmani.anchoredPosition = new Vector2((r.x - ekran.x) / olcek, (r.y - ekran.y) / olcek);
                haritaKatmani.sizeDelta = new Vector2(r.width / olcek, r.height / olcek);
                sagUst = new Vector2((r.xMax - ekran.x) / olcek, (r.y - ekran.y) / olcek - 4f);
            } else {
                RectTransform tuvalRt = (RectTransform)transform;
                sagUst = new Vector2(tuvalRt.rect.width - 12f, tuvalRt.rect.height - 12f);
            }
            bool oyunda = ui != null && oyunYoneticisi.PlayerManagerClient != null && oyunYoneticisi.PlayerManagerClient.UnitController != null;
            if (afis.gameObject.activeSelf != oyunda) {
                afis.gameObject.SetActive(oyunda);
            }
            if (oyunda) {
                afisYazisi.text = Etkinlikler.AfisYazisi();
                afisYazisi.color = Etkinlikler.Suruyor ? new Color(1f, 0.85f, 0.35f, 1f) : new Color(0.9f, 0.86f, 0.78f, 1f);
                float genislik = Mathf.Clamp(afisYazisi.preferredWidth + 20f, 200f, 560f);
                afis.sizeDelta = new Vector2(genislik, 30f);
                afis.anchoredPosition = sagUst;
            }
        }

        private void KontrolEkle(string ad, GameObject nesne, float boyut) {
            RectTransform rt = nesne.GetComponent<RectTransform>();
            kontroller.Add(new Kontrol() { ad = ad, rt = rt, ev = rt.anchoredPosition, hedef = rt.anchoredPosition, yaricap = boyut * 0.5f, tabanYaricap = boyut * 0.5f });
        }

        private void OnEnable() {
            // gösterilir gösterilmez yerleşime bak
            sonrakiDenetim = 0f;
            imzaVar = false;
        }

        private void OnDestroy() {
            OtomatikAv.Degisti -= OtoAvGuncelle;
            if (Ornek == this) {
                Ornek = null;
            }
        }

        private void Update() {
            if (Time.unscaledTime < sonrakiDenetim) {
                return;
            }
            sonrakiDenetim = Time.unscaledTime + DenetimAraligi;
            try {
                DurumlariGuncelle();
                Denetle(false);
            } catch (System.Exception exception) {
                Debug.LogWarning("MobileHud yerleşim denetimi: " + exception.Message);
            }
        }

        /// <summary>derleme önizlemesi ve testler için: yerleşimi hemen yeniden hesapla</summary>
        public void YerlesimiUygula() {
            Denetle(true);
        }

        /// <summary>düğmeleri ilk yerlerine koyar (derleme önizlemesinde kaydırmadan önceki durumu görmek için)</summary>
        public void EveDon() {
            foreach (Kontrol k in kontroller) {
                k.rt.anchoredPosition = k.ev;
                k.kaydirildi = false;
            }
            imzaVar = false;
        }

        /// <summary>yerinden kaydırılan düğmeler (hata bildirimindeki arayüz raporu için)</summary>
        public string YerlesimOzeti() {
            StringBuilder sb = new StringBuilder();
            foreach (Kontrol k in kontroller) {
                if (k.kaydirildi) {
                    Vector2 fark = k.rt.anchoredPosition - k.hedef;
                    sb.Append(k.ad).Append(" kaydırıldı (").Append(Mathf.RoundToInt(fark.x)).Append(", ").Append(Mathf.RoundToInt(fark.y)).Append(")\n");
                }
            }
            return sb.Length > 0 ? sb.ToString() : "Düğmeler yerinde.\n";
        }

        /// <summary>düğmelerin ekran dikdörtgenleri (ad, dikdörtgen)</summary>
        public void DugmeDikdortgenleri(List<KeyValuePair<string, Rect>> liste) {
            liste.Clear();
            foreach (Kontrol k in kontroller) {
                liste.Add(new KeyValuePair<string, Rect>(k.ad, ArayuzDenetimi.Dikdortgen(k.rt, canvas)));
            }
        }

        private void Denetle(bool zorla) {
            if (canvas == null) {
                canvas = GetComponent<Canvas>();
            }
            // Seçenekler > Oyun: ekran düğmelerinin boyutu ve saydamlığı (MobilArayuzDuzeni)
            float olcek = MobilArayuzDuzeni.DugmeOlcegi;
            if (Mathf.Abs(olcek - uygulananOlcek) > 0.001f) {
                uygulananOlcek = olcek;
                foreach (Kontrol k in kontroller) {
                    k.rt.localScale = new Vector3(olcek, olcek, 1f);
                    k.yaricap = k.tabanYaricap * olcek;
                }
                imzaVar = false;
            }
            if (saydamlik == null) {
                saydamlik = GetComponent<CanvasGroup>();
                if (saydamlik == null) {
                    saydamlik = gameObject.AddComponent<CanvasGroup>();
                }
            }
            saydamlik.alpha = MobilArayuzDuzeni.DugmeSaydamligi;
            ArayuzDenetimi.GostergeParcalari(engeller, false);
            AyrilmisYerler(engeller);

            Rect ekran = ArayuzDenetimi.TuvalEkrani(canvas, new Rect(0f, 0f, Screen.width, Screen.height));
            int imza = Imza(ekran);
            if (zorla == false && imzaVar && imza == sonImza) {
                return;
            }
            sonImza = imza;
            imzaVar = true;
            Yerlestir(ekran);
        }

        /// <summary>
        /// hedef çerçevesi ve büyü çubuğu yalnız bir şey seçilince ya da büyü yapılırken çıkar; düğmeler o an zıplamasın
        /// diye yerleri her zaman dolu sayılır
        /// </summary>
        private void AyrilmisYerler(List<ArayuzDenetimi.Parca> liste) {
            if (SystemGameManager.IsShuttingDown) {
                return;
            }
            if (oyunYoneticisi == null) {
                oyunYoneticisi = FindAnyObjectByType<SystemGameManager>();
            }
            UIManager ui = oyunYoneticisi != null ? oyunYoneticisi.UIManager : null;
            if (ui == null) {
                return;
            }
            AyrilmisEkle(liste, ui.FocusUnitFrameWindow, "Ayrılmış:hedef çerçevesi");
        }

        private static void AyrilmisEkle(List<ArayuzDenetimi.Parca> liste, CloseableWindow pencere, string ad) {
            if (pencere == null || pencere.RectTransform == null) {
                return;
            }
            Canvas tuval = pencere.GetComponentInParent<Canvas>(true);
            if (tuval == null || tuval.rootCanvas.gameObject.activeInHierarchy == false) {
                return;
            }
            Rect r = ArayuzDenetimi.Dikdortgen(pencere.RectTransform, tuval);
            if (r.width < 3f || r.height < 3f) {
                return;
            }
            liste.Add(new ArayuzDenetimi.Parca() { grup = ad, yol = pencere.name, ekran = r });
        }

        private int Imza(Rect ekran) {
            // göstergelerin kapladığı alanlar 8 piksele yuvarlanır: yazı değişince küçük kıpırtılar yerleşimi bozmasın.
            // Her saniye çalıştığı için yazı üretmeden, sayı olarak hesaplanır
            grupAlanlari.Clear();
            foreach (ArayuzDenetimi.Parca p in engeller) {
                Rect r;
                grupAlanlari[p.grup] = grupAlanlari.TryGetValue(p.grup, out r) ? Rect.MinMaxRect(Mathf.Min(r.xMin, p.ekran.xMin),
                    Mathf.Min(r.yMin, p.ekran.yMin), Mathf.Max(r.xMax, p.ekran.xMax), Mathf.Max(r.yMax, p.ekran.yMax)) : p.ekran;
            }
            int toplam = 0;
            foreach (KeyValuePair<string, Rect> g in grupAlanlari) {
                Rect r = g.Value;
                int h = g.Key.GetHashCode();
                h = h * 31 + Mathf.RoundToInt(r.xMin / 8f);
                h = h * 31 + Mathf.RoundToInt(r.yMin / 8f);
                h = h * 31 + Mathf.RoundToInt(r.xMax / 8f);
                h = h * 31 + Mathf.RoundToInt(r.yMax / 8f);
                // sıradan bağımsız
                toplam += h * 0x2545F491;
            }
            Rect guvenli = Screen.safeArea;
            int imza = toplam;
            imza = imza * 31 + Mathf.RoundToInt(ekran.width);
            imza = imza * 31 + Mathf.RoundToInt(ekran.height);
            imza = imza * 31 + Mathf.RoundToInt(guvenli.xMin) * 7 + Mathf.RoundToInt(guvenli.yMin) * 13
                + Mathf.RoundToInt(guvenli.xMax) * 17 + Mathf.RoundToInt(guvenli.yMax) * 19;
            // ölçek de imzada: tuval ölçekleyicisi sonradan eklenince (MobileBootstrap.ScaleCanvases) yeniden yerleşsin
            imza = imza * 31 + Mathf.RoundToInt(canvas.scaleFactor * 1000f);
            // bir düğme görünür olunca (binek öğrenildi) yeniden yerleşsin
            foreach (Kontrol k in kontroller) {
                imza = imza * 3 + (k.rt.gameObject.activeSelf ? 1 : 0);
            }
            return imza;
        }

        private static Vector2[] Adaylar() {
            if (adaylar != null) {
                return adaylar;
            }
            List<Vector2> liste = new List<Vector2>();
            int n = Mathf.CeilToInt(AramaYaricapi / AramaAdimi);
            for (int x = -n; x <= n; x++) {
                for (int y = -n; y <= n; y++) {
                    Vector2 v = new Vector2(x * AramaAdimi, y * AramaAdimi);
                    if (v.magnitude <= AramaYaricapi) {
                        liste.Add(v);
                    }
                }
            }
            // en yakın boş yer; eşitlikte dikey kayma (sütun düzeni bozulmasın)
            liste.Sort((a, b) => (a.magnitude + Mathf.Abs(a.x) * 0.05f).CompareTo(b.magnitude + Mathf.Abs(b.x) * 0.05f));
            adaylar = liste.ToArray();
            return adaylar;
        }

        private void Yerlestir(Rect ekran) {
            RectTransform tuvalRt = (RectTransform)transform;
            float olcek = canvas.scaleFactor > 0.0001f ? canvas.scaleFactor : 1f;
            Vector2 boyut = tuvalRt.rect.size;

            engelTuval.Clear();
            foreach (ArayuzDenetimi.Parca p in engeller) {
                Rect r = p.ekran;
                engelTuval.Add(new Rect((r.x - ekran.x) / olcek, (r.y - ekran.y) / olcek, r.width / olcek, r.height / olcek));
            }
            Rect guvenli = new Rect(0f, 0f, boyut.x, boyut.y);
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay && Screen.width > 0 && Screen.height > 0) {
                Rect s = Screen.safeArea;
                guvenli = new Rect(s.x / olcek, s.y / olcek, s.width / olcek, s.height / olcek);
            }

            Vector2[] liste = Adaylar();
            yerlesenler.Clear();
            foreach (Kontrol k in kontroller) {
                if (k.rt.gameObject.activeSelf == false) {
                    // gizli düğme (henüz öğrenilmemiş binek) yer tutmaz
                    continue;
                }
                Vector2 ev = Vector2.Scale(k.rt.anchorMin, boyut) + k.ev;
                // çentik / yuvarlak köşe: kenara bağlı düğmeler topluca güvenli alanın içine kayar (düzen bozulmasın)
                if (k.rt.anchorMin.x < 0.01f) {
                    ev.x += guvenli.xMin;
                } else if (k.rt.anchorMin.x > 0.99f) {
                    ev.x -= boyut.x - guvenli.xMax;
                }
                if (k.rt.anchorMin.y < 0.01f) {
                    ev.y += guvenli.yMin;
                } else if (k.rt.anchorMin.y > 0.99f) {
                    ev.y -= boyut.y - guvenli.yMax;
                }
                k.hedef = ev - Vector2.Scale(k.rt.anchorMin, boyut);
                float r = k.yaricap;
                float erim = AramaYaricapi + r + EngelPayi;
                yakinEngeller.Clear();
                foreach (Rect e in engelTuval) {
                    if (e.xMax > ev.x - erim && e.xMin < ev.x + erim && e.yMax > ev.y - erim && e.yMin < ev.y + erim) {
                        yakinEngeller.Add(e);
                    }
                }
                Vector2 secilen = ev;
                bool bulundu = false;
                foreach (Vector2 kayma in liste) {
                    Vector2 c = ev + kayma;
                    if (c.x - r < guvenli.xMin + KenarPayi || c.x + r > guvenli.xMax - KenarPayi
                        || c.y - r < guvenli.yMin + KenarPayi || c.y + r > guvenli.yMax - KenarPayi) {
                        continue;
                    }
                    if (Carpisiyor(c, r)) {
                        continue;
                    }
                    secilen = c;
                    bulundu = true;
                    break;
                }
                if (bulundu == false) {
                    // boş yer yok: ekranın içinde kalsın, yerinde dursun
                    secilen = new Vector2(Mathf.Clamp(ev.x, guvenli.xMin + r, Mathf.Max(guvenli.xMin + r, guvenli.xMax - r)),
                        Mathf.Clamp(ev.y, guvenli.yMin + r, Mathf.Max(guvenli.yMin + r, guvenli.yMax - r)));
                }
                k.rt.anchoredPosition = secilen - Vector2.Scale(k.rt.anchorMin, boyut);
                k.kaydirildi = (secilen - ev).sqrMagnitude > 1f;
                yerlesenler.Add(new Vector3(secilen.x, secilen.y, r));
            }
        }

        private bool Carpisiyor(Vector2 c, float r) {
            foreach (Rect e in yakinEngeller) {
                if (ArayuzDenetimi.DaireDikdortgen(c, r + EngelPayi, e)) {
                    return true;
                }
            }
            foreach (Vector3 y in yerlesenler) {
                float enAz = r + y.z + DugmeAraligi;
                if ((new Vector2(y.x, y.y) - c).sqrMagnitude < enAz * enAz) {
                    return true;
                }
            }
            return false;
        }

        private GameObject CreateCircle(Transform parent, string name, Vector2 anchor, Vector2 position, float size, Color fillColor, Color outlineColor) {
            GameObject circleObject = new GameObject(name);
            circleObject.transform.SetParent(parent, false);
            RectTransform rectTransform = circleObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = new Vector2(size, size);
            Image image = circleObject.AddComponent<Image>();
            image.sprite = MobileShapes.Circle;
            image.color = fillColor;
            Outline outline = circleObject.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);
            return circleObject;
        }

        private GameObject CreateActionButton(string label, string actionName, Vector2 anchor, Vector2 position, float size, int fontSize) {
            return CreateActionButton(label, () => MobileInput.PressVirtualKey(actionName), anchor, position, size, fontSize);
        }

        private GameObject CreateActionButton(string label, System.Action onPress, Vector2 anchor, Vector2 position, float size, int fontSize) {
            GameObject buttonObject = CreateCircle(transform, label + "Button", anchor, position, size, new Color(0.1f, 0.08f, 0.06f, 0.55f), new Color(0.85f, 0.7f, 0.4f, 0.85f));
            KontrolEkle(label, buttonObject, size);
            Image image = buttonObject.GetComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(1f, 0.85f, 0.5f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => {
                onPress();
                MobileFeedback.Tap();
            });

            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.AddComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.97f, 0.92f, 0.82f, 1f);
            text.raycastTarget = false;
            return buttonObject;
        }

        private static readonly Color dugmeRengi = new Color(0.1f, 0.08f, 0.06f, 0.55f);
        private static readonly Color otoAvAcikRengi = new Color(0.78f, 0.56f, 0.16f, 0.9f);
        private Image otoAvDugmesi = null;

        private GameObject gunlukRozeti = null;
        private GameObject binekDugmesi = null;

        private void Sohbet() {
            SystemGameManager oyun = FindAnyObjectByType<SystemGameManager>();
            if (oyun == null || oyun.UIManager == null || oyun.UIManager.MessageLogWindow == null) {
                return;
            }
            MobileFeedback.Tap();
            oyun.UIManager.MessageLogWindow.OpenWindow();
            MessageLogPanel panel = FindAnyObjectByType<MessageLogPanel>();
            if (panel != null) {
                panel.ShowGeneralLog();
                panel.HandleBeginChatCommand(string.Empty);
            }
        }
        private Image binekResmi = null;

        /// <summary>günlük görev rozeti, binek düğmesinin görünürlüğü ve rengi (saniyede bir)</summary>
        private void DurumlariGuncelle() {
            if (gunlukRozeti != null) {
                bool odul = GunlukGorevler.OdulVar;
                if (gunlukRozeti.activeSelf != odul) {
                    gunlukRozeti.SetActive(odul);
                }
                if (menuGunlukYazisi != null) {
                    menuGunlukYazisi.text = odul ? "Günlük Görevler ★" : "Günlük Görevler";
                }
            }
            HaritaKatmaniniGuncelle();
            if (binekDugmesi != null) {
                if (binekDugmesi.activeSelf != Binek.Var) {
                    binekDugmesi.SetActive(Binek.Var);
                }
                if (binekResmi == null) {
                    binekResmi = binekDugmesi.GetComponent<Image>();
                }
                binekResmi.color = Binek.Binili ? otoAvAcikRengi : dugmeRengi;
            }
        }

        private void OtoAvGuncelle() {
            if (otoAvDugmesi != null) {
                otoAvDugmesi.color = OtomatikAv.Acik ? otoAvAcikRengi : dugmeRengi;
            }
        }
    }

    /// <summary>
    /// Hareket çubuğu: dairenin içinde sürükle; merkezden uzaklık -1..1 yön ve hızdır.
    ///  - Az itince yürür, itildikçe hızlanır, kenara yakın tam koşu (hız MovementMoveState'te: SavasAyarlari.JoystickHizi)
    ///  - Kenara itilip 0,8 sn tutulursa OTO KOŞU: parmak kalksa da o yöne koşmaya devam eder; çubuğa yeniden
    ///    dokununca biter (ortaya doğru çekince yavaşlar)
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {

        private const float KilitEsigi = 0.96f;
        private const float KilitSuresi = 0.8f;

        private RectTransform baseRect = null;
        private RectTransform knob = null;
        private GameObject otoKosuEtiketi = null;
        private float radius = 100f;
        private bool basili = false;
        private float kenardaBaslangic = -1f;
        private bool kilitli = false;
        private Vector2 kilitYonu = Vector2.zero;

        public static bool OtoKosu { get; private set; }

        public void Configure(RectTransform knob, float radius) {
            Configure(knob, radius, null);
        }

        public void Configure(RectTransform knob, float radius, GameObject otoKosuEtiketi) {
            this.knob = knob;
            this.radius = radius;
            this.otoKosuEtiketi = otoKosuEtiketi;
            baseRect = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData) {
            basili = true;
            KilidiAc();
            UpdateStick(eventData);
        }

        public void OnDrag(PointerEventData eventData) {
            UpdateStick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData) {
            basili = false;
            kenardaBaslangic = -1f;
            if (kilitli) {
                // oto koşu sürer
                MobileInput.SetJoystick(kilitYonu, true);
                if (knob != null) {
                    knob.anchoredPosition = kilitYonu * radius;
                }
                return;
            }
            MobileInput.SetJoystick(Vector2.zero, false);
            if (knob != null) {
                knob.anchoredPosition = Vector2.zero;
            }
        }

        private void KilidiAc() {
            if (kilitli == false) {
                return;
            }
            kilitli = false;
            OtoKosu = false;
            if (otoKosuEtiketi != null) {
                otoKosuEtiketi.SetActive(false);
            }
        }

        private void Update() {
            if (kilitli && basili == false) {
                // pencere açılınca (çanta, harita...) oto koşu durur
                if (MobileInput.JoystickHeld == false) {
                    MobileInput.SetJoystick(kilitYonu, true);
                }
            }
        }

        private void OnDisable() {
            basili = false;
            KilidiAc();
            MobileInput.SetJoystick(Vector2.zero, false);
            if (knob != null) {
                knob.anchoredPosition = Vector2.zero;
            }
        }

        private void UpdateStick(PointerEventData eventData) {
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, eventData.position, eventData.pressEventCamera, out localPoint) == false) {
                return;
            }
            Vector2 offset = Vector2.ClampMagnitude(localPoint, radius);
            if (knob != null) {
                knob.anchoredPosition = offset;
            }
            Vector2 direction = offset / radius;
            // a small dead zone so a resting thumb does not drift
            if (direction.magnitude < 0.15f) {
                direction = Vector2.zero;
            }
            // kenarda tutunca oto koşu
            if (direction.magnitude >= KilitEsigi) {
                if (kenardaBaslangic < 0f) {
                    kenardaBaslangic = Time.unscaledTime;
                } else if (kilitli == false && Time.unscaledTime - kenardaBaslangic >= KilitSuresi) {
                    kilitli = true;
                    OtoKosu = true;
                    MobileFeedback.Tap();
                    if (otoKosuEtiketi != null) {
                        otoKosuEtiketi.SetActive(true);
                    }
                }
                if (kilitli) {
                    kilitYonu = direction.normalized;
                }
            } else {
                kenardaBaslangic = -1f;
                if (kilitli && direction.magnitude < 0.6f) {
                    // ortaya doğru çekince oto koşu biter
                    KilidiAc();
                }
            }
            MobileInput.SetJoystick(direction, true);
        }
    }

    /// <summary>
    /// A round sprite made at runtime, so the on-screen controls need no image files.
    /// </summary>
    public static class MobileShapes {

        private static Sprite circle = null;

        public static Sprite Circle {
            get {
                if (circle == null) {
                    const int size = 128;
                    Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    float center = (size - 1) * 0.5f;
                    float outer = size * 0.5f;
                    Color32[] pixels = new Color32[size * size];
                    for (int y = 0; y < size; y++) {
                        for (int x = 0; x < size; x++) {
                            float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                            // soft one pixel edge
                            float alpha = Mathf.Clamp01(outer - distance);
                            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                        }
                    }
                    texture.SetPixels32(pixels);
                    texture.Apply(false, true);
                    circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                }
                return circle;
            }
        }
    }
}
