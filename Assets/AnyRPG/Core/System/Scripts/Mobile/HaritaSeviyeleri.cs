using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnyRPG {

    /// <summary>
    /// Haritaların seviye basamakları. Haritalar kolaydan zora dizilir (Geçit Taşı ve Işınlan penceresindeki sıra);
    /// her harita 4 seviyelik bir aralıktır ve bir sonraki harita 2 seviye yukarıdan başlar:
    ///   Ötüken Yaylası 1–4, Umay Tarlaları 3–6, Börü Tepesi 5–8 ... Tamu Zindanı 29–32.
    /// Canavarlar ve Ötüken Taşları oyuncunun seviyesine göre değil haritaya göre doğar (UnitSpawnNode.CommonSpawn):
    /// girişe yakın olanlar aralığın altında, haritanın derinlerindekiler üstünde; dayanıklı (seçkin) birimler bir
    /// seviye yukarıda, boss'lar aralığın tepesinde. Aynı kural çevrimdışı oyunda ve çevrimiçi sunucuda geçerlidir.
    /// Harita adlarının altında yazan "Seviye a–b" de buradan gelir.
    /// </summary>
    public static class HaritaSeviyeleri {

        public const int Genislik = 3;      // bir haritanın içindeki seviye farkı (en ile üst arası)
        public const int Adim = 2;          // bir sonraki haritanın kaç seviye yukarıdan başladığı

        // kolaydan zora (IsinlanmaPenceresi ile aynı sıra)
        public static readonly string[] Sira = {
            "FeaturesDemoZone", "UmayTarlalari", "BoruTepesi", "AkDenizKiyisi", "OrdubalikCarsisi", "OrdubalikKenti",
            "UlukayinOrmani", "KoncolosIni", "KaganOrdasi", "KafDagiYolu", "ErgenekonMagarasi", "KurganMezarligi",
            "AyDedeKoyu", "FeaturesDemoDungeon", "TamuZindani",
        };

        private static readonly HashSet<string> bossDayanikliliklari = new HashSet<string>() {
            "Solo Dungeon Boss", "2 Man", "5 Man", "10 Man", "25 Man"
        };
        private const string SeckinDayaniklilik = "Solo Dungeon Minion";

        public static bool Aralik(string sahne, out int en, out int ust) {
            int i = System.Array.IndexOf(Sira, sahne);
            if (i < 0) {
                en = 0;
                ust = 0;
                return false;
            }
            en = 1 + Adim * i;
            ust = en + Genislik;
            return true;
        }

        /// <summary>"Seviye 3–6" (harita basamaklı değilse boş)</summary>
        public static string Yazi(string sahne) {
            int en, ust;
            if (Aralik(sahne, out en, out ust) == false) {
                return string.Empty;
            }
            return "Seviye " + en + "–" + ust;
        }

        /// <summary>
        /// oyuncunun seviyesine göre kısa yorum ve renk: aralığın altındaysa "zor" (kırmızı), aralıktaysa "sana göre"
        /// (yeşil), çok üstündeyse "kolay" (gri). oyuncuSeviyesi 0 ise yorum yok, altın renk.
        /// </summary>
        public static string Yorum(string sahne, int oyuncuSeviyesi, out Color renk) {
            int en, ust;
            renk = new Color(0.91f, 0.77f, 0.48f, 1f);
            if (oyuncuSeviyesi <= 0 || Aralik(sahne, out en, out ust) == false) {
                return string.Empty;
            }
            if (oyuncuSeviyesi < en - 1) {
                renk = new Color(1f, 0.45f, 0.35f, 1f);
                return "zor";
            }
            if (oyuncuSeviyesi > ust + 2) {
                renk = new Color(0.62f, 0.62f, 0.62f, 1f);
                return "kolay";
            }
            renk = new Color(0.55f, 0.9f, 0.45f, 1f);
            return "sana göre";
        }

        private static SystemGameManager oyun = null;

        /// <summary>yerel oyuncunun seviyesi (oyuncu yoksa 0)</summary>
        public static int OyuncuSeviyesi() {
            if (oyun == null) {
                oyun = Object.FindAnyObjectByType<SystemGameManager>();
            }
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            return oyuncu != null && oyuncu.CharacterStats != null ? oyuncu.CharacterStats.Level : 0;
        }

        // ---------------------------------------------------------------- doğan birimin seviyesi

        private class SahneBilgisi {
            public Vector3 giris;
            public float uzaklik;   // girişten en uzak doğma noktasının uzaklığı
        }

        private static readonly Dictionary<int, SahneBilgisi> sahneler = new Dictionary<int, SahneBilgisi>();

        private static SahneBilgisi Bilgi(Scene sahne) {
            SahneBilgisi b;
            if (sahneler.TryGetValue(sahne.handle, out b)) {
                return b;
            }
            b = new SahneBilgisi();
            bool girisVar = false;
            foreach (GameObject g in GameObject.FindGameObjectsWithTag("DefaultSpawnLocation")) {
                if (g.scene == sahne) {
                    b.giris = g.transform.position;
                    girisVar = true;
                    break;
                }
            }
            float enUzak = 0f;
            List<Vector3> noktalar = new List<Vector3>();
            foreach (UnitSpawnNode n in Object.FindObjectsByType<UnitSpawnNode>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (n.gameObject.scene == sahne) {
                    noktalar.Add(n.transform.position);
                }
            }
            if (girisVar == false && noktalar.Count > 0) {
                // giriş yoksa noktaların ortası
                Vector3 toplam = Vector3.zero;
                foreach (Vector3 p in noktalar) {
                    toplam += p;
                }
                b.giris = toplam / noktalar.Count;
            }
            foreach (Vector3 p in noktalar) {
                enUzak = Mathf.Max(enUzak, Yatay(p, b.giris));
            }
            b.uzaklik = Mathf.Max(60f, enUzak);
            sahneler[sahne.handle] = b;
            return b;
        }

        private static float Yatay(Vector3 a, Vector3 b) {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        /// <summary>sahne kapanınca önbellek boşalsın (aynı tutamaç başka sahneye verilebilir)</summary>
        public static void SahneKapandi(Scene sahne) {
            sahneler.Remove(sahne.handle);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Kur() {
            sahneler.Clear();
            SceneManager.sceneUnloaded -= SahneKapandi;
            SceneManager.sceneUnloaded += SahneKapandi;
        }

        /// <summary>
        /// bu doğma noktasından doğacak birimin seviyesi; harita basamaklı değilse 0 (AnyRPG'nin kendi kuralı geçerli)
        /// </summary>
        public static int BirimSeviyesi(UnitSpawnNode nokta, UnitProfile profil, UnitToughness dayaniklilik) {
            if (nokta == null) {
                return 0;
            }
            int en, ust;
            if (Aralik(nokta.gameObject.scene.name, out en, out ust) == false) {
                return 0;
            }
            string tur = dayaniklilik != null ? dayaniklilik.ResourceName : string.Empty;
            if (bossDayanikliliklari.Contains(tur)) {
                return ust;
            }
            SahneBilgisi b = Bilgi(nokta.gameObject.scene);
            float t = Mathf.Clamp01(Yatay(nokta.transform.position, b.giris) / b.uzaklik);
            int seviye = en + Mathf.Min(Genislik, Mathf.FloorToInt(t * (Genislik + 1)));
            if (tur == SeckinDayaniklilik) {
                seviye++;
            }
            return Mathf.Clamp(seviye, en, ust);
        }
    }
}
