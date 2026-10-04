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
            public string okHedefi;
            public int uzaktaGizlenen;
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
            public string binekTesti;
            public string gunlukGorevler;
            public string dunyaHaritasi;
            public string ilkHaritaAvi;
            public string demirciTesti;
            public string gelisim;
        }

        private enum Adim { OyunModu, Acilis, YeniOyun, Dogus, Bekle, Dirilis, BinekTesti, DemirciTesti, Bitti }

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
                    HaritaBitir(sonuc.not == null ? "tamam" : "sorunlu");
                    if (sira == 0 && rapor.binekTesti == null) {
                        // ilk haritada (binmeye izin var) ejderha bineği denenir, sonra yolculuk sürer
                        binekAsama = 0;
                        binekZamani = EditorApplication.timeSinceStartup;
                        Gec(Adim.BinekTesti);
                        break;
                    }
                    SonrakiHarita(oyun);
                    break;

                case Adim.BinekTesti:
                    if (BinekTesti(oyun)) {
                        demirciAsama = 0;
                        Gec(Adim.DemirciTesti);
                    }
                    break;

                case Adim.DemirciTesti:
                    if (DemirciTesti(oyun)) {
                        SonrakiHarita(oyun);
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
                return true;
            } catch (Exception e) {
                rapor.demirciTesti = (demirciOzet != null ? demirciOzet + "; " : string.Empty) + "hata: " + e.Message;
                Debug.LogError("[OyunTesti] demirci denemesi: " + e);
                return true;
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
                rapor.gunlukGorevler = AnyRPG.GunlukGorevler.Ozet();
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
            sb.Append("Gelişim: ").Append(rapor.gelisim).Append("\n\n");
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
