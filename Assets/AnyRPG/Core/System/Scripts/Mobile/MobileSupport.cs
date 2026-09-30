using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Touch helpers shared by the input and UI code.
    /// Touch mode = a touchscreen is present and no physical mouse is connected.
    /// </summary>
    public static class MobileInput {

        public const float LongPressSeconds = 0.45f;

        private static readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        private static readonly List<string> virtualPresses = new List<string>();
        private static readonly List<string> consumedPresses = new List<string>();

        public static bool TouchActive {
            get {
                return Mouse.current == null && Touchscreen.current != null;
            }
        }

        /// <summary>
        /// true when the finger that produced the current click was held down long enough to count as a long press
        /// </summary>
        public static bool IsLongPress() {
            if (TouchActive == false) {
                return false;
            }
            double heldFor = InputState.currentTime - Touchscreen.current.primaryTouch.startTime.ReadValue();
            return heldFor >= LongPressSeconds;
        }

        /// <summary>
        /// true when a screen position is over a UI element that blocks raycasts
        /// </summary>
        public static bool IsOverUI(Vector2 screenPosition) {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) {
                return false;
            }
            PointerEventData pointerEventData = new PointerEventData(eventSystem);
            pointerEventData.position = screenPosition;
            raycastResults.Clear();
            eventSystem.RaycastAll(pointerEventData, raycastResults);
            return raycastResults.Count > 0;
        }

        /// <summary>
        /// Touch aware replacement for EventSystem.current.IsPointerOverGameObject()
        /// </summary>
        public static bool PointerOverUI(Vector2 screenPosition) {
            if (TouchActive) {
                return IsOverUI(screenPosition);
            }
            EventSystem eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        /// <summary>
        /// queue a key bind action (for example "JUMP") to be treated as pressed on the next input frame
        /// </summary>
        public static void PressVirtualKey(string actionName) {
            if (virtualPresses.Contains(actionName) == false) {
                virtualPresses.Add(actionName);
            }
        }

        public static List<string> ConsumeVirtualPresses() {
            consumedPresses.Clear();
            consumedPresses.AddRange(virtualPresses);
            virtualPresses.Clear();
            return consumedPresses;
        }
    }

    /// <summary>
    /// Short vibrations for taps and game events. Can be turned off with the "haptics-enabled" PlayerPrefs key.
    /// </summary>
    public static class MobileFeedback {

        private const string enabledKey = "haptics-enabled";

        private static float lastVibrateTime = -1f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator = null;
        private static bool vibratorLookedUp = false;
        private static int sdkLevel = 0;
#endif

        public static bool Enabled {
            get { return PlayerPrefs.GetInt(enabledKey, 1) == 1; }
            set { PlayerPrefs.SetInt(enabledKey, value ? 1 : 0); }
        }

        public static void Tap() {
            Vibrate(12, 60, 0.05f);
        }

        public static void Light() {
            Vibrate(20, 90, 0.08f);
        }

        public static void Medium() {
            Vibrate(35, 160, 0.1f);
        }

        public static void Heavy() {
            Vibrate(70, 255, 0.15f);
        }

        public static void Success() {
            Vibrate(60, 200, 0.2f);
        }

        /// <summary>
        /// called for every combat text event the local player receives
        /// </summary>
        public static void OnCombatText(bool targetIsPlayer, bool sourceIsPlayer, CombatTextType combatTextType, CombatMagnitude combatMagnitude) {
            if (combatTextType == CombatTextType.levelUp) {
                Success();
                return;
            }
            if (combatTextType != CombatTextType.normal && combatTextType != CombatTextType.ability && combatTextType != CombatTextType.fallDamage) {
                return;
            }
            if (targetIsPlayer) {
                if (combatMagnitude == CombatMagnitude.critical) {
                    Heavy();
                } else {
                    Medium();
                }
            } else if (sourceIsPlayer) {
                if (combatMagnitude == CombatMagnitude.critical) {
                    Medium();
                } else {
                    Light();
                }
            }
        }

        private static void Vibrate(long milliseconds, int amplitude, float minimumInterval) {
            if (Application.isMobilePlatform == false || Enabled == false) {
                return;
            }
            // avoid a constant buzz when many events happen in the same moment
            if (lastVibrateTime > 0f && Time.unscaledTime - lastVibrateTime < minimumInterval) {
                return;
            }
            lastVibrateTime = Time.unscaledTime;

#if UNITY_ANDROID && !UNITY_EDITOR
            try {
                if (vibratorLookedUp == false) {
                    vibratorLookedUp = true;
                    using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION")) {
                        sdkLevel = version.GetStatic<int>("SDK_INT");
                    }
                    using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer")) {
                        AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                }
                if (vibrator == null) {
                    return;
                }
                if (sdkLevel >= 26) {
                    using (AndroidJavaClass vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect")) {
                        AndroidJavaObject effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255));
                        vibrator.Call("vibrate", effect);
                    }
                } else {
                    vibrator.Call("vibrate", milliseconds);
                }
            } catch (System.Exception) {
                // vibration is optional feedback, never let it break the game
            }
#elif UNITY_IOS && !UNITY_EDITOR
            Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID
        /// <summary>
        /// Never called. Referencing Handheld.Vibrate makes Unity add the VIBRATE permission to the Android manifest.
        /// </summary>
        private static void RequestVibratePermission() {
            Handheld.Vibrate();
        }
#endif
    }

    /// <summary>
    /// Created automatically on startup. Applies phone friendly settings, scales the UI for small screens,
    /// saves the game when the app is sent to the background and shows touch buttons for actions that have no on-screen control.
    /// </summary>
    public class MobileBootstrap : MonoBehaviour {

        // the UI is laid out as if the screen was this many pixels tall, so it keeps the same physical size on every phone
        private const float referenceHeight = 720f;
        private const float canvasScanInterval = 1f;
        private const float autoSaveInterval = 180f;

        private static MobileBootstrap instance = null;

        private readonly HashSet<int> scaledCanvases = new HashSet<int>();
        private float nextCanvasScan = 0f;
        private float nextAutoSave = 0f;

        private SystemGameManager systemGameManager = null;
        private GameObject touchButtonsCanvas = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize() {
            if (Application.isMobilePlatform == false || instance != null) {
                return;
            }
            GameObject bootstrapObject = new GameObject("MobileBootstrap");
            instance = bootstrapObject.AddComponent<MobileBootstrap>();
            DontDestroyOnLoad(bootstrapObject);
        }

        private void Awake() {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            nextAutoSave = Time.unscaledTime + autoSaveInterval;
        }

        private void OnDestroy() {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode) {
            nextCanvasScan = 0f;
        }

        private void Update() {
            if (Time.unscaledTime >= nextCanvasScan) {
                nextCanvasScan = Time.unscaledTime + canvasScanInterval;
                ScaleCanvases();
                UpdateTouchButtons();
            }
            if (Time.unscaledTime >= nextAutoSave) {
                nextAutoSave = Time.unscaledTime + autoSaveInterval;
                AutoSave();
            }
        }

        private void OnApplicationPause(bool paused) {
            if (paused) {
                AutoSave();
                PlayerPrefs.Save();
            }
        }

        private void OnApplicationFocus(bool hasFocus) {
            if (hasFocus == false) {
                PlayerPrefs.Save();
            }
        }

        private SystemGameManager GetSystemGameManager() {
            if (systemGameManager == null) {
                systemGameManager = FindAnyObjectByType<SystemGameManager>();
            }
            return systemGameManager;
        }

        private bool PlayerInGame() {
            SystemGameManager gameManager = GetSystemGameManager();
            if (gameManager == null || SystemGameManager.IsShuttingDown) {
                return false;
            }
            PlayerManagerClient playerManagerClient = gameManager.PlayerManagerClient;
            return playerManagerClient != null && playerManagerClient.PlayerUnitSpawned && playerManagerClient.UnitController != null;
        }

        private void AutoSave() {
            if (PlayerInGame() == false) {
                return;
            }
            SystemGameManager gameManager = GetSystemGameManager();
            if (gameManager.GameMode != GameMode.Local || gameManager.SaveManager == null) {
                return;
            }
            try {
                gameManager.SaveManager.SaveGame(gameManager.PlayerManagerClient.UnitController.CharacterSaveManager.SaveData, true);
            } catch (System.Exception exception) {
                Debug.LogWarning($"MobileBootstrap.AutoSave(): {exception.Message}");
            }
        }

        /// <summary>
        /// make every screen space canvas scale with the screen height so buttons and text are finger sized on phones
        /// </summary>
        private void ScaleCanvases() {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases) {
                if (canvas == null || canvas.isRootCanvas == false || canvas.renderMode == RenderMode.WorldSpace) {
                    continue;
                }
                int id = canvas.GetInstanceID();
                if (scaledCanvases.Contains(id)) {
                    continue;
                }
                scaledCanvases.Add(id);

                CanvasScaler canvasScaler = canvas.GetComponent<CanvasScaler>();
                if (canvasScaler == null) {
                    canvasScaler = canvas.gameObject.AddComponent<CanvasScaler>();
                }
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = new Vector2(referenceHeight * 16f / 9f, referenceHeight);
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 1f;
            }
        }

        private void UpdateTouchButtons() {
            bool show = MobileInput.TouchActive && PlayerInGame();
            if (show && touchButtonsCanvas == null) {
                CreateTouchButtons();
            }
            if (touchButtonsCanvas != null && touchButtonsCanvas.activeSelf != show) {
                touchButtonsCanvas.SetActive(show);
            }
        }

        private void CreateTouchButtons() {
            touchButtonsCanvas = new GameObject("MobileTouchButtons");
            DontDestroyOnLoad(touchButtonsCanvas);
            Canvas canvas = touchButtonsCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            touchButtonsCanvas.AddComponent<GraphicRaycaster>();
            // the canvas is scaled by ScaleCanvases on the next scan

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            CreateTouchButton(touchButtonsCanvas.transform, font, "Zıpla", new Vector2(-70f, 230f), "JUMP");
            CreateTouchButton(touchButtonsCanvas.transform, font, "Hedef", new Vector2(-70f, 330f), "NEXTTARGET");
        }

        private void CreateTouchButton(Transform parent, Font font, string label, Vector2 position, string actionName) {
            GameObject buttonObject = new GameObject(label + "Button");
            buttonObject.transform.SetParent(parent, false);
            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 0f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = new Vector2(84f, 84f);

            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.1f, 0.08f, 0.06f, 0.55f);

            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.7f, 0.4f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
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
            text.fontSize = 20;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.95f, 0.9f, 0.8f, 1f);
            text.raycastTarget = false;
        }
    }
}
