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
        private static Vector2 joystickValue = Vector2.zero;
        private static bool joystickHeld = false;

        /// <summary>
        /// direction of the on-screen movement stick, -1..1 on each axis
        /// </summary>
        public static Vector2 JoystickValue { get { return joystickValue; } }

        public static bool JoystickHeld { get { return joystickHeld; } }

        public static void SetJoystick(Vector2 value, bool held) {
            joystickValue = value;
            joystickHeld = held;
        }
        private static readonly List<string> consumedPresses = new List<string>();

        public static bool TouchActive {
            get {
                return Touchscreen.current != null && (Application.isMobilePlatform || Mouse.current == null);
            }
        }

        /// <summary>
        /// true while at least one finger is on the screen
        /// </summary>
        public static bool AnyTouchPressed {
            get {
                Touchscreen touchscreen = Touchscreen.current;
                if (touchscreen == null) {
                    return false;
                }
                foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches) {
                    if (touch.press.isPressed) {
                        return true;
                    }
                }
                return false;
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

        // the on-screen attack button: attack the current enemy target, or the nearest enemy (PlayerController.HandleMobileAttack)
        private static float attackRequestTime = -10f;

        public static void RequestAttack() {
            attackRequestTime = Time.unscaledTime;
        }

        /// <summary>
        /// true once after the attack button was pressed; a press older than half a second (for example made while
        /// the player could not act) is dropped instead of firing later by surprise
        /// </summary>
        public static bool ConsumeAttackRequest() {
            bool requested = Time.unscaledTime - attackRequestTime < 0.5f;
            attackRequestTime = -10f;
            return requested;
        }
    }

    /// <summary>
    /// The game compares and parses many internal names and numbers as text. On a phone set to Turkish, the device
    /// culture lowercases 'I' to 'ı' and writes decimals with a comma, which breaks those comparisons and number parsing.
    /// The game logic runs with the invariant culture; only text shown to the player is Turkish.
    /// </summary>
    public static class CultureGuard {

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize() {
            System.Globalization.CultureInfo invariant = System.Globalization.CultureInfo.InvariantCulture;
            System.Globalization.CultureInfo.DefaultThreadCurrentCulture = invariant;
            System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = invariant;
            System.Threading.Thread.CurrentThread.CurrentCulture = invariant;
            System.Threading.Thread.CurrentThread.CurrentUICulture = invariant;
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

        // in-game HUD canvases (built for 1:1 pixels on a monitor) are laid out as if the screen was this many pixels tall,
        // which makes them about 1.35x larger on a 1080p phone and keeps the same physical size on every phone
        private const float hudReferenceHeight = 800f;
        // windows (bag, character, quests, vendor...) and their tooltips have 32 pixel slots and small text; on a phone they
        // need to be bigger still to be read and tapped. Windows that end up larger than the screen are shrunk to fit by
        // FitWindowsOnScreen, so they are never smaller than with the HUD size.
        private const float windowReferenceHeight = 500f;
        private static readonly HashSet<string> windowCanvasNames = new HashSet<string>() {
            "PopupWindowContainerCanvas", "ToolTipCanvas", "HandIconCanvas"
        };
        // full screen menus were designed on a 1920x1080 canvas; keep that design and fit it to the screen height
        private static readonly Vector2 menuReferenceResolution = new Vector2(1920f, 1080f);
        private const float canvasScanInterval = 1f;
        private const float autoSaveInterval = 180f;

        private static MobileBootstrap instance = null;

        private readonly HashSet<int> scaledCanvases = new HashSet<int>();
        private readonly Vector3[] windowCorners = new Vector3[4];
        private float nextCanvasScan = 0f;
        private float nextWindowFit = 0f;
        private float nextAutoSave = 0f;

        private SystemGameManager systemGameManager = null;
        private MobileHud mobileHud = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize() {
            if (Application.isMobilePlatform == false || instance != null) {
                return;
            }
            GameObject bootstrapObject = new GameObject("MobileBootstrap");
            instance = bootstrapObject.AddComponent<MobileBootstrap>();
            DontDestroyOnLoad(bootstrapObject);
        }

        // bump when the phone layout defaults change, so they are applied once more on existing installs
        private const int mobileUiDefaultsVersion = 2;

        /// <summary>
        /// The desktop layout shows seven action bars and a large chat log, which cover most of a phone screen and
        /// the on-screen controls. On phones keep the main action bar and the system bar, and hide the rest.
        /// Players can turn them back on in Ayarlar > Arayüz.
        /// </summary>
        private static void ApplyMobileUiDefaults() {
            if (PlayerPrefs.GetInt("mobile-ui-defaults", 0) >= mobileUiDefaultsVersion) {
                return;
            }
            PlayerPrefs.SetInt("UseActionBar1", 1);
            for (int i = 2; i <= 7; i++) {
                PlayerPrefs.SetInt("UseActionBar" + i, 0);
            }
            PlayerPrefs.SetInt("UseMessageLog", 0);
            // single player: the empty group frame column sat on the left edge over the Çanta and Görevler buttons
            PlayerPrefs.SetInt("UseGroupUnitFrames", 0);
            // keep the HUD panels locked so a finger cannot drag them around by accident
            PlayerPrefs.SetInt("LockUI", 1);
            PlayerPrefs.SetInt("mobile-ui-defaults", mobileUiDefaultsVersion);
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeDefaults() {
            if (Application.isMobilePlatform) {
                ApplyMobileUiDefaults();
                // ilk açılışta cihaza göre grafik kalitesi
                OyunAyarlari.IlkAcilisAyari();
            }
        }

        private void Awake() {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            nextAutoSave = Time.unscaledTime + autoSaveInterval;
            // oyuncunun Seçenekler penceresindeki ayarları (FPS sınırı, çözünürlük, gölge...)
            OyunAyarlari.Uygula();
            // oyun açıkken hatırlatma bildirimi gelmesin
            Bildirimler.OnPlanaGecti();
        }

        private void OnDestroy() {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode) {
            nextCanvasScan = 0f;
            OyunAyarlari.SahneYuklendi();
        }

        private void Update() {
            if (Time.unscaledTime >= nextCanvasScan) {
                nextCanvasScan = Time.unscaledTime + canvasScanInterval;
                UpdateTouchButtons();
                ScaleCanvases();
            }
            if (Time.unscaledTime >= nextWindowFit) {
                nextWindowFit = Time.unscaledTime + 0.2f;
                FitWindowsOnScreen();
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
                // ertesi akşam için "günlük armağan" hatırlatması
                Bildirimler.ArkaPlan();
                CevrimdisiKazanc.ArkaPlan();
                PlayerPrefs.Save();
            } else {
                Bildirimler.OnPlanaGecti();
                CevrimdisiKazanc.OnPlanaGecti();
            }
        }

        private void OnApplicationQuit() {
            Bildirimler.ArkaPlan();
            CevrimdisiKazanc.ArkaPlan();
            PlayerPrefs.Save();
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
        private const string ErrorOverlayCanvasName = "ErrorOverlayCanvas";

        private void ScaleCanvases() {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases) {
                if (canvas == null || canvas.isRootCanvas == false || canvas.renderMode == RenderMode.WorldSpace
                    || canvas.name == ErrorOverlayCanvasName || canvas.name == GameGuide.CanvasName || canvas.name == GunlukArmagan.CanvasName
                    || canvas.name == SeceneklerPenceresi.CanvasName || canvas.name == IsinlanmaPenceresi.CanvasName
                    || canvas.name == HataBildirici.CanvasName || canvas.name == GunlukGorevler.CanvasName || canvas.name == GorevOku.CanvasName
                    || canvas.name == DunyaHaritasi.CanvasName || canvas.name == CevrimdisiKazanc.CanvasName
                    || canvas.name == Demirci.CanvasName || canvas.name == Gelisim.CanvasName || canvas.name == Canta.CanvasName
                    || canvas.name == BolgeGirisi.CanvasName
                    || canvas.transform.root.name == "[Graphy]"
                    || canvas.transform.root.name == "IngameDebugConsole") {
                    continue;
                }
                int id = canvas.GetInstanceID();
                if (scaledCanvases.Contains(id)) {
                    continue;
                }
                scaledCanvases.Add(id);

                // Layering. The game's HUD canvases (action bar, unit frames, mini map...) use sorting order 0-1, windows 2,
                // tooltips and menus 7-10. The on-screen controls must be above the HUD (an invisible HUD panel at the same
                // order swallowed taps on Çanta and Görevler) and below every window. Move windows and menus up by 10 and
                // put the controls at 5 (MobileHud.SortingOrder).
                if (canvas.name != MobileHud.CanvasName && canvas.sortingOrder >= 2) {
                    canvas.sortingOrder += 10;
                }

                CanvasScaler canvasScaler = canvas.GetComponent<CanvasScaler>();
                // canvases that already scale with the screen hold full screen menus (main menu, settings, character creation)
                bool fullScreenMenu = (canvasScaler != null && canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    || canvas.name.Contains("MainMenu");
                if (canvasScaler == null) {
                    canvasScaler = canvas.gameObject.AddComponent<CanvasScaler>();
                }
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                float referenceHeight = windowCanvasNames.Contains(canvas.name) ? windowReferenceHeight : hudReferenceHeight;
                canvasScaler.referenceResolution = fullScreenMenu ? menuReferenceResolution : new Vector2(referenceHeight * 16f / 9f, referenceHeight);
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 1f;
            }
        }

        /// <summary>
        /// the windows were laid out for large monitors; shrink any open window that does not fit on the phone screen
        /// and slide it back inside if part of it is off screen
        /// </summary>
        private void FitWindowsOnScreen() {
            CloseableWindow[] windows = FindObjectsByType<CloseableWindow>(FindObjectsSortMode.None);
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            foreach (CloseableWindow window in windows) {
                if (window == null || window.IsOpen == false) {
                    continue;
                }
                RectTransform rectTransform = window.transform as RectTransform;
                Canvas canvas = window.GetComponentInParent<Canvas>();
                if (rectTransform == null || canvas == null || canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) {
                    continue;
                }
                rectTransform.GetWorldCorners(windowCorners);
                float width = windowCorners[2].x - windowCorners[0].x;
                float height = windowCorners[2].y - windowCorners[0].y;
                if (width <= 1f || height <= 1f) {
                    continue;
                }
                if (width > screenWidth * 1.01f || height > screenHeight * 1.01f) {
                    float fitFactor = Mathf.Min(screenWidth / width, screenHeight / height);
                    rectTransform.localScale = rectTransform.localScale * fitFactor;
                    rectTransform.GetWorldCorners(windowCorners);
                }
                float moveX = 0f;
                float moveY = 0f;
                if (windowCorners[0].x < -1f) {
                    moveX = -windowCorners[0].x;
                } else if (windowCorners[2].x > screenWidth + 1f) {
                    moveX = screenWidth - windowCorners[2].x;
                }
                if (windowCorners[0].y < -1f) {
                    moveY = -windowCorners[0].y;
                } else if (windowCorners[2].y > screenHeight + 1f) {
                    moveY = screenHeight - windowCorners[2].y;
                }
                if (moveX != 0f || moveY != 0f) {
                    rectTransform.position = rectTransform.position + new Vector3(moveX, moveY, 0f);
                }
            }
        }

        private void UpdateTouchButtons() {
            bool inGame = PlayerInGame();
            bool show = MobileInput.TouchActive && inGame;
            if (show && mobileHud == null) {
                mobileHud = MobileHud.Create();
            }
            if (mobileHud != null && mobileHud.gameObject.activeSelf != show) {
                mobileHud.gameObject.SetActive(show);
            }
            // the how-to-play guide: opens by itself the first time a character enters the world,
            // and the main menu gets a "Nasıl Oynanır" button
            if (inGame) {
                GameGuide.ShowFirstTimeIfNeeded();
            }
            GameGuide.SetMenuLauncherVisible(inGame == false && MainMenuOpen());
            // the daily gift: opens a few seconds after entering the world, after the guide is closed
            GunlukArmagan.Tick(GetSystemGameManager(), inGame);
            try {
                // günlük görevler: oyuncunun öldürmelerini dinler, bugün girilen haritaları sayar
                GunlukGorevler.Tick(GetSystemGameManager(), inGame);
            } catch (System.Exception exception) {
                Debug.LogWarning($"MobileBootstrap: GunlukGorevler.Tick(): {exception.Message}");
            }
            try {
                // ejderha bineği: 5. seviyede öğrenilir, HUD'daki Binek düğmesi
                Binek.Tick(GetSystemGameManager(), inGame);
            } catch (System.Exception exception) {
                Debug.LogWarning($"MobileBootstrap: Binek.Tick(): {exception.Message}");
            }
            try {
                // ekranda sıradaki görev hedefini gösteren ok
                GorevOku.Tick(GetSystemGameManager(), inGame);
            } catch (System.Exception exception) {
                Debug.LogWarning($"MobileBootstrap: GorevOku.Tick(): {exception.Message}");
            }
            try {
                // ara verip dönene "sen yokken" kazancı
                CevrimdisiKazanc.Tick(GetSystemGameManager(), inGame);
            } catch (System.Exception exception) {
                Debug.LogWarning($"MobileBootstrap: CevrimdisiKazanc.Tick(): {exception.Message}");
            }
            try {
                Gelisim.Tick(GetSystemGameManager(), inGame);
            } catch (System.Exception exception) {
                Debug.LogWarning($"MobileBootstrap: Gelisim.Tick(): {exception.Message}");
            }
            try {
                OyunAyarlari.Tick(GetSystemGameManager());
            } catch (System.Exception exception) {
                Debug.LogWarning($"MobileBootstrap: OyunAyarlari.Tick(): {exception.Message}");
            }
            try {
                // büyük yetenek çubuğu, sistem çubuğu üstte (Seçenekler > Oyun > Yetenek çubuğu)
                MobilArayuzDuzeni.Tick(GetSystemGameManager());
            } catch (System.Exception exception) {
                Debug.LogWarning($"MobileBootstrap: MobilArayuzDuzeni.Tick(): {exception.Message}");
            }
            Bildirimler.Tick(inGame);
            if (inGame == false && MainMenuOpen()) {
                // ana menüye dönünce otomatik av kapanır (harita değişirken açık kalır)
                OtomatikAv.Kapat();
            }
        }

        private bool MainMenuOpen() {
            SystemGameManager gameManager = GetSystemGameManager();
            if (gameManager == null || SystemGameManager.IsShuttingDown || gameManager.UIManager == null) {
                return false;
            }
            CloseableWindow mainMenuWindow = gameManager.UIManager.mainMenuWindow;
            return mainMenuWindow != null && mainMenuWindow.IsOpen;
        }
    }
}
