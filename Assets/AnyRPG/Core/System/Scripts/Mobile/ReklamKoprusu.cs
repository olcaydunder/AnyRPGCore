using UnityEngine;

namespace AnyRPG {

    /// <summary>Reklam olaylarını (Java'nın iş parçacığından gelen) Unity'nin ana iş parçacığında işletir (Reklam)</summary>
    public class ReklamKoprusu : MonoBehaviour {

        private void Update() {
            Reklam.Guncelle();
        }
    }
}
