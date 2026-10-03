using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// On phones there is no console to read error messages. This collects everything the game reports while it runs
    /// (errors, exceptions, unhandled exceptions from any thread, failed background tasks, low memory warnings,
    /// warnings and the latest log lines) and shows a small "Durum" (status) button at the top of the screen.
    /// The button turns red and shows a count when an error happens. Tapping it opens a scrollable report with the
    /// game state; "Kopyala" copies the full report to the clipboard so it can be pasted into a message.
    /// Errors are also written to a file, so if the game crashes the next launch shows what happened before the crash.
    /// </summary>
    public class ErrorOverlay : MonoBehaviour {

        private const int maxErrors = 40;
        private const int maxWarnings = 25;
        private const int maxLogLines = 40;
        private const int maxStackLines = 6;
        private const string logFileName = "durum_kaydi.txt";
        private const string previousLogFileName = "durum_kaydi_onceki.txt";
        private const string cleanMarker = "#OTURUM_DURDU";

        private class ErrorEntry {
            public string time;
            public string message;
            public string stack;
            public int count;
        }

        private static ErrorOverlay instance = null;
        private static readonly object entriesLock = new object();
        private static readonly List<ErrorEntry> errors = new List<ErrorEntry>();
        private static readonly List<string> warnings = new List<string>();
        private static readonly List<string> logLines = new List<string>();
        private static int version = 0;
        private static string logFilePath = null;
        private static string previousSessionReport = string.Empty;
        private static bool previousSessionEndedUnexpectedly = false;
        private static float startTime = 0f;

        private int shownVersion = -1;
        private float fps = 0f;
        // smoothed CPU and GPU time per frame in milliseconds (needs "Frame Timing Stats" in the player settings);
        // tells whether a low frame rate comes from the scripts (CPU) or from drawing (GPU)
        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        private float cpuFrameMs = 0f;
        private float gpuFrameMs = 0f;
        // what the last finger landed on: tells whether a button tap reached the button or something covered it
        private string lastTouchTarget = "-";
        private readonly List<UnityEngine.EventSystems.RaycastResult> touchHits = new List<UnityEngine.EventSystems.RaycastResult>();
        private float nextStatusRefresh = 0f;
        private GameObject canvasObject = null;
        private Text badgeText = null;
        private Image badgeImage = null;
        private GameObject panelObject = null;
        private Text panelText = null;
        private ScrollRect scrollRect = null;
        private Font font = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() {
            if (Application.isMobilePlatform == false || instance != null) {
                return;
            }
            startTime = Time.realtimeSinceStartup;
            PrepareLogFile();

            Application.logMessageReceivedThreaded += HandleLog;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += HandleUnobservedTaskException;
            Application.lowMemory += HandleLowMemory;

            GameObject overlayObject = new GameObject("ErrorOverlay");
            instance = overlayObject.AddComponent<ErrorOverlay>();
            DontDestroyOnLoad(overlayObject);
        }

        // ---- hata bildirimi (HataBildirici) için ----

        /// <summary>önceki oturum arka plana geçmeden kapandı mı (çökme ya da zorla kapatma)</summary>
        public static bool PreviousSessionEndedUnexpectedly { get { return previousSessionEndedUnexpectedly; } }

        /// <summary>önceki oturumun kayıt dosyası (hatalar ve son satırlar)</summary>
        public static string PreviousSessionReport { get { return previousSessionReport; } }

        /// <summary>oyunun durumu: sürüm, cihaz, kare hızı, sahneler, oyuncu (ana iş parçacığında çağrılmalı)</summary>
        public static string StatusSummary() {
            return BuildStatus();
        }

        /// <summary>son kayıt satırları, yeniden eskiye</summary>
        public static string RecentLogLines(int count) {
            StringBuilder builder = new StringBuilder();
            lock (entriesLock) {
                for (int i = logLines.Count - 1, n = 0; i >= 0 && n < count; i--, n++) {
                    builder.Append(logLines[i]).Append('\n');
                }
            }
            return builder.ToString();
        }

        // ---- collecting ----

        private static void HandleLog(string message, string stackTrace, LogType logType) {
            string time = Timestamp();
            lock (entriesLock) {
                AddLogLine(time + " [" + logType + "] " + FirstLine(message));
                if (logType == LogType.Warning) {
                    if (warnings.Contains(message) == false) {
                        warnings.Add(message);
                        if (warnings.Count > maxWarnings) {
                            warnings.RemoveAt(0);
                        }
                    }
                    version++;
                    return;
                }
                if (logType == LogType.Log) {
                    version++;
                    return;
                }
                AddError(time, "[" + logType + "] " + message, stackTrace);
            }
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args) {
            Exception exception = args.ExceptionObject as Exception;
            lock (entriesLock) {
                AddError(Timestamp(), "[Yakalanmamış hata] " + (exception != null ? exception.GetType().Name + ": " + exception.Message : "bilinmiyor"),
                    exception != null ? exception.StackTrace : string.Empty);
            }
        }

        private static void HandleUnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs args) {
            Exception exception = args.Exception != null && args.Exception.InnerException != null ? args.Exception.InnerException : args.Exception;
            lock (entriesLock) {
                AddError(Timestamp(), "[Arka plan görevi] " + (exception != null ? exception.GetType().Name + ": " + exception.Message : "bilinmiyor"),
                    exception != null ? exception.StackTrace : string.Empty);
            }
        }

        private static void HandleLowMemory() {
            lock (entriesLock) {
                string message = "Cihazın belleği azaldı (" + SystemInfo.systemMemorySize + " MB toplam)";
                if (warnings.Contains(message) == false) {
                    warnings.Add(message);
                }
                AddLogLine(Timestamp() + " [Bellek] " + message);
                version++;
            }
        }

        // must be called with entriesLock held
        private static void AddError(string time, string message, string stackTrace) {
            foreach (ErrorEntry existing in errors) {
                if (existing.message == message) {
                    existing.count++;
                    version++;
                    return;
                }
            }
            ErrorEntry entry = new ErrorEntry() { time = time, message = message, stack = ShortenStack(stackTrace), count = 1 };
            errors.Add(entry);
            if (errors.Count > maxErrors) {
                errors.RemoveAt(0);
            }
            version++;
            AppendToFile(time + " " + message + "\n" + entry.stack);
        }

        // must be called with entriesLock held
        private static void AddLogLine(string line) {
            logLines.Add(line);
            if (logLines.Count > maxLogLines) {
                logLines.RemoveAt(0);
            }
        }

        private static string Timestamp() {
            return DateTime.Now.ToString("HH:mm:ss");
        }

        private static string FirstLine(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }
            int newline = text.IndexOf('\n');
            string line = newline >= 0 ? text.Substring(0, newline) : text;
            return line.Length > 200 ? line.Substring(0, 200) + "..." : line;
        }

        private static string ShortenStack(string stackTrace) {
            if (string.IsNullOrEmpty(stackTrace)) {
                return string.Empty;
            }
            string[] lines = stackTrace.Split('\n');
            StringBuilder builder = new StringBuilder();
            int added = 0;
            foreach (string line in lines) {
                string trimmed = line.Trim();
                if (trimmed.Length == 0) {
                    continue;
                }
                builder.Append("   ").Append(trimmed).Append('\n');
                added++;
                if (added >= maxStackLines) {
                    break;
                }
            }
            return builder.ToString();
        }

        // ---- crash memory (errors are kept in a file so the next launch can show them) ----

        private static void PrepareLogFile() {
            try {
                logFilePath = Path.Combine(Application.persistentDataPath, logFileName);
                string previousPath = Path.Combine(Application.persistentDataPath, previousLogFileName);
                if (File.Exists(logFilePath)) {
                    string previous = File.ReadAllText(logFilePath);
                    // the marker is written when the app goes to the background or quits; without it the game was killed while running
                    previousSessionEndedUnexpectedly = previous.TrimEnd().EndsWith(cleanMarker) == false;
                    previousSessionReport = previous.Replace(cleanMarker, string.Empty).Trim();
                    File.Copy(logFilePath, previousPath, true);
                }
                File.WriteAllText(logFilePath, "Oturum başladı " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n");
            } catch (Exception) {
                logFilePath = null;
            }
        }

        private static void AppendToFile(string text) {
            if (logFilePath == null) {
                return;
            }
            lock (entriesLock) {
                try {
                    File.AppendAllText(logFilePath, text + "\n");
                } catch (Exception) {
                    // the report on screen still works without the file
                }
            }
        }

        private void OnApplicationPause(bool paused) {
            AppendToFile(paused ? cleanMarker : "Oyuna dönüldü " + Timestamp());
        }

        private void OnApplicationQuit() {
            AppendToFile(cleanMarker);
        }

        // ---- report ----

        private static string BuildReport(bool includeStack) {
            StringBuilder builder = new StringBuilder();
            builder.Append(BuildStatus()).Append('\n');
            lock (entriesLock) {
                builder.Append("\n== HATALAR (").Append(errors.Count).Append(") ==\n");
                if (errors.Count == 0) {
                    builder.Append("Hata yok.\n");
                }
                for (int i = errors.Count - 1; i >= 0; i--) {
                    ErrorEntry entry = errors[i];
                    builder.Append(entry.time).Append(' ').Append(entry.count > 1 ? "(" + entry.count + " kez) " : "").Append(entry.message).Append('\n');
                    if (includeStack && entry.stack.Length > 0) {
                        builder.Append(entry.stack);
                    }
                }
                if (warnings.Count > 0) {
                    builder.Append("\n== UYARILAR (").Append(warnings.Count).Append(") ==\n");
                    for (int i = warnings.Count - 1; i >= 0; i--) {
                        builder.Append("- ").Append(FirstLine(warnings[i])).Append('\n');
                    }
                }
                if (logLines.Count > 0) {
                    builder.Append("\n== SON KAYITLAR ==\n");
                    for (int i = logLines.Count - 1; i >= 0; i--) {
                        builder.Append(logLines[i]).Append('\n');
                    }
                }
            }
            if (previousSessionReport.Length > 0) {
                builder.Append("\n== ÖNCEKİ OTURUM").Append(previousSessionEndedUnexpectedly ? " (beklenmedik şekilde kapandı, çökme olabilir)" : "").Append(" ==\n");
                builder.Append(previousSessionReport.Length > 4000 ? previousSessionReport.Substring(previousSessionReport.Length - 4000) : previousSessionReport).Append('\n');
            }
            return builder.ToString();
        }

        /// <summary>
        /// a short summary of the game state, so a screenshot shows where things stopped
        /// </summary>
        private static string BuildStatus() {
            StringBuilder builder = new StringBuilder();
            builder.Append("Sürüm ").Append(Application.version)
                .Append(" | ").Append(SystemInfo.deviceModel)
                .Append(" | ").Append(SystemInfo.operatingSystem)
                .Append('\n').Append(SystemInfo.graphicsDeviceType).Append(' ').Append(SystemInfo.graphicsDeviceName)
                .Append(" | Bellek ").Append(SystemInfo.systemMemorySize).Append(" MB")
                .Append(" | ").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(" | FPS ").Append(instance != null ? instance.fps.ToString("0") : "?")
                .Append(" | Süre ").Append(((int)(Time.realtimeSinceStartup - startTime))).Append(" sn").Append('\n');
            builder.Append("Kare süresi: işlemci ").Append(instance != null && instance.cpuFrameMs > 0f ? instance.cpuFrameMs.ToString("0.0") + " ms" : "?")
                .Append(" | ekran kartı ").Append(instance != null && instance.gpuFrameMs > 0f ? instance.gpuFrameMs.ToString("0.0") + " ms" : "?")
                .Append(" | ekran ").Append(Screen.currentResolution.refreshRateRatio.value.ToString("0")).Append(" Hz")
                .Append(" | hedef ").Append(Application.targetFrameRate)
                .Append(" | kalite ").Append(QualitySettings.names[QualitySettings.GetQualityLevel()])
                .Append(" | pil ").Append((SystemInfo.batteryLevel * 100f).ToString("0")).Append('%').Append('\n');
            builder.Append("Sahneler:");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {
                UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                builder.Append(' ').Append(scene.name).Append(scene.isLoaded ? "" : "(yükleniyor)");
            }
            // which input devices the game sees; decides whether the on-screen controls are shown
            builder.Append("\nGiriş: dokunmatik ").Append(UnityEngine.InputSystem.Touchscreen.current != null ? "var" : "yok")
                .Append(" | fare ").Append(UnityEngine.InputSystem.Mouse.current != null ? "bağlı" : "yok")
                .Append(" | ekran tuşları ").Append(MobileInput.TouchActive ? "açık" : "kapalı");
            builder.Append("\nSon dokunuş: ").Append(instance != null ? instance.lastTouchTarget : "-");
            try {
                SystemGameManager gameManager = FindAnyObjectByType<SystemGameManager>();
                if (gameManager == null) {
                    builder.Append("\nOyun yöneticisi bulunamadı");
                } else {
                    PlayerManagerClient playerManagerClient = gameManager.PlayerManagerClient;
                    builder.Append("\nOyuncu karakteri: ").Append(playerManagerClient != null && playerManagerClient.PlayerUnitSpawned ? "oluştu" : "yok");
                    if (playerManagerClient != null && playerManagerClient.UnitController != null) {
                        builder.Append(" (").Append(playerManagerClient.UnitController.gameObject.name).Append(')');
                    }
                    CharacterCreatorManager creator = gameManager.CharacterCreatorManager;
                    builder.Append(" | Önizleme karakteri: ").Append(creator != null && creator.UnitController != null ? "var" : "yok");
                    CameraManager cameraManager = gameManager.CameraManager;
                    if (cameraManager != null && cameraManager.CharacterPreviewCamera != null) {
                        builder.Append(" | Önizleme kamerası: ").Append(cameraManager.CharacterPreviewCamera.enabled ? "açık" : "kapalı");
                    }
                }
            } catch (Exception exception) {
                builder.Append("\nDurum okunamadı: ").Append(exception.Message);
            }
            return builder.ToString();
        }

        private void RecordTouchTarget() {
            UnityEngine.InputSystem.Touchscreen touchscreen = UnityEngine.InputSystem.Touchscreen.current;
            UnityEngine.EventSystems.EventSystem eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (touchscreen == null) {
                return;
            }
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in touchscreen.touches) {
                if (touch.press.wasPressedThisFrame == false) {
                    continue;
                }
                Vector2 position = touch.position.ReadValue();
                string target = "oyun dünyası";
                if (eventSystem != null) {
                    UnityEngine.EventSystems.PointerEventData pointer = new UnityEngine.EventSystems.PointerEventData(eventSystem);
                    pointer.position = position;
                    touchHits.Clear();
                    eventSystem.RaycastAll(pointer, touchHits);
                    if (touchHits.Count > 0 && touchHits[0].gameObject != null) {
                        GameObject hit = touchHits[0].gameObject;
                        Canvas hitCanvas = hit.GetComponentInParent<Canvas>();
                        target = (hit.transform.parent != null ? hit.transform.parent.name + "/" : "") + hit.name
                            + (hitCanvas != null ? " [" + hitCanvas.rootCanvas.name + " " + hitCanvas.rootCanvas.sortingOrder + "]" : "");
                    }
                }
                lastTouchTarget = ((int)position.x) + "," + ((int)position.y) + " -> " + target;
            }
        }

        // ---- screen ----

        private void Update() {
            if (Time.unscaledDeltaTime > 0f) {
                fps = Mathf.Lerp(fps, 1f / Time.unscaledDeltaTime, 0.05f);
            }
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0) {
                cpuFrameMs = Mathf.Lerp(cpuFrameMs, (float)frameTimings[0].cpuFrameTime, 0.05f);
                gpuFrameMs = Mathf.Lerp(gpuFrameMs, (float)frameTimings[0].gpuFrameTime, 0.05f);
            }
            RecordTouchTarget();
            if (canvasObject == null) {
                CreateOverlay();
            }
            // hatalar artık kendiliğinden geliştiriciye gider (HataBildirici); "Durum" düğmesi yalnız test için,
            // Seçenekler > Akıcılık > Hata konsolu açıkken görünür (göstergelerin üstüne binmesin)
            bool badgeVisible = OyunAyarlari.HataKonsolu || panelObject.activeSelf;
            if (badgeImage.gameObject.activeSelf != badgeVisible) {
                badgeImage.gameObject.SetActive(badgeVisible);
            }
            int currentVersion;
            int errorCount;
            lock (entriesLock) {
                currentVersion = version;
                errorCount = errors.Count;
            }
            // refresh the open panel every second so the status lines stay current
            bool refreshPanel = panelObject.activeSelf && Time.unscaledTime >= nextStatusRefresh;
            if (currentVersion == shownVersion && refreshPanel == false) {
                return;
            }
            nextStatusRefresh = Time.unscaledTime + 1f;
            shownVersion = currentVersion;
            bool alert = errorCount > 0 || previousSessionEndedUnexpectedly;
            badgeText.text = errorCount > 0 ? "Hata (" + errorCount + ")" : (previousSessionEndedUnexpectedly ? "Çökme?" : "Durum");
            badgeImage.color = alert ? new Color(0.7f, 0.05f, 0.05f, 0.85f) : new Color(0.15f, 0.15f, 0.15f, 0.45f);
            if (panelObject.activeSelf) {
                panelText.text = BuildReport(false);
            }
        }

        private void CreateOverlay() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            canvasObject = new GameObject("ErrorOverlayCanvas");
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1422f, 800f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();

            // small button at the top center
            GameObject badgeObject = CreateButton(canvasObject.transform, "Durum", new Vector2(0.5f, 1f), new Vector2(-360f, -24f), new Vector2(140f, 40f),
                new Color(0.15f, 0.15f, 0.15f, 0.45f), TogglePanel, out badgeText);
            badgeImage = badgeObject.GetComponent<Image>();

            // scrollable report
            panelObject = new GameObject("ErrorPanel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.03f, 0.04f);
            panelRect.anchorMax = new Vector2(0.97f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.9f);

            GameObject viewportObject = new GameObject("Viewport");
            viewportObject.transform.SetParent(panelObject.transform, false);
            RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(14f, 74f);
            viewportRect.offsetMax = new Vector2(-14f, -10f);
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);
            viewportObject.AddComponent<RectMask2D>();

            GameObject textObject = new GameObject("ReportText");
            textObject.transform.SetParent(viewportObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 1f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = new Vector2(0f, 100f);
            panelText = textObject.AddComponent<Text>();
            panelText.font = font;
            panelText.fontSize = 17;
            panelText.color = new Color(1f, 0.88f, 0.82f, 1f);
            panelText.alignment = TextAnchor.UpperLeft;
            panelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            panelText.verticalOverflow = VerticalWrapMode.Overflow;
            panelText.raycastTarget = false;
            ContentSizeFitter sizeFitter = textObject.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect = panelObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = textRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            Text unusedText;
            CreateButton(panelObject.transform, "Kopyala", new Vector2(0.5f, 0f), new Vector2(-190f, 38f), new Vector2(170f, 52f),
                new Color(0.2f, 0.35f, 0.6f, 1f), CopyReport, out unusedText);
            CreateButton(panelObject.transform, "Temizle", new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(170f, 52f),
                new Color(0.35f, 0.25f, 0.1f, 1f), ClearReport, out unusedText);
            CreateButton(panelObject.transform, "Kapat", new Vector2(0.5f, 0f), new Vector2(190f, 38f), new Vector2(170f, 52f),
                new Color(0.3f, 0.3f, 0.3f, 1f), TogglePanel, out unusedText);
            panelObject.SetActive(false);
        }

        private GameObject CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, out Text labelText) {
            GameObject buttonObject = new GameObject(label + "Button");
            buttonObject.transform.SetParent(parent, false);
            RectTransform rectTransform = buttonObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchor;
            rectTransform.anchorMax = anchor;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            GameObject textObject = new GameObject("Label");
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            labelText = textObject.AddComponent<Text>();
            labelText.font = font;
            labelText.text = label;
            labelText.fontSize = 20;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            return buttonObject;
        }

        private void TogglePanel() {
            bool show = panelObject.activeSelf == false;
            panelObject.SetActive(show);
            if (show) {
                panelText.text = BuildReport(false);
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void ClearReport() {
            lock (entriesLock) {
                errors.Clear();
                warnings.Clear();
                logLines.Clear();
                version++;
            }
            previousSessionReport = string.Empty;
            previousSessionEndedUnexpectedly = false;
            panelText.text = BuildReport(false);
        }

        private void CopyReport() {
            GUIUtility.systemCopyBuffer = "Ötüken Destanı durum raporu\n" + BuildReport(true);
            MobileFeedback.Success();
        }
    }
}
