using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Ganimet kolaylıkları (mobil):
    /// - Otomatik toplama: ganimet penceresi açılmadan her şey çantaya girer; çanta doluysa pencere yine açılır.
    /// - Alınan her ganimet için eşya rengiyle "+ Kurt Dişi" mesajı, nadir ve üstü ganimette titreşim.
    /// - Değersiz (gri) eşyaları tanır; satıcı penceresindeki "Değersizleri Sat" düğmesi bunları tek seferde satar.
    /// </summary>
    public static class Ganimet {

        private const string AutoLootKey = "otomatik-topla";
        public const string JunkQualityName = "Poor";
        private const string CurrencyColor = "FFD54A";

        private static readonly HashSet<string> preciousQualities = new HashSet<string>() { "Rare", "Epic", "Legendary" };

        public static bool AutoLoot {
            get { return PlayerPrefs.GetInt(AutoLootKey, 1) == 1; }
            set { PlayerPrefs.SetInt(AutoLootKey, value ? 1 : 0); }
        }

        public static bool IsJunk(InstantiatedItem instantiatedItem) {
            return instantiatedItem != null
                && instantiatedItem.ItemQuality != null
                && instantiatedItem.ItemQuality.ResourceName == JunkQualityName;
        }

        public static bool IsPrecious(InstantiatedItem instantiatedItem) {
            return instantiatedItem != null
                && instantiatedItem.ItemQuality != null
                && preciousQualities.Contains(instantiatedItem.ItemQuality.ResourceName);
        }

        /// <summary>
        /// "+ Kurt Dişi" in the color of the item quality, gold for money.
        /// Must be built before the item is taken: money purses are used up the moment they are looted.
        /// </summary>
        public static string PickupMessage(InstantiatedItem instantiatedItem) {
            if (instantiatedItem == null) {
                return string.Empty;
            }
            string itemName = instantiatedItem.DisplayName;
            if (string.IsNullOrEmpty(itemName)) {
                return string.Empty;
            }
            string color = "FFFFFF";
            if (instantiatedItem is InstantiatedCurrencyItem) {
                color = CurrencyColor;
            } else if (instantiatedItem.ItemQuality != null) {
                color = ColorUtility.ToHtmlStringRGB(instantiatedItem.ItemQuality.QualityColor);
            }
            if (IsPrecious(instantiatedItem)) {
                return $"<b><color=#{color}>+ {itemName} !</color></b>";
            }
            return $"<color=#{color}>+ {itemName}</color>";
        }

        /// <summary>
        /// called after the local player took a piece of loot
        /// </summary>
        public static void OnLooted(InstantiatedItem instantiatedItem) {
            if (IsPrecious(instantiatedItem)) {
                MobileFeedback.Success();
            }
        }

        /// <summary>
        /// all junk items in the bags (bank not included), every item of a stack listed separately
        /// </summary>
        public static List<InstantiatedItem> FindJunk(UnitController unitController) {
            List<InstantiatedItem> junk = new List<InstantiatedItem>();
            if (unitController == null || unitController.CharacterInventoryManager == null) {
                return junk;
            }
            foreach (InventorySlot inventorySlot in unitController.CharacterInventoryManager.InventorySlots) {
                if (inventorySlot == null || inventorySlot.IsEmpty) {
                    continue;
                }
                foreach (InstantiatedItem instantiatedItem in inventorySlot.InstantiatedItems.Values) {
                    if (IsJunk(instantiatedItem)) {
                        junk.Add(instantiatedItem);
                    }
                }
            }
            return junk;
        }
    }
}
