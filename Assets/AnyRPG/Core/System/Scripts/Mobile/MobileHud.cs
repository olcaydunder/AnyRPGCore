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
            public float yaricap;
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

            // movement stick, bottom left
            GameObject stickBase = CreateCircle(transform, "MoveStick", new Vector2(0f, 0f), new Vector2(200f, 210f), 230f, new Color(0f, 0f, 0f, 0.28f), new Color(0.85f, 0.7f, 0.4f, 0.55f));
            GameObject stickKnob = CreateCircle(stickBase.transform, "MoveStickKnob", new Vector2(0.5f, 0.5f), Vector2.zero, 100f, new Color(0.85f, 0.7f, 0.4f, 0.6f), new Color(1f, 0.9f, 0.7f, 0.8f));
            stickKnob.GetComponent<Image>().raycastTarget = false;
            VirtualJoystick joystick = stickBase.AddComponent<VirtualJoystick>();
            joystick.Configure(stickKnob.GetComponent<RectTransform>(), 115f);

            // action buttons, bottom right: the big attack button sits under the thumb, the others around it
            Vector2 bottomRight = new Vector2(1f, 0f);
            CreateActionButton("Saldır", MobileInput.RequestAttack, bottomRight, new Vector2(-150f, 150f), 150f, 26);
            KontrolEkle("Hareket", stickBase, 230f);
            CreateActionButton("Zıpla", "JUMP", bottomRight, new Vector2(-320f, 95f), 100f, 20);
            // görev listesinin altında kalsın (16:9 ekranda liste 280 birimden yukarıda)
            CreateActionButton("Hedef", "NEXTTARGET", bottomRight, new Vector2(-295f, 220f), 100f, 20);
            CreateActionButton("Koş/Yürü", "TOGGLERUN", bottomRight, new Vector2(-125f, 320f), 90f, 16);

            // menus, a column on the left edge above the movement stick (the right side holds the mini map and quest tracker)
            Vector2 leftMiddle = new Vector2(0f, 0.5f);
            CreateActionButton("Harita", "MAINMAP", leftMiddle, new Vector2(60f, 210f), 78f, 16);
            // Harita'nın yanında: istenen haritayı seçip oraya ışınlanma penceresi
            CreateActionButton("Işınlan", IsinlanmaPenceresi.Show, leftMiddle, new Vector2(146f, 210f), 78f, 15);
            CreateActionButton("Karakter", "CHARACTERPANEL", leftMiddle, new Vector2(60f, 126f), 78f, 14);
            CreateActionButton("Çanta", "INVENTORY", leftMiddle, new Vector2(60f, 42f), 78f, 16);
            CreateActionButton("Görevler", "QUESTLOG", leftMiddle, new Vector2(60f, -42f), 78f, 14);
            // Seçenekler penceresi; "Nasıl Oynanır" rehberi de oradan açılır
            CreateActionButton("Menü", SeceneklerPenceresi.Show, leftMiddle, new Vector2(60f, -126f), 70f, 17);
        }

        private void KontrolEkle(string ad, GameObject nesne, float boyut) {
            RectTransform rt = nesne.GetComponent<RectTransform>();
            kontroller.Add(new Kontrol() { ad = ad, rt = rt, ev = rt.anchoredPosition, yaricap = boyut * 0.5f });
        }

        private void OnEnable() {
            // gösterilir gösterilmez yerleşime bak
            sonrakiDenetim = 0f;
            imzaVar = false;
        }

        private void OnDestroy() {
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
                    Vector2 fark = k.rt.anchoredPosition - k.ev;
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
                Vector2 ev = Vector2.Scale(k.rt.anchorMin, boyut) + k.ev;
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

        private void CreateActionButton(string label, string actionName, Vector2 anchor, Vector2 position, float size, int fontSize) {
            CreateActionButton(label, () => MobileInput.PressVirtualKey(actionName), anchor, position, size, fontSize);
        }

        private void CreateActionButton(string label, System.Action onPress, Vector2 anchor, Vector2 position, float size, int fontSize) {
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
        }
    }

    /// <summary>
    /// A movement stick: drag inside the circle, the offset from the center becomes a -1..1 direction.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler {

        private RectTransform baseRect = null;
        private RectTransform knob = null;
        private float radius = 100f;

        public void Configure(RectTransform knob, float radius) {
            this.knob = knob;
            this.radius = radius;
            baseRect = GetComponent<RectTransform>();
        }

        public void OnPointerDown(PointerEventData eventData) {
            UpdateStick(eventData);
        }

        public void OnDrag(PointerEventData eventData) {
            UpdateStick(eventData);
        }

        public void OnPointerUp(PointerEventData eventData) {
            MobileInput.SetJoystick(Vector2.zero, false);
            if (knob != null) {
                knob.anchoredPosition = Vector2.zero;
            }
        }

        private void OnDisable() {
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
