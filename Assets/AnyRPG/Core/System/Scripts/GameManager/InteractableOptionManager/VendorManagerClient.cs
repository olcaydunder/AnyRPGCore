using System.Collections.Generic;

namespace AnyRPG {
    public class VendorManagerClient : InteractableOptionManager {

        private VendorProps vendorProps = null;
        private VendorComponent vendorComponent = null;

        InstantiatedItem instantiatedItem = null;

        // game manager references
        private PlayerManagerClient playerManagerClient = null;

        public VendorProps VendorProps { get => vendorProps; set => vendorProps = value; }
        public VendorComponent VendorComponent { get => vendorComponent; set => vendorComponent = value; }

        public override void SetGameManagerReferences() {
            base.SetGameManagerReferences();
            playerManagerClient = systemGameManager.PlayerManagerClient;
        }

        public void SetProps(VendorProps vendorProps, VendorComponent vendorComponent, int componentIndex, int choiceIndex) {
            //Debug.Log("VendorManager.SetProps()");
            this.vendorProps = vendorProps;
            this.vendorComponent = vendorComponent;
            BeginInteraction(vendorComponent, componentIndex, choiceIndex);
        }

        public override void EndInteraction() {
            base.EndInteraction();

            vendorProps = null;
        }

        public void SetSellItem(InstantiatedItem instantiatedItem) {
            //Debug.Log($"VendorManagerClient.SetSellItem({instantiatedItem.DisplayName})");
            this.instantiatedItem = instantiatedItem;
        }

        public void RequestSellItemToVendor(InstantiatedItem instantiatedItem) {
            if (systemGameManager.GameMode == GameMode.Local) {
                vendorComponent.SellItemToVendor(playerManagerClient.UnitController, componentIndex, instantiatedItem);
            } else {
                networkManagerClient.SellItemToVendor(vendorComponent.Interactable, componentIndex, instantiatedItem.InstanceId);
            }
        }

        public void RequestBuyItemFromVendor(UnitController sourceUnitController, VendorItem vendorItem, int collectionIndex, int itemIndex) {
            //Debug.Log($"VendorManager.BuyItemFromVendor({sourceUnitController.gameObject.name}, {vendorItem.Item.ResourceName}, {collectionIndex}, {itemIndex})");

            if (systemGameManager.GameMode == GameMode.Local) {
                vendorComponent.BuyItemFromVendor(sourceUnitController, componentIndex, vendorItem, collectionIndex, itemIndex);
            } else {
                networkManagerClient.BuyItemFromVendor(vendorComponent.Interactable, componentIndex, collectionIndex, itemIndex, vendorItem.Item.ResourceName);
            }
        }

        public void RequestSellItemToVendor() {
            RequestSellItemToVendor(instantiatedItem);
        }

        /// <summary>
        /// "Değersizleri Sat": sell every junk (gray) item in the bags in one go and write a single summary message
        /// </summary>
        public int RequestSellJunkToVendor() {
            UnitController unitController = playerManagerClient.UnitController;
            if (vendorComponent == null || unitController == null) {
                return 0;
            }
            List<InstantiatedItem> junkItems = Ganimet.FindJunk(unitController);
            if (junkItems.Count == 0) {
                unitController.WriteMessageFeedMessage("Çantanda değersiz (gri) eşya yok");
                return 0;
            }
            if (systemGameManager.GameMode != GameMode.Local) {
                foreach (InstantiatedItem junkItem in junkItems) {
                    RequestSellItemToVendor(junkItem);
                }
                return junkItems.Count;
            }
            int soldCount = 0;
            Dictionary<Currency, int> earned = new Dictionary<Currency, int>();
            foreach (InstantiatedItem junkItem in junkItems) {
                KeyValuePair<Currency, int> sellPrice = junkItem.Item.GetSellPrice(junkItem, unitController);
                if (vendorComponent.SellItemToVendor(unitController, componentIndex, junkItem, false) == false) {
                    continue;
                }
                soldCount++;
                if (sellPrice.Key != null) {
                    earned.TryGetValue(sellPrice.Key, out int amount);
                    earned[sellPrice.Key] = amount + sellPrice.Value;
                }
            }
            if (soldCount > 0) {
                List<string> priceStrings = new List<string>();
                foreach (KeyValuePair<Currency, int> earning in earned) {
                    priceStrings.Add(systemGameManager.CurrencyConverter.GetCombinedPriceString(earning.Key, earning.Value));
                }
                unitController.WriteMessageFeedMessage($"{soldCount} değersiz eşya satıldı: +{string.Join(", ", priceStrings)}");
                MobileFeedback.Success();
            }
            return soldCount;
        }
    }

}