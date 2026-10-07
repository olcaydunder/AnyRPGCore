using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Çevrimdışı kazanç (birçok mobil RPG'deki "sen yokken" ödülü): oyuna en az 30 dakika ara verip dönen oyuncuya,
    /// ara süresine göre (en çok 10 saat sayılır) Gümüş Akçe ve tecrübe. "Topla"ya dokununca verilir.
    /// Son görülme karakter başına PlayerPrefs'te tutulur: oyundayken 30 saniyede bir ve arka plana geçince yazılır;
    /// oyuna girince ve arka plandan dönünce bakılır. Günlük Armağan ve rehber kapanınca açılır. Kodla kurulur.
    /// Çevrimiçi oyunda son görülmeyi ve birikeni sunucu tutar (karakter kaydında); kazancı "Topla"da sunucu verir.
    /// </summary>
    public class CevrimdisiKazanc : MonoBehaviour {

        public const string CanvasName = "CevrimdisiKazancCanvas";
        // Günlük Armağan ile aynı katman
        private const int SortingOrder = 28;
        private const string Anahtar = "son-gorulme-";
        private const double EnAzDakika = 30;
        private const double EnCokSaat = 10;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color claimColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);

        private static CevrimdisiKazanc instance = null;
        private static string karakter = string.Empty;
        private static bool bakilacak = true;
        private static float sonYazma = -100f;
        private static float oyundaBeri = -1f;
        private static SystemGameManager oyun = null;

        private GameObject panelRoot = null;
        private Text metin = null;
        private int bekleyenGumus = 0;
        private int bekleyenTecrube = 0;

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        /// <summary>MobileBootstrap saniyede bir çağırır</summary>
        public static void Tick(SystemGameManager systemGameManager, bool oyunda) {
            if (oyunda && systemGameManager != null && systemGameManager.GameMode == GameMode.Network) {
                // çevrimiçi: kazancı sunucu hesaplar ("kazanc" yanıtı); pencere armağan ve rehber kapanınca açılır
                oyun = systemGameManager;
                if (sunucuKazanci != null && GunlukArmagan.IsOpen == false && GameGuide.IsOpen == false && IsOpen == false) {
                    string[] p = sunucuKazanci;
                    sunucuKazanci = null;
                    int dakika, gumus, tecrube;
                    if (int.TryParse(p[0], out dakika) && int.TryParse(p[1], out gumus) && int.TryParse(p[2], out tecrube)) {
                        Ensure();
                        instance.Ac(TimeSpan.FromMinutes(dakika), p[3] == "1", gumus, tecrube);
                    }
                }
                return;
            }
            oyunda = oyunda && (systemGameManager == null || systemGameManager.GameMode != GameMode.Network);
            oyun = systemGameManager;
            UnitController oyuncu = oyunda && systemGameManager != null && systemGameManager.PlayerManagerClient != null
                ? systemGameManager.PlayerManagerClient.UnitController : null;
            if (oyuncu == null) {
                oyundaBeri = -1f;
                return;
            }
            if (oyundaBeri < 0f) {
                oyundaBeri = Time.unscaledTime;
            }
            if (oyuncu.DisplayName != karakter) {
                karakter = oyuncu.DisplayName;
                bakilacak = true;
            }
            // önce Günlük Armağan ve rehber
            if (bakilacak && Time.unscaledTime - oyundaBeri > 6f && GunlukArmagan.IsOpen == false && GameGuide.IsOpen == false && IsOpen == false) {
                bakilacak = false;
                Bak(oyuncu);
            }
            if (Time.unscaledTime - sonYazma > 30f && bakilacak == false) {
                Yaz();
            }
        }

        /// <summary>arka plandan dönünce: aradaki süreye bakılır</summary>
        public static void OnPlanaGecti() {
            bakilacak = true;
            oyundaBeri = Time.unscaledTime;
        }

        /// <summary>arka plana geçerken ve kapanırken: son görülme yazılır</summary>
        public static void ArkaPlan() {
            if (bakilacak == false) {
                Yaz();
            }
        }

        private static void Yaz() {
            if (string.IsNullOrEmpty(karakter)) {
                return;
            }
            PlayerPrefs.SetString(Anahtar + karakter, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            sonYazma = Time.unscaledTime;
        }

        private static void Bak(UnitController oyuncu) {
            string kayit = PlayerPrefs.GetString(Anahtar + karakter, string.Empty);
            Yaz();
            long onceki;
            if (long.TryParse(kayit, NumberStyles.Integer, CultureInfo.InvariantCulture, out onceki) == false
                || onceki <= 0 || onceki > DateTime.UtcNow.Ticks) {
                // kayıt yok ya da saat geri alınmış
                return;
            }
            TimeSpan ara = DateTime.UtcNow - new DateTime(onceki, DateTimeKind.Utc);
            if (ara.TotalMinutes < EnAzDakika) {
                return;
            }
            double saat = Math.Min(ara.TotalHours, EnCokSaat);
            int gumus, tecrube;
            Hesapla(saat, oyuncu, out gumus, out tecrube);
            Ensure();
            instance.Ac(ara, saat >= EnCokSaat, gumus, tecrube);
        }

        private static void Hesapla(double saat, UnitController oyuncu, out int gumus, out int tecrube) {
            int seviye = Mathf.Max(1, oyuncu.CharacterStats.Level);
            gumus = Mathf.Max(1, (int)Math.Round(saat * (1.0 + seviye * 0.5)));
            tecrube = 0;
            SystemGameManager o = oyun != null ? oyun : OtukenAg.Oyun;
            if (o != null && o.SystemConfigurationManager != null) {
                tecrube = Mathf.Max(1, (int)Math.Round(saat * 0.04 * LevelEquations.GetXPNeededForLevel(seviye, o.SystemConfigurationManager)));
            }
        }

        // ---------------------------------------------------------------- çevrimiçi (sunucu)

        private const string BekleyenAnahtar = "bekleyen-kazanc-";
        private static readonly Dictionary<UnitController, float> sunucuYazma = new Dictionary<UnitController, float>();
        private static string[] sunucuKazanci = null;

        /// <summary>sunucu her oyuncu için saniyede bir çağırır (OtukenSunucu): girişte araya bakar, sonra son görülmeyi yazar</summary>
        public static void SunucuTick(UnitController oyuncu) {
            float son;
            if (sunucuYazma.TryGetValue(oyuncu, out son) == false) {
                sunucuYazma[oyuncu] = Time.realtimeSinceStartup;
                SunucuBak(oyuncu);
                return;
            }
            if (Time.realtimeSinceStartup - son > 30f) {
                sunucuYazma[oyuncu] = Time.realtimeSinceStartup;
                OtukenVeri.Yaz(oyuncu, Anahtar, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            }
        }

        public static void SunucuCikti(UnitController oyuncu) {
            if (oyuncu != null && sunucuYazma.ContainsKey(oyuncu)) {
                OtukenVeri.Yaz(oyuncu, Anahtar, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                sunucuYazma.Remove(oyuncu);
            }
        }

        private static double Bekleyen(UnitController oyuncu) {
            double saat;
            return double.TryParse(OtukenVeri.Oku(oyuncu, BekleyenAnahtar, "0"), NumberStyles.Float, CultureInfo.InvariantCulture, out saat)
                ? Math.Max(0, Math.Min(EnCokSaat, saat)) : 0;
        }

        private static void SunucuBak(UnitController oyuncu) {
            string kayit = OtukenVeri.Oku(oyuncu, Anahtar, string.Empty);
            OtukenVeri.Yaz(oyuncu, Anahtar, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            double bekleyen = Bekleyen(oyuncu);
            long onceki;
            if (long.TryParse(kayit, NumberStyles.Integer, CultureInfo.InvariantCulture, out onceki) && onceki > 0 && onceki <= DateTime.UtcNow.Ticks) {
                TimeSpan ara = DateTime.UtcNow - new DateTime(onceki, DateTimeKind.Utc);
                if (ara.TotalMinutes >= EnAzDakika) {
                    bekleyen = Math.Min(EnCokSaat, bekleyen + ara.TotalHours);
                    OtukenVeri.Yaz(oyuncu, BekleyenAnahtar, bekleyen.ToString("0.###", CultureInfo.InvariantCulture));
                }
            }
            if (bekleyen <= 0) {
                return;
            }
            int gumus, tecrube;
            Hesapla(bekleyen, oyuncu, out gumus, out tecrube);
            OtukenAg.Yanitla(oyuncu, "kazanc", (int)Math.Round(bekleyen * 60) + "|" + gumus + "|" + tecrube + "|" + (bekleyen >= EnCokSaat ? "1" : "0"));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("kazanc-topla", (oyuncu, veri) => {
                double bekleyen = Bekleyen(oyuncu);
                if (bekleyen <= 0) {
                    return;
                }
                int gumus, tecrube;
                Hesapla(bekleyen, oyuncu, out gumus, out tecrube);
                // "2": oyuncu ödüllü reklam izledi; günlük hakkı varsa kazanç iki kat (Reklam)
                bool ikiKat = veri == "2" && Reklam.HakKullan(oyuncu, Reklam.Kazanc);
                if (ikiKat) {
                    gumus *= 2;
                    tecrube *= 2;
                }
                OtukenVeri.Yaz(oyuncu, BekleyenAnahtar, "0");
                Ver(oyuncu, gumus, tecrube);
                Debug.Log("[Sunucu] " + oyuncu.DisplayName + " çevrimdışı kazanç: " + gumus + " gümüş, " + tecrube + " tecrübe" + (ikiKat ? " (reklamla 2 kat)" : string.Empty));
            });
            OtukenAg.IstemciDinle("kazanc", veri => {
                string[] p = veri.Split('|');
                if (p.Length >= 4) {
                    sunucuKazanci = p;
                }
            });
        }

        private static void Ver(UnitController oyuncu, int gumus, int tecrube) {
            SystemGameManager o = oyun != null ? oyun : OtukenAg.Oyun;
            Currency para = o.SystemDataFactory.GetResource<Currency>("Silver");
            if (para != null && gumus > 0) {
                oyuncu.CharacterCurrencyManager.AddCurrency(para, gumus);
            }
            if (tecrube > 0) {
                oyuncu.CharacterStats.GainExperience(tecrube);
            }
            OtukenAg.Mesaj(oyuncu, $"<color=#FFD54A>Sen yokken kazandıkların: {gumus} Gümüş Akçe, {tecrube} tecrübe</color>");
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
            instance = canvasObject.AddComponent<CevrimdisiKazanc>();
            instance.Build();
        }

        private void Build() {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelRoot = Kutu(transform, "CevrimdisiKazanc", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            GameObject panel = Kutu(panelRoot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-330f, -170f), new Vector2(330f, 170f));
            panel.AddComponent<Image>().color = panelColor;
            Outline cizgi = panel.AddComponent<Outline>();
            cizgi.effectColor = gold;
            cizgi.effectDistance = new Vector2(2f, -2f);

            Text baslik = Yazi(Kutu(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -66f), new Vector2(-20f, -10f)),
                font, "SEN YOKKEN", 32, gold);
            baslik.fontStyle = FontStyle.Bold;
            metin = Yazi(Kutu(panel.transform, "Metin", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 100f), new Vector2(-30f, -70f)),
                font, string.Empty, 22, textColor);

            GameObject dugme = Kutu(panel.transform, "Topla", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            RectTransform drt = dugme.GetComponent<RectTransform>();
            drt.anchoredPosition = new Vector2(Reklam.Kurulu ? -140f : 0f, 52f);
            drt.sizeDelta = new Vector2(Reklam.Kurulu ? 250f : 260f, 66f);
            Image dresim = dugme.AddComponent<Image>();
            dresim.color = claimColor;
            Button b = dugme.AddComponent<Button>();
            b.targetGraphic = dresim;
            b.onClick.AddListener(Topla);
            Text dyazi = Yazi(Kutu(dugme.transform, "Yazi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), font, "Topla", 28, textColor);
            dyazi.fontStyle = FontStyle.Bold;

            if (Reklam.Kurulu) {
                // ödüllü reklam: kazancı iki katına çıkarır (günde sınırlı)
                GameObject reklam = Kutu(panel.transform, "IkiKat", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
                RectTransform rrt = reklam.GetComponent<RectTransform>();
                rrt.anchoredPosition = new Vector2(140f, 52f);
                rrt.sizeDelta = new Vector2(250f, 66f);
                Image rresim = reklam.AddComponent<Image>();
                rresim.color = adColor;
                reklamDugmesi = reklam.AddComponent<Button>();
                reklamDugmesi.targetGraphic = rresim;
                reklamDugmesi.onClick.AddListener(IkiKatIcinIzle);
                reklamYazisi = Yazi(Kutu(reklam.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f)), font, string.Empty, 20, textColor);
                durumYazisi = Yazi(Kutu(panel.transform, "Durum", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 1f), new Vector2(-20f, 17f)),
                    font, string.Empty, 15, new Color(0.75f, 0.7f, 0.62f, 1f));
                Reklam.Degisti += ReklamDugmesiniYenile;
            }
            panelRoot.SetActive(false);
        }

        private static readonly Color adColor = new Color(0.7f, 0.32f, 0.12f, 1f);
        private Button reklamDugmesi = null;
        private Text reklamYazisi = null;
        private Text durumYazisi = null;

        private void ReklamDugmesiniYenile() {
            if (reklamDugmesi == null || panelRoot == null || panelRoot.activeSelf == false) {
                return;
            }
            int kalan = Reklam.IstemciKalan(Reklam.Kazanc);
            reklamDugmesi.gameObject.SetActive(kalan > 0);
            reklamDugmesi.interactable = Reklam.Hazir;
            reklamYazisi.text = Reklam.Hazir ? "<b>Reklam izle · 2 kat</b>" : "Reklam hazırlanıyor...";
            durumYazisi.text = kalan > 0 ? "Reklamla iki kat: bugün " + kalan + " hakkın var." : string.Empty;
        }

        private void IkiKatIcinIzle() {
            MobileFeedback.Tap();
            string hata = Reklam.Goster(Reklam.Kazanc, IkiKatTopla);
            if (hata != null && durumYazisi != null) {
                durumYazisi.text = hata;
            }
        }

        /// <summary>reklam sonuna kadar izlendi: kazanç iki kat (hak sunucuda / tek oyunculu oyunda telefonda düşer)</summary>
        private void IkiKatTopla() {
            MobileFeedback.Success();
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (Cevrimici.Acik) {
                OtukenAg.Gonder("kazanc-topla", "2");
            } else if (oyuncu != null && oyuncu.DisplayName == karakter) {
                bool ikiKat = Reklam.HakKullan(oyuncu, Reklam.Kazanc);
                Ver(oyuncu, bekleyenGumus * (ikiKat ? 2 : 1), bekleyenTecrube * (ikiKat ? 2 : 1));
            }
            bekleyenGumus = 0;
            bekleyenTecrube = 0;
            panelRoot.SetActive(false);
            Reklam.DurumIste();
        }

        private void Ac(TimeSpan ara, bool sinirda, int gumus, int tecrube) {
            bekleyenGumus = gumus;
            bekleyenTecrube = tecrube;
            string sure = ara.TotalHours >= 1 ? $"{(int)ara.TotalHours} saat {ara.Minutes} dakika" : $"{ara.Minutes} dakika";
            metin.text = $"{sure} yoktun. Bu sürede obanın yiğitleri senin adına sınırı korudu ve ganimet topladı.\n\n"
                + $"<color=#FFD54A><b>{gumus} Gümüş Akçe</b></color>   ·   <color=#9FD8FF><b>{tecrube} tecrübe</b></color>"
                + (sinirda ? "\n<size=17>(En çok 10 saat sayılır.)</size>" : string.Empty);
            panelRoot.SetActive(true);
            MobileFeedback.Light();
            if (reklamDugmesi != null) {
                Reklam.Baslat();
                Reklam.DurumIste();
                ReklamDugmesiniYenile();
            }
        }

        private void Topla() {
            MobileFeedback.Success();
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (Cevrimici.Acik) {
                // çevrimiçi: kazancı sunucu verir
                OtukenAg.Gonder("kazanc-topla");
            } else if (oyuncu != null && oyuncu.DisplayName == karakter) {
                Ver(oyuncu, bekleyenGumus, bekleyenTecrube);
            }
            bekleyenGumus = 0;
            bekleyenTecrube = 0;
            panelRoot.SetActive(false);
        }

        private static GameObject Kutu(Transform ust, string ad, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject go = new GameObject(ad);
            go.transform.SetParent(ust, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        private static Text Yazi(GameObject hedef, Font font, string icerik, int boyut, Color renk) {
            Text t = hedef.AddComponent<Text>();
            t.font = font;
            t.text = icerik;
            t.fontSize = boyut;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = renk;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }
    }
}
