using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnyRPG {

    public enum GunlukGorevTuru { Oldurme, Ganimet, Sandik, Harita }

    /// <summary>
    /// Günlük Görevler: her gün (cihazın tarihine göre) karakter başına 3 görev çıkar; ertesi gün yenilenir.
    /// Görevler 4 türden seçilir: düşman yen, ganimet topla, hazine sandığı boşalt, farklı haritalara git.
    /// Her biri tamamlanınca Gümüş Akçe ve tecrübe, üçü de tamamlanınca büyük ödül (Gök Taşı Parçası) alınır.
    /// İlerleme oyunun kendi olaylarından sayılır:
    ///  - düşman: oyuncunun UnitEventController.OnKillEvent'i (pay alınan her öldürme)
    ///  - ganimet: Ganimet.OnLooted (oyuncunun aldığı her ganimet)
    ///  - sandık: LootableNodeComponent.CheckDropListSize (içi tamamen boşaltılan hazine sandığı)
    ///  - harita: o gün girilen farklı haritalar (Işınlan penceresindeki 15 harita)
    /// Sol sütundaki "Günlük" düğmesiyle açılır; ödül bekleyince düğmede altın nokta yanar. Kodla kurulur.
    /// </summary>
    public class GunlukGorevler : MonoBehaviour {

        public const string CanvasName = "GunlukGorevlerCanvas";
        // Işınlan ve Seçenekler pencereleriyle aynı katman
        private const int SortingOrder = 31;
        private const int GorevSayisi = 3;
        private const string BuyukOdulEsyasi = "Gok Tasi Parcasi";
        private const string BuyukOdulEsyaAdi = "Gök Taşı Parçası";
        private const int BuyukOdulGumus = 20;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color rowColor = new Color(0.16f, 0.12f, 0.08f, 0.95f);
        private static readonly Color rowDoneColor = new Color(0.3f, 0.23f, 0.1f, 0.98f);
        private static readonly Color buttonColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color claimColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color barBackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color barColor = new Color(0.85f, 0.65f, 0.25f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color hintColor = new Color(0.75f, 0.7f, 0.62f, 1f);
        private static readonly Color errorColor = new Color(1f, 0.55f, 0.45f, 1f);

        private struct Tanim {
            public GunlukGorevTuru tur;
            public string baslik;
            public string aciklama;
            public int[] adetler;

            public Tanim(GunlukGorevTuru tur, string baslik, string aciklama, int[] adetler) {
                this.tur = tur;
                this.baslik = baslik;
                this.aciklama = aciklama;
                this.adetler = adetler;
            }
        }

        private static readonly Tanim[] tanimlar = {
            new Tanim(GunlukGorevTuru.Oldurme, "Erlik'in yandaşları", "{0} düşman yen", new int[] { 12, 18, 25 }),
            new Tanim(GunlukGorevTuru.Ganimet, "Ganimet avcısı", "{0} ganimet topla", new int[] { 8, 12, 16 }),
            new Tanim(GunlukGorevTuru.Sandik, "Hazine avı", "{0} hazine sandığını boşalt", new int[] { 1, 2, 3 }),
            new Tanim(GunlukGorevTuru.Harita, "Yolculuk", "{0} farklı diyara ayak bas", new int[] { 2, 3, 4 }),
        };

        // ---------------------------------------------------------------- günün durumu

        private class Gorev {
            public Tanim tanim;
            public int hedef;
            public int ilerleme;
            public bool alindi;

            public bool Tamam { get { return ilerleme >= hedef; } }
            public string Yazi { get { return string.Format(tanim.aciklama, hedef); } }
        }

        private static string karakter = string.Empty;
        private static string gun = string.Empty;
        private static readonly List<Gorev> gorevler = new List<Gorev>();
        private static readonly HashSet<string> gezilenler = new HashSet<string>();
        private static bool buyukOdulAlindi = false;
        private static bool kirli = false;
        private static float sonKayit = 0f;
        private static UnitController takipEdilen = null;
        private static SystemGameManager oyun = null;

        /// <summary>alınmayı bekleyen ödül var mı (HUD düğmesindeki altın nokta)</summary>
        public static bool OdulVar {
            get {
                if (gorevler.Count == 0) {
                    return false;
                }
                bool hepsi = true;
                foreach (Gorev g in gorevler) {
                    if (g.Tamam && g.alindi == false) {
                        return true;
                    }
                    hepsi &= g.alindi;
                }
                return hepsi && buyukOdulAlindi == false;
            }
        }

        /// <summary>bugünün sandık görevi sürüyor mu (görev oku sıradaki hedef yoksa en yakın sandığı gösterir)</summary>
        public static bool SandikGoreviSuruyor {
            get {
                foreach (Gorev g in gorevler) {
                    if (g.tanim.tur == GunlukGorevTuru.Sandik && g.Tamam == false) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>MobileBootstrap saniyede bir çağırır</summary>
        public static void Tick(SystemGameManager systemGameManager, bool oyunda) {
            // çevrimiçi oyunda karakter sunucudadır: telefondan ödül/yetenek verilmez (Cevrimici)
            oyunda = oyunda && (systemGameManager == null || systemGameManager.GameMode != GameMode.Network);
            oyun = systemGameManager;
            UnitController oyuncu = oyunda && systemGameManager != null && systemGameManager.PlayerManagerClient != null
                ? systemGameManager.PlayerManagerClient.UnitController : null;
            if (oyuncu != takipEdilen) {
                if (takipEdilen != null && takipEdilen.UnitEventController != null) {
                    takipEdilen.UnitEventController.OnKillEvent -= OldurmeOldu;
                }
                takipEdilen = oyuncu;
                if (takipEdilen != null && takipEdilen.UnitEventController != null) {
                    takipEdilen.UnitEventController.OnKillEvent += OldurmeOldu;
                }
            }
            if (oyuncu == null) {
                Kaydet(true);
                if (IsOpen) {
                    instance.Kapat();
                }
                return;
            }
            Hazirla(oyuncu.DisplayName);
            // bugün girilen haritalar
            string sahne = SceneManager.GetActiveScene().name;
            if (Array.IndexOf(IsinlanmaPenceresi.SahneAdlari, sahne) >= 0 && gezilenler.Add(sahne)) {
                kirli = true;
                Ilerlet(GunlukGorevTuru.Harita, 0, gezilenler.Count);
            }
            if (kirli && Time.unscaledTime - sonKayit > 10f) {
                Kaydet(false);
            }
        }

        /// <summary>oyunun olaylarından: ganimet ve sandık (düşman öldürme kendi olayından sayılır)</summary>
        public static void Bildir(GunlukGorevTuru tur, int adet = 1) {
            if (takipEdilen == null || gorevler.Count == 0) {
                return;
            }
            Ilerlet(tur, adet, -1);
        }

        private static void OldurmeOldu(UnitController olduren, UnitController olen, float pay) {
            if (pay <= 0f || olen == null || olen == takipEdilen) {
                return;
            }
            Ilerlet(GunlukGorevTuru.Oldurme, 1, -1);
        }

        /// <param name="kesin">0 veya üstüyse ilerleme bu değere çekilir (harita sayısı), değilse adet eklenir</param>
        private static void Ilerlet(GunlukGorevTuru tur, int adet, int kesin) {
            foreach (Gorev g in gorevler) {
                if (g.tanim.tur != tur || g.Tamam) {
                    continue;
                }
                g.ilerleme = Mathf.Min(g.hedef, kesin >= 0 ? kesin : g.ilerleme + adet);
                kirli = true;
                if (g.Tamam && takipEdilen != null) {
                    takipEdilen.WriteMessageFeedMessage($"<color=#FFD54A>Günlük görev tamamlandı: {g.Yazi}. Ödülün için Günlük'e dokun.</color>");
                    MobileFeedback.Success();
                }
            }
            if (IsOpen) {
                instance.Yenile();
            }
        }

        // ---------------------------------------------------------------- kayıt (PlayerPrefs, karakter başına)

        private static string GunAnahtari(DateTime tarih) {
            return tarih.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string KayitAnahtari(string ad) {
            return "gunluk-gorevler-" + ad;
        }

        private static void Hazirla(string ad) {
            string bugun = GunAnahtari(DateTime.Now);
            if (ad == karakter && bugun == gun && gorevler.Count > 0) {
                return;
            }
            Kaydet(true);
            karakter = ad;
            gun = bugun;
            gorevler.Clear();
            gezilenler.Clear();
            buyukOdulAlindi = false;

            // günün görevleri: tarih ve karakterden türeyen sayı; dört türden biri dışarıda kalır
            uint tohum = Ozet32(bugun + "|" + ad);
            int disarida = (int)(tohum % (uint)tanimlar.Length);
            int sira = 0;
            for (int i = 0; i < tanimlar.Length && gorevler.Count < GorevSayisi; i++) {
                if (i == disarida) {
                    continue;
                }
                Tanim t = tanimlar[i];
                int zorluk = (int)((tohum / 7u + (uint)sira * 3u) % (uint)t.adetler.Length);
                gorevler.Add(new Gorev() { tanim = t, hedef = t.adetler[zorluk] });
                sira++;
            }

            // kayıt: gün|ilerleme,ilerleme,ilerleme|alındı bitleri|büyük ödül|gezilen;haritalar
            string kayit = PlayerPrefs.GetString(KayitAnahtari(ad), string.Empty);
            string[] parcalar = kayit.Split('|');
            if (parcalar.Length >= 5 && parcalar[0] == bugun) {
                string[] ilerlemeler = parcalar[1].Split(',');
                for (int i = 0; i < gorevler.Count && i < ilerlemeler.Length; i++) {
                    int deger;
                    if (int.TryParse(ilerlemeler[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out deger)) {
                        gorevler[i].ilerleme = Mathf.Clamp(deger, 0, gorevler[i].hedef);
                    }
                    gorevler[i].alindi = i < parcalar[2].Length && parcalar[2][i] == '1';
                }
                buyukOdulAlindi = parcalar[3] == "1";
                foreach (string s in parcalar[4].Split(';')) {
                    if (s.Length > 0) {
                        gezilenler.Add(s);
                    }
                }
            }
            kirli = false;
        }

        private static void Kaydet(bool zorla) {
            if (kirli == false || string.IsNullOrEmpty(karakter) || gorevler.Count == 0) {
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append(gun).Append('|');
            for (int i = 0; i < gorevler.Count; i++) {
                sb.Append(i > 0 ? "," : string.Empty).Append(gorevler[i].ilerleme.ToString(CultureInfo.InvariantCulture));
            }
            sb.Append('|');
            foreach (Gorev g in gorevler) {
                sb.Append(g.alindi ? '1' : '0');
            }
            sb.Append('|').Append(buyukOdulAlindi ? '1' : '0').Append('|').Append(string.Join(";", gezilenler));
            PlayerPrefs.SetString(KayitAnahtari(karakter), sb.ToString());
            PlayerPrefs.Save();
            kirli = false;
            sonKayit = Time.unscaledTime;
        }

        private static uint Ozet32(string metin) {
            uint h = 2166136261u;
            foreach (char c in metin) {
                h ^= c;
                h *= 16777619u;
            }
            return h;
        }

        /// <summary>oyun testi raporu için kısa özet</summary>
        public static string Ozet() {
            if (gorevler.Count == 0) {
                return "günlük görev yok";
            }
            List<string> satirlar = new List<string>();
            foreach (Gorev g in gorevler) {
                satirlar.Add(g.Yazi + " " + g.ilerleme + "/" + g.hedef + (g.Tamam ? " (tamam)" : string.Empty));
            }
            return string.Join(", ", satirlar);
        }

        // ---------------------------------------------------------------- ödüller

        private static int GumusOdulu(int seviye) {
            return 6 + 2 * Mathf.Max(1, seviye);
        }

        private static int TecrubeOdulu(int seviye) {
            SystemConfigurationManager ayarlar = oyun != null ? oyun.SystemConfigurationManager : null;
            if (ayarlar == null) {
                // derleme önizlemesi: oyun yok
                return 25 * Mathf.Max(1, seviye);
            }
            return Mathf.Max(10, Mathf.RoundToInt(LevelEquations.GetXPNeededForLevel(Mathf.Max(1, seviye), ayarlar) * 0.12f));
        }

        private void OdulVer(int index) {
            UnitController oyuncu = takipEdilen;
            if (oyuncu == null || index < 0 || index >= gorevler.Count) {
                return;
            }
            Gorev g = gorevler[index];
            if (g.Tamam == false || g.alindi) {
                return;
            }
            int seviye = oyuncu.CharacterStats.Level;
            int gumus = GumusOdulu(seviye);
            int tecrube = TecrubeOdulu(seviye);
            Currency para = oyun.SystemDataFactory.GetResource<Currency>("Silver");
            if (para != null) {
                oyuncu.CharacterCurrencyManager.AddCurrency(para, gumus);
            }
            g.alindi = true;
            kirli = true;
            Kaydet(true);
            if (tecrube > 0) {
                oyuncu.CharacterStats.GainExperience(tecrube);
            }
            oyuncu.WriteMessageFeedMessage($"<color=#FFD54A>Günlük görev ödülü: {gumus} Gümüş Akçe, {tecrube} tecrübe</color>");
            MobileFeedback.Success();
            Yenile();
        }

        private void BuyukOdulVer() {
            UnitController oyuncu = takipEdilen;
            if (oyuncu == null || buyukOdulAlindi) {
                return;
            }
            foreach (Gorev g in gorevler) {
                if (g.alindi == false) {
                    return;
                }
            }
            if (oyuncu.CharacterInventoryManager.EmptySlotCount() == 0) {
                durumYazisi.text = "Çantan dolu! Biraz yer aç, sonra büyük ödülü al.";
                return;
            }
            InstantiatedItem esya = oyuncu.CharacterInventoryManager.GetNewInstantiatedItem(BuyukOdulEsyasi);
            if (esya != null) {
                oyuncu.CharacterInventoryManager.AddItem(esya, false);
            }
            Currency para = oyun.SystemDataFactory.GetResource<Currency>("Silver");
            if (para != null) {
                oyuncu.CharacterCurrencyManager.AddCurrency(para, BuyukOdulGumus);
            }
            buyukOdulAlindi = true;
            kirli = true;
            Kaydet(true);
            oyuncu.WriteMessageFeedMessage($"<color=#FFD54A>Günün büyük ödülü: {BuyukOdulEsyaAdi} ve {BuyukOdulGumus} Gümüş Akçe!</color>");
            MobileFeedback.Success();
            Yenile();
        }

        // ---------------------------------------------------------------- pencere

        private static GunlukGorevler instance = null;

        private Font font = null;
        private GameObject panelRoot = null;
        private Text altBaslik = null;
        private Text durumYazisi = null;
        private readonly List<Image> satirResimleri = new List<Image>();
        private readonly List<Text> satirBasliklari = new List<Text>();
        private readonly List<Text> satirYazilari = new List<Text>();
        private readonly List<RectTransform> cubuklar = new List<RectTransform>();
        private readonly List<Text> sayilar = new List<Text>();
        private readonly List<Text> odulYazilari = new List<Text>();
        private readonly List<Button> alDugmeleri = new List<Button>();
        private readonly List<Text> alYazilari = new List<Text>();
        private Button buyukDugme = null;
        private Text buyukYazi = null;
        private Text buyukDugmeYazisi = null;
        private float sonrakiYenileme = 0f;

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        public static void Goster() {
            if (Cevrimici.Engelle("Günlük görevler")) {
                return;
            }
            if (takipEdilen == null) {
                return;
            }
            Hazirla(takipEdilen.DisplayName);
            Ensure();
            instance.panelRoot.SetActive(true);
            instance.durumYazisi.text = string.Empty;
            instance.Yenile();
            MobileFeedback.Light();
        }

        private static void Ensure() {
            if (instance != null) {
                return;
            }
            GameObject canvasObject = new GameObject(CanvasName);
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1422f, 800f);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 1f;
            canvasObject.AddComponent<GraphicRaycaster>();
            instance = canvasObject.AddComponent<GunlukGorevler>();
            instance.Build();
        }

        private void Kapat() {
            panelRoot.SetActive(false);
        }

        /// <summary>derleme önizlemesi (HaritaHazirlik, tani/arayuz_gunluk.jpg): örnek görevlerle pencere</summary>
        private void Onizleme() {
            gorevler.Clear();
            int[] ilerlemeler = { 18, 5, 1 };
            for (int i = 0; i < GorevSayisi; i++) {
                Tanim t = tanimlar[i];
                gorevler.Add(new Gorev() { tanim = t, hedef = t.adetler[1], ilerleme = Mathf.Min(ilerlemeler[i], t.adetler[1]), alindi = false });
            }
            buyukOdulAlindi = false;
            panelRoot.SetActive(true);
            durumYazisi.text = string.Empty;
            Yenile();
        }

        /// <summary>önizlemeden sonra örnek görevleri sil</summary>
        private void OnizlemeBitti() {
            gorevler.Clear();
            karakter = string.Empty;
            gun = string.Empty;
        }

        private void Update() {
            if (IsOpen && Time.unscaledTime >= sonrakiYenileme) {
                Yenile();
            }
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panelRoot = CreateRect(transform, "GunlukGorevler", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimmer = panelRoot.AddComponent<Image>();
            dimmer.color = new Color(0f, 0f, 0f, 0.65f);

            GameObject panel = CreateRect(panelRoot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-540f, -330f), new Vector2(540f, 330f));
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = panelColor;
            Outline panelOutline = panel.AddComponent<Outline>();
            panelOutline.effectColor = gold;
            panelOutline.effectDistance = new Vector2(2f, -2f);

            GameObject title = CreateRect(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -62f), new Vector2(-20f, -8f));
            Text titleText = CreateText(title, "GÜNLÜK GÖREVLER", 36, TextAnchor.MiddleCenter, gold);
            titleText.fontStyle = FontStyle.Bold;
            GameObject subtitle = CreateRect(panel.transform, "Aciklama", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -96f), new Vector2(-20f, -60f));
            altBaslik = CreateText(subtitle, string.Empty, 19, TextAnchor.MiddleCenter, hintColor);

            const float satirYuksekligi = 118f;
            const float satirAraligi = 10f;
            float ust = -104f;
            for (int i = 0; i < GorevSayisi; i++) {
                int index = i;
                GameObject satir = CreateRect(panel.transform, "Gorev" + (i + 1), new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(24f, ust - satirYuksekligi), new Vector2(-24f, ust));
                Image satirResmi = satir.AddComponent<Image>();
                satirResmi.color = rowColor;
                satirResimleri.Add(satirResmi);
                ust -= satirYuksekligi + satirAraligi;

                GameObject baslik = CreateRect(satir.transform, "Baslik", new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(18f, -40f), new Vector2(0f, -8f));
                Text baslikYazisi = CreateText(baslik, string.Empty, 22, TextAnchor.MiddleLeft, gold);
                baslikYazisi.fontStyle = FontStyle.Bold;
                satirBasliklari.Add(baslikYazisi);

                GameObject yazi = CreateRect(satir.transform, "Yazi", new Vector2(0f, 1f), new Vector2(0.62f, 1f), new Vector2(18f, -72f), new Vector2(0f, -40f));
                satirYazilari.Add(CreateText(yazi, string.Empty, 21, TextAnchor.MiddleLeft, textColor));

                // ilerleme çubuğu
                GameObject cubukArka = CreateRect(satir.transform, "Cubuk", new Vector2(0f, 0f), new Vector2(0.62f, 0f), new Vector2(18f, 14f), new Vector2(-90f, 34f));
                cubukArka.AddComponent<Image>().color = barBackColor;
                GameObject cubuk = CreateRect(cubukArka.transform, "Dolu", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
                cubuk.AddComponent<Image>().color = barColor;
                cubuklar.Add(cubuk.GetComponent<RectTransform>());
                GameObject sayi = CreateRect(satir.transform, "Sayi", new Vector2(0.62f, 0f), new Vector2(0.62f, 0f), new Vector2(-84f, 8f), new Vector2(0f, 40f));
                sayilar.Add(CreateText(sayi, string.Empty, 20, TextAnchor.MiddleLeft, textColor));

                GameObject odul = CreateRect(satir.transform, "Odul", new Vector2(0.62f, 0f), new Vector2(1f, 1f), new Vector2(10f, 10f), new Vector2(-190f, -10f));
                odulYazilari.Add(CreateText(odul, string.Empty, 19, TextAnchor.MiddleCenter, hintColor));

                Text alYazisi;
                Button al = CreateButton(satir.transform, "Al", new Vector2(1f, 0.5f), new Vector2(-92f, 0f), new Vector2(160f, 64f), 24, claimColor,
                    () => { MobileFeedback.Tap(); OdulVer(index); }, out alYazisi);
                alDugmeleri.Add(al);
                alYazilari.Add(alYazisi);
            }

            // büyük ödül
            GameObject buyuk = CreateRect(panel.transform, "BuyukOdul", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 92f), new Vector2(-24f, 150f));
            buyuk.AddComponent<Image>().color = new Color(0.13f, 0.1f, 0.07f, 0.95f);
            GameObject buyukMetin = CreateRect(buyuk.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(18f, 0f), new Vector2(-230f, 0f));
            buyukYazi = CreateText(buyukMetin, string.Empty, 20, TextAnchor.MiddleLeft, textColor);
            buyukDugme = CreateButton(buyuk.transform, "Büyük Ödülü Al", new Vector2(1f, 0.5f), new Vector2(-112f, 0f), new Vector2(210f, 48f), 21, claimColor,
                () => { MobileFeedback.Tap(); BuyukOdulVer(); }, out buyukDugmeYazisi);

            GameObject durum = CreateRect(panel.transform, "Durum", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 64f), new Vector2(-24f, 90f));
            durumYazisi = CreateText(durum, string.Empty, 19, TextAnchor.MiddleCenter, errorColor);

            Text kapatYazisi;
            CreateButton(panel.transform, "Kapat", new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(220f, 52f), 24, buttonColor,
                () => { MobileFeedback.Tap(); Kapat(); }, out kapatYazisi);

            panelRoot.SetActive(false);
        }

        private void Yenile() {
            sonrakiYenileme = Time.unscaledTime + 0.5f;
            if (panelRoot == null) {
                return;
            }
            TimeSpan kalan = DateTime.Now.Date.AddDays(1) - DateTime.Now;
            altBaslik.text = $"Her gün 3 yeni görev. Hepsini bitirene büyük ödül! Yenilenmesine {(int)kalan.TotalHours} sa {kalan.Minutes} dk var.";
            int seviye = takipEdilen != null ? takipEdilen.CharacterStats.Level : 1;
            bool hepsiAlindi = gorevler.Count > 0;
            for (int i = 0; i < GorevSayisi; i++) {
                bool mevcut = i < gorevler.Count;
                satirResimleri[i].gameObject.SetActive(mevcut);
                if (mevcut == false) {
                    hepsiAlindi = false;
                    continue;
                }
                Gorev g = gorevler[i];
                satirBasliklari[i].text = g.tanim.baslik;
                satirYazilari[i].text = g.Yazi;
                float oran = g.hedef > 0 ? Mathf.Clamp01(g.ilerleme / (float)g.hedef) : 0f;
                cubuklar[i].anchorMax = new Vector2(oran, 1f);
                sayilar[i].text = g.ilerleme + "/" + g.hedef;
                odulYazilari[i].text = $"{GumusOdulu(seviye)} Gümüş Akçe\n{TecrubeOdulu(seviye)} tecrübe";
                satirResimleri[i].color = g.Tamam ? rowDoneColor : rowColor;
                alDugmeleri[i].interactable = g.Tamam && g.alindi == false;
                alYazilari[i].text = g.alindi ? "Alındı" : (g.Tamam ? "Ödülü Al" : "Sürüyor");
                hepsiAlindi &= g.alindi;
            }
            buyukYazi.text = buyukOdulAlindi
                ? "Bugünün büyük ödülünü aldın. Yarın yeni görevler seni bekliyor."
                : $"Üç görevin ödülünü de alınca: {BuyukOdulEsyaAdi} + {BuyukOdulGumus} Gümüş Akçe";
            buyukDugme.interactable = hepsiAlindi && buyukOdulAlindi == false;
            buyukDugmeYazisi.text = buyukOdulAlindi ? "Alındı" : "Büyük Ödülü Al";
        }

        // ---------------------------------------------------------------- yapı taşları

        private GameObject CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject rectObject = new GameObject(name);
            rectObject.transform.SetParent(parent, false);
            RectTransform rectTransform = rectObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
            return rectObject;
        }

        private Text CreateText(GameObject target, string content, int fontSize, TextAnchor alignment, Color color) {
            Text text = target.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size, int fontSize,
            Color color, UnityEngine.Events.UnityAction onClick, out Text labelText) {
            GameObject buttonObject = CreateRect(parent, label, anchor, anchor, Vector2.zero, Vector2.zero);
            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Outline outline = buttonObject.AddComponent<Outline>();
            outline.effectColor = new Color(gold.r, gold.g, gold.b, 0.6f);
            outline.effectDistance = new Vector2(1f, -1f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            button.colors = colors;
            button.onClick.AddListener(onClick);
            GameObject textObject = CreateRect(buttonObject.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f));
            labelText = CreateText(textObject, label, fontSize, TextAnchor.MiddleCenter, textColor);
            return button;
        }
    }
}
