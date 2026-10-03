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

        public static void Derle() {
            try {
                HaritaHazirlik.Calistir();
            } catch (Exception e) {
                Debug.LogError("HaritaHazirlik başarısız oldu, derlemeye devam ediliyor: " + e);
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
                    Yaz($"!! zemine oturtulamayan nesne: {oturmayan}");
                }
                NavMeshPisir(sahne, yol);
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
                toplam++;
                string durum;
                if (!NavMesh.SamplePosition(t.position, out NavMeshHit nokta, 4f, NavMesh.AllAreas)) {
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
            Vector3 konum = new Vector3(merkez.x, s.max.y + 300f, merkez.z);
            Cek($"{ad}_kusbakisi", konum, konum + Vector3.down, 60f, true, boyut, 1024, 1024);
            Yaz($"kuşbakışı: merkez ({merkez.x:0.0}, {merkez.z:0.0}), yarı boyut {boyut:0.0} m (görüntü 1024x1024, kuzey yukarı)");

            // dört köşeden eğik bakış
            float r2 = Mathf.Max(s.extents.x, s.extents.z);
            for (int i = 0; i < 4; i++) {
                float aci = (45f + 90f * i) * Mathf.Deg2Rad;
                Vector3 k = merkez + new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci)) * r2 * 0.9f + Vector3.up * (r2 * 0.55f + 20f);
                Cek($"{ad}_egik{i + 1}", k, merkez, 55f, false, 0f, 960, 540);
            }
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
