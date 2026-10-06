using UnityEngine;

namespace AnyRPG {

    /// <summary>Google Play ödeme olaylarını (Java'nın iş parçacığından gelen) Unity'nin ana iş parçacığında işletir (Odeme)</summary>
    public class OdemeKoprusu : MonoBehaviour {

        private void Update() {
            Odeme.Guncelle();
        }
    }
}
