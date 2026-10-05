using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AnyRPG {

    /// <summary>
    /// 4K görüntünün renk ve ışık katmanı: grafik kalitesi 4K iken ana kameraya küresel bir hacim (Volume) uygulanır:
    ///  - Tonlama (Neutral): parlak yerler patlamaz, renkler bozulmaz (kamera HDR çizer)
    ///  - Işıltı (Bloom): Ötüken Taşı kristalleri, runeler, büyüler ve ateş hafifçe parlar
    ///  - Renk ayarı: biraz daha canlı ve kontrastlı
    ///  - Hafif kenar gölgesi (Vignette)
    /// Hacim kendi katmanındadır (30); yalnız 4K'da ana kameranın hacim maskesine eklenir, öteki kalitelerde hiçbir
    /// kamera onu görmez. Ayarlar kodla kurulur (sahneye ya da varlığa bağlı değil); OyunAyarlari çağırır.
    /// </summary>
    public static class GorselEtkiler {

        public const int Katman = 30;

        private static Volume hacim = null;

        /// <summary>4K açıkken hacmi kurar/açar, kapalıyken kapatır</summary>
        public static void Uygula(bool acik) {
            if (acik == false) {
                if (hacim != null) {
                    hacim.enabled = false;
                }
                return;
            }
            if (Application.isBatchMode && Sunucu.BatchSunucuOlsun) {
                return;
            }
            if (hacim == null) {
                Kur();
            }
            hacim.enabled = true;
        }

        public static bool Acik {
            get { return hacim != null && hacim.enabled; }
        }

        private static void Kur() {
            GameObject go = new GameObject("[Gorsel4K]");
            go.layer = Katman;
            Object.DontDestroyOnLoad(go);
            hacim = go.AddComponent<Volume>();
            hacim.isGlobal = true;
            hacim.priority = 50f;
            hacim.weight = 1f;

            VolumeProfile profil = ScriptableObject.CreateInstance<VolumeProfile>();
            profil.name = "Gorsel4K";

            Tonemapping tonlama = profil.Add<Tonemapping>(false);
            tonlama.mode.Override(TonemappingMode.Neutral);

            Bloom isilti = profil.Add<Bloom>(false);
            isilti.threshold.Override(1.1f);
            isilti.intensity.Override(0.3f);
            isilti.scatter.Override(0.6f);
            isilti.highQualityFiltering.Override(false);

            ColorAdjustments renk = profil.Add<ColorAdjustments>(false);
            // açık renkli kayalar ve sis solmasın: pozlama artmaz, kontrast biraz yüksek
            renk.postExposure.Override(0f);
            renk.contrast.Override(14f);
            renk.saturation.Override(12f);

            Vignette kenar = profil.Add<Vignette>(false);
            kenar.intensity.Override(0.18f);
            kenar.smoothness.Override(0.45f);

            hacim.sharedProfile = profil;
        }
    }
}
