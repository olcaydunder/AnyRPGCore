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

        private Font font = null;

        public static MobileHud Create() {
            GameObject canvasObject = new GameObject("MobileHudCanvas");
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasObject.AddComponent<GraphicRaycaster>();
            // the canvas scaler is added by MobileBootstrap.ScaleCanvases (in-game HUD size)
            MobileHud hud = canvasObject.AddComponent<MobileHud>();
            hud.Build();
            return hud;
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // movement stick, bottom left
            GameObject stickBase = CreateCircle(transform, "MoveStick", new Vector2(0f, 0f), new Vector2(200f, 210f), 230f, new Color(0f, 0f, 0f, 0.28f), new Color(0.85f, 0.7f, 0.4f, 0.55f));
            GameObject stickKnob = CreateCircle(stickBase.transform, "MoveStickKnob", new Vector2(0.5f, 0.5f), Vector2.zero, 100f, new Color(0.85f, 0.7f, 0.4f, 0.6f), new Color(1f, 0.9f, 0.7f, 0.8f));
            stickKnob.GetComponent<Image>().raycastTarget = false;
            VirtualJoystick joystick = stickBase.AddComponent<VirtualJoystick>();
            joystick.Configure(stickKnob.GetComponent<RectTransform>(), 115f);

            // action buttons, bottom right
            Vector2 bottomRight = new Vector2(1f, 0f);
            CreateActionButton("Saldır", "ACCEPT", bottomRight, new Vector2(-165f, 190f), 150f, 26);
            CreateActionButton("Zıpla", "JUMP", bottomRight, new Vector2(-330f, 120f), 105f, 21);
            CreateActionButton("Hedef", "NEXTTARGET", bottomRight, new Vector2(-315f, 290f), 105f, 21);
            CreateActionButton("Koş/Yürü", "TOGGLERUN", bottomRight, new Vector2(-165f, 380f), 95f, 17);

            // menus, right edge below the mini map
            Vector2 topRight = new Vector2(1f, 1f);
            CreateActionButton("Harita", "MAINMAP", topRight, new Vector2(-60f, -290f), 88f, 18);
            CreateActionButton("Çanta", "INVENTORY", topRight, new Vector2(-60f, -390f), 88f, 18);
            CreateActionButton("Görevler", "QUESTLOG", topRight, new Vector2(-60f, -490f), 88f, 16);
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
            GameObject buttonObject = CreateCircle(transform, label + "Button", anchor, position, size, new Color(0.1f, 0.08f, 0.06f, 0.55f), new Color(0.85f, 0.7f, 0.4f, 0.85f));
            Image image = buttonObject.GetComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.pressedColor = new Color(1f, 0.85f, 0.5f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => {
                MobileInput.PressVirtualKey(actionName);
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
