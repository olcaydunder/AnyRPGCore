using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Çevrimiçi oyunun otomatik denemesi için istemci botu. Linux derlemesi "-istemciBotu Ad" ile açılınca:
    /// sunucuya (-adres, varsayılan 127.0.0.1:7770) Ad adlı hesapla girer (yoksa hesap açılır), karakteri yoksa
    /// oluşturur, dünyaya girer, otomatik avı açar ve -sure saniye boyunca (varsayılan 60) beş saniyede bir
    /// gördüklerini yazar: öteki oyuncular, sunucunun doğurduğu NPC/düşmanlar, seviye ve tecrübe.
    /// Sonunda -rapor dosyasına (varsayılan ag_botu_Ad.txt) özet yazar ve oyunu kapatır. Workflow sunucuyu ve iki
    /// botu aynı makinede çalıştırır: iki bot birbirini görürse çevrimiçi oyun çalışıyor demektir.
    /// </summary>
    public class AgBotu : MonoBehaviour {

        private static string Arguman(string ad) {
            string[] a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, ad);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        public static bool Etkin {
            get { return Arguman("-istemciBotu") != null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Kur() {
            if (Etkin == false || Application.isEditor) {
                return;
            }
            Application.targetFrameRate = 30;
            AudioListener.volume = 0f;
            GameObject go = new GameObject("AgBotu");
            DontDestroyOnLoad(go);
            go.AddComponent<AgBotu>();
        }

        private readonly StringBuilder rapor = new StringBuilder();
        private string ad = "Bot";
        private string raporYolu = null;
        private float baslangic = 0f;

        // telefonda hata penceresi açacak kayıtlar (Error/Exception); sanal ekranın (xvfb) kendi uyarıları sayılmaz
        private int hataSayisi = 0;
        private readonly List<string> hatalar = new List<string>();

        private void OnEnable() {
            Application.logMessageReceived += KayitGeldi;
        }

        private void OnDisable() {
            Application.logMessageReceived -= KayitGeldi;
        }

        private void KayitGeldi(string mesaj, string yigin, LogType tur) {
            if (tur != LogType.Error && tur != LogType.Exception && tur != LogType.Assert) {
                return;
            }
            if (mesaj == null || mesaj.StartsWith("Screen position out of view frustum")) {
                return;
            }
            hataSayisi++;
            string kisa = mesaj.Length > 160 ? mesaj.Substring(0, 160) : mesaj;
            // ağ paketinde istisna: kendi kodumuzdaki ilk iki çerçeve de yazılsın
            if (mesaj.IndexOf("while parsing data", StringComparison.Ordinal) >= 0) {
                List<string> cerceveler = new List<string>();
                foreach (string satir in mesaj.Split('\n')) {
                    string t = satir.Trim();
                    if (t.StartsWith("at AnyRPG.", StringComparison.Ordinal) && cerceveler.Count < 2) {
                        cerceveler.Add(t.Length > 140 ? t.Substring(0, 140) : t);
                    }
                }
                if (cerceveler.Count > 0) {
                    kisa += " @ " + string.Join(" < ", cerceveler);
                }
            }
            if (hatalar.Count < 3 && hatalar.Contains(kisa) == false) {
                hatalar.Add(kisa);
            }
        }

        private string HataOzeti() {
            return hataSayisi == 0 ? "hata yok" : hataSayisi + " HATA (" + string.Join(" | ", hatalar) + ")";
        }

        private void Not(string satir) {
            string s = "[" + (Time.realtimeSinceStartup - baslangic).ToString("0") + " sn] " + satir;
            rapor.AppendLine(s);
            Debug.Log("[AgBotu] " + s);
            try {
                File.WriteAllText(raporYolu, rapor.ToString());
            } catch (Exception) {
            }
        }

        private string ticaretOzeti = "-";
        private string kusanmaOzeti = "-";

        private static InstantiatedEquipment CantadaSilah(UnitController ben, bool uygun) {
            foreach (InventorySlot yuva in ben.CharacterInventoryManager.InventorySlots) {
                if (yuva == null || yuva.IsEmpty) {
                    continue;
                }
                InstantiatedEquipment e = yuva.InstantiatedItem as InstantiatedEquipment;
                Weapon w = e != null ? e.Equipment as Weapon : null;
                if (w != null && w.RequireWeaponSkill && SinifUygunlugu.Uygun(w, ben) == uygun) {
                    return e;
                }
            }
            return null;
        }

        private static bool Kusanili(UnitController ben, long kimlik) {
            foreach (EquipmentInventorySlot y in ben.CharacterEquipmentManager.CurrentEquipment.Values) {
                if (y != null && y.InstantiatedEquipment != null && y.InstantiatedEquipment.InstanceId == kimlik) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Kuşanma: sınıfa uygun silah çantaya gelir, çanta sıralanır, eşya kimliğiyle kuşanılır (eskiden yuva numarası
        /// gönderildiği için sıralamadan sonra kuşanma boşa çıkıyordu); başka sınıfın silahı reddedilmeli.
        /// </summary>
        private IEnumerator KusanmaDenemesi(SystemGameManager oyun) {
            UnitController ben = oyun.PlayerManagerClient.UnitController;
            List<string> ozet = new List<string>();
            Odeme.TestIcinEsya("@silah");
            Odeme.TestIcinEsya("@yabanci");
            yield return new WaitForSecondsRealtime(2.5f);
            InstantiatedEquipment uygun = CantadaSilah(ben, true);
            InstantiatedEquipment yabanci = CantadaSilah(ben, false);
            Canta.SiralaDugmesi();
            yield return new WaitForSecondsRealtime(2.5f);
            if (uygun == null) {
                ozet.Add("uygun silah gelmedi");
            } else {
                ben.CharacterEquipmentManager.RequestEquip(uygun);
                yield return new WaitForSecondsRealtime(2.5f);
                ozet.Add("uygun " + uygun.DisplayName + (Kusanili(ben, uygun.InstanceId) ? " kuşanıldı" : " KUŞANILAMADI"));
            }
            if (yabanci == null) {
                ozet.Add("yabancı silah gelmedi");
            } else {
                ben.CharacterEquipmentManager.RequestEquip(yabanci);
                yield return new WaitForSecondsRealtime(2.5f);
                ozet.Add("başka sınıfın " + yabanci.DisplayName + (Kusanili(ben, yabanci.InstanceId) ? " YANLIŞLIKLA KUŞANILDI" : " reddedildi"));
            }
            kusanmaOzeti = string.Join(", ", ozet);
        }

        private UnitController YakindakiOyuncu(SystemGameManager oyun, float mesafe) {
            UnitController ben = oyun.PlayerManagerClient.UnitController;
            UnitController en = null;
            float enYakin = mesafe;
            foreach (UnitController u in FindObjectsByType<UnitController>(FindObjectsSortMode.None)) {
                if (u != null && u != ben && u.UnitControllerMode == UnitControllerMode.Player && ben != null) {
                    float m = Vector3.Distance(u.transform.position, ben.transform.position);
                    if (m <= enYakin) {
                        enYakin = m;
                        en = u;
                    }
                }
            }
            return en;
        }

        /// <summary>
        /// Adı 1 ile biten bot takası başlatır ve öbürünün pazarından alır; öbürü takası kabul eder, pazar açar.
        /// Kut: deneme sunucusunda (-testOdeme) imzasız deneme satın alması, sonra Kut Dükkânı'ndan kutsama.
        /// </summary>
        private IEnumerator TicaretDenemesi(SystemGameManager oyun) {
            List<string> ozet = new List<string>();
            // Kut
            int kutOnce = KutDukkani.KutMiktari(oyun.PlayerManagerClient.UnitController);
            Odeme.TestIcinSatinAl("kut_550");
            yield return new WaitForSecondsRealtime(3f);
            int kutSonra = KutDukkani.KutMiktari(oyun.PlayerManagerClient.UnitController);
            ozet.Add("Kut " + kutOnce + " → " + kutSonra + " (" + Odeme.SonSonuc + ")");
            if (kutSonra >= 40) {
                KutDukkani.TestIcinUrunAl("kutsama1");
                yield return new WaitForSecondsRealtime(2.5f);
                ozet.Add("dükkân: " + KutDukkani.SonSonuc + ", kutsama " + KutDukkani.IstemciKutsama);
            }

            bool baslatan = ad.EndsWith("1");
            // deneme sunucusunda takas ve pazar için çantaya eşya (canlı sunucuda bir şey olmaz)
            Odeme.TestIcinEsya("Health Potion");
            Odeme.TestIcinEsya("Kurt Disi");
            yield return new WaitForSecondsRealtime(1.5f);
            float t0 = Time.realtimeSinceStartup;
            if (baslatan) {
                // takas: yakındaki öbür bota teklif, eşya koy, onayla
                UnitController karsi = null;
                while (karsi == null && Time.realtimeSinceStartup - t0 < 25f) {
                    karsi = YakindakiOyuncu(oyun, Ticaret.TakasMesafesi - 1f);
                    if (karsi == null) {
                        yield return new WaitForSecondsRealtime(1f);
                    }
                }
                if (karsi == null) {
                    ozet.Add("takas: yakında oyuncu yok");
                } else {
                    Takas.Iste(karsi.DisplayName);
                    float t1 = Time.realtimeSinceStartup;
                    while (Takas.AcikMi == false && Time.realtimeSinceStartup - t1 < 12f) {
                        yield return new WaitForSecondsRealtime(0.5f);
                    }
                    if (Takas.AcikMi) {
                        bool kondu = Takas.TestIcinEsyaKoy();
                        yield return new WaitForSecondsRealtime(2f);
                        float t2 = Time.realtimeSinceStartup;
                        while (Takas.AcikMi && Time.realtimeSinceStartup - t2 < 15f) {
                            Takas.TestIcinOnayla();
                            yield return new WaitForSecondsRealtime(2f);
                        }
                        ozet.Add("takas " + karsi.DisplayName + " ile: " + (kondu ? "eşya kondu, " : "koyacak eşya yok, ") + Takas.TamamlananSayisi + " tamam (" + Takas.SonBilgi + ")");
                    } else {
                        ozet.Add("takas açılmadı (" + Takas.SonBilgi + ")");
                    }
                }
                // pazar: öbür botun pazarından ilk malı al
                float t3 = Time.realtimeSinceStartup;
                string sahip = null;
                while (sahip == null && Time.realtimeSinceStartup - t3 < 25f) {
                    Pazar.ListeIste();
                    yield return new WaitForSecondsRealtime(1.5f);
                    foreach (Pazar.PazarBilgisi b in Pazar.Pazarlar) {
                        if (b.sahip != oyun.PlayerManagerClient.UnitController.DisplayName) {
                            sahip = b.sahip;
                        }
                    }
                }
                if (sahip == null) {
                    ozet.Add("pazar bulunamadı");
                } else {
                    Pazar.Bak(sahip);
                    yield return new WaitForSecondsRealtime(2f);
                    long mal = Pazar.TestIcinIlkMal;
                    if (mal >= 0) {
                        Pazar.TestIcinAl(sahip, mal);
                        yield return new WaitForSecondsRealtime(2.5f);
                    }
                    ozet.Add("pazar " + sahip + ": " + Pazar.AlinanSayisi + " alındı (" + Pazar.SonSonuc + ")");
                }
            } else {
                // takası kabul eden: karşı taraf eşya koyunca onayla (teklif bu bot hazırlanırken de gelmiş olabilir)
                while (Takas.AcikMi == false && Time.realtimeSinceStartup - t0 < 40f) {
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                float t2 = Time.realtimeSinceStartup;
                while (Takas.AcikMi && Time.realtimeSinceStartup - t2 < 20f) {
                    yield return new WaitForSecondsRealtime(2.5f);
                    Takas.TestIcinOnayla();
                }
                ozet.Add("takas " + Takas.GelenDavet + " davet, " + Takas.TamamlananSayisi + " tamam (" + Takas.SonBilgi + ")");
                // pazar aç ve 25 sn kıpırdama
                bool acildi = Pazar.TestIcinAc(1);
                float t3 = Time.realtimeSinceStartup;
                while (acildi && Time.realtimeSinceStartup - t3 < 25f) {
                    yield return new WaitForSecondsRealtime(1f);
                    if (Pazar.BenimAcik == false && Time.realtimeSinceStartup - t3 > 4f) {
                        break;
                    }
                }
                ozet.Add("pazarım: " + (acildi ? Pazar.SonSonuc : "satacak eşya yok") + (Pazar.BenimAcik ? " (açık kaldı)" : string.Empty));
                if (Pazar.BenimAcik) {
                    Pazar.KendiPazariniKapat();
                }
            }
            ticaretOzeti = string.Join("; ", ozet);
        }

        /// <summary>
        /// telefonda kolaylıkları MobileBootstrap saniyede bir yürütür; o yalnız telefonda kurulur. Linux botunda
        /// aynı işleri bot yürütür ki armağan penceresi, günlük görev isteği, seviye ödülü kartları telefondaki gibi denensin.
        /// </summary>
        private IEnumerator Kolayliklar() {
            while (true) {
                yield return new WaitForSecondsRealtime(1f);
                SystemGameManager oyun = OtukenAg.Oyun;
                bool oyunda = oyun != null && SystemGameManager.IsShuttingDown == false && oyun.PlayerManagerClient != null
                    && oyun.PlayerManagerClient.PlayerUnitSpawned && oyun.PlayerManagerClient.UnitController != null;
                Dene("GunlukArmagan", () => GunlukArmagan.Tick(oyun, oyunda));
                Dene("GunlukGorevler", () => GunlukGorevler.Tick(oyun, oyunda));
                Dene("Binek", () => Binek.Tick(oyun, oyunda));
                Dene("CevrimdisiKazanc", () => CevrimdisiKazanc.Tick(oyun, oyunda));
                Dene("Gelisim", () => Gelisim.Tick(oyun, oyunda));
                Dene("Pazar", () => Pazar.Tick(oyun, oyunda));
                if (oyunda) {
                    Dene("YerdekiGanimet", () => YerdekiGanimet.IstemciTick(oyun));
                    Dene("Etkinlikler", () => Etkinlikler.Tick(oyun));
                }
            }
        }

        private static void Dene(string ad, Action is_) {
            try {
                is_();
            } catch (Exception e) {
                Debug.LogWarning("[AgBotu] " + ad + ".Tick(): " + e.Message);
            }
        }

        private IEnumerator Start() {
            if (Application.isMobilePlatform == false) {
                StartCoroutine(Kolayliklar());
            }
            // öbür botun takas teklifi ne zaman gelirse gelsin kabul edilir
            Takas.OtoKabul = true;
            baslangic = Time.realtimeSinceStartup;
            ad = Arguman("-istemciBotu");
            string adres = Arguman("-adres") ?? "127.0.0.1:" + Sunucu.VarsayilanPort;
            raporYolu = Arguman("-rapor") ?? Path.Combine(Directory.GetCurrentDirectory(), "ag_botu_" + ad + ".txt");
            float sure = 60f;
            float.TryParse(Arguman("-sure") ?? "60", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out sure);
            Not("bot " + ad + " başladı, sunucu " + adres + ", istemci sürümü " + Application.version);

            SystemGameManager oyun = null;
            while (true) {
                oyun = FindAnyObjectByType<SystemGameManager>();
                if (oyun != null && oyun.LevelManagerClient != null && oyun.LevelManagerClient.IsMainMenu() && oyun.NetworkManagerClient != null) {
                    break;
                }
                if (Time.realtimeSinceStartup - baslangic > 180f) {
                    Bitir("SONUÇ: ana menü açılmadı");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return new WaitForSecondsRealtime(3f);

            // giriş (hesap yoksa sunucu açar)
            bool istendi = false;
            try {
                istendi = oyun.NetworkManagerClient.Login(ad, "deneme-" + ad, adres);
            } catch (Exception e) {
                Not("giriş hatası: " + e.Message);
            }
            Not("giriş isteği: " + istendi);
            // giriş olmazsa nedenini ayır: sunucuya hiç ulaşılamadı mı, sürüm mü tutmadı, hesap mı reddedildi
            string sunucuSurumu = null;
            Action<string> surumDinle = v => sunucuSurumu = v ?? "?";
            oyun.NetworkManagerClient.OnClientVersionFailure += surumDinle;
            bool baglandi = false;
            bool reddedildi = false;
            float t = Time.realtimeSinceStartup;
            while (oyun.NetworkManagerClient.AccountId <= 0 && Time.realtimeSinceStartup - t < 60f && sunucuSurumu == null && reddedildi == false) {
                global::FishNet.Managing.Client.ClientManager istemci = global::FishNet.InstanceFinder.ClientManager;
                if (istemci != null && istemci.Started && baglandi == false) {
                    baglandi = true;
                    Not("sunucuya bağlandı (" + (Time.realtimeSinceStartup - t).ToString("0.0") + " sn)");
                }
                reddedildi = oyun.UIManager != null && oyun.UIManager.loginFailedWindow != null && oyun.UIManager.loginFailedWindow.IsOpen;
                yield return new WaitForSecondsRealtime(0.5f);
            }
            oyun.NetworkManagerClient.OnClientVersionFailure -= surumDinle;
            if (oyun.NetworkManagerClient.AccountId <= 0) {
                string neden = sunucuSurumu != null ? "SÜRÜM TUTMUYOR: sunucu " + sunucuSurumu + " istiyor, bu istemci " + oyun.SystemConfigurationManager.ClientVersion
                    : reddedildi ? "hesap/şifre reddedildi"
                    : baglandi ? "sunucuya bağlandı ama 60 sn'de giriş cevabı gelmedi"
                    : "sunucuya ulaşılamadı (sunucu kapalı, açılıyor ya da UDP portu kapalı)";
                Bitir("SONUÇ: giriş olmadı: " + neden);
                yield break;
            }
            Not("giriş tamam: hesap " + oyun.NetworkManagerClient.AccountId + ", kip " + oyun.NetworkManagerClient.ClientMode);
            yield return new WaitForSecondsRealtime(2f);

            // karakter listesi; yoksa oluştur
            List<SinglePlayerSaveData> liste = null;
            for (int deneme = 0; deneme < 2; deneme++) {
                bool geldi = false;
                Action dinle = () => geldi = true;
                oyun.LoadGameManager.OnLoadCharacterList += dinle;
                oyun.LoadGameManager.LoadCharacterList();
                t = Time.realtimeSinceStartup;
                while (geldi == false && Time.realtimeSinceStartup - t < 30f) {
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                oyun.LoadGameManager.OnLoadCharacterList -= dinle;
                liste = oyun.LoadGameManager.CharacterList;
                Not("karakter listesi: " + (geldi ? liste.Count + " karakter" : "gelmedi"));
                if (liste != null && liste.Count > 0) {
                    break;
                }
                if (deneme == 0) {
                    try {
                        oyun.NewGameManager.InitializeData();
                        oyun.NewGameManager.SetupSaveData();
                        oyun.NewGameManager.EditPlayerName(ad);
                        oyun.NewGameManager.CreateNetworkCharacter();
                        Not("karakter oluşturma istendi");
                    } catch (Exception e) {
                        Not("karakter oluşturulamadı: " + e.Message);
                    }
                    yield return new WaitForSecondsRealtime(5f);
                }
            }
            if (liste == null || liste.Count == 0) {
                Bitir("SONUÇ: karakter yok");
                yield break;
            }

            // dünyaya gir
            oyun.LoadGameManager.LoadGame(liste[0]);
            Not("karaktere giriliyor: " + liste[0].CharacterSaveData.CharacterName + " (" + liste[0].CharacterSaveData.CurrentScene + ")");
            t = Time.realtimeSinceStartup;
            while ((oyun.PlayerManagerClient.PlayerUnitSpawned == false || oyun.PlayerManagerClient.UnitController == null) && Time.realtimeSinceStartup - t < 120f) {
                yield return new WaitForSecondsRealtime(0.5f);
            }
            UnitController ben = oyun.PlayerManagerClient.UnitController;
            if (ben == null) {
                Bitir("SONUÇ: dünyaya girilemedi (120 sn)");
                yield break;
            }
            Not("dünyada: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", konum " + ben.transform.position.ToString("0")
                + (ben.CharacterStats.IsAlive ? ", canlı" : ", ÖLÜ (önceki oturumda ölmüş)") + "; " + SavasTanisi(ben));
            yield return new WaitForSecondsRealtime(5f);
            // ölü girdiyse oyuncunun yapacağı gibi ölüm penceresinden yeniden doğ
            yield return Diril(oyun);
            ben = oyun.PlayerManagerClient.UnitController;
            if (ben == null) {
                Bitir("SONUÇ: yeniden doğduktan sonra oyuncu birimi yok");
                yield break;
            }

            // günlük armağan: sunucu durumu bildirince pencere açılır; oyuncu gibi "Armağanı Al"a basılır
            // (ilk girişte rehber açılır, oyuncu kapatınca armağana bakılır)
            GameGuide.Kapat();
            string armagan = "pencere açılmadı";
            for (int i = 0; i < 50; i++) {
                GameGuide.Kapat();
                if (GunlukArmagan.IsOpen) {
                    GunlukArmagan.TestIcinAl();
                    yield return new WaitForSecondsRealtime(3f);
                    armagan = "pencere açıldı, " + GunlukArmagan.SonDurum;
                    break;
                }
                if (GunlukArmagan.SonDurum == "bugün alınmış") {
                    armagan = "bugün alınmış";
                    break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Not("günlük armağan: " + armagan + " (sunucu: " + GunlukArmagan.SonDurum + ", " + GunlukArmagan.IstekSayisi + " istek, engel " + GunlukArmagan.Engel + ")");

            // ticaret: Kut (deneme satın alması), Kut Dükkânı, iki bot arasında takas ve pazar (yürümeden önce, yan yanayken)
            yield return TicaretDenemesi(oyun);
            Not("ticaret: " + ticaretOzeti);
            yield return KusanmaDenemesi(oyun);
            Not("kuşanma: " + kusanmaOzeti);

            // yürüme: hareket çubuğu 4 sn ileri (sunucu hareketi kabul edip ötekilere yayıyor mu)
            Vector3 yurumeOncesi = ben.transform.position;
            MobileInput.SetJoystick(Vector2.up, true);
            yield return new WaitForSecondsRealtime(4f);
            MobileInput.SetJoystick(Vector2.zero, false);
            yield return new WaitForSecondsRealtime(1f);
            ben = oyun.PlayerManagerClient.UnitController;
            float yurunen = ben != null ? Vector3.Distance(yurumeOncesi, ben.transform.position) : 0f;
            Not("hareket çubuğuyla 4 sn: " + yurunen.ToString("0.0") + " m");
            if (ben == null) {
                Bitir("SONUÇ: oyuncu birimi kayboldu");
                yield break;
            }

            // gerçek oyuncu gibi otomatik av (beceriler, ganimet); ayrıca 5 sn'de bir en yakın düşmana saldırı isteği
            if (OtomatikAv.Acik == false) {
                OtomatikAv.Degistir();
            }
            int enCokOyuncu = 0;
            int enCokNpc = 0;
            int ilkTecrube = ben.CharacterStats != null ? ben.CharacterStats.CurrentXP : 0;
            int ilkSeviye = ben.CharacterStats != null ? ben.CharacterStats.Level : 0;
            Vector3 ilkKonum = ben.transform.position;
            HashSet<string> gorulenOyuncular = new HashSet<string>();
            Dictionary<string, Vector3> otekiIlk = new Dictionary<string, Vector3>();
            float otekiYuruyus = 0f;
            int saldiri = 0;
            int tanilar = 0;
            int gorevIstegi = 0;
            t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t < sure) {
                yield return new WaitForSecondsRealtime(5f);
                ben = oyun.PlayerManagerClient.UnitController;
                if (ben != null && ben.CharacterStats.IsAlive == false) {
                    yield return Diril(oyun);
                    ben = oyun.PlayerManagerClient.UnitController;
                }
                if (ben == null) {
                    Not("oyuncu birimi kayboldu");
                    break;
                }
                // biten günlük görevin ödülünü iste (sunucu verir)
                try {
                    if (GunlukGorevler.TestIcinOdulIste()) {
                        gorevIstegi++;
                    }
                } catch (Exception e) {
                    Not("görev ödülü hatası: " + e.Message);
                }
                // av: savaşta değilse en yakın düşmana yürü ve saldır (dokunmayla aynı yol: sunucuya istek)
                try {
                    if (ben.CharacterStats.IsAlive && (ben.CharacterCombat == null || ben.CharacterCombat.GetInCombat() == false)) {
                        UnitController dusman = EnYakinDusman(ben);
                        if (dusman != null && oyun.PlayerManagerClient.PlayerController != null) {
                            oyun.PlayerManagerClient.PlayerController.RightMouseInteraction(dusman);
                            saldiri++;
                        }
                    }
                } catch (Exception e) {
                    Not("av hatası: " + e.Message);
                }
                int oyuncu = 0;
                int npc = 0;
                foreach (UnitController u in FindObjectsByType<UnitController>(FindObjectsSortMode.None)) {
                    if (u == ben) {
                        continue;
                    }
                    if (u.UnitControllerMode == UnitControllerMode.Player) {
                        oyuncu++;
                        gorulenOyuncular.Add(u.DisplayName);
                        Vector3 ilk;
                        if (otekiIlk.TryGetValue(u.DisplayName, out ilk)) {
                            otekiYuruyus = Mathf.Max(otekiYuruyus, Vector3.Distance(ilk, u.transform.position));
                        } else {
                            otekiIlk[u.DisplayName] = u.transform.position;
                        }
                    } else if (u.UnitControllerMode == UnitControllerMode.AI) {
                        npc++;
                    }
                }
                enCokOyuncu = Mathf.Max(enCokOyuncu, oyuncu);
                enCokNpc = Mathf.Max(enCokNpc, npc);
                UnitController hedefBirim = ben.Target as UnitController;
                if (hedefBirim != null && ben.CharacterCombat != null && ben.CharacterCombat.GetInCombat() && tanilar < 3) {
                    tanilar++;
                    Not("savaş tanısı (istemci): " + SavasTanisi(ben));
                }
                Not("öteki oyuncu " + oyuncu + ", NPC/düşman " + npc + ", seviye " + ben.CharacterStats.Level + ", tecrübe " + ben.CharacterStats.CurrentXP
                    + ", can %" + Yuzde(ben) + (ben.CharacterCombat != null && ben.CharacterCombat.GetInCombat() ? " (savaşta)" : string.Empty)
                    + ", konum " + ben.transform.position.ToString("0")
                    + (ben.Target != null ? ", hedef " + ben.Target.DisplayName + (hedefBirim != null ? " " + hedefBirim.CharacterStats.Level + ". sv can %" + Yuzde(hedefBirim)
                        + " " + Vector3.Distance(ben.transform.position, hedefBirim.transform.position).ToString("0") + " m" : string.Empty) : string.Empty));
            }
            // kolaylıklar: demirci, toplu satış, sıralama (hepsi sunucuda yapılır)
            OtomatikAv.Kapat();
            string kolaylik = string.Empty;
            try {
                Demirci.TestIcinYukselt();
            } catch (Exception e) {
                Not("demirci hatası: " + e.Message);
            }
            yield return new WaitForSecondsRealtime(3f);
            bool satildi = false;
            try {
                satildi = Canta.TestIcinSat();
            } catch (Exception e) {
                Not("toplu satış hatası: " + e.Message);
            }
            yield return new WaitForSecondsRealtime(3f);
            try {
                Canta.SiralaDugmesi();
            } catch (Exception e) {
                Not("sıralama hatası: " + e.Message);
            }
            yield return new WaitForSecondsRealtime(3f);
            kolaylik = "armağan " + GunlukArmagan.SonDurum + " (" + GunlukArmagan.IstekSayisi + " istek, engel " + GunlukArmagan.Engel + "); günlük görevler: " + GunlukGorevler.Ozet() + " (" + gorevIstegi + " ödül isteği)"
                + "; demirci " + Demirci.DenemeSayisi + " deneme/" + Demirci.BasariSayisi + " başarı (" + Demirci.SonSonuc + ")"
                + "; toplu satış " + (satildi ? Canta.SonSatis : "satılacak yok")
                + "; sıralama " + Canta.SonSiralama
                + "; seviye ödülü " + Gelisim.VerilenOdulSayisi + (Gelisim.SonOdul.Length > 0 ? " (" + Gelisim.SonOdul + ")" : string.Empty)
                + "; binek " + (Binek.Var ? "öğrenildi" : "yok")
                + "; ticaret: " + ticaretOzeti
                + "; kuşanma: " + kusanmaOzeti
                + "; yerdeki ganimet: en çok " + YerdekiGanimet.EnCokGorulen + " görüldü, " + YerdekiGanimet.IstekSayisi + " alma isteği";
            Not("kolaylıklar: " + kolaylik);
            Vector3 son = ben != null ? ben.transform.position : ilkKonum;
            Bitir("SONUÇ: " + (enCokOyuncu > 0 ? "öteki oyuncu GÖRÜLDÜ (" + string.Join(", ", gorulenOyuncular) + ", onun yürüyüşü " + otekiYuruyus.ToString("0") + " m)" : "öteki oyuncu görülmedi")
                + ", en çok NPC/düşman " + enCokNpc + ", çubukla " + yurunen.ToString("0") + " m, toplam yer değiştirme " + Vector3.Distance(ilkKonum, son).ToString("0") + " m"
                + ", " + saldiri + " saldırı isteği, oto av becerisi " + OtomatikAv.BeceriSayisi
                + (olum > 0 ? ", " + olum + " ölüm / " + dirilme + " yeniden doğma" + (olumPenceresi < olum ? " (ölüm penceresi " + olumPenceresi + " kez açıldı)" : string.Empty) : string.Empty)
                + ", seviye " + ilkSeviye + " → " + (ben != null ? ben.CharacterStats.Level : 0)
                + ", tecrübe " + ilkTecrube + " → " + (ben != null ? ben.CharacterStats.CurrentXP : 0)
                + " | kolaylıklar: " + kolaylik);
        }

        private int olum = 0;
        private int dirilme = 0;
        private int olumPenceresi = 0;

        /// <summary>oyuncu ölüyse ölüm penceresinin açıldığını denetler ve "Yeniden doğ" düğmesinin yaptığını yapar</summary>
        private IEnumerator Diril(SystemGameManager oyun) {
            UnitController ben = oyun.PlayerManagerClient.UnitController;
            if (ben == null || ben.CharacterStats.IsAlive) {
                yield break;
            }
            olum++;
            Vector3 olduguYer = ben.transform.position;
            // pencere ölümden 2 sn sonra açılır
            yield return new WaitForSecondsRealtime(3f);
            CloseableWindow pencere = oyun.UIManager != null ? oyun.UIManager.playerOptionsMenuWindow : null;
            bool acik = pencere != null && pencere.IsOpen;
            if (acik) {
                olumPenceresi++;
                pencere.CloseWindow();
            }
            oyun.PlayerManagerClient.RequestRespawnPlayer();
            float t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t < 30f) {
                yield return new WaitForSecondsRealtime(0.5f);
                ben = oyun.PlayerManagerClient.UnitController;
                if (ben != null && ben.CharacterStats.IsAlive) {
                    break;
                }
            }
            bool dirildi = ben != null && ben.CharacterStats.IsAlive;
            if (dirildi) {
                dirilme++;
            }
            Not("ölüm: " + olduguYer.ToString("0") + ", ölüm penceresi " + (acik ? "açıktı" : "AÇILMAMIŞTI") + "; yeniden doğma "
                + (dirildi ? "oldu: " + ben.transform.position.ToString("0") : "OLMADI (30 sn)"));
        }

        /// <summary>savaş tanısı: sınıf, kuşanılanlar, normal saldırı ve açık mı, savaşta mı, hedef (istemcide ve sunucuda)</summary>
        public static string SavasTanisi(UnitController u) {
            if (u == null) {
                return "birim yok";
            }
            try {
                string sinif = u.BaseCharacter != null && u.BaseCharacter.CharacterClass != null ? u.BaseCharacter.CharacterClass.DisplayName : "sınıfsız";
                List<string> esyalar = new List<string>();
                if (u.CharacterEquipmentManager != null && u.CharacterEquipmentManager.CurrentEquipment != null) {
                    foreach (EquipmentInventorySlot yuva in u.CharacterEquipmentManager.CurrentEquipment.Values) {
                        if (yuva != null && yuva.InstantiatedEquipment != null) {
                            esyalar.Add(yuva.InstantiatedEquipment.DisplayName);
                        }
                    }
                }
                string oto = u.CharacterAbilityManager != null && u.CharacterAbilityManager.AutoAttackAbility != null ? u.CharacterAbilityManager.AutoAttackAbility.DisplayName : "YOK";
                bool acik = u.CharacterCombat != null && u.CharacterCombat.AutoAttackActive;
                bool savasta = u.CharacterCombat != null && u.CharacterCombat.GetInCombat();
                return "sınıf " + sinif + ", " + esyalar.Count + " eşya (" + string.Join("/", esyalar.GetRange(0, Mathf.Min(5, esyalar.Count))) + ")"
                    + ", normal saldırı " + oto + (acik ? " AÇIK" : " kapalı") + (savasta ? ", savaşta" : string.Empty)
                    + ", hedef " + (u.Target != null ? u.Target.DisplayName : "yok");
            } catch (Exception e) {
                return "tanı hatası: " + e.Message;
            }
        }

        private static string Yuzde(UnitController u) {
            if (u == null || u.CharacterStats == null) {
                return "?";
            }
            return (100f * u.CharacterStats.CurrentPrimaryResource / Mathf.Max(1, u.CharacterStats.MaxPrimaryResource)).ToString("0");
        }

        private static UnitController EnYakinDusman(UnitController ben) {
            UnitController enYakin = null;
            float enAz = 80f;
            foreach (UnitController u in FindObjectsByType<UnitController>(FindObjectsSortMode.None)) {
                if (u == ben || u.UnitControllerMode != UnitControllerMode.AI || u.CharacterStats == null || u.CharacterStats.IsAlive == false
                    || ben.BaseCharacter == null || Faction.RelationWith(u, ben.BaseCharacter.Faction) > -1) {
                    continue;
                }
                float d = Vector3.Distance(u.transform.position, ben.transform.position);
                if (d < enAz) {
                    enAz = d;
                    enYakin = u;
                }
            }
            return enYakin;
        }

        private void Bitir(string sonuc) {
            Not(sonuc + ", " + HataOzeti());
            StartCoroutine(Kapat());
        }

        private IEnumerator Kapat() {
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit(0);
        }
    }
}
