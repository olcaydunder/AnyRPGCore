using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Demirci (Metin2'deki demirci gibi): kuşanılmış ya da çantadaki eşya Gümüş Akçe ve Gök Taşı Parçası ile
    /// +1'den +9'a kadar güçlendirilir. Her basamağın başarı şansı vardır (+1 kesin, +9 %25); başarısızlıkta eşya
    /// bozulmaz, yalnız malzeme gider.
    /// Basamak eşyanın adında saklanır ("Demir Kılıç +3"): ad kayda zaten giriyor, kayıt biçimi değişmez.
    /// Gücü: eşyanın verdiği her temel değere (Güç, Çeviklik, Zekâ, Dayanıklılık) basamak başına seviyeye göre ek,
    /// zırhlara zırh, temel değeri olmayan silahlara hasar (CharacterStats.CalculateEquipmentChanged ekler ve çıkarır).
    /// Sol sütundaki "Demirci" düğmesiyle açılır. Kodla kurulur.
    /// Çevrimiçi oyunda yükseltmeyi sunucu yapar (malzeme, şans ve yeni ad sunucuda; "demirci" yanıtıyla telefona gelir).
    /// </summary>
    public class Demirci : MonoBehaviour {

        public const string CanvasName = "DemirciCanvas";
        private const int SortingOrder = 31;
        public const int EnYuksek = 9;
        public const string TasEsyasi = "Gok Tasi Parcasi";
        private const string TasAdi = "Gök Taşı Parçası";

        // basamağa çıkma şansı (yüzde), indeks: hedef basamak
        private static readonly int[] sanslar = { 100, 100, 95, 90, 80, 70, 60, 45, 35, 25 };

        private static readonly Regex basamakDeseni = new Regex(@"\s\+([1-9])$");

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color rowColor = new Color(0.16f, 0.12f, 0.08f, 0.95f);
        private static readonly Color rowSelectedColor = new Color(0.45f, 0.32f, 0.14f, 1f);
        private static readonly Color buttonColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color forgeColor = new Color(0.7f, 0.32f, 0.12f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color hintColor = new Color(0.75f, 0.7f, 0.62f, 1f);
        private static readonly Color okColor = new Color(0.55f, 0.9f, 0.45f, 1f);
        private static readonly Color errorColor = new Color(1f, 0.5f, 0.4f, 1f);

        // ---------------------------------------------------------------- kurallar

        /// <summary>eşyanın demirci basamağı (0-9)</summary>
        public static int Seviye(InstantiatedItem esya) {
            if (esya == null) {
                return 0;
            }
            Match m = basamakDeseni.Match(esya.DisplayName ?? string.Empty);
            return m.Success ? m.Groups[1].Value[0] - '0' : 0;
        }

        // Ölçü: 10. seviyede Güç ~100, fiziksel güç ~50 (Güç × 0,5). +9 silah fiziksel gücü ~%45, +9 takı/zırh eşyadaki
        // her temel değeri ~%14 artırır; zırh değeri düz hasar indirimi olduğu için küçük tutulur.

        /// <summary>basamak başına eşyadaki her temel değere eklenen</summary>
        public static float StatBonusu(int basamak, int seviye) {
            return basamak <= 0 ? 0f : Mathf.Ceil(basamak * Mathf.Max(1, seviye) * 0.15f);
        }

        public static float ZirhBonusu(int basamak, int seviye) {
            return basamak <= 0 ? 0f : Mathf.Ceil(basamak * (0.5f + Mathf.Max(1, seviye) * 0.1f));
        }

        public static float HasarBonusu(int basamak, int seviye) {
            return basamak <= 0 ? 0f : Mathf.Ceil(basamak * Mathf.Max(1, seviye) * 0.25f);
        }

        public enum Kazanc { Hasar, Zirh, Deger }

        /// <summary>silah hasar; temel değer veren zırh ve takı o değerleri; ötekiler zırh kazanır</summary>
        public static Kazanc KazancTuru(InstantiatedEquipment esya) {
            if (esya.Equipment is Weapon) {
                return Kazanc.Hasar;
            }
            if (esya.Equipment.PrimaryStats.Count > 0) {
                return Kazanc.Deger;
            }
            return Kazanc.Zirh;
        }

        public static string KazancYazisi(InstantiatedEquipment esya, int basamak, int seviye) {
            switch (KazancTuru(esya)) {
                case Kazanc.Hasar:
                    return "hasar +" + HasarBonusu(basamak, seviye);
                case Kazanc.Zirh:
                    return "zırh +" + ZirhBonusu(basamak, seviye);
                default:
                    return "eşyadaki her temel değer +" + StatBonusu(basamak, seviye);
            }
        }

        /// <summary>
        /// CharacterStats.CalculateEquipmentChanged çağırır: kuşanılan eşyanın basamak gücünü ekler (ekle) ya da çıkarır.
        /// Ekleme ve çıkarma aynı seviye ile yapılır; seviye atlayınca bütün eşya değerleri baştan hesaplanır.
        /// </summary>
        public static void Uygula(InstantiatedEquipment esya, int seviye, Dictionary<string, Stat> temel, Dictionary<SecondaryStatType, Stat> ikincil, bool ekle) {
            int basamak = Seviye(esya);
            if (basamak <= 0 || esya.Equipment == null) {
                return;
            }
            switch (KazancTuru(esya)) {
                case Kazanc.Hasar:
                    Degistir(ikincil, SecondaryStatType.Damage, HasarBonusu(basamak, seviye), ekle);
                    break;
                case Kazanc.Zirh:
                    Degistir(ikincil, SecondaryStatType.Armor, ZirhBonusu(basamak, seviye), ekle);
                    break;
                default:
                    float ek = StatBonusu(basamak, seviye);
                    foreach (ItemPrimaryStatNode dugum in esya.Equipment.PrimaryStats) {
                        if (temel.ContainsKey(dugum.StatName)) {
                            if (ekle) {
                                temel[dugum.StatName].AddModifier(ek);
                            } else {
                                temel[dugum.StatName].RemoveModifier(ek);
                            }
                        }
                    }
                    break;
            }
        }

        private static void Degistir(Dictionary<SecondaryStatType, Stat> ikincil, SecondaryStatType tur, float ek, bool ekle) {
            if (ikincil.ContainsKey(tur) == false) {
                return;
            }
            if (ekle) {
                ikincil[tur].AddModifier(ek);
            } else {
                ikincil[tur].RemoveModifier(ek);
            }
        }

        public static int GumusBedeli(int hedef) {
            return 6 * hedef * hedef;
        }

        public static int TasBedeli(int hedef) {
            return hedef <= 3 ? 0 : (hedef <= 6 ? 1 : 2);
        }

        public static int Sans(int hedef) {
            return sanslar[Mathf.Clamp(hedef, 0, sanslar.Length - 1)];
        }

        // demirci basamağına göre eşyanın değeri (satıcı fiyatı, pazar önerisi): +9 eşya sıradanın 14 katı eder
        private static readonly float[] degerler = { 1f, 1.3f, 1.7f, 2.2f, 3f, 4f, 5.5f, 7.5f, 10f, 14f };

        public static float DegerCarpani(InstantiatedItem esya) {
            return degerler[Mathf.Clamp(Seviye(esya), 0, degerler.Length - 1)];
        }

        private static void AdiYaz(InstantiatedItem esya, int basamak) {
            string temel = basamakDeseni.Replace(esya.DisplayName ?? esya.Item.DisplayName, string.Empty);
            esya.DisplayName = basamak > 0 ? temel + " +" + basamak : temel;
        }

        /// <summary>
        /// bir basamak yükseltmeyi dener; sonuç yazısını döndürür. hileli = oyun testi (malzeme ve şans yok sayılır)
        /// </summary>
        public static string Yukselt(UnitController oyuncu, InstantiatedEquipment esya, SystemGameManager oyun, bool hileli, out bool basarili) {
            return Yukselt(oyuncu, esya, oyun, hileli, false, out basarili);
        }

        /// <summary>kutsama: Kut Dükkânı'nın Demirci Kutsaması varsa biri harcanır, şans +KutsamaEki puan</summary>
        public static string Yukselt(UnitController oyuncu, InstantiatedEquipment esya, SystemGameManager oyun, bool hileli, bool kutsama, out bool basarili) {
            basarili = false;
            if (oyuncu == null || esya == null || oyun == null) {
                return "Eşya bulunamadı.";
            }
            int simdiki = Seviye(esya);
            if (simdiki >= EnYuksek) {
                return "Bu eşya zaten +" + EnYuksek + ".";
            }
            int hedef = simdiki + 1;
            Currency gumus = oyun.SystemDataFactory.GetResource<Currency>("Silver");
            int bedel = GumusBedeli(hedef);
            int tas = TasBedeli(hedef);
            if (hileli == false) {
                if (gumus == null || oyuncu.CharacterCurrencyManager.GetBaseCurrencyValue(gumus) < oyun.CurrencyConverter.GetBaseCurrencyAmount(gumus, bedel)) {
                    return "Yeterli Gümüş Akçen yok (" + bedel + " gerekli).";
                }
                List<InstantiatedItem> taslar = tas > 0 ? oyuncu.CharacterInventoryManager.GetItems(TasEsyasi, tas) : new List<InstantiatedItem>();
                if (taslar.Count < tas) {
                    return "Çantanda yeterli " + TasAdi + " yok (" + tas + " gerekli). Seviye, günlük görev ve günlük armağan ödüllerinde bulunur.";
                }
                oyuncu.CharacterCurrencyManager.SpendCurrency(gumus, bedel);
                foreach (InstantiatedItem t in taslar) {
                    oyuncu.CharacterInventoryManager.RemoveInventoryItem(t);
                }
                int sans = Sans(hedef);
                bool kutsandi = kutsama && KutDukkani.KutsamaHarca(oyuncu);
                if (kutsandi) {
                    sans = Mathf.Min(100, sans + KutDukkani.KutsamaEki);
                }
                if (Random.Range(0, 100) >= sans) {
                    MobileFeedback.Medium();
                    return "Başarısız! Örs kıvılcım saçtı ama eşya güçlenmedi. Eşyan sağlam, malzeme gitti." + (kutsandi ? " (kutsama kullanıldı)" : string.Empty);
                }
            }
            bool kusanili = KusaniliMi(oyuncu, esya);
            AdiYaz(esya, hedef);
            if (kusanili) {
                // kuşanılı eşyanın değerleri baştan hesaplanır (CharacterStats.CalculateEquipmentChanged yeni basamakla ekler)
                oyuncu.CharacterStats.CalculateEquipmentStats();
                oyuncu.CharacterStats.CalculatePrimaryStats();
            }
            basarili = true;
            MobileFeedback.Success();
            OtukenAg.Mesaj(oyuncu, "<color=#FFD54A>Demirci: " + esya.DisplayName + "!</color>");
            return "Başarılı! " + esya.DisplayName;
        }

        public static bool KusaniliMi(UnitController oyuncu, InstantiatedEquipment esya) {
            if (oyuncu == null || oyuncu.CharacterEquipmentManager == null) {
                return false;
            }
            foreach (EquipmentInventorySlot yuva in oyuncu.CharacterEquipmentManager.CurrentEquipment.Values) {
                if (yuva != null && yuva.InstantiatedEquipment == esya) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>kuşanılı ve çantadaki bütün eşyalar (kuşanılılar önce)</summary>
        public static List<InstantiatedEquipment> Esyalar(UnitController oyuncu) {
            List<InstantiatedEquipment> liste = new List<InstantiatedEquipment>();
            if (oyuncu == null) {
                return liste;
            }
            if (oyuncu.CharacterEquipmentManager != null) {
                foreach (EquipmentInventorySlot yuva in oyuncu.CharacterEquipmentManager.CurrentEquipment.Values) {
                    if (yuva != null && yuva.InstantiatedEquipment != null && liste.Contains(yuva.InstantiatedEquipment) == false) {
                        liste.Add(yuva.InstantiatedEquipment);
                    }
                }
            }
            if (oyuncu.CharacterInventoryManager != null) {
                foreach (InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                    InstantiatedEquipment esya = yuva != null && yuva.IsEmpty == false ? yuva.InstantiatedItem as InstantiatedEquipment : null;
                    if (esya != null && liste.Contains(esya) == false) {
                        liste.Add(esya);
                    }
                }
            }
            return liste;
        }

        // ---------------------------------------------------------------- pencere

        private static Demirci instance = null;

        private Font font = null;
        private GameObject panelRoot = null;
        private RectTransform liste = null;
        private Text gucYazisi = null;
        private Text adYazisi = null;
        private Text ayrintiYazisi = null;
        private Text sonucYazisi = null;
        private Button yukseltDugmesi = null;
        private Text yukseltYazisi = null;
        private readonly List<GameObject> satirlar = new List<GameObject>();
        private List<InstantiatedEquipment> esyalar = new List<InstantiatedEquipment>();
        private InstantiatedEquipment secili = null;
        private SystemGameManager oyun = null;
        private bool kutsamaAcik = false;
        private GameObject kutsamaDugmesi = null;

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        public static void Goster() {
            Ensure();
            instance.Ac();
        }

        // ---------------------------------------------------------------- çevrimiçi

        private bool bekliyor = false;

        /// <summary>bu süreçteki yükseltme denemeleri ve başarılar (ağ botu, oyun testi)</summary>
        public static int DenemeSayisi { get; private set; }
        public static int BasariSayisi { get; private set; }
        public static string SonSonuc { get; private set; } = "-";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            // sunucu: eşyayı kimliğinden bul, yükselt, yeni adı kaydet
            OtukenAg.SunucuIsle("demirci", (oyuncu, veri) => {
                InstantiatedEquipment esya = null;
                long kimlik;
                bool kutsama = veri.EndsWith("|k");
                if (kutsama) {
                    veri = veri.Substring(0, veri.Length - 2);
                }
                if (long.TryParse(veri, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out kimlik)) {
                    foreach (InstantiatedEquipment e in Esyalar(oyuncu)) {
                        if (e.InstanceId == kimlik) {
                            esya = e;
                            break;
                        }
                    }
                }
                bool basarili = false;
                SystemGameManager o = OtukenAg.Oyun;
                string sonuc = esya == null ? "Eşya bulunamadı." : Yukselt(oyuncu, esya, o, false, kutsama, out basarili);
                KutDukkani.DurumGonder(oyuncu);
                if (basarili) {
                    // basamak eşyanın adındadır: eşyalar karakter kaydıyla birlikte yazılır (PlayerCharacterSaveData),
                    // kayıt kirlensin ki yeni ad saklansın
                    oyuncu.UnitEventController.NotifyOnSaveDataUpdated();
                }
                Debug.Log("[Sunucu] " + oyuncu.DisplayName + " demirci: " + sonuc);
                OtukenAg.Yanitla(oyuncu, "demirci", (basarili ? "1" : "0") + "|" + (esya != null ? esya.InstanceId : 0)
                    + "|" + (esya != null ? esya.DisplayName : string.Empty) + "|" + sonuc);
            });
            // istemci: sonuç; başarılıysa telefondaki eşyanın adı ve değerleri yenilenir
            OtukenAg.IstemciDinle("demirci", veri => {
                string[] p = veri.Split(new char[] { '|' }, 4);
                if (p.Length < 4) {
                    return;
                }
                bool basarili = p[0] == "1";
                DenemeSayisi++;
                SonSonuc = p[3];
                SystemGameManager o = OtukenAg.Oyun;
                UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
                long kimlik;
                if (basarili && oyuncu != null && long.TryParse(p[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out kimlik)) {
                    BasariSayisi++;
                    foreach (InstantiatedEquipment e in Esyalar(oyuncu)) {
                        if (e.InstanceId == kimlik) {
                            e.DisplayName = p[2];
                            if (KusaniliMi(oyuncu, e)) {
                                oyuncu.CharacterStats.CalculateEquipmentStats();
                                oyuncu.CharacterStats.CalculatePrimaryStats();
                            }
                            break;
                        }
                    }
                    MobileFeedback.Success();
                } else {
                    MobileFeedback.Medium();
                }
                if (instance != null) {
                    instance.bekliyor = false;
                    instance.sonucYazisi.text = p[3];
                    instance.sonucYazisi.color = basarili ? okColor : errorColor;
                    if (IsOpen) {
                        instance.ListeyiKur();
                        instance.Yenile();
                    }
                }
            });
        }

        /// <summary>ağ botu: pencereyi açıp ilk eşyayı yükseltmeyi dener (çevrimiçinde sunucuya istek)</summary>
        public static void TestIcinYukselt() {
            Ensure();
            instance.Ac();
            instance.YukseltDugmesi();
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
            instance = canvasObject.AddComponent<Demirci>();
            instance.Build();
        }

        private UnitController Oyuncu {
            get {
                if (oyun == null) {
                    oyun = FindAnyObjectByType<SystemGameManager>();
                }
                return oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            }
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelRoot = Kutu(transform, "Demirci", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            GameObject panel = Kutu(panelRoot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-580f, -340f), new Vector2(580f, 340f));
            panel.AddComponent<Image>().color = panelColor;
            Outline cizgi = panel.AddComponent<Outline>();
            cizgi.effectColor = gold;
            cizgi.effectDistance = new Vector2(2f, -2f);

            Text baslik = Yazi(Kutu(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(24f, -62f), new Vector2(0f, -8f)),
                "DEMİRCİ", 34, TextAnchor.MiddleLeft, gold);
            baslik.fontStyle = FontStyle.Bold;
            gucYazisi = Yazi(Kutu(panel.transform, "Guc", new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(0f, -62f), new Vector2(-24f, -8f)),
                string.Empty, 24, TextAnchor.MiddleRight, gold);
            Yazi(Kutu(panel.transform, "Aciklama", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -98f), new Vector2(-24f, -60f)),
                "Eşyanı Gümüş Akçe ve " + TasAdi + " ile +9'a kadar güçlendir. Başarısızlıkta eşyan bozulmaz, yalnız malzeme gider.",
                18, TextAnchor.MiddleLeft, hintColor);

            // sol: eşya listesi
            GameObject cerceve = Kutu(panel.transform, "Liste", new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(20f, 88f), new Vector2(-10f, -106f));
            cerceve.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            cerceve.AddComponent<RectMask2D>();
            ScrollRect kaydirma = cerceve.AddComponent<ScrollRect>();
            GameObject icerik = Kutu(cerceve.transform, "Icerik", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            liste = icerik.GetComponent<RectTransform>();
            liste.pivot = new Vector2(0.5f, 1f);
            kaydirma.content = liste;
            kaydirma.horizontal = false;
            kaydirma.vertical = true;
            kaydirma.movementType = ScrollRect.MovementType.Clamped;
            kaydirma.scrollSensitivity = 30f;

            // sağ: seçili eşya
            adYazisi = Yazi(Kutu(panel.transform, "Ad", new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(14f, -160f), new Vector2(-24f, -110f)),
                string.Empty, 28, TextAnchor.MiddleLeft, textColor);
            adYazisi.fontStyle = FontStyle.Bold;
            ayrintiYazisi = Yazi(Kutu(panel.transform, "Ayrinti", new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(14f, 190f), new Vector2(-24f, -166f)),
                string.Empty, 21, TextAnchor.UpperLeft, textColor);
            sonucYazisi = Yazi(Kutu(panel.transform, "Sonuc", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(14f, 104f), new Vector2(-24f, 184f)),
                string.Empty, 22, TextAnchor.MiddleLeft, okColor);
            sonucYazisi.fontStyle = FontStyle.Bold;
            GameObject yukselt = Dugme(panel.transform, "Yükselt", new Vector2(0.75f, 0f), new Vector2(0f, 56f), new Vector2(280f, 72f), 30, forgeColor, YukseltDugmesi);
            yukseltDugmesi = yukselt.GetComponent<Button>();
            yukseltYazisi = yukselt.GetComponentInChildren<Text>();
            yukseltYazisi.fontStyle = FontStyle.Bold;
            Dugme(panel.transform, "Kapat", new Vector2(0.25f, 0f), new Vector2(0f, 48f), new Vector2(200f, 56f), 24, buttonColor, () => {
                MobileFeedback.Tap();
                panelRoot.SetActive(false);
            });
            // Kut Dükkânı'nın Demirci Kutsaması: açıkken denemede biri kullanılır (+15 puan şans)
            kutsamaDugmesi = Dugme(panel.transform, "Kutsama", new Vector2(0.75f, 0f), new Vector2(0f, 128f), new Vector2(280f, 46f), 18, buttonColor, () => {
                MobileFeedback.Tap();
                if (KutDukkani.IstemciKutsama <= 0) {
                    KutDukkani.Goster();
                    return;
                }
                kutsamaAcik = !kutsamaAcik;
                Yenile();
            });
            panelRoot.SetActive(false);
        }

        private void Ac() {
            UnitController oyuncu = Oyuncu;
            if (oyuncu == null) {
                return;
            }
            panelRoot.SetActive(true);
            sonucYazisi.text = string.Empty;
            esyalar = Esyalar(oyuncu);
            if (secili == null || esyalar.Contains(secili) == false) {
                secili = esyalar.Count > 0 ? esyalar[0] : null;
            }
            ListeyiKur();
            Yenile();
            MobileFeedback.Light();
        }

        private const float SatirBoyu = 66f;

        private void ListeyiKur() {
            foreach (GameObject s in satirlar) {
                Destroy(s);
            }
            satirlar.Clear();
            UnitController oyuncu = Oyuncu;
            for (int i = 0; i < esyalar.Count; i++) {
                InstantiatedEquipment esya = esyalar[i];
                Color renk = esya.ItemQuality != null ? esya.ItemQuality.QualityColor : textColor;
                Satir(i, esya.DisplayName, renk, esya.Icon, KusaniliMi(oyuncu, esya), esya == secili,
                    () => { MobileFeedback.Tap(); secili = esya; sonucYazisi.text = string.Empty; ListeyiKur(); Yenile(); });
            }
            liste.sizeDelta = new Vector2(0f, esyalar.Count * (SatirBoyu + 6f) + 6f);
        }

        private void Satir(int i, string ad, Color renk, Sprite simge, bool kusanili, bool seciliMi, UnityEngine.Events.UnityAction tik) {
            GameObject satir = Kutu(liste, "Esya", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -(i + 1) * (SatirBoyu + 6f)), new Vector2(-6f, -i * (SatirBoyu + 6f) - 6f));
            Image arka = satir.AddComponent<Image>();
            arka.color = seciliMi ? rowSelectedColor : rowColor;
            Button b = satir.AddComponent<Button>();
            b.targetGraphic = arka;
            b.onClick.AddListener(tik);
            GameObject simgeKutusu = Kutu(satir.transform, "Simge", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -26f), new Vector2(60f, 26f));
            Image simgeResmi = simgeKutusu.AddComponent<Image>();
            simgeResmi.sprite = simge;
            simgeResmi.preserveAspect = true;
            simgeResmi.raycastTarget = false;
            if (simge == null) {
                simgeResmi.color = new Color(0.3f, 0.25f, 0.2f, 1f);
            }
            Yazi(Kutu(satir.transform, "Ad", Vector2.zero, Vector2.one, new Vector2(70f, 0f), new Vector2(-8f, 0f)),
                "<color=#" + ColorUtility.ToHtmlStringRGB(renk) + ">" + ad + "</color>"
                + (kusanili ? "  <size=16><color=#B8C9A0>kuşanılı</color></size>" : string.Empty), 21, TextAnchor.MiddleLeft, textColor);
            satirlar.Add(satir);
        }

        /// <summary>editördeki arayüz önizlemesi (HaritaHazirlik): oyun olmadan örnek eşyalarla</summary>
        private void Onizleme() {
            panelRoot.SetActive(true);
            string[] adlar = { "Kurt Dişi Kılıç +4", "Demir Pullu Zırh +2", "Kartal Tüylü Miğfer", "Bozkır Çizmesi +1", "Gök Taşı Yüzüğü" };
            Color[] renkler = { new Color(0.35f, 0.6f, 1f), Color.white, new Color(0.45f, 0.85f, 0.35f), Color.white, new Color(0.7f, 0.45f, 0.95f) };
            for (int i = 0; i < adlar.Length; i++) {
                Satir(i, adlar[i], renkler[i], null, i < 4, i == 0, () => { });
            }
            liste.sizeDelta = new Vector2(0f, adlar.Length * (SatirBoyu + 6f) + 6f);
            gucYazisi.text = "Güç Puanı: 412";
            adYazisi.text = adlar[0];
            ayrintiYazisi.text = "+4  →  <color=#FFD54A><b>+5</b></color>\n"
                + "Kazanç: hasar +" + HasarBonusu(5, 8) + "\n"
                + "Başarı şansı: <b>%" + Sans(5) + "</b>\n\n"
                + "Bedel: <color=#E8E0D0>" + GumusBedeli(5) + " Gümüş Akçe</color>  (sende 214)\n"
                + "<color=#E8E0D0>" + TasBedeli(5) + " " + TasAdi + "</color>  (sende 3)";
            sonucYazisi.text = "Başarılı! Kurt Dişi Kılıç +4";
            sonucYazisi.color = okColor;
            yukseltDugmesi.interactable = true;
            yukseltYazisi.text = "Yükselt (+5)";
        }

        private void Yenile() {
            UnitController oyuncu = Oyuncu;
            gucYazisi.text = oyuncu != null ? "Güç Puanı: " + Gelisim.GucPuani(oyuncu) : string.Empty;
            if (secili == null || oyuncu == null) {
                adYazisi.text = "Eşyan yok";
                ayrintiYazisi.text = "Kuşandığın ya da çantandaki silah ve zırhlar burada listelenir.";
                yukseltDugmesi.interactable = false;
                return;
            }
            int simdiki = Seviye(secili);
            int seviye = oyuncu.CharacterStats.Level;
            adYazisi.text = secili.DisplayName;
            if (simdiki >= EnYuksek) {
                ayrintiYazisi.text = "En yüksek basamakta (+" + EnYuksek + ").\nKazanç: " + KazancYazisi(secili, simdiki, seviye);
                yukseltDugmesi.interactable = false;
                yukseltYazisi.text = "+" + EnYuksek;
                return;
            }
            int hedef = simdiki + 1;
            Currency gumus = oyun.SystemDataFactory.GetResource<Currency>("Silver");
            int varolanGumus = gumus != null ? oyuncu.CharacterCurrencyManager.GetBaseCurrencyValue(gumus) / Mathf.Max(1, oyun.CurrencyConverter.GetBaseCurrencyAmount(gumus, 1)) : 0;
            int varolanTas = oyuncu.CharacterInventoryManager.GetItems(TasEsyasi, 999).Count;
            int bedel = GumusBedeli(hedef);
            int tas = TasBedeli(hedef);
            bool yeter = varolanGumus >= bedel && varolanTas >= tas;
            string ek = KazancYazisi(secili, hedef, seviye);
            int kutsamaSayisi = KutDukkani.IstemciKutsama;
            if (kutsamaSayisi <= 0) {
                kutsamaAcik = false;
            }
            int sans = Sans(hedef);
            string sansYazisi = kutsamaAcik ? "<b>%" + Mathf.Min(100, sans + KutDukkani.KutsamaEki) + "</b>  <size=16>(%" + sans + " + %" + KutDukkani.KutsamaEki + " kutsama)</size>" : "<b>%" + sans + "</b>";
            if (kutsamaDugmesi != null) {
                kutsamaDugmesi.GetComponentInChildren<Text>().text = kutsamaSayisi <= 0 ? "Kutsama al (Kut Dükkânı)"
                    : "Kutsama: " + (kutsamaAcik ? "AÇIK" : "kapalı") + " (" + kutsamaSayisi + ")";
                kutsamaDugmesi.GetComponent<Image>().color = kutsamaAcik ? new Color(0.7f, 0.5f, 0.12f, 1f) : buttonColor;
            }
            ayrintiYazisi.text = "+" + simdiki + "  →  <color=#FFD54A><b>+" + hedef + "</b></color>\n"
                + "Kazanç: " + ek + "\n"
                + "Başarı şansı: " + sansYazisi + "\n\n"
                + "Bedel: <color=#" + (varolanGumus >= bedel ? "E8E0D0" : "FF7A66") + ">" + bedel + " Gümüş Akçe</color>  (sende " + varolanGumus + ")\n"
                + (tas > 0 ? "<color=#" + (varolanTas >= tas ? "E8E0D0" : "FF7A66") + ">" + tas + " " + TasAdi + "</color>  (sende " + varolanTas + ")" : TasAdi + " gerekmez");
            yukseltDugmesi.interactable = yeter;
            yukseltYazisi.text = "Yükselt (+" + hedef + ")";
        }

        private void YukseltDugmesi() {
            MobileFeedback.Tap();
            if (Cevrimici.Acik) {
                if (secili == null || bekliyor) {
                    return;
                }
                if (OtukenAg.Gonder("demirci", secili.InstanceId.ToString(System.Globalization.CultureInfo.InvariantCulture) + (kutsamaAcik ? "|k" : string.Empty))) {
                    if (kutsamaAcik) {
                        KutDukkani.IstemciKutsamaDus();
                    }
                    bekliyor = true;
                    sonucYazisi.text = "Örs çalışıyor...";
                    sonucYazisi.color = hintColor;
                } else {
                    sonucYazisi.text = "Sunucuya ulaşılamadı, biraz sonra yeniden dene.";
                    sonucYazisi.color = errorColor;
                }
                return;
            }
            bool basarili;
            string sonuc = Yukselt(Oyuncu, secili, oyun, false, kutsamaAcik, out basarili);
            sonucYazisi.text = sonuc;
            sonucYazisi.color = basarili ? okColor : errorColor;
            ListeyiKur();
            Yenile();
        }

        // ---------------------------------------------------------------- yapı taşları

        private GameObject Kutu(Transform ust, string ad, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject go = new GameObject(ad);
            go.transform.SetParent(ust, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        private Text Yazi(GameObject hedef, string icerik, int boyut, TextAnchor hiza, Color renk) {
            Text t = hedef.AddComponent<Text>();
            t.font = font;
            t.text = icerik;
            t.fontSize = boyut;
            t.alignment = hiza;
            t.color = renk;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private GameObject Dugme(Transform ust, string etiket, Vector2 anchor, Vector2 konum, Vector2 boyut, int yaziBoyu, Color renk, UnityEngine.Events.UnityAction tik) {
            GameObject go = Kutu(ust, etiket, anchor, anchor, Vector2.zero, Vector2.zero);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = konum;
            rt.sizeDelta = boyut;
            Image resim = go.AddComponent<Image>();
            resim.color = renk;
            Outline cizgi = go.AddComponent<Outline>();
            cizgi.effectColor = new Color(gold.r, gold.g, gold.b, 0.6f);
            cizgi.effectDistance = new Vector2(1f, -1f);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = resim;
            ColorBlock renkler = b.colors;
            renkler.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            b.colors = renkler;
            b.onClick.AddListener(tik);
            Yazi(Kutu(go.transform, "Yazi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), etiket, yaziBoyu, TextAnchor.MiddleCenter, textColor);
            return go;
        }
    }
}
