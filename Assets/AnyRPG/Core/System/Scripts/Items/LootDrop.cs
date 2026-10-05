using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {
    public class LootDrop : ConfiguredClass, IDescribable {

        private int lootDropId;

        // game manager references
        protected LootManager lootManager = null;
        protected UIManager uIManager = null;

        public virtual string ResourceName => string.Empty;
        public virtual string Description => string.Empty;

        public ItemQuality ItemQuality {
            get {
                return InstantiatedItem.ItemQuality;
            }
        }

        public Sprite Icon {
            get {
                return InstantiatedItem.Icon;
            }
        }

        public string DisplayName {
            get {
                return InstantiatedItem.DisplayName;
            }
        }

        public InstantiatedItem InstantiatedItem { get; set; }

        public int LootDropId { get => lootDropId; }

        public LootDrop(InstantiatedItem item, SystemGameManager systemGameManager) {
            InstantiatedItem = item;
            Configure(systemGameManager);
        }

        public LootDrop(int lootDropId, InstantiatedItem item, SystemGameManager systemGameManager) {
            this.lootDropId = lootDropId;
            InstantiatedItem = item;
            Configure(systemGameManager);
        }

        public override void SetGameManagerReferences() {
            base.SetGameManagerReferences();
            lootManager = systemGameManager.LootManager;
            uIManager = systemGameManager.UIManager;
        }

        public virtual void TakeLoot(UnitController sourceUnitController) {

            // build the "+ item" message first: money purses are used up as soon as they are looted
            string pickupMessage = Ganimet.PickupMessage(InstantiatedItem);
            InstantiatedItem lootedItem = InstantiatedItem;
            if (ProcessTakeLoot(sourceUnitController)) {
                AfterLoot(sourceUnitController);
                // Ötüken: boşalan sandık kimin günlük görevine sayılacak (LootableNodeComponent.CheckDropListSize)
                Ganimet.SonAlan = sourceUnitController;
                lootManager.TakeLoot(sourceUnitController, this);
                if (pickupMessage != string.Empty) {
                    sourceUnitController.WriteMessageFeedMessage(pickupMessage);
                }
                if (systemGameManager.PlayerManagerClient.UnitController == sourceUnitController) {
                    Ganimet.OnLooted(lootedItem);
                } else if (OtukenAg.Sunucuda) {
                    // çevrimiçi: ganimeti sunucu verir, günlük görev sunucuda sayılır
                    GunlukGorevler.SunucuBildir(sourceUnitController, GunlukGorevTuru.Ganimet);
                }
            }
        }

        public void SetBackgroundImage(Image backgroundImage) {
            uIManager.SetItemBackground(InstantiatedItem.Item, backgroundImage, new Color32(0, 0, 0, 255), InstantiatedItem.ItemQuality);
        }

        public bool HasItem(Item item) {
            return (InstantiatedItem.ResourceName == item.ResourceName);
        }

        protected bool ProcessTakeLoot(UnitController sourceUnitController) {
            return sourceUnitController.CharacterInventoryManager.AddItem(InstantiatedItem, false);
        }

        public void AfterLoot(UnitController sourceUnitController) {
            if (InstantiatedItem is InstantiatedCurrencyItem) {
                (InstantiatedItem as InstantiatedCurrencyItem).Use(sourceUnitController);
            } else if (InstantiatedItem is InstantiatedQuestStartItem) {
                (InstantiatedItem as InstantiatedQuestStartItem).Use(sourceUnitController);
            }
        }

        public string GetSummary() {
            return InstantiatedItem.GetSummary();
        }

        public string GetDescription() {
            return InstantiatedItem.GetDescription();
        }

        public void ProcessShowTooltip(TooltipController tooltipController) {
        }

    }

}