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
        private const double ToplamSure = 52 * 60;
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
            public string okHedefi;
            public int uzaktaGizlenen;
            public string goruntu;
            public string hikaye;
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
            public string binekTesti;
            public string gunlukGorevler;
            public string dunyaHaritasi;
            public string ilkHaritaAvi;
            public string demirciTesti;
            public string cantaTesti;
            public string gelisim;
            public string hikaye;
            public List<string> savaslar = new List<string>();
            public List<string> dortKGoruntuleri = new List<string>();
        }

        private enum Adim { OyunModu, Acilis, YeniOyun, Dogus, Bekle, Dirilis, BinekTesti, DemirciTesti, Savas, Bitti }

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
            // telefon katmanının (MobileBootstrap ve pencereleri) yakaladığı istisnalar uyarı olarak yazılır: onlar da sayılır
            bool mobilUyarisi = tur == LogType.Warning && mesaj != null && (mesaj.StartsWith("MobileBootstrap", StringComparison.Ordinal)
                || mesaj.StartsWith("GorevOku", StringComparison.Ordinal) || mesaj.StartsWith("MobileHud", StringComparison.Ordinal));
            if (tur != LogType.Error && tur != LogType.Exception && tur != LogType.Assert && mobilUyarisi == false) {
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
                    tur = tur == LogType.Exception ? "istisna" : (mobilUyarisi ? "uyarı" : "hata"), mesaj = Kes(mesaj, 600), yigin = Kes(yigin, 1500), sahne = aktifSahne, adet = 1
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
                    // telefondaki katman (MobileBootstrap: günlük görevler, görev oku, ayarlar, armağan...) editörde
                    // kendiliğinden kurulmaz; test telefondaki gibi çalışsın diye kurulur
                    if (Object.FindAnyObjectByType<AnyRPG.MobileBootstrap>() == null) {
                        GameObject mobil = new GameObject("MobileBootstrap (oyun testi)");
                        mobil.AddComponent<AnyRPG.MobileBootstrap>();
                        Object.DontDestroyOnLoad(mobil);
                    }
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
                        OldurmeleriSay(oyuncular.UnitController);
                        HikayeGoreviniAl(oyun, oyuncular.UnitController);
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
                    // ilk haritada uzun av: öldürme, ganimet, tecrübe ve günlük görev sayaçları da denensin
                    // ikinci haritada (Umay Tarlaları: girişin yakınında kamplar) uzun av
                    double av = sira == UzunAvSirasi ? IlkAvSuresi : AvSuresi;
                    double kalma = sira == UzunAvSirasi ? IlkAvSuresi + 4 : HaritadaKalma;
                    if (sira == UzunAvSirasi && avAcildi && ilkTecrube < 0 && oyun != null && oyun.PlayerManagerClient.UnitController != null) {
                        ilkTecrube = oyun.PlayerManagerClient.UnitController.CharacterStats.CurrentXP;
                        ilkSeviye = oyun.PlayerManagerClient.UnitController.CharacterStats.Level;
                        ilkOldurme = oldurmeSayisi;
                    }
                    // uzun avda kamplar girişten uzakta olabilir: görev okunun "Git"i gibi en yakın düşmana yürüyüp saldır
                    if (sira == UzunAvSirasi && AdimSuresi > 2 && AdimSuresi < 2 + av && EditorApplication.timeSinceStartup >= sonrakiSaldiri) {
                        sonrakiSaldiri = EditorApplication.timeSinceStartup + 2;
                        DusmanaGit(oyun);
                    }
                    if (AdimSuresi > 2 + av && AnyRPG.OtomatikAv.Acik) {
                        AnyRPG.OtomatikAv.Kapat();
                    }
                    if (AdimSuresi < kalma) {
                        break;
                    }
                    HaritaDenetle(oyun);
                    if (sira == UzunAvSirasi && rapor.ilkHaritaAvi == null && oyun.PlayerManagerClient.UnitController != null) {
                        AnyRPG.CharacterStats st = oyun.PlayerManagerClient.UnitController.CharacterStats;
                        rapor.ilkHaritaAvi = (oldurmeSayisi - Mathf.Max(0, ilkOldurme)) + " düşman yenildi, seviye " + ilkSeviye + " → " + st.Level
                            + ", tecrübe " + Mathf.Max(0, ilkTecrube) + " → " + st.CurrentXP;
                    }
                    sonuc.okHedefi = AnyRPG.GorevOku.SonHedef;
                    sonuc.uzaktaGizlenen = AnyRPG.OyunAyarlari.KucukNesneSayisi;
                    sonuc.goruntu = GoruntuAl(oyun, "bot_" + sonuc.sahne);
                    DortKGoruntu(oyun, sonuc.sahne);
                    HaritaBitir(sonuc.not == null ? "tamam" : "sorunlu");
                    if (sira == 0 && rapor.binekTesti == null) {
                        // ilk haritada (binmeye izin var) ejderha bineği denenir, sonra yolculuk sürer
                        binekAsama = 0;
                        binekZamani = EditorApplication.timeSinceStartup;
                        Gec(Adim.BinekTesti);
                        break;
                    }
                    SavasaBasla();
                    break;

                case Adim.BinekTesti:
                    if (BinekTesti(oyun)) {
                        demirciAsama = 0;
                        Gec(Adim.DemirciTesti);
                    }
                    break;

                case Adim.DemirciTesti:
                    if (DemirciTesti(oyun)) {
                        SavasaBasla();
                    }
                    break;

                case Adim.Savas:
                    if (savasAsama < 9 && SavasTesti(oyun)) {
                        rapor.savaslar.Add(savasSatiri);
                        Debug.Log("[OyunTesti] savaş denemesi: " + savasSatiri);
                        // yolculuktan önce savaş bitsin (savaşta ışınlanılamaz): kalan düşmanlar otomatik avla temizlenir
                        savasAsama = 9;
                        savasZamani = EditorApplication.timeSinceStartup;
                        if (AnyRPG.OtomatikAv.Acik == false) {
                            AnyRPG.OtomatikAv.Degistir();
                        }
                    }
                    if (savasAsama == 9) {
                        AnyRPG.UnitController o = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
                        bool savasta = o != null && o.CharacterStats != null && o.CharacterStats.IsAlive && o.CharacterCombat != null && o.CharacterCombat.GetInCombat();
                        if (savasta == false || EditorApplication.timeSinceStartup - savasZamani > 25) {
                            AnyRPG.OtomatikAv.Kapat();
                            SonrakiHarita(oyun);
                        } else if (o != null) {
                            o.CharacterStats.SetResourceAmountsToMaximum();
                        }
                    }
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

        // ---------------------------------------------------------------- savaş denemesi (güç dengesi)

        private static int savasAsama = 0;
        private static double savasZamani = 0;
        private static double sonEmir = 0;
        private static AnyRPG.UnitController savasHedefi = null;
        private static AnyRPG.UnitController tasHedefi = null;
        private static float enAzCan = 1f;
        private static double kirilma = -1;
        private static int ganimetOnce = 0;
        private static string savasSatiri = null;
        private static int tasResmi = 0;
        private static string tasTani = null;

        /// <summary>kırılan taşın ganimet durumu: tablo sayısı, oyuncuya ganimet, etkileşim seçeneği, ganimet penceresi</summary>
        private static string TasTani(AnyRPG.SystemGameManager oyun, AnyRPG.UnitController oyuncu, AnyRPG.UnitController tas) {
            if (tas == null) {
                return "taş yok oldu";
            }
            try {
                AnyRPG.LootableCharacterComponent lc = AnyRPG.LootableCharacterComponent.GetLootableCharacterComponent(tas);
                int tablo = lc != null && lc.LootHolder != null ? lc.LootHolder.LootTableStates.Count : -1;
                int ganimet = lc != null ? lc.GetLootCount(oyuncu) : -1;
                int secenek = tas.GetCurrentInteractables(oyuncu).Count;
                bool pencere = oyun.UIManager != null && oyun.UIManager.lootWindow != null && oyun.UIManager.lootWindow.IsOpen;
                return "tablo " + tablo + ", ganimet " + ganimet + ", seçenek " + secenek + ", pencere " + (pencere ? "açık" : "kapalı")
                    + ", uzaklık " + Vector3.Distance(oyuncu.transform.position, tas.transform.position).ToString("0.0") + " m";
            } catch (Exception e) {
                return "tanı hatası: " + e.Message;
            }
        }

        /// <summary>hedefin 3 m yanından, ayrı bir kamerayla görüntü (oyun kamerası oyuncunun arkasında kalır)</summary>
        private static void YakinGoruntu(AnyRPG.SystemGameManager oyun, Transform hedef, string ad) {
            Camera ana = oyun != null && oyun.CameraManager != null ? oyun.CameraManager.ActiveMainCamera : Camera.main;
            if (ana == null || hedef == null) {
                return;
            }
            GameObject go = new GameObject("YakinKamera");
            try {
                Camera k = go.AddComponent<Camera>();
                k.CopyFrom(ana);
                k.enabled = false;
                Vector3 merkez = hedef.position + Vector3.up * 0.9f;
                Vector3 yon = (ana.transform.position - hedef.position);
                yon.y = 0f;
                if (yon.sqrMagnitude < 0.01f) {
                    yon = Vector3.back;
                }
                k.transform.position = merkez + yon.normalized * 3.2f + Vector3.up * 0.9f;
                k.transform.LookAt(merkez);
                k.fieldOfView = 50f;
                GoruntuAl(k, ad);
            } finally {
                Object.Destroy(go);
            }
        }

        private static void SavasaBasla() {
            savasAsama = 0;
            savasZamani = EditorApplication.timeSinceStartup;
            Gec(Adim.Savas);
        }

        /// <summary>
        /// Hilesiz dövüş: oyuncu haritanın en düşük seviyesine çıkarılır (başlangıç donanımıyla), canı doldurulur,
        /// en yakın canavara saldırır, otomatik av (beceriler) açık. Sonuç: yendi / öldü / bitmedi, süre, en düşük can.
        /// Sonra en yakın Ötüken Taşı kırılır: kaç saniyede kırıldı, ödülden kaç eşya alındı. Bitince true.
        /// </summary>
        private static bool SavasTesti(AnyRPG.SystemGameManager oyun) {
            AnyRPG.PlayerManagerClient pm = oyun != null ? oyun.PlayerManagerClient : null;
            AnyRPG.UnitController oyuncu = pm != null ? pm.UnitController : null;
            AnyRPG.PlayerController kontrol = pm != null ? pm.PlayerController : null;
            double simdi = EditorApplication.timeSinceStartup;
            double gecen = simdi - savasZamani;
            string sahne = SceneManager.GetActiveScene().name;
            string ad = AnyRPG.IsinlanmaPenceresi.GorunenAd(sahne);
            if (oyuncu == null || kontrol == null || oyuncu.CharacterStats == null) {
                savasSatiri = ad + ": oyuncu yok";
                return true;
            }
            AnyRPG.CharacterStats st = oyuncu.CharacterStats;
            switch (savasAsama) {
                case 0: {
                    int en, ust;
                    if (AnyRPG.HaritaSeviyeleri.Aralik(sahne, out en, out ust) == false) {
                        savasSatiri = ad + ": seviye basamağı yok";
                        return true;
                    }
                    if (st.IsAlive == false) {
                        savasSatiri = ad + ": oyuncu ölü başladı";
                        return true;
                    }
                    if (st.Level < en) {
                        st.SetLevel(en);
                    }
                    st.SetResourceAmountsToMaximum();
                    AnyRPG.OtomatikAv.Kapat();
                    savasSatiri = ad + " (" + AnyRPG.HaritaSeviyeleri.Yazi(sahne) + "), oyuncu " + st.Level + ". sv, can " + st.MaxPrimaryResource;
                    savasHedefi = EnYakinBirim(oyuncu, false, 160f);
                    if (savasHedefi == null) {
                        savasSatiri += ": yakında canavar yok";
                        savasAsama = 2;
                        return false;
                    }
                    savasSatiri += ": " + savasHedefi.DisplayName + " " + savasHedefi.CharacterStats.Level + ". sv (can " + savasHedefi.CharacterStats.MaxPrimaryResource
                        + ", " + Vector3.Distance(oyuncu.transform.position, savasHedefi.transform.position).ToString("0") + " m)";
                    AnyRPG.Denge.OlcumuSifirla(oyuncu);
                    kontrol.RightMouseInteraction(savasHedefi);
                    if (AnyRPG.OtomatikAv.Acik == false) {
                        AnyRPG.OtomatikAv.Degistir();
                    }
                    enAzCan = 1f;
                    sonEmir = simdi;
                    savasZamani = simdi;
                    savasAsama = 1;
                    return false;
                }
                case 1: {
                    enAzCan = Mathf.Min(enAzCan, st.CurrentPrimaryResource / (float)Mathf.Max(1, st.MaxPrimaryResource));
                    if (st.IsAlive == false) {
                        savasSatiri += " → ÖLDÜ (" + gecen.ToString("0") + " sn, düşmanın canı %" + CanYuzde(savasHedefi) + ")";
                        return true;
                    }
                    bool oldu = savasHedefi == null || savasHedefi.CharacterStats == null || savasHedefi.CharacterStats.IsAlive == false;
                    if (oldu) {
                        savasSatiri += " → YENDİ, " + gecen.ToString("0") + " sn, oyuncunun canı en az %" + (enAzCan * 100f).ToString("0")
                            + " [" + AnyRPG.Denge.OlcumYazisi(oyuncu) + "]";
                        savasAsama = 2;
                        return false;
                    }
                    if (gecen > 50) {
                        savasSatiri += " → 50 sn'de bitmedi (düşmanın canı %" + CanYuzde(savasHedefi) + ", oyuncunun en az %" + (enAzCan * 100f).ToString("0") + ")";
                        savasAsama = 2;
                        return false;
                    }
                    // otomatik av başka hedefe geçerse ya da durursa ilk hedefe yeniden saldır
                    if (simdi - sonEmir > 4 && (oyuncu.CharacterCombat == null || oyuncu.CharacterCombat.GetInCombat() == false)) {
                        sonEmir = simdi;
                        kontrol.RightMouseInteraction(savasHedefi);
                    }
                    return false;
                }
                case 2: {
                    AnyRPG.OtomatikAv.Kapat();
                    st.SetResourceAmountsToMaximum();
                    tasHedefi = EnYakinBirim(oyuncu, true, 220f);
                    if (tasHedefi == null) {
                        savasSatiri += " | Ötüken Taşı yok";
                        return true;
                    }
                    savasSatiri += " | Ötüken Taşı " + tasHedefi.CharacterStats.Level + ". sv (can " + tasHedefi.CharacterStats.MaxPrimaryResource + ", "
                        + Vector3.Distance(oyuncu.transform.position, tasHedefi.transform.position).ToString("0") + " m)";
                    ganimetOnce = AnyRPG.Ganimet.AlinanSayisi;
                    kirilma = -1;
                    tasTani = null;
                    tasResmi = 0;
                    kontrol.RightMouseInteraction(tasHedefi);
                    sonEmir = simdi;
                    savasZamani = simdi;
                    savasAsama = 3;
                    return false;
                }
                case 3: {
                    if (st.IsAlive == false) {
                        savasSatiri += ": oyuncu öldü";
                        return true;
                    }
                    bool kirildi = tasHedefi == null || tasHedefi.CharacterStats == null || tasHedefi.CharacterStats.IsAlive == false;
                    if (kirildi && kirilma < 0) {
                        kirilma = gecen;
                    }
                    // ilk iki haritada taşın yakın görüntüsü: vurulurken ve kırıldıktan hemen sonra (ayrı bir kamerayla)
                    if (rapor.savaslar.Count < 2 && tasHedefi != null) {
                        float uzaklik = Vector3.Distance(oyuncu.transform.position, tasHedefi.transform.position);
                        if (tasResmi == 0 && kirildi == false && uzaklik < 6f) {
                            tasResmi = 1;
                            YakinGoruntu(oyun, tasHedefi.transform, "tas_" + sahne);
                        } else if (tasResmi <= 1 && kirildi && gecen - kirilma > 0.35) {
                            tasResmi = 2;
                            YakinGoruntu(oyun, tasHedefi.transform, "tas_kirik_" + sahne);
                        }
                    }
                    if (kirildi && tasTani == null && gecen - kirilma > 0.3) {
                        tasTani = TasTani(oyun, oyuncu, tasHedefi);
                    }
                    int alinan = AnyRPG.Ganimet.AlinanSayisi - ganimetOnce;
                    if (kirildi && (alinan > 0 && gecen - kirilma > 2 || gecen - kirilma > 10)) {
                        int n = Mathf.Min(alinan, AnyRPG.Ganimet.SonAlinanlar.Count);
                        savasSatiri += ": " + kirilma.ToString("0") + " sn'de kırıldı, ödül " + alinan + " eşya"
                            + (n > 0 ? " (" + string.Join(", ", AnyRPG.Ganimet.SonAlinanlar.GetRange(AnyRPG.Ganimet.SonAlinanlar.Count - n, n)) + ")"
                                : " — ÖDÜL ALINMADI [kırılınca: " + tasTani + "; şimdi: " + TasTani(oyun, oyuncu, tasHedefi) + "]");
                        return true;
                    }
                    if (kirildi == false && gecen > 45) {
                        savasSatiri += ": 45 sn'de kırılamadı (taşın canı %" + CanYuzde(tasHedefi) + ")";
                        return true;
                    }
                    if (kirildi == false && simdi - sonEmir > 4 && (oyuncu.CharacterCombat == null || oyuncu.CharacterCombat.GetInCombat() == false)) {
                        sonEmir = simdi;
                        kontrol.RightMouseInteraction(tasHedefi);
                    }
                    return false;
                }
            }
            return true;
        }

        private static string CanYuzde(AnyRPG.UnitController u) {
            if (u == null || u.CharacterStats == null) {
                return "?";
            }
            return (100f * u.CharacterStats.CurrentPrimaryResource / Mathf.Max(1, u.CharacterStats.MaxPrimaryResource)).ToString("0");
        }

        /// <summary>en yakın canlı düşman (tas: yalnız Ötüken Taşları, değilse taş olmayanlar)</summary>
        private static AnyRPG.UnitController EnYakinBirim(AnyRPG.UnitController oyuncu, bool tas, float menzil) {
            AnyRPG.UnitController enYakin = null;
            float enAz = menzil;
            foreach (AnyRPG.UnitController birim in Object.FindObjectsByType<AnyRPG.UnitController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (birim == oyuncu || birim.CharacterStats == null || birim.CharacterStats.IsAlive == false || oyuncu.BaseCharacter == null
                    || birim.UnitControllerMode != AnyRPG.UnitControllerMode.AI
                    || AnyRPG.Faction.RelationWith(birim, oyuncu.BaseCharacter.Faction) > -1
                    || AnyRPG.OtukenTasi.TasMi(birim) != tas) {
                    continue;
                }
                float d = Vector3.Distance(birim.transform.position, oyuncu.transform.position);
                if (d < enAz) {
                    enAz = d;
                    enYakin = birim;
                }
            }
            return enYakin;
        }

        private static int binekAsama = 0;
        private static double binekZamani = 0;
        private static Vector3 binekYeri;
        private static float binekYolu = 0f;

        /// <summary>
        /// Binek denemesi: yetenek öğretilir, HUD'daki Binek düğmesinin yaptığı çağrıyla binilir, hareket çubuğuyla
        /// 2,5 saniye ileri gidilir, düğmeyle inilir. Bitince true döner; sonuç rapor.binekTesti'ne yazılır.
        /// </summary>
        private static bool BinekTesti(AnyRPG.SystemGameManager oyun) {
            AnyRPG.UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu == null) {
                rapor.binekTesti = "oyuncu yok";
                return true;
            }
            double gecen = EditorApplication.timeSinceStartup - binekZamani;
            switch (binekAsama) {
                case 0:
                    // otomatik avdan kalan savaş bitsin (savaşta binilmez)
                    if (oyuncu.CharacterCombat != null && oyuncu.CharacterCombat.GetInCombat() && gecen < 15) {
                        return false;
                    }
                    AnyRPG.Ability yetenek = oyun.SystemDataFactory.GetResource<AnyRPG.Ability>(AnyRPG.Binek.YetenekAdi);
                    if (yetenek == null) {
                        rapor.binekTesti = "binek yeteneği bulunamadı";
                        return true;
                    }
                    oyuncu.CharacterAbilityManager.LearnAbility(yetenek.AbilityProperties);
                    AnyRPG.Binek.Tick(oyun, true);
                    if (AnyRPG.Binek.Var == false) {
                        rapor.binekTesti = "binek öğrenilemedi";
                        return true;
                    }
                    AnyRPG.Binek.Degistir();
                    binekAsama = 1;
                    binekZamani = EditorApplication.timeSinceStartup;
                    return false;
                case 1:
                    if (oyuncu.IsMounted) {
                        binekYeri = oyuncu.transform.position;
                        AnyRPG.MobileInput.SetJoystick(Vector2.up, true);
                        binekAsama = 2;
                        binekZamani = EditorApplication.timeSinceStartup;
                    } else if (gecen > 8) {
                        rapor.binekTesti = "binilemedi (8 sn)";
                        return true;
                    }
                    return false;
                case 2:
                    if (gecen < 2.5) {
                        return false;
                    }
                    AnyRPG.MobileInput.SetJoystick(Vector2.zero, false);
                    binekYolu = Vector3.Distance(binekYeri, oyuncu.transform.position);
                    AnyRPG.Binek.Degistir();
                    binekAsama = 3;
                    binekZamani = EditorApplication.timeSinceStartup;
                    return false;
                default:
                    if (oyuncu.IsMounted == false) {
                        rapor.binekTesti = "binildi, 2,5 sn'de " + binekYolu.ToString("0") + " m gidildi, inildi";
                        return true;
                    }
                    if (gecen > 4) {
                        rapor.binekTesti = "binildi (" + binekYolu.ToString("0") + " m) ama inilemedi";
                        oyuncu.CancelMountEffects();
                        return true;
                    }
                    return false;
            }
        }

        private static int demirciAsama = 0;
        private static int demirciOneri = 0;
        private static int demirciOdul = 0;
        private static string demirciOzet = null;
        private static AnyRPG.InstantiatedEquipment demirciYeni = null;

        /// <summary>
        /// Demirci ve gelişim denemesi. 1) kuşanılı ilk eşyaya Gümüş Akçe verilip pencerenin yoluyla +1 (şans %100), sonra iki
        /// basamak daha (malzemesiz) basılır; Güç Puanı ve kayda giden ad raporlanır. 2) oyuncu 3. seviyeye çıkarılır
        /// (seviye ödülleri), çantaya aynı eşyanın +9'u konur ("daha iyi eşya" kartı). 3) kart gelince Kuşan'a basılır.
        /// Bitince true döner; sonuç rapor.demirciTesti'ne yazılır.
        /// </summary>
        private static bool DemirciTesti(AnyRPG.SystemGameManager oyun) {
            try {
                AnyRPG.UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
                if (oyuncu == null) {
                    rapor.demirciTesti = "oyuncu yok";
                    return true;
                }
                if (demirciAsama == 0) {
                    AnyRPG.InstantiatedEquipment esya = null;
                    foreach (AnyRPG.InstantiatedEquipment e in AnyRPG.Demirci.Esyalar(oyuncu)) {
                        if (AnyRPG.Demirci.KusaniliMi(oyuncu, e)) {
                            esya = e;
                            break;
                        }
                    }
                    if (esya == null) {
                        rapor.demirciTesti = "kuşanılı eşya yok";
                        return true;
                    }
                    string ilkAd = esya.DisplayName;
                    int ilkGuc = AnyRPG.Gelisim.GucPuani(oyuncu);
                    AnyRPG.Currency gumus = oyun.SystemDataFactory.GetResource<AnyRPG.Currency>("Silver");
                    if (gumus != null) {
                        oyuncu.CharacterCurrencyManager.AddCurrency(gumus, AnyRPG.Demirci.GumusBedeli(1) + 5);
                    }
                    bool basarili;
                    string ilkDeneme = AnyRPG.Demirci.Yukselt(oyuncu, esya, oyun, false, out basarili);
                    AnyRPG.Demirci.Yukselt(oyuncu, esya, oyun, true, out basarili);
                    AnyRPG.Demirci.Yukselt(oyuncu, esya, oyun, true, out basarili);
                    int sonGuc = AnyRPG.Gelisim.GucPuani(oyuncu);
                    string kayit = esya.GetItemSaveData().DisplayName;
                    demirciOzet = ilkAd + " → " + esya.DisplayName + " (" + AnyRPG.Demirci.KazancYazisi(esya, AnyRPG.Demirci.Seviye(esya), oyuncu.CharacterStats.Level)
                        + "); ilk deneme: " + ilkDeneme + "; Güç Puanı " + ilkGuc + " → " + sonGuc + "; kayıttaki ad: " + kayit;
                    rapor.demirciTesti = demirciOzet;

                    // seviye ödülleri: 3. seviyeye çıkar (2. ve 3. seviye ödülleri gelmeli)
                    demirciOdul = AnyRPG.Gelisim.VerilenOdulSayisi;
                    demirciOneri = AnyRPG.Gelisim.OneriSayisi;
                    AnyRPG.CharacterStats st = oyuncu.CharacterStats;
                    int gereken = 0;
                    for (int l = st.Level; l < 3; l++) {
                        gereken += AnyRPG.Gelisim.SeviyeIcinTecrube(oyun, l);
                    }
                    if (gereken > 0) {
                        st.GainExperience(Mathf.Max(1, gereken - st.CurrentXP));
                    }
                    // daha iyi eşya: aynı eşyanın +9'u çantaya
                    demirciYeni = oyuncu.CharacterInventoryManager.GetNewInstantiatedItem(esya.ResourceName) as AnyRPG.InstantiatedEquipment;
                    if (demirciYeni != null) {
                        demirciYeni.DisplayName = esya.Item.DisplayName + " +9";
                        oyuncu.CharacterInventoryManager.AddItem(demirciYeni, false);
                    }
                    demirciAsama = 1;
                    binekZamani = EditorApplication.timeSinceStartup;
                    return false;
                }
                double gecen = EditorApplication.timeSinceStartup - binekZamani;
                bool odulGeldi = AnyRPG.Gelisim.VerilenOdulSayisi - demirciOdul >= 2;
                bool oneriGeldi = AnyRPG.Gelisim.OneriSayisi > demirciOneri;
                if ((odulGeldi == false || oneriGeldi == false) && gecen < 10) {
                    return false;
                }
                string kusan = oneriGeldi ? AnyRPG.Gelisim.TestIcinKusan(oyuncu) : "kart gelmedi";
                rapor.demirciTesti = demirciOzet + "; seviye " + oyuncu.CharacterStats.Level + ", ödül " + (AnyRPG.Gelisim.VerilenOdulSayisi - demirciOdul)
                    + " (" + AnyRPG.Gelisim.SonOdul + "); çantaya +9 konunca: " + kusan + ", Güç Puanı " + AnyRPG.Gelisim.GucPuani(oyuncu);
                try {
                    rapor.cantaTesti = CantaTesti(oyun, oyuncu);
                } catch (Exception e) {
                    rapor.cantaTesti = "hata: " + e.Message;
                    Debug.LogError("[OyunTesti] çanta denemesi: " + e);
                }
                return true;
            } catch (Exception e) {
                rapor.demirciTesti = (demirciOzet != null ? demirciOzet + "; " : string.Empty) + "hata: " + e.Message;
                Debug.LogError("[OyunTesti] demirci denemesi: " + e);
                return true;
            }
        }

        private static int CantadakiEsya(AnyRPG.UnitController oyuncu) {
            int adet = 0;
            foreach (AnyRPG.InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                if (yuva != null) {
                    adet += yuva.Count;
                }
            }
            return adet;
        }

        /// <summary>
        /// Çanta denemesi: çantaya giyilen eşyanın 2 gri kopyası ve 3 Şifa İksiri konur; "Sırala" (eşya sayısı aynı mı, türler
        /// sırada mı, arada boş yuva var mı) ve "Toplu Sat" (öneriler, satılan, kazanç, gri kalan, demirci eşyası korundu mu)
        /// </summary>
        private static string CantaTesti(AnyRPG.SystemGameManager oyun, AnyRPG.UnitController oyuncu) {
            AnyRPG.CharacterInventoryManager canta = oyuncu.CharacterInventoryManager;
            AnyRPG.ItemQuality gri = oyun.SystemDataFactory.GetResource<AnyRPG.ItemQuality>(AnyRPG.Ganimet.JunkQualityName);
            string kaynak = null;
            foreach (AnyRPG.InstantiatedEquipment e in AnyRPG.Demirci.Esyalar(oyuncu)) {
                if (AnyRPG.Demirci.KusaniliMi(oyuncu, e)) {
                    kaynak = e.ResourceName;
                    break;
                }
            }
            int griEklenen = 0;
            for (int i = 0; i < 2 && kaynak != null && gri != null; i++) {
                AnyRPG.InstantiatedItem kopya = canta.GetNewInstantiatedItem(kaynak, gri);
                if (kopya != null && canta.AddItem(kopya, false)) {
                    griEklenen++;
                }
            }
            for (int i = 0; i < 3; i++) {
                AnyRPG.InstantiatedItem iksir = canta.GetNewInstantiatedItem("Health Potion");
                if (iksir != null) {
                    canta.AddItem(iksir, false);
                }
            }
            int once = CantadakiEsya(oyuncu);
            int dolu = AnyRPG.Canta.Sirala(oyuncu, false);
            int sonra = CantadakiEsya(oyuncu);
            bool sirali = true;
            bool bosluk = false;
            bool bosGoruldu = false;
            int onceki = -1;
            List<string> ilkler = new List<string>();
            foreach (AnyRPG.InventorySlot yuva in canta.InventorySlots) {
                if (yuva == null || yuva.IsEmpty) {
                    bosGoruldu = true;
                    continue;
                }
                if (bosGoruldu) {
                    bosluk = true;
                }
                int k = AnyRPG.Canta.Kategori(yuva.InstantiatedItem);
                if (k < onceki) {
                    sirali = false;
                }
                onceki = k;
                if (ilkler.Count < 4) {
                    ilkler.Add(yuva.InstantiatedItem.DisplayName + (yuva.Count > 1 ? " ×" + yuva.Count : string.Empty));
                }
            }
            List<KeyValuePair<AnyRPG.InstantiatedItem, string>> adaylar = AnyRPG.Canta.Adaylar(oyuncu, true, true, false);
            List<AnyRPG.InstantiatedItem> satilacak = new List<AnyRPG.InstantiatedItem>();
            Dictionary<string, int> nedenler = new Dictionary<string, int>();
            bool demirciKorundu = true;
            foreach (KeyValuePair<AnyRPG.InstantiatedItem, string> aday in adaylar) {
                satilacak.Add(aday.Key);
                int n;
                nedenler.TryGetValue(aday.Value, out n);
                nedenler[aday.Value] = n + 1;
                if (AnyRPG.Demirci.Seviye(aday.Key) > 0) {
                    demirciKorundu = false;
                }
            }
            List<string> nedenYazisi = new List<string>();
            foreach (KeyValuePair<string, int> n in nedenler) {
                nedenYazisi.Add(n.Value + " " + n.Key);
            }
            string kazanc;
            int satilan = AnyRPG.Canta.Sat(oyuncu, satilacak, out kazanc);
            int griKalan = AnyRPG.Ganimet.FindJunk(oyuncu).Count;
            return "sıralama: " + once + " eşya → " + sonra + ", " + dolu + " yuva, tür sırası " + (sirali ? "doğru" : "YANLIŞ")
                + (bosluk ? ", arada boş yuva VAR" : ", boşluk yok") + " (ilk: " + string.Join(", ", ilkler) + "); toplu satış: "
                + griEklenen + " gri kopya eklendi, " + adaylar.Count + " öneri (" + string.Join(", ", nedenYazisi) + "), " + satilan + " satıldı"
                + (kazanc.Length > 0 ? " +" + kazanc : string.Empty) + ", kalan gri " + griKalan + ", demirci eşyası " + (demirciKorundu ? "korundu" : "SATILDI");
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

        private const double IlkAvSuresi = 40;
        private const int UzunAvSirasi = 1;
        private static int oldurmeSayisi = 0;
        private static int ilkOldurme = -1;
        private static int ilkTecrube = -1;
        private static int ilkSeviye = 0;
        private static AnyRPG.UnitController sayilan = null;

        private static double sonrakiSaldiri = 0;

        private static void DusmanaGit(AnyRPG.SystemGameManager oyun) {
            AnyRPG.UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            AnyRPG.PlayerController kontrol = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.PlayerController : null;
            if (oyuncu == null || kontrol == null || oyuncu.CharacterStats == null || oyuncu.CharacterStats.IsAlive == false) {
                return;
            }
            // deneme: 1. seviye bot kamp içinde ölmesin, saldırdığı düşman bir vuruşta düşsün (öldürme → tecrübe →
            // günlük görev sayacı zinciri denensin)
            oyuncu.CharacterStats.SetResourceAmountsToMaximum();
            if (oyuncu.CharacterCombat != null && oyuncu.CharacterCombat.GetInCombat()) {
                AnyRPG.UnitController hedef = oyuncu.Target as AnyRPG.UnitController;
                if (hedef != null && hedef.CharacterStats != null && hedef.CharacterStats.IsAlive) {
                    hedef.CharacterStats.SetResourceAmount("Health", 1f);
                }
                return;
            }
            AnyRPG.UnitController enYakin = null;
            float enAz = float.MaxValue;
            foreach (AnyRPG.UnitController birim in Object.FindObjectsByType<AnyRPG.UnitController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (birim == oyuncu || birim.CharacterStats == null || birim.CharacterStats.IsAlive == false || oyuncu.BaseCharacter == null
                    || AnyRPG.Faction.RelationWith(birim, oyuncu.BaseCharacter.Faction) > -1) {
                    continue;
                }
                float d = Vector3.Distance(birim.transform.position, oyuncu.transform.position);
                if (d < enAz) {
                    enAz = d;
                    enYakin = birim;
                }
            }
            if (enYakin != null) {
                Debug.Log("[OyunTesti] en yakın düşmana gidiliyor: " + enYakin.DisplayName + " (" + enAz.ToString("0") + " m)");
                kontrol.RightMouseInteraction(enYakin);
            }
        }

        private static void OldurmeleriSay(AnyRPG.UnitController oyuncu) {
            if (oyuncu == sayilan || oyuncu == null) {
                return;
            }
            if (sayilan != null && sayilan.UnitEventController != null) {
                sayilan.UnitEventController.OnKillEvent -= OldurmeOldu;
            }
            sayilan = oyuncu;
            sayilan.UnitEventController.OnKillEvent += OldurmeOldu;
        }

        private static void OldurmeOldu(AnyRPG.UnitController olduren, AnyRPG.UnitController olen, float pay) {
            if (pay > 0f) {
                oldurmeSayisi++;
            }
        }

        private static void Isinla(AnyRPG.SystemGameManager oyun) {
            string sahne = sahneler[sira];
            HaritaBasla(sahne);
            string engel;
            if (sira == 1) {
                // ilk yolculuk (binek denemesinde savaş bitti) dünya haritası üzerinden: pencere kurulur, diyar seçilir, Işınlan'a basılır
                engel = AnyRPG.DunyaHaritasi.TestIcinIsinla(sahne);
                rapor.dunyaHaritasi = AnyRPG.DunyaHaritasi.ResimliBolgeSayisi + "/" + sahneler.Length + " diyar görüntülü; harita üzerinden ışınlanma: "
                    + (engel == null ? "oldu" : "olmadı (" + engel + ")");
                if (engel != null) {
                    engel = AnyRPG.IsinlanmaPenceresi.Teleport(sahne, true);
                }
            } else {
                engel = AnyRPG.IsinlanmaPenceresi.Teleport(sahne, true);
            }
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
            HikayeDenetle(p, notlar);
            if (notlar.Count > 0) {
                sonuc.not = string.Join("; ", notlar);
            }
        }

        private const string IlkHikayeGorevi = "Destan 01 KutunCagrisi";

        /// <summary>ilk haritada ana hikâyenin ilk görevi alınır (Olcayto Han'dan almak gibi); sayaçları yolculuk boyunca dolar</summary>
        private static void HikayeGoreviniAl(AnyRPG.SystemGameManager oyun, AnyRPG.UnitController oyuncu) {
            try {
                AnyRPG.Quest gorev = oyun.SystemDataFactory.GetResource<AnyRPG.Quest>(IlkHikayeGorevi);
                if (gorev != null && oyuncu.CharacterQuestLog.HasQuest(IlkHikayeGorevi) == false && gorev.TurnedIn(oyuncu) == false) {
                    oyuncu.CharacterQuestLog.AcceptQuest(gorev);
                    Debug.Log("[OyunTesti] ana hikâyenin ilk görevi alındı: " + gorev.DisplayName);
                }
            } catch (Exception e) {
                Debug.LogWarning("[OyunTesti] hikâye görevi alınamadı: " + e.Message);
            }
        }

        private static string HikayeIlerlemesi(AnyRPG.SystemGameManager oyun) {
            AnyRPG.UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            AnyRPG.Quest gorev = oyun != null ? oyun.SystemDataFactory.GetResource<AnyRPG.Quest>(IlkHikayeGorevi) : null;
            if (oyuncu == null || gorev == null) {
                return "ilk görev denenemedi";
            }
            if (oyuncu.CharacterQuestLog.HasQuest(IlkHikayeGorevi) == false) {
                return "ilk görev günlükte değil";
            }
            List<string> hedefler = new List<string>();
            foreach (AnyRPG.QuestStep adim_ in gorev.Steps) {
                foreach (AnyRPG.QuestObjective h in adim_.QuestObjectives) {
                    hedefler.Add(h.DisplayName + " " + h.CurrentAmount(oyuncu) + "/" + h.Amount);
                }
            }
            return gorev.DisplayName + ": " + string.Join(", ", hedefler) + (gorev.IsComplete(oyuncu) ? " (TAMAM)" : string.Empty);
        }

        /// <summary>ana hikâyenin görevleri yüklendi mi (her birinin hedefi var mı), yardımcı ve boss profilleri var mı</summary>
        private static string HikayeOzeti(AnyRPG.SystemGameManager oyun) {
            if (oyun == null || oyun.SystemDataFactory == null) {
                return "oyun yok";
            }
            try {
                int gorev = 0;
                int bos = 0;
                string ilk = null;
                foreach (AnyRPG.Quest g in oyun.SystemDataFactory.GetResourceList<AnyRPG.Quest>()) {
                    if (g == null || g.ResourceName == null || g.ResourceName.StartsWith("Destan ", StringComparison.Ordinal) == false) {
                        continue;
                    }
                    gorev++;
                    int hedef = 0;
                    foreach (AnyRPG.QuestStep adim_ in g.Steps) {
                        hedef += adim_.QuestObjectives.Count;
                    }
                    if (hedef == 0) {
                        bos++;
                    }
                    if (g.ResourceName.StartsWith("Destan 01 ", StringComparison.Ordinal)) {
                        ilk = g.DisplayName + " (" + hedef + " hedef)";
                    }
                }
                int yardimci = 0;
                int boss = 0;
                int eksik = 0;
                foreach (string[] hk in Otuken.EditorAraclari.HikayeVerisi.Haritalar.Values) {
                    for (int k = 0; k < hk.Length; k++) {
                        if (hk[k].Length == 0) {
                            continue;
                        }
                        if (oyun.SystemDataFactory.GetResource<AnyRPG.UnitProfile>(hk[k]) == null) {
                            eksik++;
                        } else if (k == 0) {
                            yardimci++;
                        } else {
                            boss++;
                        }
                    }
                }
                return gorev + " görev yüklü" + (bos > 0 ? " (" + bos + " görevin hedefi YOK)" : string.Empty)
                    + ", ilk: " + (ilk ?? "YOK") + "; " + yardimci + " yardımcı, " + boss + " yeni boss profili"
                    + (eksik > 0 ? ", " + eksik + " profil EKSİK" : string.Empty)
                    + "; yolculukta " + HikayeIlerlemesi(oyun);
            } catch (Exception e) {
                return "denetlenemedi: " + e.Message;
            }
        }

        /// <summary>ana hikâyenin bu haritadaki yardımcısı ve boss'u doğdu mu, yürüyerek ulaşılıyor mu (HikayeVerisi)</summary>
        private static void HikayeDenetle(Vector3 oyuncuYeri, List<string> notlar) {
            string[] hk;
            if (Otuken.EditorAraclari.HikayeVerisi.Haritalar.TryGetValue(sonuc.sahne, out hk) == false) {
                return;
            }
            NavMeshHit baslangic;
            bool yolVar = NavMesh.SamplePosition(oyuncuYeri, out baslangic, 3f, NavMesh.AllAreas);
            NavMeshPath yol = new NavMeshPath();
            List<string> parcalar = new List<string>();
            AnyRPG.UnitController[] birimler = Object.FindObjectsByType<AnyRPG.UnitController>(FindObjectsSortMode.None);
            for (int k = 0; k < hk.Length; k++) {
                string profil = hk[k];
                if (profil.Length == 0) {
                    continue;
                }
                AnyRPG.UnitController bulunan = null;
                foreach (AnyRPG.UnitController u in birimler) {
                    if (u != null && u.UnitProfile != null && u.UnitProfile.ResourceName == profil) {
                        bulunan = u;
                        break;
                    }
                }
                if (bulunan == null) {
                    notlar.Add("hikâye " + (k == 0 ? "yardımcısı" : "boss'u") + " doğmadı: " + profil);
                    continue;
                }
                bool ulasilir = yolVar && NoktayaUlasilir(baslangic.position, bulunan.transform.position, yol);
                parcalar.Add((k == 0 ? "yardımcı " : "boss ") + bulunan.DisplayName + " "
                    + Vector3.Distance(oyuncuYeri, bulunan.transform.position).ToString("0") + " m"
                    + (k == 1 && bulunan.CharacterStats != null ? ", " + bulunan.CharacterStats.Level + ". sv" : string.Empty)
                    + (ulasilir ? string.Empty : " (YÜRÜYEREK ULAŞILAMIYOR)"));
                if (ulasilir == false) {
                    notlar.Add("hikâye birimine yürüyerek ulaşılamıyor: " + profil);
                }
            }
            sonuc.hikaye = string.Join(", ", parcalar);
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
                BoxCollider kutu = kapi ? kok.GetComponent<BoxCollider>() : null;
                bool ulasildi = kutu != null ? KutuyaUlasilir(baslangic.position, kutu, yol) : NoktayaUlasilir(baslangic.position, kok.transform.position, yol);
                if (ulasildi == false) {
                    yolsuz.Add(ad + " " + kok.transform.position.ToString("F0"));
                }
            }
            if (yolsuz.Count > 0) {
                notlar.Add("yürüyerek ulaşılamayan: " + string.Join(", ", yolsuz));
            }
        }

        private static bool NoktayaUlasilir(Vector3 baslangic, Vector3 hedefYeri, NavMeshPath yol) {
            NavMeshHit hedef;
            return NavMesh.SamplePosition(hedefYeri, out hedef, 6f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(baslangic, hedef.position, NavMesh.AllAreas, yol) && yol.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>kapı tetiğinin çevresinde, oyuncunun gövdesi tetiğe değecek kadar yakın ve oyuncunun
        /// yerinden yürünerek gidilebilen bir yürüme ağı noktası var mı</summary>
        internal static bool KutuyaUlasilir(Vector3 baslangic, BoxCollider kutu, NavMeshPath yol) {
            Bounds b = kutu.bounds;
            const int adim = 6;
            for (int ix = 0; ix <= adim; ix++) {
                for (int iz = 0; iz <= adim; iz++) {
                    Vector3 nokta = new Vector3(Mathf.Lerp(b.min.x - 2f, b.max.x + 2f, ix / (float)adim), b.center.y,
                        Mathf.Lerp(b.min.z - 2f, b.max.z + 2f, iz / (float)adim));
                    NavMeshHit hedef;
                    if (NavMesh.SamplePosition(nokta, out hedef, b.extents.y + 3f, NavMesh.AllAreas) == false) {
                        continue;
                    }
                    Vector3 govde = hedef.position + Vector3.up * 0.9f;
                    if (Vector3.Distance(kutu.ClosestPoint(govde), govde) > 0.8f) {
                        continue;
                    }
                    if (NavMesh.CalculatePath(baslangic, hedef.position, NavMesh.AllAreas, yol) && yol.status == NavMeshPathStatus.PathComplete) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>malzemesi eksik (boş yuva) ya da gölgelendiricisi bozuk (pembe görünen) nesneler</summary>
        private static void MalzemeDenetle(List<string> notlar) {
            Scene sahne = SceneManager.GetActiveScene();
            List<string> bozuk = new List<string>();
            int adet = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (r.enabled == false || r.gameObject.scene != sahne || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is BillboardRenderer) {
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
            return GoruntuAl(kamera, ad);
        }

        // 4K görüntü alınan haritalar (her biri ~3 MB; hepsi alınsa tanı arşivi şişer)
        private static readonly HashSet<string> dortKHaritalari = new HashSet<string>() {
            "FeaturesDemoZone", "UmayTarlalari", "UlukayinOrmani", "KafDagiYolu", "ErgenekonMagarasi", "TamuZindani"
        };

        /// <summary>
        /// oyuncunun gördüğünün 3840x2160 görüntüsü, grafik kalitesi 4K iken (ışıltı, renk katmanı, SMAA, Ultra gölgeler);
        /// ilk haritada karşılaştırma için Yüksek kalitede de alınır. Sonra kalite eski hâline döner.
        /// </summary>
        private static void DortKGoruntu(AnyRPG.SystemGameManager oyun, string sahne) {
            if (dortKHaritalari.Contains(sahne) == false) {
                return;
            }
            Camera kamera = oyun != null && oyun.CameraManager != null ? oyun.CameraManager.ActiveMainCamera : Camera.main;
            if (kamera == null) {
                return;
            }
            int eskiKalite = QualitySettings.GetQualityLevel();
            try {
                if (sahne == "FeaturesDemoZone") {
                    GoruntuAl(kamera, "yuksek_" + sahne, 3840, 2160, 90);
                }
                QualitySettings.SetQualityLevel(AnyRPG.OyunAyarlari.DortKSeviyesi, true);
                AnyRPG.OyunAyarlari.UygulaDortK(kamera);
                UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp =
                    UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                float eskiOlcek = urp != null ? urp.renderScale : 1f;
                if (urp != null) {
                    // görüntünün kendisi 4K: ayrıca büyütülmesin
                    urp.renderScale = 1f;
                }
                string dosya = GoruntuAl(kamera, "4k_" + sahne, 3840, 2160, 90);
                if (urp != null) {
                    urp.renderScale = eskiOlcek;
                }
                if (dosya != null) {
                    rapor.dortKGoruntuleri.Add(dosya);
                }
            } catch (Exception e) {
                Debug.LogWarning("[OyunTesti] 4K görüntü alınamadı: " + e.Message);
            } finally {
                QualitySettings.SetQualityLevel(eskiKalite, true);
                AnyRPG.OyunAyarlari.UygulaDortK(kamera);
            }
        }

        private static string GoruntuAl(Camera kamera, string ad, int genislik = 960, int yukseklik = 540, int jpgKalitesi = 80) {
            if (kamera == null) {
                return null;
            }
            RenderTexture rt = null;
            Texture2D doku = null;
            RenderTexture eskiHedef = kamera.targetTexture;
            try {
                rt = new RenderTexture(genislik, yukseklik, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
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
                File.WriteAllBytes(Path.Combine(Path.GetFullPath("tani"), ad + ".jpg"), doku.EncodeToJPG(jpgKalitesi));
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
                rapor.gunlukGorevler = AnyRPG.GunlukGorevler.Ozet();
                rapor.hikaye = HikayeOzeti(Object.FindAnyObjectByType<AnyRPG.SystemGameManager>());
                AnyRPG.UnitController sonOyuncu = Object.FindAnyObjectByType<AnyRPG.SystemGameManager>()?.PlayerManagerClient?.UnitController;
                rapor.gelisim = "Güç Puanı " + AnyRPG.Gelisim.GucPuani(sonOyuncu) + ", seviye ödülü " + AnyRPG.Gelisim.VerilenOdulSayisi
                    + (AnyRPG.Gelisim.SonOdul.Length > 0 ? " (son: " + AnyRPG.Gelisim.SonOdul + ")" : string.Empty)
                    + ", daha iyi eşya önerisi " + AnyRPG.Gelisim.OneriSayisi + ", oto av becerisi " + AnyRPG.OtomatikAv.BeceriSayisi + " kez";
                if (rapor.demirciTesti == null) {
                    rapor.demirciTesti = "yapılmadı";
                }
                if (rapor.binekTesti == null) {
                    rapor.binekTesti = "yapılmadı";
                }
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
                .Append(" (").Append(rapor.hatalar.Count).Append(" farklı)\n");
            sb.Append("Binek denemesi: ").Append(rapor.binekTesti).Append('\n');
            sb.Append("Günlük görevler: ").Append(rapor.gunlukGorevler).Append('\n');
            sb.Append("Umay Tarlaları'nda 40 sn av: ").Append(rapor.ilkHaritaAvi).Append('\n');
            sb.Append("Dünya haritası: ").Append(rapor.dunyaHaritasi).Append('\n');
            sb.Append("Demirci: ").Append(rapor.demirciTesti).Append('\n');
            sb.Append("Çanta: ").Append(rapor.cantaTesti).Append('\n');
            sb.Append("Gelişim: ").Append(rapor.gelisim).Append("\n");
            sb.Append("Ana hikâye: ").Append(rapor.hikaye).Append("\n");
            sb.Append("4K görüntüler (3840x2160, kalite 4K): ").Append(rapor.dortKGoruntuleri.Count > 0 ? string.Join(", ", rapor.dortKGoruntuleri) : "yok").Append('\n');
            sb.Append("Savaş denemesi (her haritada o haritanın en düşük seviyesinde, hilesiz):\n");
            foreach (string satir in rapor.savaslar) {
                sb.Append("  - ").Append(satir).Append('\n');
            }
            sb.Append('\n');
            foreach (HaritaSonucu h in rapor.haritalar) {
                sb.Append(h.sonuc == "tamam" ? "  ok  " : "  !!  ").Append(h.ad).Append(" (").Append(h.sahne).Append("): ").Append(h.sonuc)
                    .Append(", yükleme ").Append(h.yuklemeSuresi.ToString("0")).Append(" sn, ").Append(h.hata).Append(" hata");
                if (string.IsNullOrEmpty(h.not) == false) {
                    sb.Append(" — ").Append(h.not);
                }
                if (h.uzaktaGizlenen > 0) {
                    sb.Append(" [uzakta gizlenen küçük nesne: ").Append(h.uzaktaGizlenen).Append(']');
                }
                if (string.IsNullOrEmpty(h.okHedefi) == false) {
                    sb.Append(" [ok: ").Append(h.okHedefi).Append(']');
                }
                if (string.IsNullOrEmpty(h.hikaye) == false) {
                    sb.Append(" [hikâye: ").Append(h.hikaye).Append(']');
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
