using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Ötüken: bütün düğmelere "dolgun" basma hissi.
    ///  - basınca düğme hafifçe çöker (%93), bırakınca yay gibi biraz taşıp yerine oturur
    ///  - kısa dokunma titreşimi (MobileFeedback.Tap; Seçenekler'deki titreşim ayarına uyar)
    ///  - kendi tık sesi olmayan düğmelerde oyunun arayüz tık sesi
    /// Sahnedeki düğmelere kendiliğinden eklenir (Kur, MobileSupport yarım saniyede bir); yeni açılan pencereler de kapsanır.
    /// </summary>
    [DisallowMultipleComponent]
    public class DugmeHissi : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerEnterHandler {

        private const float Cokme = 0.93f;
        private const float Yay = 520f;      // yay sertliği
        private const float Sonum = 22f;     // sönüm (düşük: daha çok taşar)

        private static float sonrakiTarama = 0f;
        private static readonly List<Button> bulunanlar = new List<Button>();
        private static SystemGameManager oyun = null;
        // yalnız canlanan düğmeler güncellenir (yüzlerce düğmenin her karede Update'i çağrılmasın)
        private static readonly List<DugmeHissi> canlilar = new List<DugmeHissi>();
        private static Yonetici yonetici = null;

        private class Yonetici : MonoBehaviour {
            private void Update() {
                for (int i = canlilar.Count - 1; i >= 0; i--) {
                    DugmeHissi d = canlilar[i];
                    if (d == null || d.Adim() == false) {
                        canlilar.RemoveAt(i);
                    }
                }
            }
        }

        private Button dugme;
        private RectTransform rt;
        private Vector3 temel = Vector3.one;
        private float olcek = 1f;
        private float hiz = 0f;
        private float hedef = 1f;
        private bool basili = false;
        private bool canli = false;
        private bool kendiSesi = false;

        /// <summary>MobileSupport her karede çağırır; yarım saniyede bir yeni düğmeleri bulur</summary>
        public static void Kur(SystemGameManager o) {
            oyun = o;
            if (Time.unscaledTime < sonrakiTarama) {
                return;
            }
            sonrakiTarama = Time.unscaledTime + 0.5f;
            if (yonetici == null) {
                GameObject go = new GameObject("DugmeHissiYonetici");
                DontDestroyOnLoad(go);
                yonetici = go.AddComponent<Yonetici>();
            }
            bulunanlar.Clear();
            bulunanlar.AddRange(FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            foreach (Button b in bulunanlar) {
                if (b != null && b.GetComponent<DugmeHissi>() == null && b.GetComponent<YaylanmaYok>() == null) {
                    b.gameObject.AddComponent<DugmeHissi>();
                }
            }
        }

        private void Awake() {
            dugme = GetComponent<Button>();
            rt = transform as RectTransform;
            kendiSesi = GetComponent<NavigableElement>() != null;
            if (dugme != null) {
                dugme.onClick.AddListener(Tiklandi);
            }
        }

        private void OnDestroy() {
            if (dugme != null) {
                dugme.onClick.RemoveListener(Tiklandi);
            }
        }

        private void OnDisable() {
            // pencere kapanırken çökük kalmasın
            if (canli && rt != null) {
                rt.localScale = temel;
            }
            canli = false;
            basili = false;
            olcek = 1f;
            hiz = 0f;
        }

        private bool Etkin {
            get { return dugme != null && dugme.interactable && dugme.enabled; }
        }

        public void OnPointerDown(PointerEventData e) {
            if (Etkin == false || rt == null) {
                return;
            }
            if (canli == false) {
                // başkası ölçeği değiştirmiş olabilir: her basışta o anki ölçek temel alınır
                temel = rt.localScale;
                olcek = 1f;
                hiz = 0f;
            }
            if (canli == false) {
                canlilar.Add(this);
            }
            canli = true;
            basili = true;
            hedef = Cokme;
            // çöküş anında hissedilsin: ilk adım hızlı
            hiz = -2.2f;
        }

        public void OnPointerUp(PointerEventData e) {
            Birak();
        }

        public void OnPointerExit(PointerEventData e) {
            Birak();
        }

        public void OnPointerEnter(PointerEventData e) {
            // parmak düğmeden çıkıp geri girerse yeniden çöksün
            if (e != null && e.eligibleForClick && e.pointerPress == gameObject && Etkin && canli) {
                basili = true;
                hedef = Cokme;
            }
        }

        private void Birak() {
            if (basili == false) {
                return;
            }
            basili = false;
            hedef = 1f;
            // bırakınca yukarı itilsin: yay taşar, düğme "oturur"
            hiz += 1.6f;
        }

        private void Tiklandi() {
            Dokunus();
            if (kendiSesi == false && oyun != null && oyun.AudioManager != null) {
                try {
                    oyun.AudioManager.PlayUIClickSound();
                } catch (System.Exception) {
                }
            }
        }

        /// <summary>bir kare ilerletir; canlanma bittiyse false</summary>
        private bool Adim() {
            if (canli == false || rt == null || isActiveAndEnabled == false) {
                canli = false;
                return false;
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            // yarı örtük Euler: sönümlü yay
            float ivme = (hedef - olcek) * Yay - hiz * Sonum;
            hiz += ivme * dt;
            olcek += hiz * dt;
            if (basili == false && Mathf.Abs(olcek - 1f) < 0.0015f && Mathf.Abs(hiz) < 0.02f) {
                olcek = 1f;
                hiz = 0f;
                canli = false;
            }
            rt.localScale = temel * olcek;
            return canli;
        }

        private static void Dokunus() {
            try {
                MobileFeedback.Tap();
            } catch (System.Exception) {
            }
        }
    }

    /// <summary>bu düğmeye basma hissi eklenmesin (kendi canlandırması olanlar)</summary>
    public class YaylanmaYok : MonoBehaviour {
    }
}
