using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Günlük Armağan: oyuna her gün girene bir armağan. 7 gün üst üste girilirse en büyük armağan gelir;
    /// bir gün kaçırılırsa seri 1. günden yeniden başlar. Karakter başına, cihazın tarihine göre tutulur.
    /// Oyuna girdikten birkaç saniye sonra (rehber açıksa o kapandıktan sonra) kendiliğinden açılır.
    /// Kodla kurulur, prefab gerektirmez.
    /// </summary>
    public class GunlukArmagan : MonoBehaviour {

        public const string CanvasName = "GunlukArmaganCanvas";
        // above windows and menus (12-20), below the guide (30) and the Durum overlay (1000)
        private const int SortingOrder = 28;
        private const float DelayAfterSpawn = 3f;
        private const int DayCount = 7;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color cardColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color cardTodayColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color cardClaimedColor = new Color(0.12f, 0.1f, 0.08f, 0.95f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color dimTextColor = new Color(0.6f, 0.56f, 0.5f, 1f);

        private struct Reward {
            public string label;
            public string currencyName;
            public int currencyAmount;
            public string itemName;
            public int itemCount;

            public Reward(string label, string currencyName, int currencyAmount, string itemName, int itemCount) {
                this.label = label;
                this.currencyName = currencyName;
                this.currencyAmount = currencyAmount;
                this.itemName = itemName;
                this.itemCount = itemCount;
            }
        }

        // resource names (not display names) so the lookups work whatever the language
        private static readonly Reward[] rewards = new Reward[DayCount] {
            new Reward("5 Gümüş Akçe", "Silver", 5, null, 0),
            new Reward("3 Şifa İksiri", null, 0, "Health Potion", 3),
            new Reward("15 Gümüş Akçe", "Silver", 15, null, 0),
            new Reward("3 Mana İksiri", null, 0, "Mana Potion", 3),
            new Reward("30 Gümüş Akçe", "Silver", 30, null, 0),
            new Reward("10 Gözlü Heybe", null, 0, "10 Slot Bag", 1),
            new Reward("1 Altın + Gök Taşı Parçası", "Gold", 1, "Gok Tasi Parcasi", 1),
        };

        private static GunlukArmagan instance = null;
        private static float inGameSince = -1f;
        private static string checkedKey = string.Empty;

        private SystemGameManager systemGameManager = null;
        private string characterName = string.Empty;
        private int rewardDay = 1;

        private Font font = null;
        private GameObject panelRoot = null;
        private Text subtitleText = null;
        private Text statusText = null;
        private readonly List<Image> cardImages = new List<Image>();
        private readonly List<Image> cardIcons = new List<Image>();
        private readonly List<Text> cardStateTexts = new List<Text>();

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        /// <summary>
        /// called every frame by MobileBootstrap
        /// </summary>
        public static void Tick(SystemGameManager systemGameManager, bool inGame) {
            // çevrimiçi oyunda karakter sunucudadır: telefondan ödül/yetenek verilmez (Cevrimici)
            inGame = inGame && (systemGameManager == null || systemGameManager.GameMode != GameMode.Network);
            if (inGame == false || systemGameManager == null) {
                inGameSince = -1f;
                checkedKey = string.Empty;
                if (IsOpen) {
                    instance.Close();
                }
                return;
            }
            if (inGameSince < 0f) {
                inGameSince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - inGameSince < DelayAfterSpawn || GameGuide.IsOpen || IsinlanmaPenceresi.IsOpen || IsOpen) {
                return;
            }
            UnitController unitController = systemGameManager.PlayerManagerClient.UnitController;
            if (unitController == null) {
                return;
            }
            string character = unitController.DisplayName;
            string today = DateKey(DateTime.Now);
            string key = character + "|" + today;
            if (key == checkedKey) {
                return;
            }
            // checked once per character and day; closing with "Sonra" brings it back on the next game load
            checkedKey = key;
            if (PlayerPrefs.GetString(LastClaimKey(character), string.Empty) == today) {
                return;
            }
            Ensure();
            instance.Open(systemGameManager, character);
        }

        private static string DateKey(DateTime date) {
            return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string LastClaimKey(string character) {
            return "gunluk-armagan-son-" + character;
        }

        private static string StreakKey(string character) {
            return "gunluk-armagan-seri-" + character;
        }

        /// <summary>
        /// the day of the 7 day cycle that would be claimed today
        /// </summary>
        private static int GetRewardDay(string character) {
            string lastClaim = PlayerPrefs.GetString(LastClaimKey(character), string.Empty);
            if (lastClaim == DateKey(DateTime.Now.AddDays(-1))) {
                int streak = PlayerPrefs.GetInt(StreakKey(character), 0);
                return (streak % DayCount) + 1;
            }
            return 1;
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
            instance = canvasObject.AddComponent<GunlukArmagan>();
            instance.Build();
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // dimmer that also keeps taps from reaching the game
            panelRoot = CreateRect(transform, "GunlukArmagan", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimmer = panelRoot.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject panel = CreateRect(panelRoot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, -270f), new Vector2(520f, 270f));
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = panelColor;
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = gold;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            GameObject title = CreateRect(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -70f), new Vector2(-20f, -12f));
            Text titleText = CreateText(title, "GÜNLÜK ARMAĞAN", 38, TextAnchor.MiddleCenter);
            titleText.color = gold;
            titleText.fontStyle = FontStyle.Bold;

            GameObject subtitle = CreateRect(panel.transform, "Aciklama", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(30f, -140f), new Vector2(-30f, -72f));
            subtitleText = CreateText(subtitle, string.Empty, 22, TextAnchor.MiddleCenter);

            // seven day cards
            const float cardWidth = 132f;
            const float cardHeight = 210f;
            const float cardSpacing = 12f;
            float rowWidth = DayCount * cardWidth + (DayCount - 1) * cardSpacing;
            for (int i = 0; i < DayCount; i++) {
                float left = -rowWidth / 2f + i * (cardWidth + cardSpacing);
                GameObject card = CreateRect(panel.transform, "Gun" + (i + 1), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(left, -140f), new Vector2(left + cardWidth, -140f + cardHeight));
                Image cardImage = card.AddComponent<Image>();
                cardImage.color = cardColor;
                Outline cardOutline = card.AddComponent<Outline>();
                cardOutline.effectColor = new Color(gold.r, gold.g, gold.b, 0.5f);
                cardOutline.effectDistance = new Vector2(1f, -1f);
                cardImages.Add(cardImage);

                GameObject dayLabel = CreateRect(card.transform, "GunAdi", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -38f), new Vector2(-4f, -4f));
                Text dayText = CreateText(dayLabel, (i + 1) + ". Gün", 22, TextAnchor.MiddleCenter);
                dayText.color = gold;
                dayText.fontStyle = FontStyle.Bold;

                GameObject icon = CreateRect(card.transform, "Simge", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-34f, -110f), new Vector2(34f, -42f));
                Image iconImage = icon.AddComponent<Image>();
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                cardIcons.Add(iconImage);

                GameObject rewardLabel = CreateRect(card.transform, "Armagan", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(6f, 34f), new Vector2(-6f, -114f));
                CreateText(rewardLabel, rewards[i].label, 18, TextAnchor.MiddleCenter);

                GameObject stateLabel = CreateRect(card.transform, "Durum", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(4f, 4f), new Vector2(-4f, 32f));
                Text stateText = CreateText(stateLabel, string.Empty, 18, TextAnchor.MiddleCenter);
                cardStateTexts.Add(stateText);
            }

            GameObject status = CreateRect(panel.transform, "Uyari", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(30f, 92f), new Vector2(-30f, 124f));
            statusText = CreateText(status, string.Empty, 22, TextAnchor.MiddleCenter);
            statusText.color = new Color(1f, 0.55f, 0.45f, 1f);

            CreateButton(panel.transform, "Armağanı Al", new Vector2(0.5f, 0f), new Vector2(-90f, 50f), new Vector2(340f, 70f), 30,
                cardTodayColor, Claim);
            CreateButton(panel.transform, "Sonra", new Vector2(0.5f, 0f), new Vector2(190f, 50f), new Vector2(180f, 70f), 26,
                cardColor, () => { Close(); MobileFeedback.Tap(); });

            panelRoot.SetActive(false);
        }

        private void Open(SystemGameManager gameManager, string character) {
            systemGameManager = gameManager;
            characterName = character;
            rewardDay = GetRewardDay(character);
            subtitleText.text = "Her gün oyuna gir, armağanını al! 7 gün üst üste gelirsen en büyük armağan seni bekler.\n" +
                "Bir gün kaçırırsan seri 1. günden yeniden başlar.";
            statusText.text = string.Empty;
            for (int i = 0; i < DayCount; i++) {
                int day = i + 1;
                cardIcons[i].sprite = GetRewardIcon(rewards[i]);
                cardIcons[i].enabled = cardIcons[i].sprite != null;
                if (day < rewardDay) {
                    cardImages[i].color = cardClaimedColor;
                    cardStateTexts[i].text = "Alındı";
                    cardStateTexts[i].color = dimTextColor;
                } else if (day == rewardDay) {
                    cardImages[i].color = cardTodayColor;
                    cardStateTexts[i].text = "BUGÜN";
                    cardStateTexts[i].color = gold;
                } else {
                    cardImages[i].color = cardColor;
                    cardStateTexts[i].text = string.Empty;
                }
            }
            panelRoot.SetActive(true);
            MobileFeedback.Light();
        }

        private void Close() {
            panelRoot.SetActive(false);
        }

        private Sprite GetRewardIcon(Reward reward) {
            if (systemGameManager == null || systemGameManager.SystemDataFactory == null) {
                return null;
            }
            if (reward.itemName != null) {
                Item item = systemGameManager.SystemDataFactory.GetResource<Item>(reward.itemName);
                if (item != null) {
                    return item.Icon;
                }
            }
            if (reward.currencyName != null) {
                Currency currency = systemGameManager.SystemDataFactory.GetResource<Currency>(reward.currencyName);
                if (currency != null) {
                    return currency.Icon;
                }
            }
            return null;
        }

        private void Claim() {
            MobileFeedback.Tap();
            UnitController unitController = null;
            if (systemGameManager != null && systemGameManager.PlayerManagerClient != null) {
                unitController = systemGameManager.PlayerManagerClient.UnitController;
            }
            if (unitController == null || unitController.DisplayName != characterName) {
                Close();
                return;
            }
            Reward reward = rewards[rewardDay - 1];
            if (reward.itemName != null && unitController.CharacterInventoryManager.EmptySlotCount() == 0) {
                statusText.text = "Çantan dolu! Biraz yer aç, sonra armağanını al.";
                return;
            }

            if (reward.currencyName != null) {
                Currency currency = systemGameManager.SystemDataFactory.GetResource<Currency>(reward.currencyName);
                if (currency != null) {
                    unitController.CharacterCurrencyManager.AddCurrency(currency, reward.currencyAmount);
                }
            }
            if (reward.itemName != null) {
                for (int i = 0; i < reward.itemCount; i++) {
                    InstantiatedItem instantiatedItem = unitController.CharacterInventoryManager.GetNewInstantiatedItem(reward.itemName);
                    if (instantiatedItem == null || unitController.CharacterInventoryManager.AddItem(instantiatedItem, false) == false) {
                        break;
                    }
                }
            }

            PlayerPrefs.SetString(LastClaimKey(characterName), DateKey(DateTime.Now));
            PlayerPrefs.SetInt(StreakKey(characterName), rewardDay);
            PlayerPrefs.Save();

            unitController.WriteMessageFeedMessage($"<color=#FFD54A>Günlük armağan ({rewardDay}. gün): {reward.label}</color>");
            MobileFeedback.Success();
            Close();
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
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private void CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize,
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
            CreateText(textObject, label, fontSize, TextAnchor.MiddleCenter);
        }
    }
}
