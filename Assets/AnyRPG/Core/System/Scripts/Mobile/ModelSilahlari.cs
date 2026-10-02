using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Silahı ayrı bir model dosyası olarak gelen canavarlara (KayKit iskeletleri) kılıç, kalkan, asa takar.
    /// Model ilk oluşturulduğunda her silahı adı verilen kemiğin (ör. "handslot.r") altına yerleştirir.
    /// Silah modeli kendi dosyasındaki yerleşimiyle (konum, dönüş, ölçek) kemiğe bağlanır; KayKit silahları
    /// el yuvası kemiğine bu şekilde oturacak biçimde hazırlanmıştır.
    /// </summary>
    public class ModelSilahlari : MonoBehaviour {

        [System.Serializable]
        public class Silah {
            public GameObject silah = null;
            public string kemik = string.Empty;
        }

        [SerializeField]
        private List<Silah> silahlar = new List<Silah>();

        private bool takildi = false;

        private void Awake() {
            if (takildi) {
                return;
            }
            takildi = true;
            foreach (Silah silah in silahlar) {
                if (silah == null || silah.silah == null || string.IsNullOrEmpty(silah.kemik)) {
                    continue;
                }
                Transform kemik = KemikBul(transform, silah.kemik);
                if (kemik == null) {
                    Debug.LogWarning($"{gameObject.name}: silah kemiği bulunamadı: {silah.kemik}");
                    continue;
                }
                GameObject takilan = Instantiate(silah.silah, kemik);
                takilan.name = silah.silah.name;
                KatmanAyarla(takilan.transform, kemik.gameObject.layer);
            }
        }

        private static Transform KemikBul(Transform kok, string ad) {
            if (kok.name == ad) {
                return kok;
            }
            for (int i = 0; i < kok.childCount; i++) {
                Transform bulunan = KemikBul(kok.GetChild(i), ad);
                if (bulunan != null) {
                    return bulunan;
                }
            }
            return null;
        }

        private static void KatmanAyarla(Transform nesne, int katman) {
            nesne.gameObject.layer = katman;
            for (int i = 0; i < nesne.childCount; i++) {
                KatmanAyarla(nesne.GetChild(i), katman);
            }
        }
    }
}
