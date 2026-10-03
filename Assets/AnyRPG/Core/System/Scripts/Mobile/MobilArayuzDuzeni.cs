using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Telefona göre arayüz düzeni ve dokunmatik ayarları (Seçenekler > Oyun):
    ///  - Yetenek çubuğu "Büyük": masaüstü için 40 piksellik yetenek kutuları parmakla zor seçiliyordu; çubuk ortalanıp
    ///    1,35 kat büyütülür, yanındaki küçük simgeli sistem çubuğu ekranın üst ortasına (durum etkilerinin altına) taşınır.
    ///    "Normal": oyunun kendi yerleşimi.
    ///  - Ekran düğmeleri boyutu ve saydamlığı (MobileHud uygular).
    /// MobileBootstrap saniyede bir Tick çağırır; ayar değişmedikçe bir şey yapılmaz.
    /// </summary>
    public static class MobilArayuzDuzeni {

        private const string CubukKey = "yetenek-cubugu-boyutu";
        private const string DugmeBoyutuKey = "ekran-dugmeleri-boyutu";
        private const string SaydamlikKey = "ekran-dugmeleri-saydamligi";
        private const float BuyukOlcek = 1.35f;

        private static readonly float[] dugmeBoyutlari = { 0.85f, 1f, 1.15f };
        private static readonly float[] saydamliklar = { 0.5f, 0.75f, 1f };

        /// <summary>0: Normal, 1: Büyük (telefonda varsayılan)</summary>
        public static int CubukSecimi {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(CubukKey, 1), 0, 1); }
            set { PlayerPrefs.SetInt(CubukKey, Mathf.Clamp(value, 0, 1)); }
        }

        /// <summary>0: Küçük, 1: Normal, 2: Büyük</summary>
        public static int DugmeBoyutuSecimi {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(DugmeBoyutuKey, 1), 0, dugmeBoyutlari.Length - 1); }
            set { PlayerPrefs.SetInt(DugmeBoyutuKey, value); }
        }

        /// <summary>0: %50, 1: %75, 2: %100</summary>
        public static int SaydamlikSecimi {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(SaydamlikKey, 2), 0, saydamliklar.Length - 1); }
            set { PlayerPrefs.SetInt(SaydamlikKey, value); }
        }

        public static float DugmeOlcegi { get { return dugmeBoyutlari[DugmeBoyutuSecimi]; } }
        public static float DugmeSaydamligi { get { return saydamliklar[SaydamlikSecimi]; } }

        // oyunun kendi yerleşimi (Normal'e dönmek için)
        private struct Yer {
            public bool alindi;
            public Transform ust;
            public int sira;
            public Vector2 anchorMin, anchorMax, pivot, anchoredPosition;
            public Vector3 scale;
        }

        private static Yer cubukYeri;
        private static Yer sistemYeri;
        private static RectTransform sonCubuk = null;
        private static RectTransform sonSistem = null;
        private static int uygulanan = -1;

        public static void Tick(SystemGameManager oyun) {
            if (oyun == null || oyun.UIManager == null || oyun.UIManager.ActionBarManager == null) {
                return;
            }
            ActionBarManager cubuklar = oyun.UIManager.ActionBarManager;
            if (cubuklar.ActionBarControllers == null || cubuklar.ActionBarControllers.Count == 0 || cubuklar.ActionBarControllers[0] == null) {
                return;
            }
            RectTransform cubuk = cubuklar.ActionBarControllers[0].transform as RectTransform;
            RectTransform sistem = cubuklar.SystemBarController != null ? cubuklar.SystemBarController.transform as RectTransform : null;
            int secim = CubukSecimi;
            if (secim == uygulanan && cubuk == sonCubuk && sistem == sonSistem) {
                return;
            }
            if (cubuk != sonCubuk) {
                cubukYeri = Al(cubuk);
                sonCubuk = cubuk;
            }
            if (sistem != sonSistem) {
                sistemYeri = sistem != null ? Al(sistem) : default(Yer);
                sonSistem = sistem;
            }
            uygulanan = secim;

            if (secim == 0) {
                Geri(cubuk, cubukYeri);
                if (sistem != null) {
                    Geri(sistem, sistemYeri);
                }
                return;
            }

            // yetenek çubuğu: alt ortada, alt kenarı yerinde kalarak büyür
            cubuk.anchorMin = new Vector2(0.5f, 0f);
            cubuk.anchorMax = new Vector2(0.5f, 0f);
            cubuk.pivot = new Vector2(0.5f, 0f);
            cubuk.anchoredPosition = Vector2.zero;
            cubuk.localScale = new Vector3(BuyukOlcek, BuyukOlcek, 1f);

            // sistem çubuğu: tuvalin üst ortasına, durum etkileri çubuğunun altına
            if (sistem != null) {
                Canvas tuval = sistem.GetComponentInParent<Canvas>();
                Transform kok = tuval != null ? tuval.rootCanvas.transform : null;
                if (kok != null) {
                    sistem.SetParent(kok, false);
                    sistem.anchorMin = new Vector2(0.5f, 1f);
                    sistem.anchorMax = new Vector2(0.5f, 1f);
                    sistem.pivot = new Vector2(0.5f, 1f);
                    sistem.anchoredPosition = new Vector2(0f, -62f);
                    sistem.localScale = Vector3.one;
                }
            }
        }

        private static Yer Al(RectTransform rt) {
            return new Yer() {
                alindi = true, ust = rt.parent, sira = rt.GetSiblingIndex(), anchorMin = rt.anchorMin, anchorMax = rt.anchorMax,
                pivot = rt.pivot, anchoredPosition = rt.anchoredPosition, scale = rt.localScale
            };
        }

        private static void Geri(RectTransform rt, Yer yer) {
            if (yer.alindi == false) {
                return;
            }
            if (rt.parent != yer.ust && yer.ust != null) {
                rt.SetParent(yer.ust, false);
                rt.SetSiblingIndex(yer.sira);
            }
            rt.anchorMin = yer.anchorMin;
            rt.anchorMax = yer.anchorMax;
            rt.pivot = yer.pivot;
            rt.anchoredPosition = yer.anchoredPosition;
            rt.localScale = yer.scale;
        }
    }
}
