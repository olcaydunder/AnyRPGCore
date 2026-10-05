using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Bir haritaya girince ekranın üstünde birkaç saniye haritanın adı ve seviyesi görünür:
    ///   UMAY TARLALARI
    ///   Seviye 3–6 · sana göre
    /// Renk oyuncunun seviyesine göredir (HaritaSeviyeleri.Yorum). Dokunmaları engellemez; kodla kurulur.
    /// </summary>
    public class BolgeGirisi : MonoBehaviour {

        public const string CanvasName = "BolgeGirisiCanvas";
        private const int SortingOrder = 24;
        private const float Sure = 4f;
        private const float Solma = 0.8f;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);

        private CanvasGroup grup = null;
        private Text adYazisi = null;
        private Text seviyeYazisi = null;
        private string sonSahne = null;
        private float gosterimBaslangici = -100f;
        private SystemGameManager oyun = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Kur() {
            // başsız sunucuda ekran yok
            if (Application.isBatchMode && Sunucu.BatchSunucuOlsun) {
                return;
            }
            GameObject go = new GameObject(CanvasName);
            DontDestroyOnLoad(go);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler olcek = go.AddComponent<CanvasScaler>();
            olcek.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olcek.referenceResolution = new Vector2(1422f, 800f);
            olcek.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            olcek.matchWidthOrHeight = 1f;
            go.AddComponent<BolgeGirisi>().Kurul();
        }

        private void Kurul() {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject kok = new GameObject("Bant", typeof(RectTransform));
            kok.transform.SetParent(transform, false);
            RectTransform rt = kok.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -96f);
            rt.sizeDelta = new Vector2(760f, 96f);
            grup = kok.AddComponent<CanvasGroup>();
            grup.blocksRaycasts = false;
            grup.interactable = false;
            grup.alpha = 0f;

            adYazisi = Yazi(kok.transform, "Ad", font, 40, new Vector2(0f, 0.42f), new Vector2(1f, 1f), gold);
            adYazisi.fontStyle = FontStyle.Bold;
            seviyeYazisi = Yazi(kok.transform, "Seviye", font, 24, new Vector2(0f, 0f), new Vector2(1f, 0.44f), Color.white);
            seviyeYazisi.fontStyle = FontStyle.Bold;
        }

        private static Text Yazi(Transform ust, string ad, Font font, int boyut, Vector2 amin, Vector2 amax, Color renk) {
            GameObject go = new GameObject(ad, typeof(RectTransform));
            go.transform.SetParent(ust, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = boyut;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = renk;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            Outline o = go.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        /// <summary>Türkçe büyük harf (i → İ, ı → I); kültür verisine dayanmaz</summary>
        private static string TurkceBuyuk(string s) {
            return s.Replace('i', 'İ').Replace('ı', 'I').ToUpperInvariant();
        }

        private void Update() {
            if (oyun == null) {
                oyun = FindAnyObjectByType<SystemGameManager>();
                if (oyun == null) {
                    return;
                }
            }
            PlayerManagerClient oyuncular = oyun.PlayerManagerClient;
            string sahne = SceneManager.GetActiveScene().name;
            if (sahne != sonSahne && oyuncular != null && oyuncular.PlayerUnitSpawned && oyuncular.UnitController != null) {
                sonSahne = sahne;
                int en, ust;
                if (HaritaSeviyeleri.Aralik(sahne, out en, out ust)) {
                    Color renk;
                    string yorum = HaritaSeviyeleri.Yorum(sahne, HaritaSeviyeleri.OyuncuSeviyesi(), out renk);
                    adYazisi.text = TurkceBuyuk(IsinlanmaPenceresi.GorunenAd(sahne));
                    seviyeYazisi.text = HaritaSeviyeleri.Yazi(sahne) + (yorum.Length > 0 ? "  ·  " + yorum : string.Empty);
                    seviyeYazisi.color = renk;
                    gosterimBaslangici = Time.unscaledTime;
                }
            }
            if (oyuncular == null || oyuncular.PlayerUnitSpawned == false) {
                // ana menüye dönülürse yeniden girişte yine gösterilsin
                if (oyun.LevelManagerClient != null && oyun.LevelManagerClient.IsMainMenu()) {
                    sonSahne = null;
                }
            }
            float gecen = Time.unscaledTime - gosterimBaslangici;
            float alfa = 0f;
            if (gecen < Solma) {
                alfa = gecen / Solma;
            } else if (gecen < Sure) {
                alfa = 1f;
            } else if (gecen < Sure + Solma) {
                alfa = 1f - (gecen - Sure) / Solma;
            }
            grup.alpha = alfa;
        }
    }
}
