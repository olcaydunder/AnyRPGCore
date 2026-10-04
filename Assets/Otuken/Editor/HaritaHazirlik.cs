using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Otuken.EditorAraclari {

    /// <summary>
    /// GitHub Actions (GameCI) derlemesinin giriş noktası (workflow'da buildMethod).
    /// Önce haritaları hazırlar (HaritaHazirlik), sonra GameCI'nin standart derleme betiğini
    /// (Assets/Editor/UnityBuilderAction) çalıştırır. Hazırlık hata verse bile derleme sürer.
    /// </summary>
    public static class Derleme {

        /// <summary>workflow'daki "yalnız arayüz önizlemesi" seçeneği: haritalar işlenmez, APK derlenmez (hızlı deneme)</summary>
        public static bool SadeceArayuz {
            get { return Environment.GetCommandLineArgs().Contains("-sadeceArayuz"); }
        }

        public static void Derle() {
            try {
                HaritaHazirlik.Calistir();
            } catch (Exception e) {
                Debug.LogError("HaritaHazirlik başarısız oldu, derlemeye devam ediliyor: " + e);
            }
            if (SadeceArayuz) {
                // GameCI derleme klasörünü bekler
                string[] argumanlar = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(argumanlar, "-customBuildPath");
                if (i >= 0 && i + 1 < argumanlar.Length) {
                    Directory.CreateDirectory(Path.GetDirectoryName(argumanlar[i + 1]));
                }
                Debug.Log("[HaritaHazirlik] yalnız arayüz önizlemesi istendi, APK derlenmedi");
                return;
            }

            Type builder = Type.GetType("UnityBuilderAction.Builder, UnityBuilderAction");
            if (builder == null) {
                throw new Exception("UnityBuilderAction.Builder bulunamadı (Assets/Editor/UnityBuilderAction)");
            }
            MethodInfo derle = builder.GetMethod("BuildProject", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            try {
                derle.Invoke(null, null);
            } catch (TargetInvocationException e) {
                throw e.InnerException ?? e;
            }
        }
    }

    /// <summary>
    /// Yeni haritalar için derleme öncesi işler:
    ///  1. Yürüme ağı (NavMesh): Assets/Otuken/Haritalar altındaki sahnelerde verisi olmayan NavMeshSurface'leri
    ///     pişirir, sahnenin yanındaki klasöre kaydeder (bir kez pişirilip depoya eklenen ağ yeniden pişirilmez).
    ///  2. Tanı raporu (tani/rapor.txt): bozuk gölgelendirici, eksik betik, yürüme ağı alanı, giriş noktasından
    ///     kapılara / düşman noktalarına yol var mı.
    ///  3. Ekran görüntüleri (tani/*.jpg): kuşbakışı harita, giriş noktası ve Tools~/dunya/tani.json'daki çekimler.
    /// Hepsi tani/ klasörüne yazılır; workflow onu "tani" ön sürümüne yükler.
    /// </summary>
    public static class HaritaHazirlik {

        public const string HaritaKlasoru = "Assets/Otuken/Haritalar";
        private const string AyarDosyasi = "Tools~/dunya/tani.json";

        private static StringBuilder rapor;
        private static string taniKlasoru;

        [Serializable]
        private class Cekim {
            public string ad = "";
            public float[] konum = new float[0];
            public float[] hedef = new float[0];
            public float fov = 55f;
        }

        [Serializable]
        private class SahneAyari {
            public string yol = "";
            public bool kusbakisi = true;
            public List<Cekim> cekimler = new List<Cekim>();
        }

        [Serializable]
        private class TaniAyari {
            public List<SahneAyari> sahneler = new List<SahneAyari>();
        }

        [MenuItem("Ötüken/Haritaları hazırla ve tanı raporu çıkar")]
        public static void Calistir() {
            taniKlasoru = Path.GetFullPath("tani");
            Directory.CreateDirectory(taniKlasoru);
            rapor = new StringBuilder();
            float baslangic = Time.realtimeSinceStartup;

            Yaz($"Unity {Application.unityVersion} | grafik: {SystemInfo.graphicsDeviceType} / {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceVersion}");
            Yaz($"hedef: {EditorUserBuildSettings.activeBuildTarget} | kalite: {QualitySettings.names[QualitySettings.GetQualityLevel()]} | boru hattı: {(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "yok")}");

            TaniAyari ayar = new TaniAyari();
            string ayarYolu = Path.GetFullPath(AyarDosyasi);
            if (File.Exists(ayarYolu)) {
                ayar = JsonUtility.FromJson<TaniAyari>(File.ReadAllText(ayarYolu)) ?? new TaniAyari();
            }

            // derlemeye giren yeni harita sahneleri + ayardaki diğer sahneler (yalnız görüntü)
            List<string> sahneler = EditorBuildSettings.scenes
                .Where(s => s.enabled && s.path.StartsWith(HaritaKlasoru + "/"))
                .Select(s => s.path).ToList();
            foreach (SahneAyari s in ayar.sahneler) {
                if (!sahneler.Contains(s.yol) && File.Exists(s.yol)) {
                    sahneler.Add(s.yol);
                }
            }

            if (Derleme.SadeceArayuz) {
                sahneler.Clear();
                Yaz("yalnız arayüz önizlemesi: haritalar işlenmedi");
            }

            bool eskiAsenkron = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try {
                foreach (string yol in sahneler) {
                    try {
                        SahneIsle(yol, ayar.sahneler.FirstOrDefault(s => s.yol == yol));
                    } catch (Exception e) {
                        Yaz($"!! HATA {yol}: {e}");
                    }
                }
                try {
                    ArayuzCek();
                } catch (Exception e) {
                    Yaz($"!! HATA arayüz önizlemesi: {e}");
                }
            } finally {
                ShaderUtil.allowAsyncCompilation = eskiAsenkron;
                AssetDatabase.SaveAssets();
                Yaz($"toplam süre: {Time.realtimeSinceStartup - baslangic:0} sn");
                File.WriteAllText(Path.Combine(taniKlasoru, "rapor.txt"), rapor.ToString());
            }

            // derleme ilk sahneden başlasın diye editörde ilk sahneyi aç
            if (EditorBuildSettings.scenes.Length > 0) {
                EditorSceneManager.OpenScene(EditorBuildSettings.scenes[0].path, OpenSceneMode.Single);
            }
        }

        private static void Yaz(string satir) {
            rapor.AppendLine(satir);
            Debug.Log("[HaritaHazirlik] " + satir);
        }

        private static void SahneIsle(string yol, SahneAyari ayar) {
            string ad = Path.GetFileNameWithoutExtension(yol);
            bool yeniHarita = yol.StartsWith(HaritaKlasoru + "/");
            Yaz("");
            Yaz($"=== {ad} ({yol})");
            Scene sahne = EditorSceneManager.OpenScene(yol, OpenSceneMode.Single);
            List<GameObject> nesneler = sahne.GetRootGameObjects()
                .SelectMany(k => k.GetComponentsInChildren<Transform>(true))
                .Select(t => t.gameObject).ToList();

            if (yeniHarita) {
                // geçit taşı ve tabelalar zemine otursun, yürüme ağı onlara göre pişsin
                int oturmayan = AnyRPG.ZemineOturt.HepsiniOturt();
                if (oturmayan > 0) {
                    Yaz($"!! zemine oturtulamayan nesne: {oturmayan} ({string.Join(", ", AnyRPG.ZemineOturt.Oturmayanlar)})");
                }
                NavMeshPisir(sahne, yol);
                List<Vector3> ulasilan = UlasilabilirNoktalar(ad);
                UlasilmayaniTasi(ulasilan);
                EditorSceneManager.MarkSceneDirty(sahne);
                EditorSceneManager.SaveScene(sahne);
                YurumeAgiRaporu(ad);
                SahneDenetle(nesneler);
            }

            DynamicGI.UpdateEnvironment();
            if (ayar == null || ayar.kusbakisi) {
                KusbakisiCek(ad);
            }
            GameObject giris = GameObject.FindGameObjectsWithTag("DefaultSpawnLocation").FirstOrDefault(g => g.scene == sahne);
            if (giris != null) {
                Vector3 p = giris.transform.position;
                Vector3 ileri = giris.transform.forward;
                Cek($"{ad}_giris", p - ileri * 7f + Vector3.up * 4f, p + ileri * 12f + Vector3.up * 1f, 55f, false, 0f, 960, 540);
            }
            if (ayar != null) {
                foreach (Cekim c in ayar.cekimler) {
                    if (c.konum.Length == 3 && c.hedef.Length == 3) {
                        Cek($"{ad}_{c.ad}", new Vector3(c.konum[0], c.konum[1], c.konum[2]),
                            new Vector3(c.hedef[0], c.hedef[1], c.hedef[2]), c.fov, false, 0f, 960, 540);
                    }
                }
            }
        }

        /// <summary>Verisi olmayan NavMeshSurface'leri pişirir ve sahnenin yanındaki klasöre kaydeder.</summary>
        private static void NavMeshPisir(Scene sahne, string yol) {
            foreach (NavMeshSurface yuzey in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (yuzey.gameObject.scene != sahne) {
                    continue;
                }
                if (yuzey.navMeshData != null) {
                    Yaz($"yürüme ağı hazır: {yuzey.name} ({AssetDatabase.GetAssetPath(yuzey.navMeshData)})");
                    continue;
                }
                float t0 = Time.realtimeSinceStartup;
                yuzey.BuildNavMesh();
                if (yuzey.navMeshData == null) {
                    Yaz($"!! yürüme ağı pişirilemedi: {yuzey.name}");
                    continue;
                }
                string ust = Path.GetDirectoryName(yol).Replace('\\', '/');
                string ad = Path.GetFileNameWithoutExtension(yol);
                string klasor = ust + "/" + ad;
                if (!AssetDatabase.IsValidFolder(klasor)) {
                    AssetDatabase.CreateFolder(ust, ad);
                }
                string varlik = klasor + "/NavMesh-" + yuzey.gameObject.name + ".asset";
                AssetDatabase.DeleteAsset(varlik);
                AssetDatabase.CreateAsset(yuzey.navMeshData, varlik);
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(yuzey);
                Yaz($"yürüme ağı pişirildi: {yuzey.name} -> {varlik} ({Time.realtimeSinceStartup - t0:0.0} sn)");

                // yerelde konum hesabı için kopyası (Tools~/dunya/navmesh.py okur)
                string hedefKlasor = Path.Combine(taniKlasoru, "navmesh", ad);
                Directory.CreateDirectory(hedefKlasor);
                File.Copy(Path.GetFullPath(varlik), Path.Combine(hedefKlasor, Path.GetFileName(varlik)), true);
                File.Copy(Path.GetFullPath(varlik) + ".meta", Path.Combine(hedefKlasor, Path.GetFileName(varlik) + ".meta"), true);
            }
        }

        /// <summary>
        /// Girişten yürüyerek ulaşılan noktalar: girişin çevresinde 3 m aralıklı ızgara, her nokta yürüme ağına
        /// oturtulup girişten tam yol var mı diye bakılır. tani/ulasilabilir/{ad}.json'a yazılır
        /// (Tools~/dunya/harita_icerik.py kamp yerlerini bunlardan seçer).
        /// </summary>
        private static List<Vector3> UlasilabilirNoktalar(string ad) {
            List<Vector3> noktalar = new List<Vector3>();
            GameObject giris = GameObject.FindGameObjectsWithTag("DefaultSpawnLocation").FirstOrDefault();
            if (giris == null || !NavMesh.SamplePosition(giris.transform.position, out NavMeshHit g, 4f, NavMesh.AllAreas)) {
                return noktalar;
            }
            const float yaricap = 170f, adim = 3f;
            NavMeshPath yolBilgisi = new NavMeshPath();
            StringBuilder json = new StringBuilder("{\"giris\": [");
            json.Append(FormattableString.Invariant($"{g.position.x:0.##}, {g.position.y:0.##}, {g.position.z:0.##}], \"noktalar\": ["));
            for (float x = -yaricap; x <= yaricap; x += adim) {
                for (float z = -yaricap; z <= yaricap; z += adim) {
                    if (x * x + z * z > yaricap * yaricap) {
                        continue;
                    }
                    Vector3 q = g.position + new Vector3(x, 0f, z);
                    if (!NavMesh.SamplePosition(q, out NavMeshHit h, 12f, NavMesh.AllAreas)) {
                        continue;
                    }
                    if (Mathf.Abs(h.position.x - q.x) > adim * 0.6f || Mathf.Abs(h.position.z - q.z) > adim * 0.6f) {
                        continue;
                    }
                    if (!NavMesh.CalculatePath(g.position, h.position, NavMesh.AllAreas, yolBilgisi)
                        || yolBilgisi.status != NavMeshPathStatus.PathComplete) {
                        continue;
                    }
                    if (noktalar.Count > 0) {
                        json.Append(", ");
                    }
                    noktalar.Add(h.position);
                    json.Append(FormattableString.Invariant($"[{h.position.x:0.##}, {h.position.y:0.##}, {h.position.z:0.##}]"));
                }
            }
            json.Append("]}");
            string klasor = Path.Combine(taniKlasoru, "ulasilabilir");
            Directory.CreateDirectory(klasor);
            File.WriteAllText(Path.Combine(klasor, ad + ".json"), json.ToString());
            Yaz($"ulaşılabilir nokta: {noktalar.Count} (girişin {yaricap:0} m çevresi, {adim:0} m aralık)");
            return noktalar;
        }

        /// <summary>Girişten yolu olmayan düşman ve sandık noktalarını en yakın ulaşılabilir noktaya taşır.</summary>
        private static void UlasilmayaniTasi(List<Vector3> ulasilan) {
            GameObject giris = GameObject.FindGameObjectsWithTag("DefaultSpawnLocation").FirstOrDefault();
            if (giris == null || ulasilan.Count == 0
                || !NavMesh.SamplePosition(giris.transform.position, out NavMeshHit g, 4f, NavMesh.AllAreas)) {
                return;
            }
            NavMeshPath yolBilgisi = new NavMeshPath();
            List<Vector3> kullanilan = new List<Vector3>();
            List<Transform> hedefler = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.parent == null && (t.name.StartsWith("TR_") || t.name.StartsWith("TRL_")))
                .OrderBy(t => t.name).ToList();
            int tasinan = 0;
            foreach (Transform t in hedefler) {
                bool yolVar = NavMesh.SamplePosition(t.position, out NavMeshHit h, 3f, NavMesh.AllAreas)
                              && NavMesh.CalculatePath(g.position, h.position, NavMesh.AllAreas, yolBilgisi)
                              && yolBilgisi.status == NavMeshPathStatus.PathComplete;
                if (yolVar) {
                    kullanilan.Add(h.position);
                    continue;
                }
                Vector3 eski = t.position;
                float enIyi = float.MaxValue;
                Vector3 secilen = Vector3.zero;
                foreach (Vector3 p in ulasilan) {
                    if (Vector3.Distance(p, g.position) < 18f || kullanilan.Any(u => Vector3.Distance(u, p) < 2.5f)) {
                        continue;
                    }
                    float d = Vector3.Distance(p, eski);
                    if (d < enIyi) {
                        enIyi = d;
                        secilen = p;
                    }
                }
                if (enIyi < float.MaxValue) {
                    t.position = secilen + Vector3.up * 0.05f;
                    kullanilan.Add(secilen);
                    tasinan++;
                    Yaz($"  taşındı: {t.name} {eski} -> {t.position} ({enIyi:0} m)");
                }
            }
            if (tasinan > 0) {
                Yaz($"ulaşılamayan {tasinan} nokta taşındı");
            }
        }

        private static void YurumeAgiRaporu(string ad) {
            NavMeshTriangulation ag = NavMesh.CalculateTriangulation();
            double alan = 0;
            for (int i = 0; i + 2 < ag.indices.Length; i += 3) {
                Vector3 a = ag.vertices[ag.indices[i]];
                Vector3 b = ag.vertices[ag.indices[i + 1]];
                Vector3 c = ag.vertices[ag.indices[i + 2]];
                alan += Vector3.Cross(b - a, c - a).magnitude * 0.5;
            }
            Yaz($"yürüme ağı: {ag.indices.Length / 3} üçgen, {alan:0} m²");
            if (ag.vertices.Length > 0) {
                Bounds s = new Bounds(ag.vertices[0], Vector3.zero);
                foreach (Vector3 v in ag.vertices) {
                    s.Encapsulate(v);
                }
                Yaz($"yürüme ağı sınırı: min {s.min} max {s.max}");
            }

            GameObject giris = GameObject.FindGameObjectsWithTag("DefaultSpawnLocation").FirstOrDefault();
            if (giris == null) {
                Yaz("!! DefaultSpawnLocation yok");
                return;
            }
            if (!NavMesh.SamplePosition(giris.transform.position, out NavMeshHit girisNoktasi, 4f, NavMesh.AllAreas)) {
                Yaz($"!! giriş noktası yürüme ağının dışında: {giris.transform.position}");
                return;
            }
            Yaz($"giriş: {giris.transform.position} (ağa uzaklık {Vector3.Distance(giris.transform.position, girisNoktasi.position):0.00})");

            // kapılar, geçit taşları, düşman noktaları: girişten yürüyerek ulaşılıyor mu
            int ulasilan = 0, toplam = 0;
            NavMeshPath yolBilgisi = new NavMeshPath();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                string n = t.name;
                bool hedef = n.StartsWith("TR_") || n.StartsWith("Kapi_") || n.StartsWith("GecitTasi") || n.StartsWith("Varis_");
                if (!hedef) {
                    continue;
                }
                BoxCollider kapiKutusu = n.StartsWith("Kapi_") ? t.GetComponent<BoxCollider>() : null;
                if (kapiKutusu != null && (t.lossyScale.x > 12f || t.lossyScale.z > 12f)) {
                    // uçurumdan düşenleri yakalayan geniş kutu: yürüyerek ulaşılması beklenmez
                    continue;
                }
                toplam++;
                string durum;
                if (kapiKutusu != null) {
                    // oyuncunun gövdesi tetiğe değecek kadar yakın, girişten yürünen bir nokta var mı (oyun testiyle aynı ölçü)
                    if (OyunTesti.KutuyaUlasilir(girisNoktasi.position, kapiKutusu, yolBilgisi)) {
                        durum = "ok";
                        ulasilan++;
                    } else {
                        durum = "YOL YOK";
                    }
                } else if (!NavMesh.SamplePosition(t.position, out NavMeshHit nokta, 4f, NavMesh.AllAreas)) {
                    durum = "AĞ DIŞI";
                } else if (NavMesh.CalculatePath(girisNoktasi.position, nokta.position, NavMesh.AllAreas, yolBilgisi)
                           && yolBilgisi.status == NavMeshPathStatus.PathComplete) {
                    durum = "ok";
                    ulasilan++;
                } else {
                    durum = "YOL YOK";
                }
                if (durum != "ok") {
                    Yaz($"  {durum}: {n} {t.position}");
                }
            }
            Yaz($"ulaşılabilir hedef: {ulasilan}/{toplam}");
        }

        private static void SahneDenetle(List<GameObject> nesneler) {
            int eksikBetik = 0;
            List<string> eksikler = new List<string>();
            foreach (GameObject go in nesneler) {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (n > 0) {
                    eksikBetik += n;
                    if (eksikler.Count < 15) {
                        eksikler.Add(go.name);
                    }
                }
            }
            Yaz($"eksik betik: {eksikBetik}" + (eksikler.Count > 0 ? " (" + string.Join(", ", eksikler) + ")" : ""));

            HashSet<Shader> golgelendiriciler = new HashSet<Shader>();
            HashSet<Material> malzemeler = new HashSet<Material>();
            int bosMalzeme = 0, cizici = 0;
            long ucgen = 0;
            foreach (GameObject go in nesneler) {
                foreach (Renderer r in go.GetComponents<Renderer>()) {
                    cizici++;
                    foreach (Material m in r.sharedMaterials) {
                        if (m == null) {
                            bosMalzeme++;
                        } else if (malzemeler.Add(m)) {
                            golgelendiriciler.Add(m.shader);
                        }
                    }
                }
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) {
                    for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) {
                        ucgen += mf.sharedMesh.GetIndexCount(i) / 3;
                    }
                }
            }
            Yaz($"çizici: {cizici}, üçgen: {ucgen}, malzeme: {malzemeler.Count}, boş malzeme yuvası: {bosMalzeme}, ışık: {Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length}");
            foreach (Shader g in golgelendiriciler) {
                if (g == null) {
                    Yaz("!! gölgelendiricisi olmayan malzeme var");
                    continue;
                }
                bool hata = g.name == "Hidden/InternalErrorShader" || ShaderUtil.ShaderHasError(g) || !g.isSupported;
                if (!hata) {
                    continue;
                }
                string kullanan = string.Join(", ", malzemeler.Where(m => m.shader == g).Take(4).Select(m => m.name));
                Yaz($"!! bozuk gölgelendirici: {g.name} (destek: {g.isSupported}) <- {kullanan}");
                foreach (ShaderMessage mesaj in ShaderUtil.GetShaderMessages(g).Take(6)) {
                    Yaz($"     {mesaj.severity} {mesaj.message} [{mesaj.file}:{mesaj.line}] {mesaj.platform}");
                }
            }
            Yaz("gölgelendiriciler: " + string.Join(", ", golgelendiriciler.Where(g => g != null).Select(g => g.name).OrderBy(n => n)));
        }

        /// <summary>Yürüme ağının (yoksa çizicilerin) kapladığı alanın yukarıdan dik görüntüsü.</summary>
        private static void KusbakisiCek(string ad) {
            NavMeshTriangulation ag = NavMesh.CalculateTriangulation();
            Bounds s;
            if (ag.vertices.Length > 0) {
                s = new Bounds(ag.vertices[0], Vector3.zero);
                foreach (Vector3 v in ag.vertices) {
                    s.Encapsulate(v);
                }
            } else {
                Renderer[] ciziciler = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                if (ciziciler.Length == 0) {
                    return;
                }
                s = ciziciler[0].bounds;
                foreach (Renderer r in ciziciler) {
                    if (r.bounds.size.x < 2000f) {
                        s.Encapsulate(r.bounds);
                    }
                }
            }
            float boyut = Mathf.Max(s.extents.x, s.extents.z) * 1.08f + 5f;
            Vector3 merkez = s.center;
            // dik kamera bazı gölgelendiricilerde boş çıkıyor: yukarıdan, sissiz, perspektif çekim (60 derece)
            float yukseklik = boyut / Mathf.Tan(30f * Mathf.Deg2Rad);
            Vector3 konum = new Vector3(merkez.x, s.max.y + yukseklik, merkez.z);
            bool sis = RenderSettings.fog;
            RenderSettings.fog = false;
            Cek($"{ad}_kusbakisi", konum, konum + Vector3.down, 60f, false, 0f, 1024, 1024);
            GameObject giris = GameObject.FindGameObjectsWithTag("DefaultSpawnLocation").FirstOrDefault();
            if (giris != null) {
                // girişin çevresi (130 m): kampların, kapıların ve geçit taşının yerleşimini görmek için
                Vector3 g = giris.transform.position;
                float yakin = 130f / Mathf.Tan(30f * Mathf.Deg2Rad);
                Vector3 k2 = new Vector3(g.x, g.y + yakin, g.z);
                Cek($"{ad}_yakin", k2, k2 + Vector3.down, 60f, false, 0f, 1024, 1024);
                Yaz($"yakın kuşbakışı: merkez ({g.x:0.0}, {g.z:0.0}), yükseklik {g.y:0.0} + {yakin:0.0} m, yarı görüş 130 m");
            }
            RenderSettings.fog = sis;
            Yaz($"kuşbakışı: merkez ({merkez.x:0.0}, {merkez.z:0.0}), yükseklik {s.max.y:0.0} + {yukseklik:0.0} m, yarı görüş {boyut:0.0} m (kuzey yukarı)");

            // dört köşeden eğik bakış
            float r2 = Mathf.Max(s.extents.x, s.extents.z);
            for (int i = 0; i < 4; i++) {
                float aci = (45f + 90f * i) * Mathf.Deg2Rad;
                Vector3 k = merkez + new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci)) * r2 * 0.9f + Vector3.up * (r2 * 0.55f + 20f);
                Cek($"{ad}_egik{i + 1}", k, merkez, 55f, false, 0f, 960, 540);
            }
        }

        /// <summary>
        /// Telefon arayüzünün önizlemesi (tani/arayuz_*.jpg): dokunmatik düğmeler ve Işınlan penceresi,
        /// 20:9 bir telefon ekranında oyundaki ölçekle. Pencereler kodla kurulduğu için burada da aynı kodla kurulur.
        /// </summary>
        private static void ArayuzCek() {
            ArayuzCiz("arayuz_isinlan", "isinlan");
            try {
                ArayuzCiz("arayuz_gunluk", "gunluk");
            } catch (Exception e) {
                Yaz($"!! HATA günlük görevler önizlemesi: {e}");
            }
            try {
                OyunArayuzu();
            } catch (Exception e) {
                Yaz($"!! HATA oyun arayüzü önizlemesi: {e}");
            }
        }

        // ---------------------------------------------------------------- oyunun gerçek göstergeleriyle önizleme

        private const string OyunYoneticisi = "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Prefab/GameManager/FeaturesDemoGameManager.prefab";

        // telefon ve tablet ekran oranları (görüntü 720 piksel yükseklikte çizilir; arayüz yüksekliğe göre ölçeklendiği için aynıdır)
        private static readonly (string ad, int en, int boy)[] Ekranlar = {
            ("20x9", 1600, 720), ("19.5x9", 1560, 720), ("16x9", 1280, 720), ("16x10", 1152, 720), ("4x3", 960, 720)
        };

        /// <summary>
        /// Oyun yöneticisinin arayüzünü (can çubuğu, hedef çerçevesi, mini harita, görev listesi, deneyim çubuğu,
        /// yetenek ve sistem çubuğu) oyundaki gibi açar, telefondaki ölçeklemeyi uygular, dokunmatik düğmeleri ekler
        /// ve birkaç ekran oranında çizer (tani/arayuz_oyun_*.jpg). Düğmelerin yerleşimden önceki ve sonraki
        /// çakışmaları rapora yazılır.
        /// </summary>
        private static void OyunArayuzu() {
            GameObject kaynak = AssetDatabase.LoadAssetAtPath<GameObject>(OyunYoneticisi);
            if (kaynak == null) {
                Yaz("!! arayüz: oyun yöneticisi bulunamadı " + OyunYoneticisi);
                return;
            }
            Scene sahne = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject oyun = (GameObject)PrefabUtility.InstantiatePrefab(kaynak, sahne);
            // oyundaki gibi serbestçe düzenlenebilsin (sistem çubuğunun yeri değişir)
            PrefabUtility.UnpackPrefabInstance(oyun, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            AnyRPG.UIManager ui = oyun.GetComponentInChildren<AnyRPG.UIManager>(true);
            if (ui == null) {
                Yaz("!! arayüz: UIManager yok");
                return;
            }

            // bütün pencereler kapalı; oyuna girince açılan göstergeler açık (UIManager.InitializePlayerUI)
            foreach (AnyRPG.CloseableWindow w in oyun.GetComponentsInChildren<AnyRPG.CloseableWindow>(true)) {
                w.gameObject.SetActive(false);
            }
            List<Component> acik = new List<Component>();
            foreach (AnyRPG.CloseableWindow w in new AnyRPG.CloseableWindow[] { ui.PlayerUnitFrameWindow, ui.FocusUnitFrameWindow,
                ui.StatusEffectWindow, ui.MiniMapWindow, ui.QuestTrackerWindow, ui.XPBarWindow, ui.FloatingCastBarWindow }) {
                if (w == null) {
                    continue;
                }
                IcerikKur(w);
                w.gameObject.SetActive(true);
                acik.Add(w);
            }
            AnyRPG.ActionBarManager cubuklar = ui.ActionBarManager;
            if (cubuklar != null) {
                // telefonda yalnız 1. yetenek çubuğu (MobileBootstrap.ApplyMobileUiDefaults); sistem çubuğu açık
                for (int i = 0; i < cubuklar.ActionBarControllers.Count; i++) {
                    if (cubuklar.ActionBarControllers[i] != null) {
                        cubuklar.ActionBarControllers[i].gameObject.SetActive(i == 0);
                        if (i == 0) {
                            acik.Add(cubuklar.ActionBarControllers[i]);
                        }
                    }
                }
                if (cubuklar.SystemBarController != null) {
                    cubuklar.SystemBarController.gameObject.SetActive(true);
                    acik.Add(cubuklar.SystemBarController);
                }
            }

            // göstergeleri taşıyan kök tuvaller açık, ötekiler (menüler, yükleme ekranı...) kapalı
            HashSet<Canvas> gerekli = new HashSet<Canvas>();
            foreach (Component c in acik) {
                Canvas k = KokTuval(c.transform);
                if (k != null) {
                    gerekli.Add(k);
                }
                for (Transform t = c.transform; t != null; t = t.parent) {
                    t.gameObject.SetActive(true);
                }
            }
            foreach (Canvas tuval in oyun.GetComponentsInChildren<Canvas>(true)) {
                if (KokTuval(tuval.transform) == tuval && gerekli.Contains(tuval) == false) {
                    tuval.gameObject.SetActive(false);
                }
            }

            List<GameObject> silinecek = new List<GameObject>() { oyun };
            RenderTexture rt = null;
            try {
                GameObject kameraNesnesi = new GameObject("ArayuzKamerasi");
                silinecek.Add(kameraNesnesi);
                Camera kamera = kameraNesnesi.AddComponent<Camera>();
                kamera.transform.position = new Vector3(0f, -5000f, 0f);
                kamera.clearFlags = CameraClearFlags.SolidColor;
                kamera.backgroundColor = new Color(0.33f, 0.42f, 0.3f, 1f);
                kamera.nearClipPlane = 0.1f;
                kamera.farClipPlane = 20f;

                // oyundaki gibi ölçekleyicisiz kurulur; ölçeği MobileBootstrap.ScaleCanvases verir (800 birim yükseklik)
                GameObject hudNesnesi = new GameObject(AnyRPG.MobileHud.CanvasName, typeof(RectTransform));
                silinecek.Add(hudNesnesi);
                Canvas hudTuvali = hudNesnesi.AddComponent<Canvas>();
                hudTuvali.renderMode = RenderMode.ScreenSpaceOverlay;
                hudTuvali.sortingOrder = AnyRPG.MobileHud.SortingOrder;
                AnyRPG.MobileHud mobilHud = hudNesnesi.AddComponent<AnyRPG.MobileHud>();
                YontemCagir(mobilHud, "Build");
                gerekli.Add(hudTuvali);

                // oyun yalnız oyun kolu kullanılırken gösterdiği tuş ipuçlarını (HideControllerHints) gizler
                foreach (Transform t in oyun.GetComponentsInChildren<Transform>(true)) {
                    if (t.name.IndexOf("HintBar", StringComparison.OrdinalIgnoreCase) >= 0) {
                        t.gameObject.SetActive(false);
                    }
                }

                // telefon düzeni: büyük yetenek çubuğu, sistem çubuğu üstte (MobilArayuzDuzeni, varsayılan "Büyük")
                AnyRPG.SystemGameManager oyunYoneticisi = oyun.GetComponentInChildren<AnyRPG.SystemGameManager>(true);
                if (oyunYoneticisi != null) {
                    AnyRPG.MobilArayuzDuzeni.Tick(oyunYoneticisi);
                }

                // telefondaki ölçekleme ve katman düzeni (MobileBootstrap.ScaleCanvases)
                GameObject onyukleyici = new GameObject("MobileBootstrapOnizleme");
                silinecek.Add(onyukleyici);
                Component bootstrap = onyukleyici.AddComponent<AnyRPG.MobileBootstrap>();
                YontemCagir(bootstrap, "ScaleCanvases");
                foreach (Canvas tuval in gerekli) {
                    tuval.renderMode = RenderMode.ScreenSpaceCamera;
                    tuval.worldCamera = kamera;
                    tuval.planeDistance = tuval == hudTuvali ? 1f : 2f;
                }

                Yaz("--- oyun arayüzü (telefon ölçeğinde, 800 birim yükseklik)");
                foreach (var e in Ekranlar) {
                    if (rt != null) {
                        kamera.targetTexture = null;
                        rt.Release();
                        Object.DestroyImmediate(rt);
                    }
                    rt = new RenderTexture(e.en, e.boy, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    rt.Create();
                    kamera.targetTexture = rt;
                    OlcekleriYenile();
                    Canvas.ForceUpdateCanvases();

                    Rect ekran = new Rect(0f, 0f, e.en, e.boy);
                    mobilHud.EveDon();
                    Canvas.ForceUpdateCanvases();
                    int once;
                    string onceRapor = AnyRPG.ArayuzDenetimi.YerlesimRaporu(mobilHud, ekran, ekran, out once);
                    mobilHud.YerlesimiUygula();
                    Canvas.ForceUpdateCanvases();
                    int sonra;
                    string sonraRapor = AnyRPG.ArayuzDenetimi.YerlesimRaporu(mobilHud, ekran, ekran, out sonra);
                    Yaz($"[{e.ad}] çakışma: yerleşimden önce {once}, sonra {sonra}");
                    foreach (string satir in sonraRapor.Split('\n')) {
                        if (satir.Length > 0) {
                            Yaz("    " + satir);
                        }
                    }
                    if (e.ad == "20x9") {
                        Yaz("    (yerleşimden önce)");
                        foreach (string satir in onceRapor.Split('\n')) {
                            if (satir.StartsWith("- ")) {
                                Yaz("    " + satir);
                            }
                        }
                    }
                    KameraCiz(kamera, rt, "arayuz_oyun_" + e.ad.Replace('.', '_'));
                }
            } finally {
                foreach (GameObject go in silinecek) {
                    if (go != null) {
                        Object.DestroyImmediate(go);
                    }
                }
                if (rt != null) {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
        }

        private static Canvas KokTuval(Transform t) {
            Canvas kok = null;
            for (Transform p = t; p != null; p = p.parent) {
                Canvas c = p.GetComponent<Canvas>();
                if (c != null) {
                    kok = c;
                }
            }
            return kok;
        }

        /// <summary>pencerenin içeriği oyunda nesne havuzundan gelir; burada prefabından kurulur</summary>
        private static void IcerikKur(AnyRPG.CloseableWindow pencere) {
            Type t = typeof(AnyRPG.CloseableWindow);
            BindingFlags b = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo prefabAlani = t.GetField("contentPrefab", b);
            FieldInfo ustAlani = t.GetField("contentParent", b);
            FieldInfo icerikAlani = t.GetField("contentGameObject", b);
            if (prefabAlani == null || ustAlani == null || icerikAlani == null) {
                return;
            }
            GameObject prefab = prefabAlani.GetValue(pencere) as GameObject;
            GameObject ust = ustAlani.GetValue(pencere) as GameObject;
            GameObject icerik = icerikAlani.GetValue(pencere) as GameObject;
            if (icerik != null) {
                icerik.SetActive(true);
                return;
            }
            if (prefab == null || ust == null) {
                return;
            }
            icerik = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ust.transform);
            icerik.SetActive(true);
            icerikAlani.SetValue(pencere, icerik);
        }

        private static void OlcekleriYenile() {
            foreach (UnityEngine.UI.CanvasScaler olcek in Object.FindObjectsByType<UnityEngine.UI.CanvasScaler>(FindObjectsSortMode.None)) {
                olcek.enabled = false;
                olcek.enabled = true;
            }
        }

        private static void KameraCiz(Camera kamera, RenderTexture rt, string dosya) {
            Texture2D doku = null;
            try {
                RenderPipeline.StandardRequest istek = new RenderPipeline.StandardRequest();
                istek.destination = rt;
                if (RenderPipeline.SupportsRenderRequest(kamera, istek)) {
                    RenderPipeline.SubmitRenderRequest(kamera, istek);
                } else {
                    kamera.Render();
                }
                RenderTexture onceki = RenderTexture.active;
                RenderTexture.active = rt;
                doku = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                doku.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                doku.Apply();
                RenderTexture.active = onceki;
                File.WriteAllBytes(Path.Combine(taniKlasoru, dosya + ".jpg"), doku.EncodeToJPG(85));
            } finally {
                if (doku != null) {
                    Object.DestroyImmediate(doku);
                }
            }
        }

        private static void ArayuzCiz(string dosya, string pencere) {
            const int en = 1600, boy = 720;
            List<GameObject> silinecek = new List<GameObject>();
            RenderTexture rt = null;
            Texture2D doku = null;
            try {
                GameObject kameraNesnesi = new GameObject("ArayuzKamerasi");
                silinecek.Add(kameraNesnesi);
                Camera kamera = kameraNesnesi.AddComponent<Camera>();
                kamera.transform.position = new Vector3(0f, -5000f, 0f);
                kamera.clearFlags = CameraClearFlags.SolidColor;
                kamera.backgroundColor = new Color(0.33f, 0.42f, 0.3f, 1f);
                kamera.nearClipPlane = 0.1f;
                kamera.farClipPlane = 10f;
                rt = new RenderTexture(en, boy, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create();
                kamera.targetTexture = rt;

                // oyundaki gibi: HUD 800 yüksekliğe göre (MobileBootstrap.ScaleCanvases), pencere kendi ölçeğiyle
                Component hud = Tuval("HUD", kamera, 2f, 5, silinecek).AddComponent<AnyRPG.MobileHud>();
                YontemCagir(hud, "Build");
                Component gunluk = null;
                if (pencere == "isinlan") {
                    Component isinlanma = Tuval("Isinlanma", kamera, 1f, 31, silinecek).AddComponent<AnyRPG.IsinlanmaPenceresi>();
                    YontemCagir(isinlanma, "Build");
                    YontemCagir(isinlanma, "Open");
                    YontemCagir(isinlanma, "Select", 9);
                } else if (pencere == "gunluk") {
                    gunluk = Tuval("GunlukGorevler", kamera, 1f, 31, silinecek).AddComponent<AnyRPG.GunlukGorevler>();
                    YontemCagir(gunluk, "Build");
                    YontemCagir(gunluk, "Onizleme");
                }
                Canvas.ForceUpdateCanvases();

                RenderPipeline.StandardRequest istek = new RenderPipeline.StandardRequest();
                istek.destination = rt;
                if (RenderPipeline.SupportsRenderRequest(kamera, istek)) {
                    RenderPipeline.SubmitRenderRequest(kamera, istek);
                } else {
                    kamera.Render();
                }
                RenderTexture onceki = RenderTexture.active;
                RenderTexture.active = rt;
                doku = new Texture2D(en, boy, TextureFormat.RGB24, false);
                doku.ReadPixels(new Rect(0, 0, en, boy), 0, 0);
                doku.Apply();
                RenderTexture.active = onceki;
                File.WriteAllBytes(Path.Combine(taniKlasoru, dosya + ".jpg"), doku.EncodeToJPG(85));
                Yaz($"arayüz önizlemesi: {dosya}.jpg");
                if (gunluk != null) {
                    YontemCagir(gunluk, "OnizlemeBitti");
                }
            } finally {
                foreach (GameObject go in silinecek) {
                    if (go != null) {
                        Object.DestroyImmediate(go);
                    }
                }
                if (rt != null) {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
                if (doku != null) {
                    Object.DestroyImmediate(doku);
                }
            }
        }

        private static GameObject Tuval(string ad, Camera kamera, float uzaklik, int sira, List<GameObject> silinecek) {
            GameObject go = new GameObject(ad, typeof(RectTransform));
            silinecek.Add(go);
            Canvas tuval = go.AddComponent<Canvas>();
            tuval.renderMode = RenderMode.ScreenSpaceCamera;
            tuval.worldCamera = kamera;
            tuval.planeDistance = uzaklik;
            tuval.sortingOrder = sira;
            UnityEngine.UI.CanvasScaler olcek = go.AddComponent<UnityEngine.UI.CanvasScaler>();
            olcek.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olcek.referenceResolution = new Vector2(1422f, 800f);
            olcek.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            olcek.matchWidthOrHeight = 1f;
            // ölçek OnEnable'da hesaplanır: ayarlardan sonra yeniden etkinleştir
            olcek.enabled = false;
            olcek.enabled = true;
            return go;
        }

        private static void YontemCagir(object nesne, string ad, params object[] degerler) {
            MethodInfo yontem = nesne.GetType().GetMethod(ad, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (yontem == null) {
                throw new Exception($"{nesne.GetType().Name}.{ad} bulunamadı");
            }
            yontem.Invoke(nesne, degerler);
        }

        private static void Cek(string dosya, Vector3 konum, Vector3 hedef, float fov, bool dik, float dikBoyut, int en, int boy) {
            GameObject go = new GameObject("TaniKamerasi");
            go.hideFlags = HideFlags.HideAndDontSave;
            RenderTexture rt = null;
            Texture2D doku = null;
            try {
                Camera kamera = go.AddComponent<Camera>();
                kamera.transform.position = konum;
                Vector3 yon = hedef - konum;
                kamera.transform.rotation = Mathf.Abs(Vector3.Dot(yon.normalized, Vector3.up)) > 0.99f
                    ? Quaternion.LookRotation(yon, Vector3.forward)
                    : Quaternion.LookRotation(yon, Vector3.up);
                kamera.fieldOfView = fov;
                kamera.nearClipPlane = 0.3f;
                kamera.farClipPlane = 5000f;
                kamera.orthographic = dik;
                kamera.orthographicSize = dikBoyut;
                kamera.clearFlags = CameraClearFlags.Skybox;
                kamera.backgroundColor = Color.black;

                rt = new RenderTexture(en, boy, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create();
                RenderPipeline.StandardRequest istek = new RenderPipeline.StandardRequest();
                istek.destination = rt;
                if (RenderPipeline.SupportsRenderRequest(kamera, istek)) {
                    RenderPipeline.SubmitRenderRequest(kamera, istek);
                } else {
                    kamera.targetTexture = rt;
                    kamera.Render();
                    kamera.targetTexture = null;
                }

                RenderTexture onceki = RenderTexture.active;
                RenderTexture.active = rt;
                doku = new Texture2D(en, boy, TextureFormat.RGB24, false);
                doku.ReadPixels(new Rect(0, 0, en, boy), 0, 0);
                doku.Apply();
                RenderTexture.active = onceki;
                File.WriteAllBytes(Path.Combine(taniKlasoru, dosya + ".jpg"), doku.EncodeToJPG(82));
            } catch (Exception e) {
                Yaz($"!! görüntü alınamadı {dosya}: {e.Message}");
            } finally {
                if (rt != null) {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
                if (doku != null) {
                    Object.DestroyImmediate(doku);
                }
                Object.DestroyImmediate(go);
            }
        }
    }
}
