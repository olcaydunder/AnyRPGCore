using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Otuken.EditorAraclari {

    /// <summary>
    /// Derlemede (HaritaHazirlik) her haritaya:
    ///  1. ÖTÜKEN TAŞLARI: girişten yürüyerek ulaşılan, çevresi açık düz yerlere birbirinden uzak taşlar
    ///     (haritanın büyüklüğüne göre 14-45 tane). Her biri "Otuken Tasi" birimi doğuran bir doğma noktasıdır;
    ///     kırılınca 150 saniye sonra yerinde yenisi biter.
    ///  2. EK KAMPLAR: haritanın canavarlarından 2-3'erli kamplar (haritanın büyüklüğüne göre 3-8 kamp), var olan
    ///     kamplardan ve taşlardan uzakta. Canavarların seviyesi haritanın basamağından, girişe uzaklıkla artar
    ///     (AnyRPG.HaritaSeviyeleri, oyunda doğarken).
    /// Eklenenlerin adı "OTK_" ile başlar; sahne yeniden işlenirse önce onlar silinir. Rastgelelik haritanın
    /// adıyla tohumlanır: aynı yürüme ağı hep aynı yerleşimi verir.
    /// </summary>
    public static class HaritaDoldur {

        public const string Onek = "OTK_";
        private const string DogmaPrefabGuid = "7ff64ca283808244380f334a006b354f";   // UnitSpawnNode prefabı
        private const string TasBirimi = AnyRPG.OtukenTasi.BirimAdi;
        private const int TasYenilenme = 150;
        private const int KampYenilenme = 90;

        // haritanın canavarları (Tools~/dunya/harita_icerik.py KADRO, zindanlar.py ve yaratiklar.py ile aynı)
        private static readonly Dictionary<string, string[]> kadro = new Dictionary<string, string[]>() {
            { "UmayTarlalari", new[] { "Cali Cini", "Yagmaci Okcu", "Cali Cini", "Kara Yek" } },
            { "BoruTepesi", new[] { "Cali Cini", "Agulu Kormos", "Yagmaci Okcu", "Kara Yek" } },
            { "AkDenizKiyisi", new[] { "Yagmaci Okcu", "Sulmus", "Yagmaci Basi", "Yagmaci Okcu" } },
            { "OrdubalikCarsisi", new[] { "Sulmus", "Kara Otaci", "Yagmaci Basi", "Sulmus" } },
            { "OrdubalikKenti", new[] { "Sulmus", "Kara Kam", "Abasi", "Kara Otaci" } },
            { "UlukayinOrmani", new[] { "Sulmus", "Agulu Kormos", "Kara Otaci", "Cali Cini" } },
            { "KaganOrdasi", new[] { "Abasi", "Kara Kam", "Kan Suvarisi", "Kara Otaci" } },
            { "KafDagiYolu", new[] { "Kizil Cin", "Kormos", "Kizil Cin", "Abasi" } },
            { "ErgenekonMagarasi", new[] { "Kormos", "Kizil Cin", "Kan Suvarisi", "Kormos" } },
            { "AyDedeKoyu", new[] { "Kemik Er", "Kemik Akinci", "Kemik Kam", "Kemik Alp" } },
            { "KoncolosIni", new[] { "Kormos", "Agulu Kormos", "Kizil Cin", "Cali Cini" } },
            { "KurganMezarligi", new[] { "Kemik Er", "Kemik Akinci", "Kemik Kam", "Kemik Alp" } },
            { "TamuZindani", new[] { "Kara Kam", "Abasi", "Sulmus", "Kara Otaci", "Kan Suvarisi" } },
            { "FeaturesDemoDungeon", new[] { "Abasi", "Sulmus", "Kara Kam", "Kormos", "Kemik Er" } },
            // Ötüken Yaylası (FeaturesDemoZone) zaten kalabalık: yalnız taş eklenir
        };

        private class Engel {
            public Vector3 p;
            public float r;
            public Engel(Vector3 p, float r) {
                this.p = p;
                this.r = r;
            }
        }

        /// <summary>sahneyi doldurur, rapor satırı döner (sahne açık olmalı; kaydetmek çağıranın işi)</summary>
        public static string Doldur(Scene sahne, string ad) {
            int silinen = Temizle(sahne);
            GameObject girisNesnesi = GameObject.FindGameObjectsWithTag("DefaultSpawnLocation").FirstOrDefault(g => g.scene == sahne);
            if (girisNesnesi == null || !NavMesh.SamplePosition(girisNesnesi.transform.position, out NavMeshHit gh, 6f, NavMesh.AllAreas)) {
                return "taş/kamp: giriş ya da yürüme ağı yok, eklenmedi";
            }
            Vector3 giris = gh.position;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(DogmaPrefabGuid));
            if (prefab == null) {
                return "!! doğma noktası prefabı bulunamadı";
            }
            System.Random rng = new System.Random(Ozet("tas:" + ad));

            // ulaşılabilir, çevresi açık noktalar
            bool buyuk = ad == "FeaturesDemoZone";
            float yaricap = buyuk ? 420f : 210f;
            float adim = buyuk ? 6f : 4f;
            List<Vector3> noktalar = AcikNoktalar(giris, yaricap, adim, 1.6f);
            float alan = noktalar.Count * adim * adim;

            // engeller: giriş, etkileşimli her şey (geçit taşı, kapılar, sandıklar, NPC'ler), var olan doğma noktaları
            List<Engel> engeller = new List<Engel>() { new Engel(giris, 16f) };
            foreach (AnyRPG.InteractableBase e in Object.FindObjectsByType<AnyRPG.InteractableBase>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (e.gameObject.scene == sahne) {
                    engeller.Add(new Engel(e.transform.position, 7f));
                }
            }
            foreach (AnyRPG.UnitSpawnNode n in Object.FindObjectsByType<AnyRPG.UnitSpawnNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (n.gameObject.scene == sahne) {
                    engeller.Add(new Engel(n.transform.position, 7f));
                }
            }

            // 1. Ötüken Taşları
            int tasHedef = Mathf.Clamp(Mathf.RoundToInt(alan / (buyuk ? 4500f : 1100f)), 14, 45);
            List<Vector3> taslar = UzakNoktalar(noktalar, engeller, giris, tasHedef, 13f);
            int i = 0;
            foreach (Vector3 p in taslar) {
                i++;
                DogmaNoktasi(prefab, sahne, Onek + "Tas_" + i.ToString("00"), p, rng.Next(0, 360), TasBirimi, TasYenilenme);
                engeller.Add(new Engel(p, 9f));
            }

            // 2. ek kamplar: biraz daha geniş açıklıklar
            string[] liste;
            int kampSayisi = 0;
            int canavar = 0;
            if (kadro.TryGetValue(ad, out liste)) {
                List<Vector3> genis = noktalar.Where(p => KenarUzakligi(p) >= 3f).ToList();
                int kampHedef = Mathf.Clamp(Mathf.RoundToInt(alan / 6000f), 3, 8);
                List<Vector3> kamplar = UzakNoktalar(genis, engeller, giris, kampHedef, 22f);
                foreach (Vector3 merkez in kamplar) {
                    kampSayisi++;
                    int adet = 2 + (kampSayisi % 2);
                    for (int j = 0; j < adet; j++) {
                        float a = 2f * Mathf.PI * j / adet + (float)rng.NextDouble();
                        Vector3 q = merkez + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 3.4f;
                        if (!NavMesh.SamplePosition(q, out NavMeshHit h, 2.5f, NavMesh.AllAreas) || Mathf.Abs(h.position.y - merkez.y) > 2f) {
                            h.position = merkez;
                        }
                        string profil = liste[(kampSayisi + j) % liste.Length];
                        DogmaNoktasi(prefab, sahne, Onek + "Kamp_" + kampSayisi.ToString("00") + "_" + (j + 1), h.position, rng.Next(0, 360), profil, KampYenilenme);
                        canavar++;
                    }
                }
            }
            return $"taş/kamp: {taslar.Count} Ötüken Taşı, {kampSayisi} ek kamp ({canavar} canavar)"
                + $" | açık alan ~{alan:0} m² ({noktalar.Count} nokta)" + (silinen > 0 ? $", eskiden {silinen} silindi" : string.Empty);
        }

        private static int Temizle(Scene sahne) {
            int n = 0;
            foreach (GameObject k in sahne.GetRootGameObjects()) {
                if (k.name.StartsWith(Onek)) {
                    Object.DestroyImmediate(k);
                    n++;
                }
            }
            return n;
        }

        /// <summary>girişten tam yolu olan, yürüme ağının kenarından en az kenar metre içerde ızgara noktaları</summary>
        private static List<Vector3> AcikNoktalar(Vector3 giris, float yaricap, float adim, float kenar) {
            List<Vector3> sonuc = new List<Vector3>();
            NavMeshPath yol = new NavMeshPath();
            for (float x = -yaricap; x <= yaricap; x += adim) {
                for (float z = -yaricap; z <= yaricap; z += adim) {
                    if (x * x + z * z > yaricap * yaricap) {
                        continue;
                    }
                    Vector3 q = giris + new Vector3(x, 0f, z);
                    if (!NavMesh.SamplePosition(q, out NavMeshHit h, 14f, NavMesh.AllAreas)) {
                        continue;
                    }
                    if (Mathf.Abs(h.position.x - q.x) > adim * 0.5f || Mathf.Abs(h.position.z - q.z) > adim * 0.5f) {
                        continue;
                    }
                    if (KenarUzakligi(h.position) < kenar) {
                        continue;
                    }
                    if (!NavMesh.CalculatePath(giris, h.position, NavMesh.AllAreas, yol) || yol.status != NavMeshPathStatus.PathComplete) {
                        continue;
                    }
                    sonuc.Add(h.position);
                }
            }
            return sonuc;
        }

        private static float KenarUzakligi(Vector3 p) {
            return NavMesh.FindClosestEdge(p, out NavMeshHit e, NavMesh.AllAreas) ? e.distance : 0f;
        }

        /// <summary>en uzak nokta örneklemesi: her seçilen, girişten, engellerden ve öncekilerden olabildiğince uzak</summary>
        private static List<Vector3> UzakNoktalar(List<Vector3> adaylar, List<Engel> engeller, Vector3 giris, int adet, float enAz) {
            List<Vector3> uygun = adaylar.Where(p => engeller.All(e => Yatay(p, e.p) >= e.r)).ToList();
            List<Vector3> secilen = new List<Vector3>();
            float[] uzaklik = uygun.Select(p => Mathf.Min(Yatay(p, giris), engeller.Min(e => Yatay(p, e.p) - e.r + enAz))).ToArray();
            for (int k = 0; k < adet && uygun.Count > 0; k++) {
                int enIyi = -1;
                float enIyiUzaklik = -1f;
                for (int j = 0; j < uygun.Count; j++) {
                    if (uzaklik[j] > enIyiUzaklik) {
                        enIyiUzaklik = uzaklik[j];
                        enIyi = j;
                    }
                }
                if (enIyi < 0 || enIyiUzaklik < enAz) {
                    break;
                }
                Vector3 s = uygun[enIyi];
                secilen.Add(s);
                for (int j = 0; j < uygun.Count; j++) {
                    uzaklik[j] = Mathf.Min(uzaklik[j], Yatay(uygun[j], s));
                }
            }
            return secilen;
        }

        private static float Yatay(Vector3 a, Vector3 b) {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static void DogmaNoktasi(GameObject prefab, Scene sahne, string ad, Vector3 p, int yon, string profil, int yenilenme) {
            GameObject n = (GameObject)PrefabUtility.InstantiatePrefab(prefab, sahne);
            n.name = ad;
            n.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, Quaternion.Euler(0f, yon, 0f));
            AnyRPG.UnitSpawnNode dogma = n.GetComponent<AnyRPG.UnitSpawnNode>();
            SerializedObject so = new SerializedObject(dogma);
            SerializedProperty adlar = so.FindProperty("unitProfileNames");
            adlar.arraySize = 1;
            adlar.GetArrayElementAtIndex(0).stringValue = profil;
            so.FindProperty("respawnTimer").intValue = yenilenme;
            so.FindProperty("extraLevels").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int Ozet(string s) {
            unchecked {
                int h = (int)2166136261;
                foreach (char c in s) {
                    h = (h ^ c) * 16777619;
                }
                return h & 0x7fffffff;
            }
        }
    }
}
