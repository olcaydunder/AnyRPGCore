using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Ötüken: bellek dolup oyunun kapanmasına (hata panosunda "Bellek yetmedi, sistem oyunu kapattı") ve kasmalara karşı.
    ///  - belleği az telefonda (4,5 GB altı) ya da daha önce bellek uyarısı almış telefonda dokular yarı çözünürlükte
    ///    yüklenir (4K harita dokuları 2K olur; bellek kullanımı belirgin düşer, görüntü telefonda pek fark etmez)
    ///  - sistem "bellek azaldı" deyince kullanılmayan varlıklar boşaltılır, çöp toplanır ve dokular küçültülür;
    ///    bu telefon bir sonraki açılışta da küçük dokularla başlar
    /// Akıcılık bekçisi (OyunAyarlari.AkicilikBekcisi) düşük kare hızında kaliteyi ayrıca düşürür; çöp toplama
    /// adım adım yapılır (Oyuncu Ayarları: artımlı GC), uzun takılmalar olmaz.
    /// </summary>
    public static class BellekKoruma {

        private const string UyariKey = "bellek-uyarisi";
        private const int AzBellekMb = 4500;

        private static bool kuruldu = false;
        private static volatile bool temizlikIstendi = false;
        private static float sonTemizlik = -100f;

        public static int DokuSiniri {
            get {
                if (Application.isEditor || Application.isBatchMode || Application.isMobilePlatform == false) {
                    return 0;
                }
                int bellek = SystemInfo.systemMemorySize;
                if ((bellek > 0 && bellek < AzBellekMb) || PlayerPrefs.GetInt(UyariKey, 0) > 0) {
                    return 1;
                }
                return 0;
            }
        }

        /// <summary>açılışta (sahne yüklenmeden) ve MobileSupport'ta saniyede bir</summary>
        public static void Tick() {
            if (kuruldu == false) {
                kuruldu = true;
                Application.lowMemory += () => temizlikIstendi = true;
            }
            // kalite seviyesi değişince (Seçenekler, akıcılık bekçisi) sınır seviyenin kendi değerine döner: yeniden uygula
            int sinir = DokuSiniri;
            if (QualitySettings.globalTextureMipmapLimit < sinir) {
                QualitySettings.globalTextureMipmapLimit = sinir;
            }
            if (temizlikIstendi && Time.unscaledTime - sonTemizlik > 20f) {
                temizlikIstendi = false;
                sonTemizlik = Time.unscaledTime;
                int kez = PlayerPrefs.GetInt(UyariKey, 0) + 1;
                PlayerPrefs.SetInt(UyariKey, kez);
                PlayerPrefs.Save();
                if (QualitySettings.globalTextureMipmapLimit < 1) {
                    QualitySettings.globalTextureMipmapLimit = 1;
                }
                Resources.UnloadUnusedAssets();
                System.GC.Collect();
                Debug.LogWarning("[Bellek] cihazın belleği azaldı (" + SystemInfo.systemMemorySize + " MB): varlıklar boşaltıldı, dokular küçültüldü (" + kez + ". uyarı)");
            }
        }
    }
}
