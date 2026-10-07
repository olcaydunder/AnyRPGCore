using UnityEngine;

namespace AnyRPG {

    /// <summary>Google Play Oyun Hizmetleri olaylarını (Java'nın iş parçacığından gelen) ana iş parçacığında işletir (GoogleGiris)</summary>
    public class GirisKoprusu : MonoBehaviour {

        private void Update() {
            GoogleGiris.Guncelle();
        }
    }
}
