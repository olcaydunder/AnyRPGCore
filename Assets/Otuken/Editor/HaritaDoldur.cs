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
    ///     (haritanın büyüklüğüne göre 14-45 tane; Ötüken Yaylası'nda 70). Her biri "Otuken Tasi" birimi doğuran bir doğma noktasıdır;
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
            float tasAraligi = 13f;
            if (noktalar.Count < 300) {
                // dar haritalar (zindanlar, mağaralar): koridorlar dar, ızgara sık, taşlar daha yakın
                adim = 3f;
                noktalar = AcikNoktalar(giris, yaricap, adim, 0.9f);
                tasAraligi = 8f;
            }
            float alan = noktalar.Count * adim * adim;

            // 0. girişte güvenli alan: haritaya girer girmez savaşa düşülmesin (zindanların girişi dar)
            int tasinan = GuvenliGiris(sahne, giris, noktalar);

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
            int tasHedef = Mathf.Clamp(Mathf.RoundToInt(alan / (buyuk ? 4500f : 1100f)), 14, buyuk ? 70 : 45);
            List<Vector3> taslar = UzakNoktalar(noktalar, engeller, giris, tasHedef, tasAraligi);
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
            // 3. ana hikâye (HikayeVerisi, Tools~/dunya/hikaye.py): görev veren yardımcı girişin yanına, haritanın yeni
            //    boss'u girişten en uzak açıklığa (sahnedeki öteki boss'lardan uzak)
            string hikaye = Hikaye(sahne, ad, prefab, giris, noktalar, engeller);
            return $"taş/kamp: {taslar.Count} Ötüken Taşı, {kampSayisi} ek kamp ({canavar} canavar)" + hikaye
                + (tasinan > 0 ? $", girişten uzaklaştırılan {tasinan} düşman" : string.Empty)
                + $" | açık alan ~{alan:0} m² ({noktalar.Count} nokta)" + (silinen > 0 ? $", eskiden {silinen} silindi" : string.Empty);
        }

        private const float GuvenliYaricap = 20f;

        /// <summary>girişe GuvenliYaricap'tan yakın düşman doğma noktalarını (TR_) en yakın uygun açık noktaya taşır</summary>
        private static int GuvenliGiris(Scene sahne, Vector3 giris, List<Vector3> noktalar) {
            List<AnyRPG.UnitSpawnNode> dugumler = Object.FindObjectsByType<AnyRPG.UnitSpawnNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(n => n.gameObject.scene == sahne).ToList();
            List<Vector3> dolu = dugumler.Select(n => n.transform.position).ToList();
            int tasinan = 0;
            foreach (AnyRPG.UnitSpawnNode n in dugumler.OrderBy(d => d.name)) {
                if (n.name.StartsWith("TR_") == false || Yatay(n.transform.position, giris) >= GuvenliYaricap) {
                    continue;
                }
                Vector3 eski = n.transform.position;
                Vector3 enIyi = eski;
                float enAz = float.MaxValue;
                foreach (Vector3 p in noktalar) {
                    if (Yatay(p, giris) < GuvenliYaricap + 6f || dolu.Any(d => Yatay(d, p) < 4f)) {
                        continue;
                    }
                    float d = Vector3.Distance(p, eski);
                    if (d < enAz) {
                        enAz = d;
                        enIyi = p;
                    }
                }
                if (enAz < float.MaxValue) {
                    n.transform.position = enIyi + Vector3.up * 0.05f;
                    dolu.Add(enIyi);
                    tasinan++;
                }
            }
            return tasinan;
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

        private static readonly HashSet<string> eskiBosslar = new HashSet<string>() {
            "Tamu Bekcisi", "Kara Koncolos", "Kemik Kagan", "Enemy Boss", "Yelbegen", "Ulu Evren"
        };

        private static string Hikaye(Scene sahne, string ad, GameObject prefab, Vector3 giris, List<Vector3> noktalar, List<Engel> engeller) {
            string[] h;
            if (HikayeVerisi.Haritalar.TryGetValue(ad, out h) == false) {
                return string.Empty;
            }
            string rapor = string.Empty;
            List<Vector3> etkilesimler = Object.FindObjectsByType<AnyRPG.InteractableBase>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(e => e.gameObject.scene == sahne).Select(e => e.transform.position).ToList();
            if (h[0].Length > 0) {
                // girişe 6-11 m, açık, geçit taşı ve kapılardan 4 m uzak; yoksa girişe en yakın uygun nokta
                List<Vector3> adaylar = noktalar.Where(p => etkilesimler.All(e => Yatay(p, e) >= 4f) && Mathf.Abs(p.y - giris.y) < 2.5f).ToList();
                List<Vector3> halka = adaylar.Where(p => Yatay(p, giris) >= 6f && Yatay(p, giris) <= 11f).ToList();
                Vector3? yer = null;
                if (halka.Count > 0) {
                    yer = halka.OrderByDescending(p => Mathf.Min(KenarUzakligi(p), 3f) - Mathf.Abs(Yatay(p, giris) - 8f) * 0.2f).First();
                } else if (GirisYani(giris, etkilesimler, out Vector3 yan)) {
                    // dar girişler: açık nokta yok, yürüme ağında girişin 5-10 m yanı
                    yer = yan;
                } else if (adaylar.Count > 0) {
                    yer = adaylar.Where(p => Yatay(p, giris) >= 3f).OrderBy(p => Yatay(p, giris)).FirstOrDefault();
                }
                if (yer.HasValue && yer.Value != Vector3.zero) {
                    // girişe dönük dursun
                    int yon = Mathf.RoundToInt(Mathf.Atan2(giris.x - yer.Value.x, giris.z - yer.Value.z) * Mathf.Rad2Deg);
                    DogmaNoktasi(prefab, sahne, Onek + "Yardimci", yer.Value, yon, h[0], 30);
                    rapor += $", yardımcı {h[0]} (girişe {Yatay(yer.Value, giris):0} m)";
                } else {
                    rapor += ", !! yardımcıya yer bulunamadı";
                }
            }
            if (h[1].Length > 0) {
                List<Vector3> eskiler = Object.FindObjectsByType<AnyRPG.UnitSpawnNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(n => n.gameObject.scene == sahne && BossMu(n)).Select(n => n.transform.position).ToList();
                // oyuncunun haritaya vardığı yerler (giriş ve geçit taşı): boss oradan en az BossUzakligi uzakta olmalı
                // (0.1.60'ta Ergenekon'un açık alanı girişin dibindeydi, boss 9 m'ye kondu ve gelen oyuncuyu hemen öldürdü)
                List<Vector3> varislar = new List<Vector3>() { giris };
                foreach (GameObject kok in sahne.GetRootGameObjects()) {
                    if (kok.name == "GecitTasi") {
                        varislar.Add(kok.transform.position);
                    }
                }
                List<Vector3> adaylar = noktalar.Where(p => KenarUzakligi(p) >= 3f && eskiler.All(e => Yatay(p, e) >= 25f)
                    && engeller.All(e => Yatay(p, e.p) >= Mathf.Min(e.r, 9f)) && varislar.All(v => Yatay(p, v) >= BossUzakligi)).ToList();
                if (adaylar.Count == 0) {
                    adaylar = noktalar.Where(p => eskiler.All(e => Yatay(p, e) >= 15f) && varislar.All(v => Yatay(p, v) >= BossUzakligi)).ToList();
                }
                if (adaylar.Count == 0) {
                    // dar koridorlu haritalar (mağaralar): açık nokta yok; yürüme ağının köşelerinden girişten yürünerek
                    // ulaşılan en uzağı
                    adaylar = UlasilanUzakNoktalar(giris, varislar, eskiler);
                }
                if (adaylar.Count > 0) {
                    Vector3 yer = adaylar.OrderByDescending(p => varislar.Min(v => Yatay(p, v))).First();
                    DogmaNoktasi(prefab, sahne, Onek + "Boss", yer, Ozet(ad) % 360, h[1], 600);
                    engeller.Add(new Engel(yer, 12f));
                    rapor += $", boss {h[1]} (girişe {Yatay(yer, giris):0} m)";
                } else {
                    rapor += ", !! boss'a yer bulunamadı";
                }
            }
            return rapor;
        }

        private const float BossUzakligi = 35f;

        /// <summary>girişin 5-10 m yanında, yürüme ağında, girişten yürünerek ulaşılan bir yer (kapı ve geçit taşından 3 m uzak)</summary>
        private static bool GirisYani(Vector3 giris, List<Vector3> etkilesimler, out Vector3 yer) {
            NavMeshPath yol = new NavMeshPath();
            foreach (float r in new[] { 7f, 5f, 9f, 11f }) {
                for (int k = 0; k < 12; k++) {
                    float a = k * Mathf.PI / 6f;
                    Vector3 q = giris + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (NavMesh.SamplePosition(q, out NavMeshHit h, 1.5f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - giris.y) < 2.5f
                        && Yatay(h.position, giris) >= 4.5f && etkilesimler.All(e => Yatay(h.position, e) >= 3f)
                        && NavMesh.CalculatePath(giris, h.position, NavMesh.AllAreas, yol) && yol.status == NavMeshPathStatus.PathComplete) {
                        yer = h.position;
                        return true;
                    }
                }
            }
            yer = Vector3.zero;
            return false;
        }

        /// <summary>yürüme ağının köşelerinden girişten yürünerek ulaşılanlar (varış yerlerinden en az BossUzakligi uzakta)</summary>
        private static List<Vector3> UlasilanUzakNoktalar(Vector3 giris, List<Vector3> varislar, List<Vector3> eskiler) {
            List<Vector3> sonuc = new List<Vector3>();
            NavMeshTriangulation ag = NavMesh.CalculateTriangulation();
            NavMeshPath yol = new NavMeshPath();
            IEnumerable<Vector3> koseler = ag.vertices
                .Where(p => varislar.All(v => Yatay(p, v) >= BossUzakligi) && eskiler.All(e => Yatay(p, e) >= 15f))
                .OrderByDescending(p => varislar.Min(v => Yatay(p, v))).Take(300);
            foreach (Vector3 k in koseler) {
                if (NavMesh.SamplePosition(k, out NavMeshHit h, 2f, NavMesh.AllAreas)
                    && NavMesh.CalculatePath(giris, h.position, NavMesh.AllAreas, yol) && yol.status == NavMeshPathStatus.PathComplete) {
                    sonuc.Add(h.position);
                    if (sonuc.Count >= 5) {
                        break;
                    }
                }
            }
            return sonuc;
        }

        private static bool BossMu(AnyRPG.UnitSpawnNode n) {
            SerializedProperty adlar = new SerializedObject(n).FindProperty("unitProfileNames");
            for (int i = 0; adlar != null && i < adlar.arraySize; i++) {
                if (eskiBosslar.Contains(adlar.GetArrayElementAtIndex(i).stringValue)) {
                    return true;
                }
            }
            return false;
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
