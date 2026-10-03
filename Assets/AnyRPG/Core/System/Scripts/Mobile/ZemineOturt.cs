using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Nesneyi altındaki zemine oturtur (Default katmanındaki ilk çarpışma yüzeyi).
    /// Haritalara koddan yerleştirilen geçit taşları ve yol tabelaları için: konumları yatayda
    /// doğru verilir, yüksekliği bu bileşen zemine göre ayarlar.
    /// Derlemede Otuken.EditorAraclari.HaritaHazirlik yürüme ağını pişirmeden önce HepsiniOturt() çağırır;
    /// oyunda da Start'ta bir kez daha bakılır (zemin zaten altındaysa yer değişmez).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class ZemineOturt : MonoBehaviour {

        [Tooltip("Aramaya nesnenin bu kadar üstünden başlanır (metre)")]
        [SerializeField]
        private float yukaridan = 4f;

        [Tooltip("Nesnenin en fazla bu kadar altına inilir (metre)")]
        [SerializeField]
        private float asagi = 40f;

        private static readonly RaycastHit[] isabetler = new RaycastHit[32];

        private void Start() {
            Oturt();
        }

        public bool Oturt() {
            Vector3 p = transform.position;
            Vector3 baslangic = p + Vector3.up * yukaridan;
            int n = Physics.RaycastNonAlloc(baslangic, Vector3.down, isabetler, yukaridan + asagi, 1, QueryTriggerInteraction.Ignore);
            float enYakin = float.MaxValue;
            bool bulundu = false;
            float y = p.y;
            for (int i = 0; i < n; i++) {
                if (isabetler[i].collider.transform.IsChildOf(transform)) {
                    continue;
                }
                if (isabetler[i].distance < enYakin) {
                    enYakin = isabetler[i].distance;
                    y = isabetler[i].point.y;
                    bulundu = true;
                }
            }
            if (bulundu && Mathf.Abs(y - p.y) > 0.01f) {
                transform.position = new Vector3(p.x, y, p.z);
            }
            return bulundu;
        }

        /// <summary>son HepsiniOturt çağrısında oturtulamayanlar (ad ve konum, derleme tanısı için)</summary>
        public static readonly System.Collections.Generic.List<string> Oturmayanlar = new System.Collections.Generic.List<string>();

        /// <summary>Yüklü sahnelerdeki bütün ZemineOturt nesnelerini oturtur; oturtulamayan sayısını döndürür.</summary>
        public static int HepsiniOturt() {
            Physics.SyncTransforms();
            int basarisiz = 0;
            Oturmayanlar.Clear();
            foreach (ZemineOturt z in FindObjectsByType<ZemineOturt>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (!z.Oturt()) {
                    basarisiz++;
                    Oturmayanlar.Add(z.name + " " + z.transform.position.ToString("F1"));
                }
            }
            Physics.SyncTransforms();
            return basarisiz;
        }
    }
}
