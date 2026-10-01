using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// On phones there is no console to read error messages. This collects errors and exceptions while the game runs
    /// and shows a small "Hata" (error) button in the corner when something goes wrong. Tapping it lists the latest errors,
    /// and "Kopyala" copies the full report to the clipboard so it can be pasted into a message.
    /// </summary>
    public class ErrorOverlay : MonoBehaviour {

        private const int maxEntries = 25;
        private const int maxStackLines = 4;

        private class ErrorEntry {
            public string message;
            public string stack;
            public int count;
        }

        private static ErrorOverlay instance = null;
        private static readonly object entriesLock = new object();
        private static readonly List<ErrorEntry> entries = new List<ErrorEntry>();
        private static readonly List<string> warnings = new List<string>();
        private const int maxWarnings = 15;
        private static int version = 0;

        private int shownVersion = -1;
        private float fps = 0f;
        private float nextStatusRefresh = 0f;
        private GameObject canvasObject = null;
        private GameObject badgeObject = null;
        private Text badgeText = null;
        private Image badgeImage = null;
        private GameObject panelObject = null;
        private Text panelText = null;
        private Font font = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() {
            if (Application.isMobilePlatform == false || instance != null) {
                return;
            }
            Application.logMessageReceivedThreaded += HandleLog;
            GameObject overlayObject = new GameObject("ErrorOverlay");
            instance = overlayObject.AddComponent<ErrorOverlay>();
            DontDestroyOnLoad(overlayObject);
        }

        private static void HandleLog(string message, string stackTrace, LogType logType) {
            if (logType == LogType.Warning) {
                lock (entriesLock) {
                    if (warnings.Contains(message) == false) {
                        warnings.Add(message);
                        if (warnings.Count > maxWarnings) {
                            warnings.RemoveAt(0);
                        }
                    }
                }
                return;
            }
            if (logType != LogType.Error && logType != LogType.Exception && logType != LogType.Assert) {
                return;
            }
            lock (entriesLock) {
                foreach (ErrorEntry existing in entries) {
                    if (existing.message == message) {
                        existing.count++;
                        version++;
                        return;
                    }
                }
                entries.Add(new ErrorEntry() { message = message, stack = ShortenStack(stackTrace), count = 1 });
                if (entries.Count > maxEntries) {
                    entries.RemoveAt(0);
                }
                version++;
            }
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

        private static string BuildReport(bool includeStack) {
            StringBuilder builder = new StringBuilder();
            builder.Append(BuildStatus()).Append('\n');
            lock (entriesLock) {
                if (entries.Count == 0) {
                    builder.Append("Hata yok.\n");
                }
                for (int i = entries.Count - 1; i >= 0; i--) {
                    ErrorEntry entry = entries[i];
                    builder.Append(entry.count > 1 ? "(" + entry.count + "x) " : "").Append(entry.message).Append('\n');
                    if (includeStack && entry.stack.Length > 0) {
                        builder.Append(entry.stack);
                    }
                }
                if (warnings.Count > 0) {
                    builder.Append("\nUyarılar:\n");
                    for (int i = warnings.Count - 1; i >= 0; i--) {
                        builder.Append("- ").Append(warnings[i]).Append('\n');
                    }
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// a one line summary of the game state, so a screenshot shows where things stopped
        /// </summary>
        private static string BuildStatus() {
            StringBuilder builder = new StringBuilder();
            builder.Append("Sürüm ").Append(Application.version)
                .Append(" | ").Append(SystemInfo.deviceModel)
                .Append(" | ").Append(SystemInfo.graphicsDeviceType)
                .Append(" | ").Append(Screen.width).Append('x').Append(Screen.height)
                .Append(" | FPS ").Append(instance != null ? instance.fps.ToString("0") : "?").Append('\n');
            builder.Append("Sahneler:");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) {
                builder.Append(' ').Append(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).name);
            }
            try {
                SystemGameManager gameManager = FindAnyObjectByType<SystemGameManager>();
                if (gameManager == null) {
                    builder.Append(" | Oyun yöneticisi yok");
                } else {
                    PlayerManagerClient playerManagerClient = gameManager.PlayerManagerClient;
                    builder.Append(" | Oyuncu: ").Append(playerManagerClient != null && playerManagerClient.PlayerUnitSpawned ? "var" : "yok");
                    CharacterCreatorManager creator = gameManager.CharacterCreatorManager;
                    builder.Append(" | Önizleme: ").Append(creator != null && creator.UnitController != null ? "var" : "yok");
                    CameraManager cameraManager = gameManager.CameraManager;
                    if (cameraManager != null && cameraManager.CharacterPreviewCamera != null) {
                        builder.Append(" | Önizleme kamerası: ").Append(cameraManager.CharacterPreviewCamera.enabled ? "açık" : "kapalı");
                    }
                }
            } catch (System.Exception exception) {
                builder.Append(" | Durum okunamadı: ").Append(exception.Message);
            }
            return builder.ToString();
        }

        private void Update() {
            if (Time.unscaledDeltaTime > 0f) {
                fps = Mathf.Lerp(fps, 1f / Time.unscaledDeltaTime, 0.05f);
            }
            if (canvasObject == null) {
                CreateOverlay();
            }
            int currentVersion;
            int entryCount;
            lock (entriesLock) {
                currentVersion = version;
                entryCount = entries.Count;
            }
            // refresh the open panel twice a second so the status line stays current
            bool refreshPanel = panelObject.activeSelf && Time.unscaledTime >= nextStatusRefresh;
            if (currentVersion == shownVersion && refreshPanel == false) {
                return;
            }
            nextStatusRefresh = Time.unscaledTime + 0.5f;
            shownVersion = currentVersion;
            badgeText.text = entryCount > 0 ? "Hata (" + entryCount + ")" : "Durum";
            badgeImage.color = entryCount > 0 ? new Color(0.7f, 0.05f, 0.05f, 0.85f) : new Color(0.15f, 0.15f, 0.15f, 0.45f);
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

            // small red button in the top left corner
            badgeObject = CreateButton(canvasObject.transform, "Hata", new Vector2(0f, 1f), new Vector2(90f, -30f), new Vector2(150f, 44f),
                new Color(0.7f, 0.05f, 0.05f, 0.85f), TogglePanel, out badgeText);
            badgeImage = badgeObject.GetComponent<Image>();

            // the list of errors
            panelObject = new GameObject("ErrorPanel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.05f, 0.08f);
            panelRect.anchorMax = new Vector2(0.95f, 0.85f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.88f);

            GameObject textObject = new GameObject("ErrorText");
            textObject.transform.SetParent(panelObject.transform, false);
            RectTransform textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(16f, 70f);
            textRect.offsetMax = new Vector2(-16f, -12f);
            panelText = textObject.AddComponent<Text>();
            panelText.font = font;
            panelText.fontSize = 16;
            panelText.color = new Color(1f, 0.85f, 0.8f, 1f);
            panelText.alignment = TextAnchor.UpperLeft;
            panelText.horizontalOverflow = HorizontalWrapMode.Wrap;
            panelText.verticalOverflow = VerticalWrapMode.Truncate;
            panelText.raycastTarget = false;

            Text unusedText;
            CreateButton(panelObject.transform, "Kopyala", new Vector2(0.5f, 0f), new Vector2(-100f, 36f), new Vector2(170f, 50f),
                new Color(0.2f, 0.35f, 0.6f, 1f), CopyReport, out unusedText);
            CreateButton(panelObject.transform, "Kapat", new Vector2(0.5f, 0f), new Vector2(100f, 36f), new Vector2(170f, 50f),
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
            }
        }

        private void CopyReport() {
            GUIUtility.systemCopyBuffer = "Ötüken Destanı " + Application.version + " hata kaydı\n" + BuildReport(true);
            MobileFeedback.Success();
        }
    }
}
