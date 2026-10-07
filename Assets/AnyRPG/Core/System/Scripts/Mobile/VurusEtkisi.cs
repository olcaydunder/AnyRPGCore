using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Vuruş hissi (Ötüken): oyuncu bir canavara vurunca canavarın modeli kısa bir an sarsılır (0,14 sn büyüyüp küçülür,
    /// hafifçe geri itilir) ki vuruşun isabet ettiği görülsün. Yalnız telefonda görünür; oyun mantığına dokunmaz.
    /// PlayerManagerClient.HandleReceiveCombatTextEvent çağırır.
    /// </summary>
    public class VurusEtkisi : MonoBehaviour {

        private const float Sure = 0.14f;
        private const float Buyume = 0.09f;
        private const float Itme = 0.08f;

        private Transform model = null;
        private Vector3 asilOlcek = Vector3.one;
        private Vector3 asilKonum = Vector3.zero;
        private Vector3 itmeYonu = Vector3.zero;
        private float baslangic = -1f;

        public static void Vur(InteractableBase hedef, InteractableBase vuran, bool kritik) {
            UnitController birim = hedef as UnitController;
            if (birim == null || birim.UnitModelController == null || birim.UnitModelController.UnitModel == null) {
                return;
            }
            Transform model = birim.UnitModelController.UnitModel.transform;
            VurusEtkisi e = model.GetComponent<VurusEtkisi>();
            if (e == null) {
                e = model.gameObject.AddComponent<VurusEtkisi>();
                e.model = model;
                e.asilOlcek = model.localScale;
                e.asilKonum = model.localPosition;
            } else if (e.baslangic >= 0f) {
                // önceki sarsıntı sürüyorsa yerine koy, yeniden başla
                e.Bitir();
            }
            Vector3 yon = vuran != null ? hedef.transform.position - vuran.transform.position : -hedef.transform.forward;
            yon.y = 0f;
            e.itmeYonu = yon.sqrMagnitude > 0.0001f ? model.parent != null ? model.parent.InverseTransformDirection(yon.normalized) : yon.normalized : Vector3.zero;
            e.itmeYonu *= kritik ? Itme * 2f : Itme;
            e.baslangic = Time.time;
            e.enabled = true;
        }

        private void Bitir() {
            if (model != null) {
                model.localScale = asilOlcek;
                model.localPosition = asilKonum;
            }
            baslangic = -1f;
            enabled = false;
        }

        private void LateUpdate() {
            if (baslangic < 0f || model == null) {
                enabled = false;
                return;
            }
            float t = (Time.time - baslangic) / Sure;
            if (t >= 1f) {
                Bitir();
                return;
            }
            float egri = Mathf.Sin(t * Mathf.PI);
            model.localScale = asilOlcek * (1f + Buyume * egri);
            model.localPosition = asilKonum + itmeYonu * egri;
        }

        private void OnDisable() {
            if (baslangic >= 0f) {
                Bitir();
            }
        }
    }
}
