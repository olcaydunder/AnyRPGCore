using System.Collections.Generic;
using IngameDebugConsole;
using Tayx.Graphy;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace AnyRPG {

    /// <summary>
    /// Metin2 tarzı sistem ve oyun seçenekleri: değerleri saklar (PlayerPrefs) ve oyuna uygular.
    /// Pencere: SeceneklerPenceresi. Uygulama: MobileBootstrap her saniye Tick çağırır; kamera, arazi ve
    /// yeni doğan canavarlar böylece sahne değişse de ayarlara uyar.
    ///
    /// Akıcılığı etkileyenler: grafik kalitesi, çözünürlük, gölge, görüş mesafesi, bitki yoğunluğu, isim mesafesi,
    /// FPS sınırı ve ekran dışındaki canavarların animasyonunu durdurma. Ayrıca küçük nesneler (taş, çalı, çit, eşya)
    /// ekranda çok küçük kalınca hiç çizilmez (KucukNesneler); eşik görüş mesafesine göre değişir.
    /// </summary>
    public static class OyunAyarlari {

        // AnyRPG'nin kendi görüntü ayarıyla ortak anahtar (SystemVideoPanel)
        public const string KaliteKey = "GraphicsQualityIndex";
        private const string CozunurlukKey = "ayar-cozunurluk";
        private const string GolgeKey = "ayar-golge";
        private const string FpsKey = "ayar-fps";
        private const string GorusKey = "ayar-gorus";
        private const string BitkiKey = "ayar-bitki";
        private const string IsimMesafesiKey = "ayar-isim-mesafesi";
        private const string AnimasyonKey = "ayar-ekran-disi-animasyon";
        private const string FpsGostergeKey = "ayar-fps-gostergesi";
        private const string KonsolKey = "ayar-hata-konsolu";

        // seçenek değerleri (pencerede soldan sağa)
        public static readonly int[] KaliteSeviyeleri = { 1, 2, 3 };              // Düşük, Orta, Yüksek (QualitySettings)
        private static readonly float[] cozunurlukOlcekleri = { 0f, 0.6f, 0.8f, 1f }; // Otomatik, Düşük, Orta, Yüksek
        private static readonly float[] golgeMesafeleri = { -1f, 0f, 15f, 35f };     // Otomatik, Kapalı, Yakın, Uzak
        private static readonly int[] fpsSinirlari = { 30, 60 };
        private static readonly float[] gorusMesafeleri = { 120f, 160f, 200f };     // Yakın, Orta, Uzak (kameranın varsayılanı 200)
        private static readonly float[] bitkiYogunluklari = { 0.3f, 0.6f, 1f };
        private static readonly float[] bitkiMesafeleri = { 40f, 60f, 80f };
        private static readonly float[] isimMesafeleri = { 20f, 30f, 40f, 60f };

        private const int AlwaysVisibleLayer = 31;

        // küçük nesneler: ekran yüksekliğine oranla bu boydan küçük kalınca çizilmez (Görüş: Yakın, Orta, Uzak)
        private static readonly float[] kucukNesneEsikleri = { 0.035f, 0.022f, 0.012f };
        // en büyük boyutu bundan küçük olanlar "küçük nesne" sayılır (metre)
        private const float KucukNesneBoyutu = 6f;
        private const int KucukNesneParti = 300;

        // ilk görülen değerler ("Otomatik" seçilince geri dönmek için)
        private static readonly Dictionary<UniversalRenderPipelineAsset, Vector2> urpVarsayilan = new Dictionary<UniversalRenderPipelineAsset, Vector2>();
        private static readonly Dictionary<int, Vector3> araziVarsayilan = new Dictionary<int, Vector3>();
        private static readonly Dictionary<int, AnimatorCullingMode> animatorVarsayilan = new Dictionary<int, AnimatorCullingMode>();

        private static RenderPipelineAsset sonUrp = null;
        private static Camera sonKamera = null;
        private static int sonKameraAyari = -1;
        private static int sonAraziAyari = -1;
        private static int tickSayaci = 0;
        private static GameObject graphyNesnesi = null;
        private static bool graphyAyarlanacak = false;
        private static GameObject konsolNesnesi = null;

        // ---------------------------------------------------------------- değerler

        public static int Kalite {
            get { return System.Array.IndexOf(KaliteSeviyeleri, QualitySettings.GetQualityLevel()); }
            set {
                int level = KaliteSeviyeleri[Mathf.Clamp(value, 0, KaliteSeviyeleri.Length - 1)];
                PlayerPrefs.SetInt(KaliteKey, level);
                PlayerPrefs.SetInt(ElleKey, 1);
                QualitySettings.SetQualityLevel(level, true);
                // yeni kalitenin URP ayarlarına çözünürlük ve gölge seçimini yeniden uygula
                Degisti();
            }
        }

        public static int Cozunurluk { get => Al(CozunurlukKey, 0, cozunurlukOlcekleri.Length); set => Yaz(CozunurlukKey, value); }
        public static int Golge { get => Al(GolgeKey, 0, golgeMesafeleri.Length); set => Yaz(GolgeKey, value); }
        public static int Fps { get => Al(FpsKey, 1, fpsSinirlari.Length); set => Yaz(FpsKey, value); }
        public static int Gorus { get => Al(GorusKey, 2, gorusMesafeleri.Length); set => Yaz(GorusKey, value); }
        public static int Bitki { get => Al(BitkiKey, 2, bitkiYogunluklari.Length); set => Yaz(BitkiKey, value); }
        public static int IsimMesafesiSecimi { get => Al(IsimMesafesiKey, 2, isimMesafeleri.Length); set => Yaz(IsimMesafesiKey, value); }
        public static bool EkranDisiAnimasyonuDurdur { get => Al(AnimasyonKey, 1, 2) == 1; set => Yaz(AnimasyonKey, value ? 1 : 0); }
        public static bool FpsGostergesi { get => Al(FpsGostergeKey, 0, 2) == 1; set => Yaz(FpsGostergeKey, value ? 1 : 0); }
        public static bool HataKonsolu { get => Al(KonsolKey, 0, 2) == 1; set => Yaz(KonsolKey, value ? 1 : 0); }

        /// <summary>
        /// isim levhalarının göründüğü en uzak mesafe (NameplateController)
        /// </summary>
        public static float IsimMesafesi {
            get { return isimMesafeleri[IsimMesafesiSecimi]; }
        }

        private static int Al(string key, int varsayilan, int adet) {
            return Mathf.Clamp(PlayerPrefs.GetInt(key, varsayilan), 0, adet - 1);
        }

        private static void Yaz(string key, int value) {
            PlayerPrefs.SetInt(key, value);
            // oyuncu kendisi seçti: akıcılık bekçisi artık ayarlara dokunmaz
            PlayerPrefs.SetInt(ElleKey, 1);
            Degisti();
        }

        // ---------------------------------------------------------------- akıcılık bekçisi

        private const string ElleKey = "ayar-elle-degisti";
        private const float OlcumSuresi = 20f;
        private static readonly string[] kaliteAdlari = { "Düşük", "Orta", "Yüksek" };
        private static float olcumBaslangic = -1f;
        private static int olcumKaresi = 0;
        private static float olcumBekleme = 0f;
        private static int dusurme = 0;

        /// <summary>
        /// Otomatik grafik ayarı: ilk açılıştaki cihaz tahmini (IlkAcilisAyari) yanılırsa (ör. belleği çok ama ekran
        /// kartı orta telefonlar) oyunda 20 saniyelik ortalama kare hızı hedefin üçte ikisinin altında kalınca kalite bir
        /// basamak, en düşükte de çözünürlük indirilir; oyuncuya yazılır. Oyuncu Seçenekler'den bir ayar değiştirdiyse
        /// hiç karışmaz. Oturum başına en çok iki kez indirir; harita yüklenirken ölçmez.
        /// </summary>
        private static void AkicilikBekcisi(SystemGameManager systemGameManager) {
            if (Application.isEditor || dusurme >= 2 || PlayerPrefs.GetInt(ElleKey, 0) == 1) {
                return;
            }
            UnitController oyuncu = systemGameManager != null && systemGameManager.PlayerManagerClient != null
                && systemGameManager.PlayerManagerClient.PlayerUnitSpawned ? systemGameManager.PlayerManagerClient.UnitController : null;
            if (oyuncu == null || Time.unscaledTime < olcumBekleme) {
                olcumBaslangic = -1f;
                return;
            }
            if (olcumBaslangic < 0f) {
                olcumBaslangic = Time.unscaledTime;
                olcumKaresi = Time.frameCount;
                return;
            }
            float gecen = Time.unscaledTime - olcumBaslangic;
            if (gecen < OlcumSuresi) {
                return;
            }
            float fps = (Time.frameCount - olcumKaresi) / gecen;
            olcumBaslangic = -1f;
            float hedef = fpsSinirlari[Fps];
            if (fps >= hedef * 0.66f) {
                return;
            }
            string mesaj;
            int kalite = Kalite;
            if (kalite > 0) {
                int yeni = kalite - 1;
                PlayerPrefs.SetInt(KaliteKey, KaliteSeviyeleri[yeni]);
                QualitySettings.SetQualityLevel(KaliteSeviyeleri[yeni], true);
                mesaj = $"Akıcılık için grafik kalitesi {kaliteAdlari[yeni]} yapıldı ({fps:0} FPS ölçüldü).";
            } else if (Cozunurluk == 0 || Cozunurluk > 2) {
                PlayerPrefs.SetInt(CozunurlukKey, 2);
                mesaj = $"Akıcılık için çözünürlük Orta yapıldı ({fps:0} FPS ölçüldü).";
            } else {
                dusurme = 2;
                return;
            }
            dusurme++;
            Degisti();
            olcumBekleme = Time.unscaledTime + 5f;
            Debug.Log("OyunAyarlari: " + mesaj);
            oyuncu.WriteMessageFeedMessage("<color=#FFD54A>" + mesaj + " Menü > Grafik'ten değiştirebilirsin.</color>");
        }

        private static void Degisti() {
            sonKameraAyari = -1;
            sonAraziAyari = -1;
            PlayerPrefs.Save();
            Uygula();
        }

        // ---------------------------------------------------------------- ilk açılış: cihaza göre grafik

        private const string OtomatikGrafikKey = "ayar-otomatik-grafik";

        /// <summary>
        /// Oyun ilk kez açılınca (oyuncu henüz kalite seçmediyse) cihazın belleğine ve ekran kartına göre kalite,
        /// kare hızı sınırı ve çözünürlük seçer: güçlü telefonda Yüksek/60, orta telefonda Orta/60, zayıf telefonda
        /// Düşük/30. Oyuncu sonra Seçenekler'den değiştirebilir. MobileBootstrap sahne yüklenmeden çağırır.
        /// </summary>
        public static void IlkAcilisAyari() {
            if (PlayerPrefs.GetInt(OtomatikGrafikKey, 0) == 1) {
                return;
            }
            PlayerPrefs.SetInt(OtomatikGrafikKey, 1);
            if (PlayerPrefs.HasKey(KaliteKey)) {
                // eski kurulum: oyuncunun seçimi kalır
                PlayerPrefs.Save();
                return;
            }
            int seviye = CihazSeviyesi();
            PlayerPrefs.SetInt(KaliteKey, KaliteSeviyeleri[seviye]);
            QualitySettings.SetQualityLevel(KaliteSeviyeleri[seviye], true);
            PlayerPrefs.SetInt(FpsKey, seviye == 0 ? 0 : 1);
            if (seviye == 0) {
                PlayerPrefs.SetInt(CozunurlukKey, 2);
            }
            PlayerPrefs.Save();
            Debug.Log($"OyunAyarlari: ilk açılış grafik seviyesi {seviye} (bellek {SystemInfo.systemMemorySize} MB, {SystemInfo.graphicsDeviceName})");
        }

        /// <summary>0: zayıf, 1: orta, 2: güçlü cihaz</summary>
        public static int CihazSeviyesi() {
            int bellek = SystemInfo.systemMemorySize;
            int seviye = bellek >= 7000 ? 2 : (bellek >= 3500 ? 1 : 0);
            string ekranKarti = SystemInfo.graphicsDeviceName ?? string.Empty;
            // eski kuşak ekran kartları bir basamak aşağı
            if (ekranKarti.Contains("Adreno (TM) 5") || ekranKarti.Contains("Adreno (TM) 4") || ekranKarti.Contains("Mali-T")
                || ekranKarti.Contains("Mali-G5") || ekranKarti.Contains("Mali-G6") || ekranKarti.Contains("PowerVR")) {
                seviye = Mathf.Max(0, seviye - 1);
            }
            if (SystemInfo.processorCount <= 4) {
                seviye = Mathf.Max(0, seviye - 1);
            }
            return seviye;
        }

        // ---------------------------------------------------------------- uygulama

        /// <summary>
        /// bütün ayarları hemen uygula (oyun açılışında ve bir ayar değişince)
        /// </summary>
        public static void Uygula() {
            Application.targetFrameRate = fpsSinirlari[Fps];
            QualitySettings.vSyncCount = 0;
            UygulaUrp();
            UygulaKamera(KameraBul());
            UygulaArazi();
            UygulaAraclar();
        }

        /// <summary>
        /// MobileBootstrap her saniye çağırır: değişen kamera ve araziye ayarları uygular, yeni canavarları ayarlar
        /// </summary>
        public static void Tick(SystemGameManager systemGameManager) {
            // kalite değişince (AnyRPG'nin kendi Görüntü ayarı da değiştirebilir) yeni URP varlığına uygula
            if (GraphicsSettings.currentRenderPipeline != sonUrp) {
                UygulaUrp();
            }
            Camera kamera = KameraBul(systemGameManager);
            if (kamera != sonKamera || sonKameraAyari != KameraAyarKodu()) {
                UygulaKamera(kamera);
            }
            if (sonAraziAyari != AraziAyarKodu()) {
                UygulaArazi();
            }
            if (graphyAyarlanacak) {
                GraphyAyarla();
            }
            tickSayaci++;
            if (tickSayaci % 3 == 0) {
                UygulaAnimatorler(systemGameManager);
            }
            KucukNesneler();
            AkicilikBekcisi(systemGameManager);
        }

        public static void SahneYuklendi() {
            // yükleme takılması ölçülmesin
            olcumBaslangic = -1f;
            olcumBekleme = Time.unscaledTime + 8f;
            sonKamera = null;
            sonAraziAyari = -1;
            kucukNesneKuyrugu = null;
            kucukNesneGruplari.Clear();
            KucukNesneSayisi = 0;
        }

        // ---------------------------------------------------------------- küçük nesneleri uzakta çizme

        private static Queue<MeshRenderer> kucukNesneKuyrugu = null;
        private static readonly List<LODGroup> kucukNesneGruplari = new List<LODGroup>();
        private static int kucukNesneSahnesi = -1;
        private static int uygulananGorus = -1;

        /// <summary>bu sahnede uzakta gizlenen küçük nesne sayısı (oyun testi raporu)</summary>
        public static int KucukNesneSayisi { get; private set; }

        /// <summary>
        /// Sahne yüklenince her küçük MeshRenderer'a tek kademeli bir LODGroup eklenir: nesne ekranda eşikten küçük
        /// kalınca Unity onu hiç çizmez (çizim çağrısı ve üçgen kazancı; en çok ağaç, çalı ve taşla dolu haritalarda).
        /// Karakterler, etkileşimli nesneler (sandık, kapı, NPC) ve zaten LOD'u olanlar dokunulmaz.
        /// Her saniye en çok 300 nesne işlenir, sahne açılışında takılma olmasın.
        /// </summary>
        private static void KucukNesneler() {
            UnityEngine.SceneManagement.Scene sahne = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (sahne.handle != kucukNesneSahnesi) {
                kucukNesneSahnesi = sahne.handle;
                kucukNesneGruplari.Clear();
                KucukNesneSayisi = 0;
                kucukNesneKuyrugu = new Queue<MeshRenderer>(Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            }
            float esik = kucukNesneEsikleri[Mathf.Clamp(Gorus, 0, kucukNesneEsikleri.Length - 1)];
            if (uygulananGorus != Gorus) {
                // görüş ayarı değişti: eklenmiş grupların eşiğini güncelle
                uygulananGorus = Gorus;
                foreach (LODGroup grup in kucukNesneGruplari) {
                    if (grup != null) {
                        LOD[] kademeler = grup.GetLODs();
                        if (kademeler.Length == 1) {
                            kademeler[0].screenRelativeTransitionHeight = esik;
                            grup.SetLODs(kademeler);
                        }
                    }
                }
            }
            if (kucukNesneKuyrugu == null) {
                return;
            }
            int islenen = 0;
            while (kucukNesneKuyrugu.Count > 0 && islenen < KucukNesneParti) {
                MeshRenderer cizici = kucukNesneKuyrugu.Dequeue();
                islenen++;
                if (cizici == null || cizici.enabled == false || cizici.gameObject.scene.handle != kucukNesneSahnesi
                    || cizici.gameObject.layer == AlwaysVisibleLayer) {
                    continue;
                }
                if (cizici.bounds.size.magnitude > KucukNesneBoyutu) {
                    continue;
                }
                if (cizici.GetComponentInParent<LODGroup>() != null || cizici.GetComponentInParent<InteractableBase>() != null
                    || cizici.GetComponentInParent<Animator>() != null) {
                    continue;
                }
                LODGroup yeni = cizici.gameObject.AddComponent<LODGroup>();
                yeni.SetLODs(new LOD[] { new LOD(esik, new Renderer[] { cizici }) });
                yeni.RecalculateBounds();
                kucukNesneGruplari.Add(yeni);
                KucukNesneSayisi++;
            }
            if (kucukNesneKuyrugu.Count == 0) {
                kucukNesneKuyrugu = null;
            }
        }

        private static Camera KameraBul(SystemGameManager systemGameManager = null) {
            Camera kamera = null;
            if (systemGameManager != null && systemGameManager.CameraManager != null) {
                kamera = systemGameManager.CameraManager.ActiveMainCamera;
            }
            if (kamera == null) {
                kamera = Camera.main;
            }
            return kamera;
        }

        private static void UygulaUrp() {
            sonUrp = GraphicsSettings.currentRenderPipeline;
            UniversalRenderPipelineAsset asset = sonUrp as UniversalRenderPipelineAsset;
            if (asset == null) {
                return;
            }
            if (urpVarsayilan.TryGetValue(asset, out Vector2 varsayilan) == false) {
                varsayilan = new Vector2(asset.renderScale, asset.shadowDistance);
                urpVarsayilan[asset] = varsayilan;
            }
            float olcek = cozunurlukOlcekleri[Cozunurluk];
            asset.renderScale = olcek > 0f ? olcek : varsayilan.x;
            float golge = golgeMesafeleri[Golge];
            asset.shadowDistance = golge > 0f ? golge : varsayilan.y;
        }

        private static int KameraAyarKodu() {
            return Gorus * 10 + Golge;
        }

        private static void UygulaKamera(Camera kamera) {
            sonKamera = kamera;
            if (kamera == null) {
                return;
            }
            sonKameraAyari = KameraAyarKodu();
            // ana kameranın katman başına uzak sınırı (CameraCullDistances ile aynı düzen; 31 = AlwaysVisible hep görünür)
            float mesafe = gorusMesafeleri[Gorus];
            float[] uzakliklar = new float[32];
            for (int i = 0; i < uzakliklar.Length; i++) {
                uzakliklar[i] = i == AlwaysVisibleLayer ? 0f : mesafe;
            }
            kamera.layerCullDistances = uzakliklar;
            // layerCullSpherical yalnız yerleşik çizicide çalışır; URP'de her atamada uyarı yazıyordu (hata panosu)
            UniversalAdditionalCameraData kameraVerisi = kamera.GetUniversalAdditionalCameraData();
            if (kameraVerisi != null) {
                kameraVerisi.renderShadows = golgeMesafeleri[Golge] != 0f;
            }
        }

        private static int AraziAyarKodu() {
            return Bitki * 10 + Gorus + Terrain.activeTerrains.Length * 100;
        }

        private static void UygulaArazi() {
            sonAraziAyari = AraziAyarKodu();
            foreach (Terrain arazi in Terrain.activeTerrains) {
                if (arazi == null) {
                    continue;
                }
                int id = arazi.GetInstanceID();
                if (araziVarsayilan.TryGetValue(id, out Vector3 varsayilan) == false) {
                    varsayilan = new Vector3(arazi.detailObjectDensity, arazi.detailObjectDistance, arazi.treeDistance);
                    araziVarsayilan[id] = varsayilan;
                }
                arazi.detailObjectDensity = Mathf.Min(varsayilan.x, bitkiYogunluklari[Bitki]);
                arazi.detailObjectDistance = Mathf.Min(varsayilan.y, bitkiMesafeleri[Bitki]);
                // Uzak: sahnenin kendi ağaç mesafesi (görünüm değişmez); Yakın/Orta: uzaktaki ağaçlar çizilmez
                arazi.treeDistance = Gorus == gorusMesafeleri.Length - 1 ? varsayilan.z : Mathf.Min(varsayilan.z, gorusMesafeleri[Gorus] + 60f);
            }
        }

        /// <summary>
        /// Ekran dışındaki canavarların iskeletini hiç hesaplama (Animator.CullCompletely). Vuruşlar animasyon olayına
        /// değil zamanlamaya bağlı (CharacterAbilityManager) olduğu için savaş etkilenmez; oyuncunun kendi modeli hep oynar.
        /// </summary>
        private static void UygulaAnimatorler(SystemGameManager systemGameManager) {
            UnitController oyuncu = systemGameManager?.PlayerManagerClient?.UnitController;
            bool durdur = EkranDisiAnimasyonuDurdur;
            Animator[] animatorler = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
            foreach (Animator animator in animatorler) {
                if (animator == null) {
                    continue;
                }
                int id = animator.GetInstanceID();
                if (animatorVarsayilan.ContainsKey(id) == false) {
                    // yalnız birim modelleri (canavar, NPC); kapılar, sandıklar, arayüz önizlemeleri olduğu gibi kalır
                    UnitController birim = animator.GetComponentInParent<UnitController>();
                    if (birim == null || birim == oyuncu) {
                        continue;
                    }
                    animatorVarsayilan[id] = animator.cullingMode;
                }
                AnimatorCullingMode istenen = durdur ? AnimatorCullingMode.CullCompletely : animatorVarsayilan[id];
                if (animator.cullingMode != istenen) {
                    animator.cullingMode = istenen;
                }
            }
            if (animatorVarsayilan.Count > 2000) {
                animatorVarsayilan.Clear();
            }
        }

        // ---------------------------------------------------------------- GitHub araçları

        private static void UygulaAraclar() {
            HariciAraclar araclar = HariciAraclar.Instance;

            // Graphy: FPS ve bellek göstergesi
            if (FpsGostergesi && graphyNesnesi == null && araclar != null && araclar.GraphyPrefab != null) {
                graphyNesnesi = Object.Instantiate(araclar.GraphyPrefab);
                graphyNesnesi.name = "[Graphy]";
                // modüller Start'ta kurulur; düzeni bir sonraki saniyede ver
                graphyAyarlanacak = true;
            } else if (FpsGostergesi == false && graphyNesnesi != null) {
                Object.Destroy(graphyNesnesi);
                graphyNesnesi = null;
                graphyAyarlanacak = false;
            }

            // In-game Debug Console: köşede küçük bir sayaç, dokununca hata günlüğü açılır
            if (HataKonsolu && konsolNesnesi == null && araclar != null && araclar.KonsolPrefab != null) {
                if (DebugLogManager.Instance != null) {
                    konsolNesnesi = DebugLogManager.Instance.gameObject;
                } else {
                    konsolNesnesi = Object.Instantiate(araclar.KonsolPrefab);
                    konsolNesnesi.name = "IngameDebugConsole";
                }
            } else if (HataKonsolu == false && konsolNesnesi != null) {
                Object.Destroy(konsolNesnesi);
                konsolNesnesi = null;
            }
        }

        private static void GraphyAyarla() {
            graphyAyarlanacak = false;
            if (graphyNesnesi == null) {
                return;
            }
            try {
                GraphyManager graphy = graphyNesnesi.GetComponent<GraphyManager>();
                if (graphy == null) {
                    return;
                }
                // sağ üstte mini harita var: sol üstte kare hızı grafiği ve bellek yazısı, ses ve ayrıntı modülleri kapalı
                graphy.SetPreset(GraphyManager.ModulePreset.FPS_FULL_RAM_TEXT);
                graphy.SetModulePosition(GraphyManager.ModuleType.FPS, GraphyManager.ModulePosition.TOP_LEFT);
                graphy.SetModulePosition(GraphyManager.ModuleType.RAM, GraphyManager.ModulePosition.TOP_LEFT);
            } catch (System.Exception exception) {
                Debug.LogWarning($"OyunAyarlari.GraphyAyarla(): {exception.Message}");
            }
        }
    }
}
