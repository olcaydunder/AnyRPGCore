using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Otuken.EditorAraclari {

    /// <summary>
    /// Otomatik oyun testi (bot): editörde oyun modunda, insan olmadan oyunu baştan sona oynar.
    ///  1. İlk sahneden açılır, ana menüyü bekler.
    ///  2. Varsayılan karakterle yeni oyun başlatır, Ötüken Yaylası'nda doğmasını bekler.
    ///  3. Işınlan penceresinin kullandığı yolla 15 haritanın hepsine sırayla ışınlanır; her haritada
    ///     otomatik avı birkaç saniye açar (savaş ve ganimet kodları da çalışsın), oyuncu düştü mü,
    ///     yürüme ağının üstünde mi bakar, bir görüntü alır.
    ///  4. Bu sürede oyunun yazdığı bütün hataları ve istisnaları haritasıyla toplar.
    /// Sonuç: tani/oyun_testi.json, tani/oyun_testi.txt, tani/bot_*.jpg. Bitince editörü kapatır.
    /// GitHub Actions'ta APK derlemesinden sonra çalışır (android-apk.yml, "Oyun testi").
    /// Komut satırı: -executeMethod Otuken.EditorAraclari.OyunTesti.Baslat (-quit olmadan).
    /// </summary>
    [InitializeOnLoad]
    public static class OyunTesti {

        private const string AktifKey = "OyunTesti.Aktif";
        private const string BaslangicKey = "OyunTesti.Baslangic";
        private const string IlkSahne = "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Scenes/Game/FeaturesDemoGame/FeaturesDemoGame.unity";
        private const double ToplamSure = 34 * 60;
        private const double MenuBekleme = 240;
        private const double DogusBekleme = 180;
        private const double HaritadaKalma = 12;
        private const double AvSuresi = 8;

        [Serializable]
        private class HaritaSonucu {
            public string sahne;
            public string ad;
            public string sonuc;
            public float yuklemeSuresi;
            public int hata;
            public string not;
            public string bilgi;
            public string goruntu;
        }

        [Serializable]
        private class HataKaydi {
            public string tur;
            public string mesaj;
            public string yigin;
            public string sahne;
            public int adet;
        }

        [Serializable]
        private class Rapor {
            public string baslangic;
            public string bitis;
            public string durum;
            public int toplamHata;
            public List<HaritaSonucu> haritalar = new List<HaritaSonucu>();
            public List<HataKaydi> hatalar = new List<HataKaydi>();
        }

        private enum Adim { OyunModu, Acilis, YeniOyun, Dogus, Bekle, Dirilis, Bitti }

        private static readonly object kilit = new object();
        private static Rapor rapor;
        private static Dictionary<string, HataKaydi> hataSozlugu;
        private static volatile string aktifSahne = "-";
        private static Adim adim;
        private static double adimBaslangic;
        private static double testBaslangic;
        private static string[] sahneler;
        private static int sira;
        private static HaritaSonucu sonuc;
        private static double dogusZamani;
        private static Vector3 dogusYeri;
        private static int haritaBasiHata;
        private static bool avAcildi;

        static OyunTesti() {
            // oyun moduna geçerken kod yeniden yüklenir; test sürüyorsa kaldığı yerden bağlanır
            if (SessionState.GetBool(AktifKey, false)) {
                Kur();
            }
        }

        private static void Kur() {
            rapor = new Rapor() { baslangic = SessionState.GetString(BaslangicKey, DateTime.UtcNow.ToString("o")) };
            hataSozlugu = new Dictionary<string, HataKaydi>();
            sahneler = AnyRPG.IsinlanmaPenceresi.SahneAdlari;
            sira = 0;
            testBaslangic = EditorApplication.timeSinceStartup;
            Gec(Adim.OyunModu);
            Application.logMessageReceivedThreaded += KayitGeldi;
            EditorApplication.update += Guncelle;
        }

        /// <summary>komut satırından çağrılır (-quit olmadan): ilk sahneyi açar ve oyun moduna geçer</summary>
        public static void Baslat() {
            Directory.CreateDirectory(Path.GetFullPath("tani"));
            EditorPrefs.SetBool("AnyRPG_DisplayWelcome", false);
            // oyuncuya ilk girişte açılan pencereler testi durdurmasın
            PlayerPrefs.SetInt("ShowNewPlayerHints", 0);
            PlayerPrefs.SetInt("oyun-rehberi-gosterildi", 1);
            PlayerPrefs.Save();
            SessionState.SetBool(AktifKey, true);
            SessionState.SetString(BaslangicKey, DateTime.UtcNow.ToString("o"));
            EditorSceneManager.OpenScene(IlkSahne, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(IlkSahne);
            // kod yeniden yüklenmezse de çalışsın (yüklenirse durağan kurucu yeniden bağlar)
            Kur();
            Debug.Log("[OyunTesti] oyun moduna geçiliyor");
            EditorApplication.EnterPlaymode();
        }

        // ---------------------------------------------------------------- hatalar

        private static void KayitGeldi(string mesaj, string yigin, LogType tur) {
            if (tur != LogType.Error && tur != LogType.Exception && tur != LogType.Assert) {
                return;
            }
            if (mesaj != null && mesaj.StartsWith("[OyunTesti]", StringComparison.Ordinal)) {
                return;
            }
            // editörün kendi iç hataları (arama dizini vb.) oyunun hatası değildir
            if (yigin != null && (yigin.StartsWith("UnityEditor.", StringComparison.Ordinal) || yigin.Contains("UnityEditor.Search."))) {
                return;
            }
            string ilkSatir = IlkSatir(mesaj);
            string ilkCerceve = IlkSatir(yigin);
            string anahtar = ilkSatir + "|" + ilkCerceve;
            lock (kilit) {
                if (rapor == null) {
                    return;
                }
                rapor.toplamHata++;
                haritaBasiHata++;
                HataKaydi kayit;
                if (hataSozlugu.TryGetValue(anahtar, out kayit)) {
                    kayit.adet++;
                    if (kayit.sahne.Contains(aktifSahne) == false && kayit.sahne.Length < 300) {
                        kayit.sahne += ", " + aktifSahne;
                    }
                    return;
                }
                kayit = new HataKaydi() {
                    tur = tur == LogType.Exception ? "istisna" : "hata", mesaj = Kes(mesaj, 600), yigin = Kes(yigin, 1500), sahne = aktifSahne, adet = 1
                };
                hataSozlugu[anahtar] = kayit;
                rapor.hatalar.Add(kayit);
            }
        }

        // ---------------------------------------------------------------- adımlar

        private static void Gec(Adim yeni) {
            adim = yeni;
            adimBaslangic = EditorApplication.timeSinceStartup;
        }

        private static double AdimSuresi {
            get { return EditorApplication.timeSinceStartup - adimBaslangic; }
        }

        private static void Guncelle() {
            if (adim == Adim.Bitti) {
                return;
            }
            double simdi = EditorApplication.timeSinceStartup;
            if (simdi - testBaslangic > ToplamSure) {
                Bitir("süre doldu (" + (int)(ToplamSure / 60) + " dk)");
                return;
            }
            if (EditorApplication.isPlaying == false) {
                if (adim != Adim.OyunModu) {
                    Bitir("oyun modu beklenmedik şekilde kapandı");
                } else if (AdimSuresi > 300) {
                    Bitir("oyun moduna geçilemedi");
                }
                return;
            }
            aktifSahne = SceneManager.GetActiveScene().name;
            try {
                Ilerle();
            } catch (Exception e) {
                Debug.LogError("[OyunTesti] test adımı hata verdi: " + e);
                Bitir("test hatası: " + e.Message);
            }
        }

        private static void Ilerle() {
            AnyRPG.SystemGameManager oyun = Object.FindAnyObjectByType<AnyRPG.SystemGameManager>();
            switch (adim) {
                case Adim.OyunModu:
                    Gec(Adim.Acilis);
                    break;

                case Adim.Acilis:
                    if (oyun != null && oyun.LevelManagerClient != null && oyun.UIManager != null && oyun.LevelManagerClient.IsMainMenu()
                        && oyun.UIManager.mainMenuWindow != null && oyun.UIManager.mainMenuWindow.IsOpen) {
                        Debug.Log("[OyunTesti] ana menü açık, yeni oyun başlatılıyor");
                        Gec(Adim.YeniOyun);
                    } else if (AdimSuresi > MenuBekleme) {
                        Bitir("ana menü " + (int)MenuBekleme + " saniyede açılmadı");
                    }
                    break;

                case Adim.YeniOyun:
                    // menünün kendini kurması için kısa bir bekleme
                    if (AdimSuresi < 3) {
                        break;
                    }
                    oyun.NewGameManager.InitializeData();
                    oyun.NewGameManager.SetupSaveData();
                    oyun.NewGameManager.NewLocalGame();
                    HaritaBasla(sahneler[0]);
                    Gec(Adim.Dogus);
                    break;

                case Adim.Dogus:
                    AnyRPG.PlayerManagerClient oyuncular = oyun != null ? oyun.PlayerManagerClient : null;
                    if (oyuncular != null && oyuncular.PlayerUnitSpawned && oyuncular.UnitController != null
                        && SceneManager.GetActiveScene().name == sonuc.sahne) {
                        sonuc.yuklemeSuresi = (float)AdimSuresi;
                        dogusZamani = EditorApplication.timeSinceStartup;
                        dogusYeri = oyuncular.UnitController.transform.position;
                        avAcildi = false;
                        Debug.Log("[OyunTesti] " + sonuc.sahne + " yüklendi (" + sonuc.yuklemeSuresi.ToString("0") + " sn)");
                        Gec(Adim.Bekle);
                    } else if (AdimSuresi > DogusBekleme) {
                        HaritaBitir("yüklenmedi ya da oyuncu doğmadı (" + (int)DogusBekleme + " sn)");
                        SonrakiHarita(oyun);
                    }
                    break;

                case Adim.Bekle:
                    // haritada biraz kal: düşmanlar, sesler, harita dokusu; ortada birkaç saniye otomatik av
                    if (avAcildi == false && AdimSuresi > 2) {
                        avAcildi = true;
                        if (AnyRPG.OtomatikAv.Acik == false) {
                            AnyRPG.OtomatikAv.Degistir();
                        }
                    }
                    if (AdimSuresi > 2 + AvSuresi && AnyRPG.OtomatikAv.Acik) {
                        AnyRPG.OtomatikAv.Kapat();
                    }
                    if (AdimSuresi < HaritadaKalma) {
                        break;
                    }
                    HaritaDenetle(oyun);
                    sonuc.goruntu = GoruntuAl(oyun, "bot_" + sonuc.sahne);
                    HaritaBitir(sonuc.not == null ? "tamam" : "sorunlu");
                    SonrakiHarita(oyun);
                    break;

                case Adim.Dirilis:
                    // ölen oyuncu ölüm penceresindeki "yeniden doğ" yoluyla dirilir, sonra yolculuk sürer
                    AnyRPG.PlayerManagerClient pm = oyun != null ? oyun.PlayerManagerClient : null;
                    bool dirildi = pm != null && pm.PlayerUnitSpawned && pm.UnitController != null
                        && pm.UnitController.CharacterStats != null && pm.UnitController.CharacterStats.IsAlive;
                    if ((dirildi && AdimSuresi > 2) || AdimSuresi > 60) {
                        Isinla(oyun);
                    }
                    break;
            }
        }

        private static void HaritaBasla(string sahne) {
            sonuc = new HaritaSonucu() { sahne = sahne, ad = AnyRPG.IsinlanmaPenceresi.GorunenAd(sahne), sonuc = "başladı" };
            lock (kilit) {
                haritaBasiHata = 0;
            }
        }

        private static void HaritaBitir(string durum) {
            lock (kilit) {
                sonuc.hata = haritaBasiHata;
            }
            sonuc.sonuc = durum;
            rapor.haritalar.Add(sonuc);
            Debug.Log("[OyunTesti] " + sonuc.sahne + ": " + durum + ", " + sonuc.hata + " hata" + (sonuc.not != null ? " (" + sonuc.not + ")" : ""));
        }

        private static void SonrakiHarita(AnyRPG.SystemGameManager oyun) {
            sira++;
            if (sira >= sahneler.Length) {
                Bitir("tamamlandı");
                return;
            }
            AnyRPG.OtomatikAv.Kapat();
            AnyRPG.UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu != null && oyuncu.CharacterStats != null && oyuncu.CharacterStats.IsAlive == false) {
                Debug.Log("[OyunTesti] oyuncu öldü, yeniden doğuyor");
                if (oyun.UIManager != null && oyun.UIManager.playerOptionsMenuWindow != null) {
                    oyun.UIManager.playerOptionsMenuWindow.CloseWindow();
                }
                oyun.PlayerManagerClient.RequestRespawnPlayer();
                Gec(Adim.Dirilis);
                return;
            }
            Isinla(oyun);
        }

        private static void Isinla(AnyRPG.SystemGameManager oyun) {
            string sahne = sahneler[sira];
            HaritaBasla(sahne);
            string engel = AnyRPG.IsinlanmaPenceresi.Teleport(sahne, true);
            if (engel != null) {
                sonuc.not = engel;
                HaritaBitir("ışınlanılamadı");
                SonrakiHarita(oyun);
                return;
            }
            Gec(Adim.Dogus);
        }

        /// <summary>oyuncu haritadan düştü mü, yürüme ağının üstünde mi</summary>
        private static void HaritaDenetle(AnyRPG.SystemGameManager oyun) {
            AnyRPG.UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu == null) {
                sonuc.not = "oyuncu yok";
                return;
            }
            Vector3 p = oyuncu.transform.position;
            List<string> notlar = new List<string>();
            if (dogusYeri.y - p.y > 15f) {
                notlar.Add("oyuncu doğduğu yerden " + (dogusYeri.y - p.y).ToString("0") + " m aşağı düştü " + p.ToString("F1"));
            }
            NavMeshHit isabet;
            if (NavMesh.SamplePosition(p, out isabet, 3f, NavMesh.AllAreas) == false) {
                notlar.Add("oyuncu yürüme ağının dışında " + p.ToString("F1"));
            }
            if (oyuncu.CharacterStats != null && oyuncu.CharacterStats.IsAlive == false) {
                // ışınlanma her haritaya açık: 1. seviye karakter zor haritada ölebilir (sorun değil, bilgi)
                sonuc.bilgi = "oyuncu öldü (" + oyuncu.CharacterStats.Level + ". seviye), yeniden doğdu";
            }
            YolDenetle(p, notlar);
            MalzemeDenetle(notlar);
            if (notlar.Count > 0) {
                sonuc.not = string.Join("; ", notlar);
            }
        }

        /// <summary>geçit taşına ve komşu haritalara açılan kapılara oyuncunun durduğu yerden yürünebiliyor mu</summary>
        private static void YolDenetle(Vector3 oyuncuYeri, List<string> notlar) {
            NavMeshHit baslangic;
            if (NavMesh.SamplePosition(oyuncuYeri, out baslangic, 3f, NavMesh.AllAreas) == false) {
                return;
            }
            Scene sahne = SceneManager.GetActiveScene();
            List<string> yolsuz = new List<string>();
            NavMeshPath yol = new NavMeshPath();
            foreach (GameObject kok in sahne.GetRootGameObjects()) {
                string ad = kok.name;
                bool gecit = ad == "GecitTasi";
                // uçurum altındaki geniş "düşenleri yakala" kutuları kapı sayılmaz
                bool kapi = ad.StartsWith("Kapi_", StringComparison.Ordinal) && kok.transform.lossyScale.x <= 12f && kok.transform.lossyScale.z <= 12f;
                if (gecit == false && kapi == false) {
                    continue;
                }
                NavMeshHit hedef;
                if (NavMesh.SamplePosition(kok.transform.position, out hedef, 6f, NavMesh.AllAreas) == false
                    || NavMesh.CalculatePath(baslangic.position, hedef.position, NavMesh.AllAreas, yol) == false
                    || yol.status != NavMeshPathStatus.PathComplete) {
                    yolsuz.Add(ad + " " + kok.transform.position.ToString("F0"));
                }
            }
            if (yolsuz.Count > 0) {
                notlar.Add("yürüyerek ulaşılamayan: " + string.Join(", ", yolsuz));
            }
        }

        /// <summary>malzemesi eksik (boş yuva) ya da gölgelendiricisi bozuk (pembe görünen) nesneler</summary>
        private static void MalzemeDenetle(List<string> notlar) {
            Scene sahne = SceneManager.GetActiveScene();
            List<string> bozuk = new List<string>();
            int adet = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (r.enabled == false || r.gameObject.scene != sahne || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) {
                    continue;
                }
                foreach (Material m in r.sharedMaterials) {
                    if (m == null || m.shader == null || m.shader.isSupported == false || m.shader.name == "Hidden/InternalErrorShader") {
                        adet++;
                        if (bozuk.Count < 5) {
                            bozuk.Add(r.name + (m == null ? " (malzeme yok)" : " (" + m.name + ")"));
                        }
                        break;
                    }
                }
            }
            if (adet > 0) {
                notlar.Add(adet + " nesnede eksik/bozuk malzeme: " + string.Join(", ", bozuk));
            }
        }

        private static string GoruntuAl(AnyRPG.SystemGameManager oyun, string ad) {
            Camera kamera = oyun != null && oyun.CameraManager != null ? oyun.CameraManager.ActiveMainCamera : null;
            if (kamera == null) {
                kamera = Camera.main;
            }
            if (kamera == null) {
                return null;
            }
            RenderTexture rt = null;
            Texture2D doku = null;
            RenderTexture eskiHedef = kamera.targetTexture;
            try {
                rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                rt.Create();
                RenderPipeline.StandardRequest istek = new RenderPipeline.StandardRequest();
                istek.destination = rt;
                if (RenderPipeline.SupportsRenderRequest(kamera, istek)) {
                    RenderPipeline.SubmitRenderRequest(kamera, istek);
                } else {
                    kamera.targetTexture = rt;
                    kamera.Render();
                }
                RenderTexture onceki = RenderTexture.active;
                RenderTexture.active = rt;
                doku = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                doku.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                doku.Apply();
                RenderTexture.active = onceki;
                File.WriteAllBytes(Path.Combine(Path.GetFullPath("tani"), ad + ".jpg"), doku.EncodeToJPG(80));
                return ad + ".jpg";
            } catch (Exception e) {
                Debug.LogWarning("[OyunTesti] görüntü alınamadı: " + e.Message);
                return null;
            } finally {
                kamera.targetTexture = eskiHedef;
                if (rt != null) {
                    rt.Release();
                    Object.Destroy(rt);
                }
                if (doku != null) {
                    Object.Destroy(doku);
                }
            }
        }

        // ---------------------------------------------------------------- bitiş

        private static void Bitir(string durum) {
            if (adim == Adim.Bitti) {
                return;
            }
            adim = Adim.Bitti;
            EditorApplication.update -= Guncelle;
            Application.logMessageReceivedThreaded -= KayitGeldi;
            SessionState.SetBool(AktifKey, false);
            try {
                if (sonuc != null && rapor.haritalar.Contains(sonuc) == false && sonuc.sonuc == "başladı") {
                    HaritaBitir("test bitince yarıda kaldı");
                }
                rapor.durum = durum;
                rapor.bitis = DateTime.UtcNow.ToString("o");
                string klasor = Path.GetFullPath("tani");
                Directory.CreateDirectory(klasor);
                lock (kilit) {
                    rapor.hatalar.Sort((a, b) => b.adet.CompareTo(a.adet));
                    File.WriteAllText(Path.Combine(klasor, "oyun_testi.json"), JsonUtility.ToJson(rapor, true));
                    File.WriteAllText(Path.Combine(klasor, "oyun_testi.txt"), Ozet());
                }
                Debug.Log("[OyunTesti] bitti: " + durum + ", " + rapor.toplamHata + " hata");
            } catch (Exception e) {
                Debug.LogError("[OyunTesti] rapor yazılamadı: " + e);
            }
            EditorApplication.Exit(0);
        }

        private static string Ozet() {
            StringBuilder sb = new StringBuilder();
            sb.Append("Otomatik oyun testi: ").Append(rapor.durum).Append('\n');
            sb.Append("Haritalar: ").Append(rapor.haritalar.Count).Append(", hata/istisna: ").Append(rapor.toplamHata)
                .Append(" (").Append(rapor.hatalar.Count).Append(" farklı)\n\n");
            foreach (HaritaSonucu h in rapor.haritalar) {
                sb.Append(h.sonuc == "tamam" ? "  ok  " : "  !!  ").Append(h.ad).Append(" (").Append(h.sahne).Append("): ").Append(h.sonuc)
                    .Append(", yükleme ").Append(h.yuklemeSuresi.ToString("0")).Append(" sn, ").Append(h.hata).Append(" hata");
                if (string.IsNullOrEmpty(h.not) == false) {
                    sb.Append(" — ").Append(h.not);
                }
                if (string.IsNullOrEmpty(h.bilgi) == false) {
                    sb.Append(" (").Append(h.bilgi).Append(')');
                }
                sb.Append('\n');
            }
            if (rapor.hatalar.Count > 0) {
                sb.Append("\nHatalar (çoktan aza):\n");
                foreach (HataKaydi k in rapor.hatalar) {
                    sb.Append("\n[").Append(k.adet).Append(" kez, ").Append(k.tur).Append(", ").Append(k.sahne).Append("] ").Append(k.mesaj).Append('\n');
                    if (string.IsNullOrEmpty(k.yigin) == false) {
                        sb.Append(k.yigin.TrimEnd()).Append('\n');
                    }
                }
            }
            return sb.ToString();
        }

        private static string IlkSatir(string metin) {
            if (string.IsNullOrEmpty(metin)) {
                return string.Empty;
            }
            int i = metin.IndexOf('\n');
            return (i >= 0 ? metin.Substring(0, i) : metin).Trim();
        }

        private static string Kes(string metin, int uzunluk) {
            if (string.IsNullOrEmpty(metin)) {
                return string.Empty;
            }
            return metin.Length <= uzunluk ? metin : metin.Substring(0, uzunluk) + "…";
        }
    }
}
