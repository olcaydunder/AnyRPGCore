using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Otuken.EditorAraclari {

    /// <summary>
    /// Çevrimiçi oyun (AnyMMO + FishNet) için derleme anında yapılan hazırlık. Depodaki sahneler ve birim profilleri
    /// tek oyunculu hâlleriyle kalır; ağ parçaları her derlemede aynı biçimde eklenir (sunucu ve telefon derlemesi
    /// aynı kimlikleri üretir):
    ///  1. Ayarlar: çevrimiçi oyun açık, sunucu adresi (Tools~/ag/sunucu.txt), istemci sürümü (-istemciSurumu).
    ///  2. Birimler: her birim profiline FishNet birim prefabı (binekler için binek birimi) ve modelinin FishNet
    ///     karşılığı (AnyMMO'da varsa o, yoksa Assets/Otuken/Ag/Modeller'e üretilen prefab varyantı).
    ///  3. Sahneler: fizik sahnesi eşleyicisi; her etkileşimli nesneye NetworkObject + FishNetInteractable +
    ///     NetworkTransform; sahne nesne kimlikleri sahne yolu ve hiyerarşi yolundan (rastgele değil).
    ///  4. Oyun sahnesi: FishNet ağ yöneticisi, GameManager'daki istemci/sunucu yöneticilerine bağlanır.
    ///  5. FishNet'in doğurulabilir prefab listesi (DefaultPrefabObjects) baştan üretilir ve ağ yöneticisine verilir.
    /// </summary>
    public static class AgHazirlik {

        private const string AnyMMO = "Assets/AnyRPG/Addons/anymmo-fishnet";
        private const string AgYoneticisiYolu = AnyMMO + "/GameManager/FishNetNetworkManager.prefab";
        private const string SahneEsleyiciYolu = AnyMMO + "/GameManager/FishNetPhysicsSceneSync.prefab";
        private const string KarakterBirimiYolu = AnyMMO + "/Prefabs/Character/Unit/FishNetDefaultCharacterUnit.prefab";
        private const string BinekBirimiYolu = AnyMMO + "/Prefabs/Character/Unit/FishNetDefaultMountUnit.prefab";
        private const string ModelKlasoruAnyMMO = AnyMMO + "/Prefabs/Character/Model";
        private const string OrnekModelYolu = ModelKlasoruAnyMMO + "/FishNetHumanMale.prefab";
        public const string UretilenKlasor = "Assets/Otuken/Ag";
        private const string UretilenModelKlasoru = UretilenKlasor + "/Modeller";
        public const string OyunSahnesi = "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Scenes/Game/FeaturesDemoGame/FeaturesDemoGame.unity";
        private const string OyunYoneticisiYolu = "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Prefab/GameManager/FeaturesDemoGameManager.prefab";
        private const string SunucuAdresiDosyasi = "Tools~/ag/sunucu.txt";
        private const string DenemeIndirmeAdresi = "https://github.com/olcaydunder/AnyRPGCore/releases/download/son-apk/OtukenDestani.apk";

        /// <summary>Google Play sürümü (1.0.N): deneme sürümleri 0.1.N, editör "gelistirme"</summary>
        public static bool PlaySurumu {
            get { return IstemciSurumu.StartsWith("0.") == false && IstemciSurumu != "gelistirme"; }
        }

        /// <summary>
        /// "Sürüm tutmuyor" penceresindeki güncelleme adresi. Play sürümünde Play Store sayfası: Play'den kurulan oyun
        /// başka yerden APK indirmeye yönlendiremez (Google Play politikası: güncelleme yalnız Play'den).
        /// </summary>
        public static string IndirmeAdresi {
            get {
                if (PlaySurumu) {
                    return "https://play.google.com/store/apps/details?id="
                        + PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android);
                }
                return DenemeIndirmeAdresi;
            }
        }

        private static StringBuilder rapor;

        private static void Yaz(string satir) {
            rapor.AppendLine(satir);
            Debug.Log("[AgHazirlik] " + satir);
        }

        public static string IstemciSurumu {
            get {
                string[] a = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-istemciSurumu");
                return i >= 0 && i + 1 < a.Length ? a[i + 1] : "gelistirme";
            }
        }

        [MenuItem("Ötüken/Çevrimiçi oyun hazırlığı")]
        public static void CalistirMenu() {
            Debug.Log(Calistir());
        }

        /// <summary>hazırlığı yapar, raporunu döndürür (tani/ag_[hedef].txt'ye de yazar)</summary>
        public static string Calistir() {
            rapor = new StringBuilder();
            Yaz("--- çevrimiçi oyun hazırlığı (AnyMMO + FishNet)");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AgYoneticisiYolu) == null) {
                Yaz("!! AnyMMO eklentisi yok: " + AgYoneticisiYolu);
                return Bitir();
            }
            Adim("ayarlar", Ayarlar);
            Adim("birimler", Birimler);
            Adim("sahneler", Sahneler);
            Adim("ağ yöneticisi", AgYoneticisi);
            AssetDatabase.SaveAssets();
            return Bitir();
        }

        private static void Adim(string ad, Action is_) {
            try {
                is_();
            } catch (Exception e) {
                Yaz($"!! HATA {ad}: {e}");
            }
        }

        private static string Bitir() {
            string metin = rapor.ToString();
            try {
                Directory.CreateDirectory(Path.GetFullPath("tani"));
                File.WriteAllText(Path.Combine(Path.GetFullPath("tani"), "ag_" + EditorUserBuildSettings.activeBuildTarget + ".txt"), metin);
            } catch (Exception) {
            }
            return metin;
        }

        // ---------------------------------------------------------------- 1. ayarlar

        private static void Ayarlar() {
            string adres = "localhost";
            if (File.Exists(SunucuAdresiDosyasi)) {
                string okunan = File.ReadAllText(SunucuAdresiDosyasi).Trim();
                if (okunan.Length > 0) {
                    adres = okunan;
                }
            }
            GameObject kok = PrefabUtility.LoadPrefabContents(OyunYoneticisiYolu);
            try {
                AnyRPG.SystemConfigurationManager ayar = kok.GetComponentInChildren<AnyRPG.SystemConfigurationManager>(true);
                if (ayar == null) {
                    Yaz("!! SystemConfigurationManager bulunamadı");
                    return;
                }
                ayar.AllowOfflinePlay = true;
                ayar.AllowOnlinePlay = true;
                ayar.GameServerAddress = adres;
                ayar.ClientVersion = IstemciSurumu;
                ayar.ClientDownloadUrl = IndirmeAdresi;
                if (string.IsNullOrEmpty(ayar.PrivateMessageChatCommand)) {
                    ayar.PrivateMessageChatCommand = "private";
                }
                PrefabUtility.SaveAsPrefabAsset(kok, OyunYoneticisiYolu);
                Yaz($"ayarlar: çevrimiçi açık, sunucu {adres}, istemci sürümü {IstemciSurumu}");
            } finally {
                PrefabUtility.UnloadPrefabContents(kok);
            }
        }

        // ---------------------------------------------------------------- 2. birimler

        private static void Birimler() {
            GameObject karakterBirimi = AssetDatabase.LoadAssetAtPath<GameObject>(KarakterBirimiYolu);
            GameObject binekBirimi = AssetDatabase.LoadAssetAtPath<GameObject>(BinekBirimiYolu);
            GameObject ornek = AssetDatabase.LoadAssetAtPath<GameObject>(OrnekModelYolu);
            if (karakterBirimi == null || binekBirimi == null || ornek == null) {
                Yaz("!! AnyMMO birim prefabları eksik");
                return;
            }

            // AnyMMO'nun hazır FishNet modelleri: kaynak model -> FishNet varyantı
            Dictionary<GameObject, GameObject> karsiliklar = new Dictionary<GameObject, GameObject>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ModelKlasoruAnyMMO, UretilenModelKlasoru }.Where(AssetDatabase.IsValidFolder).ToArray())) {
                GameObject varyant = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                GameObject kaynak = varyant != null ? PrefabUtility.GetCorrespondingObjectFromSource(varyant) : null;
                if (kaynak != null && karsiliklar.ContainsKey(kaynak) == false) {
                    karsiliklar.Add(kaynak, varyant);
                }
            }

            HashSet<string> binekler = BinekProfilleri();
            int profil = 0;
            int uretilen = 0;
            int hazir = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:UnitProfile")) {
                string yol = AssetDatabase.GUIDToAssetPath(guid);
                AnyRPG.UnitProfile birim = AssetDatabase.LoadAssetAtPath<AnyRPG.UnitProfile>(yol);
                if (birim == null || birim.UnitPrefabProps == null || birim.UnitPrefabProps.UnitPrefab == null) {
                    continue;
                }
                bool binek = binekler.Contains(birim.ResourceName.ToLowerInvariant()) || binekler.Contains((birim.DisplayName ?? string.Empty).ToLowerInvariant());
                AnyRPG.UnitPrefabProps p = birim.UnitPrefabProps;
                bool degisti = false;
                if (p.NetworkUnitPrefab == null) {
                    p.NetworkUnitPrefab = binek ? binekBirimi : karakterBirimi;
                    degisti = true;
                }
                if (p.ModelPrefab != null && p.NetworkModelPrefab == null) {
                    GameObject karsilik;
                    if (karsiliklar.TryGetValue(p.ModelPrefab, out karsilik) == false) {
                        karsilik = ModelVaryanti(p.ModelPrefab, ornek);
                        if (karsilik != null) {
                            karsiliklar.Add(p.ModelPrefab, karsilik);
                            uretilen++;
                        }
                    } else {
                        hazir++;
                    }
                    if (karsilik != null) {
                        p.NetworkModelPrefab = karsilik;
                        degisti = true;
                    }
                }
                if (degisti) {
                    EditorUtility.SetDirty(birim);
                    profil++;
                }
            }
            AssetDatabase.SaveAssets();
            Yaz($"birimler: {profil} profile ağ prefabı verildi; {uretilen} model varyantı üretildi, {hazir} kez AnyMMO'nun hazır modeli kullanıldı; binek profilleri: {string.Join(", ", binekler)}");
        }

        /// <summary>binek etkilerinin (MountEffect) çağırdığı birim profilleri</summary>
        private static HashSet<string> BinekProfilleri() {
            HashSet<string> adlar = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:MountEffect")) {
                Object etki = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));
                if (etki == null) {
                    continue;
                }
                SerializedProperty p = new SerializedObject(etki).GetIterator();
                while (p.Next(true)) {
                    if (p.propertyType == SerializedPropertyType.String && p.name == "unitProfileName" && string.IsNullOrEmpty(p.stringValue) == false) {
                        adlar.Add(p.stringValue.ToLowerInvariant());
                    }
                }
            }
            return adlar;
        }

        /// <summary>modelin FishNet varyantı: NetworkObject, FishNetCharacterModel, OfflineTickSmoother, NetworkTransform (ayarlar AnyMMO örneğinden)</summary>
        private static GameObject ModelVaryanti(GameObject model, GameObject ornek) {
            Directory.CreateDirectory(UretilenModelKlasoru);
            string yol = UretilenModelKlasoru + "/FishNet" + model.name + ".prefab";
            GameObject var_ = AssetDatabase.LoadAssetAtPath<GameObject>(yol);
            if (var_ != null) {
                return var_;
            }
            GameObject kopya = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try {
                FishNet.Object.NetworkObject nob = kopya.GetComponent<FishNet.Object.NetworkObject>();
                if (nob == null) {
                    nob = kopya.AddComponent<FishNet.Object.NetworkObject>();
                }
                BilesenKopyala<FishNet.Object.NetworkObject>(ornek, nob, false);
                FishNet.Component.Transforming.Beta.OfflineTickSmoother yumusatici = kopya.AddComponent<FishNet.Component.Transforming.Beta.OfflineTickSmoother>();
                BilesenKopyala<FishNet.Component.Transforming.Beta.OfflineTickSmoother>(ornek, yumusatici, false);
                FishNet.Component.Transforming.NetworkTransform nt = kopya.AddComponent<FishNet.Component.Transforming.NetworkTransform>();
                BilesenKopyala<FishNet.Component.Transforming.NetworkTransform>(ornek, nt, true);
                AnyRPG.FishNetCharacterModel cm = kopya.AddComponent<AnyRPG.FishNetCharacterModel>();
                BilesenKopyala<AnyRPG.FishNetCharacterModel>(ornek, cm, true);
                AgaBagla(nt, nob);
                AgaBagla(cm, nob);
                return PrefabUtility.SaveAsPrefabAsset(kopya, yol);
            } catch (Exception e) {
                Yaz($"!! model varyantı üretilemedi {model.name}: {e.Message}");
                return null;
            } finally {
                Object.DestroyImmediate(kopya);
            }
        }

        /// <summary>örnek prefabdaki aynı türden bileşenin ayarlarını kopyalar (nesne başvuruları hariç)</summary>
        private static void BilesenKopyala<T>(GameObject ornek, T hedef, bool agBilesen) where T : Component {
            T kaynak = ornek.GetComponent<T>();
            if (kaynak == null) {
                return;
            }
            SerializedObject k = new SerializedObject(kaynak);
            SerializedObject h = new SerializedObject(hedef);
            SerializedProperty p = k.GetIterator();
            bool cocuk = true;
            while (p.NextVisible(cocuk)) {
                cocuk = false;
                if (p.propertyType == SerializedPropertyType.ObjectReference || p.name == "m_Script" || p.name.Contains("SceneId")
                    || p.name.Contains("PrefabId") || p.name.Contains("AssetPathHash") || p.name == "NetworkBehaviours") {
                    continue;
                }
                if (p.hasVisibleChildren && p.propertyType == SerializedPropertyType.Generic && p.isArray) {
                    continue;
                }
                h.CopyFromSerializedProperty(p);
            }
            h.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AgaBagla(Component bilesen, FishNet.Object.NetworkObject nob) {
            SerializedObject s = new SerializedObject(bilesen);
            foreach (string ad in new[] { "_addedNetworkObject", "_networkObjectCache" }) {
                SerializedProperty p = s.FindProperty(ad);
                if (p != null) {
                    p.objectReferenceValue = nob;
                }
            }
            s.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- 3. sahneler

        private static void Sahneler() {
            GameObject esleyici = AssetDatabase.LoadAssetAtPath<GameObject>(SahneEsleyiciYolu);
            int sahneSayisi = 0;
            int toplam = 0;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) {
                if (s.enabled == false || s.path == OyunSahnesi || s.path.Contains("MainMenu")) {
                    continue;
                }
                Scene sahne = EditorSceneManager.OpenScene(s.path, OpenSceneMode.Single);
                bool degisti = false;

                // fizik sahnesi eşleyicisi (her oyun sahnesinin ilk nesnesi)
                if (esleyici != null && Object.FindObjectsByType<AnyRPG.FishNetPhysicsSceneSync>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(e => e.gameObject.scene == sahne) == false) {
                    GameObject e = (GameObject)PrefabUtility.InstantiatePrefab(esleyici, sahne);
                    e.transform.SetAsFirstSibling();
                    degisti = true;
                }

                // etkileşimli nesneler
                int eklenen = 0;
                foreach (AnyRPG.InteractableBase ib in Object.FindObjectsByType<AnyRPG.InteractableBase>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                    if (ib.gameObject.scene != sahne || ib is AnyRPG.UnitController) {
                        continue;
                    }
                    GameObject go = ib.gameObject;
                    if (go.GetComponent<AnyRPG.FishNetInteractable>() != null) {
                        continue;
                    }
                    if (go.GetComponent<FishNet.Object.NetworkObject>() == null) {
                        go.AddComponent<FishNet.Object.NetworkObject>();
                    }
                    if (go.GetComponent<FishNet.Component.Transforming.NetworkTransform>() == null) {
                        go.AddComponent<FishNet.Component.Transforming.NetworkTransform>();
                    }
                    go.AddComponent<AnyRPG.FishNetInteractable>();
                    eklenen++;
                }
                if (eklenen > 0) {
                    degisti = true;
                }

                // sahne nesne kimlikleri: sahne yolu + hiyerarşi yolu (iki derleme de aynı kimliği verir)
                int kimlik = SahneKimlikleri(sahne);
                if (kimlik > 0) {
                    degisti = true;
                }
                if (degisti) {
                    EditorSceneManager.MarkSceneDirty(sahne);
                    EditorSceneManager.SaveScene(sahne);
                }
                sahneSayisi++;
                toplam += eklenen;
                Yaz($"  {Path.GetFileNameWithoutExtension(s.path)}: {eklenen} etkileşimli nesne ağa eklendi, {kimlik} kimlik yazıldı");
            }
            Yaz($"sahneler: {sahneSayisi} sahne, {toplam} etkileşimli nesne");
        }

        private static uint KararliOzet(string metin) {
            // FNV-1a
            uint h = 2166136261;
            foreach (char c in metin) {
                h ^= c;
                h *= 16777619;
            }
            return h;
        }

        private static string HiyerarsiYolu(Transform t) {
            List<string> parcalar = new List<string>();
            for (Transform p = t; p != null; p = p.parent) {
                parcalar.Add(p.name + "#" + p.GetSiblingIndex());
            }
            parcalar.Reverse();
            return string.Join("/", parcalar);
        }

        private static int SahneKimlikleri(Scene sahne) {
            List<FishNet.Object.NetworkObject> nobs = Object.FindObjectsByType<FishNet.Object.NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(n => n.gameObject.scene == sahne && PrefabUtility.IsPartOfPrefabAsset(n) == false)
                .OrderBy(n => HiyerarsiYolu(n.transform), StringComparer.Ordinal).ToList();
            ulong ust = (ulong)KararliOzet(sahne.path) << 32;
            HashSet<ulong> kullanilan = new HashSet<ulong>();
            int yazilan = 0;
            foreach (FishNet.Object.NetworkObject n in nobs) {
                uint alt = KararliOzet(HiyerarsiYolu(n.transform));
                ulong id = ust | alt;
                while (id == 0 || kullanilan.Contains(id)) {
                    alt++;
                    id = ust | alt;
                }
                kullanilan.Add(id);
                SerializedObject so = new SerializedObject(n);
                SerializedProperty sp = so.FindProperty("SceneId");
                if (sp != null && sp.ulongValue != id) {
                    sp.ulongValue = id;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    yazilan++;
                }
            }
            return yazilan;
        }

        // ---------------------------------------------------------------- 4-5. ağ yöneticisi ve prefab listesi

        private static void AgYoneticisi() {
            // doğurulabilir prefab listesi
            Type uretici = Type.GetType("FishNet.Editing.PrefabCollectionGenerator.Generator, FishNet.Runtime");
            MethodInfo tam = uretici != null ? uretici.GetMethod("GenerateFull", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static) : null;
            if (tam != null) {
                object[] degerler = tam.GetParameters().Select(p => p.Name == "forced" ? (object)true : (p.HasDefaultValue ? p.DefaultValue : null)).ToArray();
                tam.Invoke(null, degerler);
            } else {
                Yaz("!! FishNet prefab üreticisi bulunamadı");
            }
            AssetDatabase.SaveAssets();
            string[] listeler = AssetDatabase.FindAssets("t:DefaultPrefabObjects");
            Object liste = listeler.Length > 0 ? AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(listeler[0])) : null;
            int prefabSayisi = 0;
            int bizim = 0;
            FishNet.Managing.Object.SinglePrefabObjects tekli = liste as FishNet.Managing.Object.SinglePrefabObjects;
            if (tekli != null) {
                prefabSayisi = tekli.Prefabs.Count;
                foreach (FishNet.Object.NetworkObject p in tekli.Prefabs) {
                    if (p != null && AssetDatabase.GetAssetPath(p).StartsWith(UretilenKlasor)) {
                        bizim++;
                    }
                }
            }

            Scene sahne = EditorSceneManager.OpenScene(OyunSahnesi, OpenSceneMode.Single);
            AnyRPG.SystemGameManager oyun = Object.FindObjectsByType<AnyRPG.SystemGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (oyun == null) {
                Yaz("!! oyun sahnesinde SystemGameManager yok");
                return;
            }
            AnyRPG.FishNetNetworkController denetci = Object.FindObjectsByType<AnyRPG.FishNetNetworkController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (denetci == null) {
                GameObject yonetici = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AgYoneticisiYolu), sahne);
                denetci = yonetici.GetComponentInChildren<AnyRPG.FishNetNetworkController>(true);
            }
            if (denetci == null) {
                Yaz("!! FishNetNetworkController bulunamadı");
                return;
            }
            FishNet.Managing.NetworkManager nm = denetci.GetComponentInParent<FishNet.Managing.NetworkManager>(true);
            if (nm != null && liste != null) {
                SerializedObject s = new SerializedObject(nm);
                SerializedProperty p = s.FindProperty("_spawnablePrefabs");
                if (p != null) {
                    p.objectReferenceValue = liste;
                    s.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            // NetworkManagerClient/Server.networkController serileştirilmez: FishNetNetworkController çalışınca kendini bağlar
            EditorSceneManager.MarkSceneDirty(sahne);
            EditorSceneManager.SaveScene(sahne);
            Yaz($"ağ yöneticisi: oyun sahnesinde; doğurulabilir prefab: {prefabSayisi} (üretilen model varyantı {bizim})"
                + (liste != null ? " (" + AssetDatabase.GetAssetPath(liste) + ")" : " (liste YOK)"));
        }
    }
}
