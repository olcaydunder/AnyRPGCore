using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Çanta kolaylıkları (Lineage 2M, Black Desert Mobile gibi oyunlardaki "sırala" ve "toplu sat"):
    ///  - Sırala: çantadaki yığınları birleştirir, eşyaları türe (donanım, iksir/yiyecek, kullanılanlar, heybe, görev,
    ///    tarif, malzeme, en sonda değersiz) ve sonra kaliteye, demirci basamağına, ada göre dizer. Eşyalar yuvalar
    ///    arasında olaysız taşınır (sayılar değişmediği için görev sayaçları "1/5, 2/5" diye yeniden yazmaz).
    ///  - Toplu Sat: satıcıya gitmeden değersiz (gri) eşyaları ve giydiğinden zayıf ya da kullanamadığın sıradan/yeşil
    ///    (seçilirse mavi) donanımı satıcı fiyatına satar. Demirciden geçmiş, ileri seviye ve destansı üstü eşyalar
    ///    hiç önerilmez; listede dokunulan eşya "tutulacak" olur.
    /// Düğmeler çanta penceresinde para satırının altında (InventoryPanel). Pencere kodla kurulur.
    /// </summary>
    public class Canta : MonoBehaviour {

        public const string CanvasName = "TopluSatisCanvas";
        private const int SortingOrder = 31;

        private static readonly Dictionary<string, int> kaliteSirasi = new Dictionary<string, int>() {
            { "Poor", 0 }, { "Common", 1 }, { "UnCommon", 2 }, { "Rare", 3 }, { "Epic", 4 }, { "Legendary", 5 }, { "Heirloom", 6 }
        };

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        private static readonly Color rowColor = new Color(0.16f, 0.12f, 0.08f, 0.95f);
        private static readonly Color rowKeepColor = new Color(0.1f, 0.09f, 0.08f, 0.9f);
        private static readonly Color buttonColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color toggleOnColor = new Color(0.45f, 0.32f, 0.14f, 1f);
        private static readonly Color sellColor = new Color(0.55f, 0.4f, 0.18f, 1f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);
        private static readonly Color hintColor = new Color(0.75f, 0.7f, 0.62f, 1f);

        private static SystemGameManager oyun = null;

        private static SystemGameManager Oyun {
            get {
                if (oyun == null) {
                    oyun = FindAnyObjectByType<SystemGameManager>();
                }
                return oyun;
            }
        }

        private static UnitController Oyuncu {
            get {
                SystemGameManager o = Oyun;
                return o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            }
        }

        /// <summary>"1 Gümüş Akçe 24 Bakır Akçe" (sıfır olan birimler yazılmaz)</summary>
        public static string FiyatYazisi(Currency para, int miktar) {
            SystemGameManager o = Oyun;
            if (para == null) {
                return string.Empty;
            }
            if (o == null || o.CurrencyConverter == null) {
                return miktar + " " + para.DisplayName;
            }
            List<string> parcalar = new List<string>();
            foreach (KeyValuePair<Currency, int> k in o.CurrencyConverter.RedistributeCurrency(para, miktar)) {
                if (k.Value > 0 && k.Key != null) {
                    parcalar.Add(k.Value + " " + k.Key.DisplayName);
                }
            }
            return parcalar.Count > 0 ? string.Join(" ", parcalar) : "0 " + para.DisplayName;
        }

        public static int KaliteSirasi(InstantiatedItem esya) {
            int sira;
            if (esya != null && esya.ItemQuality != null && kaliteSirasi.TryGetValue(esya.ItemQuality.ResourceName, out sira)) {
                return sira;
            }
            return 1;
        }

        // ---------------------------------------------------------------- sıralama

        /// <summary>sıralamadaki tür kümesi (küçük önce)</summary>
        public static int Kategori(InstantiatedItem esya) {
            if (Ganimet.IsJunk(esya)) {
                return 9;
            }
            if (esya is InstantiatedEquipment) {
                return 0;
            }
            if (esya is InstantiatedPowerResourcePotion || esya is InstantiatedFood) {
                return 1;
            }
            if (esya is InstantiatedActionItem || esya is InstantiatedCastableItem) {
                return 2;
            }
            if (esya is InstantiatedBag) {
                return 3;
            }
            if (esya is InstantiatedQuestStartItem) {
                return 4;
            }
            if (esya is InstantiatedRecipeItem) {
                return 5;
            }
            return 6;
        }

        private static int Karsilastir(List<InstantiatedItem> a, List<InstantiatedItem> b) {
            InstantiatedItem x = a[0];
            InstantiatedItem y = b[0];
            int fark = Kategori(x).CompareTo(Kategori(y));
            if (fark != 0) {
                return fark;
            }
            InstantiatedEquipment ex = x as InstantiatedEquipment;
            InstantiatedEquipment ey = y as InstantiatedEquipment;
            if (ex != null && ey != null) {
                string tx = ex.Equipment.EquipmentSlotType != null ? ex.Equipment.EquipmentSlotType.ResourceName : string.Empty;
                string ty = ey.Equipment.EquipmentSlotType != null ? ey.Equipment.EquipmentSlotType.ResourceName : string.Empty;
                fark = string.CompareOrdinal(tx, ty);
                if (fark != 0) {
                    return fark;
                }
            }
            fark = KaliteSirasi(y).CompareTo(KaliteSirasi(x));
            if (fark != 0) {
                return fark;
            }
            fark = Demirci.Seviye(y).CompareTo(Demirci.Seviye(x));
            if (fark != 0) {
                return fark;
            }
            fark = string.Compare(x.DisplayName, y.DisplayName, System.StringComparison.CurrentCulture);
            if (fark != 0) {
                return fark;
            }
            fark = b.Count.CompareTo(a.Count);
            if (fark != 0) {
                return fark;
            }
            return x.InstanceId.CompareTo(y.InstanceId);
        }

        /// <summary>
        /// çantayı sıralar; dolu yuva sayısını döndürür (-1: yapılamadı). Banka ve kuşanılanlar değişmez.
        /// </summary>
        public static int Sirala(UnitController oyuncu, bool mesaj = true) {
            if (oyuncu == null || oyuncu.CharacterInventoryManager == null) {
                return -1;
            }
            SystemGameManager o = Oyun;
            if (o != null && o.GameMode != GameMode.Local) {
                if (mesaj) {
                    oyuncu.WriteMessageFeedMessage("Sıralama yalnız tek oyunculu oyunda var");
                }
                return -1;
            }
            CharacterInventoryManager canta = oyuncu.CharacterInventoryManager;
            if (canta.FromSlot != null) {
                // elde taşınan bir eşya var: yerine bırakılmadan sıralanmaz
                return -1;
            }
            List<InventorySlot> yuvalar = new List<InventorySlot>(canta.InventorySlots);

            // yığınları birleştir (aynı eşya), tekleri ayrı tut
            Dictionary<string, List<InstantiatedItem>> yiginlar = new Dictionary<string, List<InstantiatedItem>>();
            List<List<InstantiatedItem>> parcalar = new List<List<InstantiatedItem>>();
            int toplam = 0;
            foreach (InventorySlot yuva in yuvalar) {
                if (yuva == null || yuva.IsEmpty) {
                    continue;
                }
                foreach (InstantiatedItem esya in yuva.InstantiatedItems.Values) {
                    toplam++;
                    if (esya.Item.MaximumStackSize > 1) {
                        List<InstantiatedItem> liste;
                        if (yiginlar.TryGetValue(esya.Item.ResourceName, out liste) == false) {
                            liste = new List<InstantiatedItem>();
                            yiginlar.Add(esya.Item.ResourceName, liste);
                        }
                        liste.Add(esya);
                    } else {
                        parcalar.Add(new List<InstantiatedItem>() { esya });
                    }
                }
            }
            foreach (List<InstantiatedItem> liste in yiginlar.Values) {
                int boy = Mathf.Max(1, liste[0].Item.MaximumStackSize);
                for (int i = 0; i < liste.Count; i += boy) {
                    parcalar.Add(liste.GetRange(i, Mathf.Min(boy, liste.Count - i)));
                }
            }
            if (parcalar.Count > yuvalar.Count) {
                return -1;
            }
            parcalar.Sort(Karsilastir);

            for (int i = 0; i < yuvalar.Count; i++) {
                InventorySlot yuva = yuvalar[i];
                if (yuva == null) {
                    continue;
                }
                Dictionary<long, InstantiatedItem> yeni = new Dictionary<long, InstantiatedItem>();
                if (i < parcalar.Count) {
                    foreach (InstantiatedItem esya in parcalar[i]) {
                        yeni[esya.InstanceId] = esya;
                    }
                }
                if (yeni.Count == 0 && yuva.IsEmpty) {
                    continue;
                }
                // InstantiatedItems'a atamak yalnız yuvayı yeniler (ekleme/çıkarma olayı yok)
                yuva.InstantiatedItems = yeni;
            }
            if (mesaj) {
                oyuncu.WriteMessageFeedMessage("Çanta sıralandı (" + toplam + " eşya, " + parcalar.Count + " yuva)");
                MobileFeedback.Success();
            }
            return parcalar.Count;
        }

        // ---------------------------------------------------------------- toplu satış

        public static bool Satilabilir(InstantiatedItem esya, UnitController oyuncu) {
            if (esya == null || esya.Item == null || oyuncu == null) {
                return false;
            }
            if (esya.Item.BuyPrice(oyuncu) <= 0) {
                return false;
            }
            KeyValuePair<Currency, int> fiyat = esya.Item.GetSellPrice(esya, oyuncu);
            return fiyat.Key != null && fiyat.Value > 0;
        }

        /// <summary>
        /// toplu satışa önerilecek eşyalar ve neden yazıları. gri: değersizler; zayif: sıradan/yeşil donanım; nadir: mavi donanım
        /// </summary>
        public static List<KeyValuePair<InstantiatedItem, string>> Adaylar(UnitController oyuncu, bool gri, bool zayif, bool nadir) {
            List<KeyValuePair<InstantiatedItem, string>> sonuc = new List<KeyValuePair<InstantiatedItem, string>>();
            if (oyuncu == null || oyuncu.CharacterInventoryManager == null) {
                return sonuc;
            }
            int seviye = oyuncu.CharacterStats.Level;
            foreach (InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                if (yuva == null || yuva.IsEmpty) {
                    continue;
                }
                foreach (InstantiatedItem esya in yuva.InstantiatedItems.Values) {
                    if (esya is InstantiatedQuestStartItem || esya is InstantiatedBag || Satilabilir(esya, oyuncu) == false) {
                        continue;
                    }
                    if (Ganimet.IsJunk(esya)) {
                        if (gri) {
                            sonuc.Add(new KeyValuePair<InstantiatedItem, string>(esya, "değersiz"));
                        }
                        continue;
                    }
                    InstantiatedEquipment donanim = esya as InstantiatedEquipment;
                    if (donanim == null || Demirci.Seviye(donanim) > 0) {
                        continue;
                    }
                    int kalite = KaliteSirasi(donanim);
                    if ((kalite <= 2 && zayif == false) || (kalite == 3 && nadir == false) || kalite >= 4) {
                        continue;
                    }
                    // ileri seviyenin eşyası: sonra işe yarar
                    if (donanim.GetItemLevel(seviye) > seviye) {
                        continue;
                    }
                    if (donanim.Equipment.CanEquip(donanim.GetItemLevel(seviye), oyuncu) == false) {
                        sonuc.Add(new KeyValuePair<InstantiatedItem, string>(esya, "kullanamazsın"));
                        continue;
                    }
                    if (Gelisim.DahaIyiMi(donanim, oyuncu) <= 0f) {
                        sonuc.Add(new KeyValuePair<InstantiatedItem, string>(esya, "giydiğinden zayıf"));
                    }
                }
            }
            return sonuc;
        }

        /// <summary>eşyaları satıcı fiyatına satar; satılan sayısını döndürür, kazancı yazar</summary>
        public static int Sat(UnitController oyuncu, List<InstantiatedItem> esyalar, out string kazanc) {
            kazanc = string.Empty;
            if (oyuncu == null || esyalar == null) {
                return 0;
            }
            Dictionary<Currency, int> toplam = new Dictionary<Currency, int>();
            int adet = 0;
            foreach (InstantiatedItem esya in esyalar) {
                if (esya == null || esya.Slot == null || esya.Slot.InstantiatedItems.ContainsValue(esya) == false || Satilabilir(esya, oyuncu) == false) {
                    continue;
                }
                KeyValuePair<Currency, int> fiyat = esya.Item.GetSellPrice(esya, oyuncu);
                oyuncu.CharacterCurrencyManager.AddCurrency(fiyat.Key, fiyat.Value);
                esya.Slot.RemoveItem(esya);
                int onceki;
                toplam.TryGetValue(fiyat.Key, out onceki);
                toplam[fiyat.Key] = onceki + fiyat.Value;
                adet++;
            }
            List<string> yazilar = new List<string>();
            foreach (KeyValuePair<Currency, int> k in toplam) {
                yazilar.Add(FiyatYazisi(k.Key, k.Value));
            }
            kazanc = string.Join(", ", yazilar);
            if (adet > 0) {
                oyuncu.WriteMessageFeedMessage("<color=#FFD54A>" + adet + " eşya satıldı: +" + kazanc + "</color>");
                MobileFeedback.Success();
            }
            return adet;
        }

        // ---------------------------------------------------------------- pencere

        private static Canta instance = null;

        private Font font = null;
        private GameObject panelRoot = null;
        private RectTransform liste = null;
        private Text toplamYazisi = null;
        private Text bosYazisi = null;
        private Button satDugmesi = null;
        private Text satYazisi = null;
        private Image griDugmesi = null;
        private Image zayifDugmesi = null;
        private Image nadirDugmesi = null;
        private bool gri = true;
        private bool zayif = true;
        private bool nadir = false;
        private readonly List<GameObject> satirlar = new List<GameObject>();
        private readonly HashSet<long> tutulacaklar = new HashSet<long>();
        private List<KeyValuePair<InstantiatedItem, string>> adaylar = new List<KeyValuePair<InstantiatedItem, string>>();

        public static bool IsOpen {
            get { return instance != null && instance.panelRoot != null && instance.panelRoot.activeSelf; }
        }

        /// <summary>çanta penceresindeki "Toplu Sat" düğmesi</summary>
        public static void TopluSatisGoster() {
            if (Oyuncu == null) {
                return;
            }
            Ensure();
            instance.tutulacaklar.Clear();
            instance.panelRoot.SetActive(true);
            instance.Yenile();
            MobileFeedback.Light();
        }

        /// <summary>çanta penceresindeki "Sırala" düğmesi</summary>
        public static void SiralaDugmesi() {
            MobileFeedback.Tap();
            Sirala(Oyuncu);
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
            instance = canvasObject.AddComponent<Canta>();
            instance.Build();
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelRoot = Kutu(transform, "TopluSatis", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            panelRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            GameObject panel = Kutu(panelRoot.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, -340f), new Vector2(520f, 340f));
            panel.AddComponent<Image>().color = panelColor;
            Outline cizgi = panel.AddComponent<Outline>();
            cizgi.effectColor = gold;
            cizgi.effectDistance = new Vector2(2f, -2f);

            Text baslik = Yazi(Kutu(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -62f), new Vector2(-24f, -8f)),
                "TOPLU SAT", 34, TextAnchor.MiddleLeft, gold);
            baslik.fontStyle = FontStyle.Bold;
            Yazi(Kutu(panel.transform, "Aciklama", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -96f), new Vector2(-24f, -60f)),
                "Satıcıya gitmeden satıcı fiyatına satılır. Tutmak istediğin eşyaya dokun. Demirciden geçmiş eşyalar listelenmez.",
                18, TextAnchor.MiddleLeft, hintColor);

            // seçimler
            griDugmesi = Dugme(panel.transform, "Değersiz (gri)", new Vector2(0f, 1f), new Vector2(150f, -128f), new Vector2(250f, 50f), 20, toggleOnColor,
                () => { gri = !gri; MobileFeedback.Tap(); Yenile(); }).GetComponent<Image>();
            zayifDugmesi = Dugme(panel.transform, "Zayıf beyaz/yeşil", new Vector2(0f, 1f), new Vector2(412f, -128f), new Vector2(250f, 50f), 20, toggleOnColor,
                () => { zayif = !zayif; MobileFeedback.Tap(); Yenile(); }).GetComponent<Image>();
            nadirDugmesi = Dugme(panel.transform, "Zayıf mavi", new Vector2(0f, 1f), new Vector2(674f, -128f), new Vector2(250f, 50f), 20, buttonColor,
                () => { nadir = !nadir; MobileFeedback.Tap(); Yenile(); }).GetComponent<Image>();

            GameObject cerceve = Kutu(panel.transform, "Liste", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 100f), new Vector2(-20f, -162f));
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
            bosYazisi = Yazi(Kutu(cerceve.transform, "Bos", Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f)),
                string.Empty, 22, TextAnchor.MiddleCenter, hintColor);

            toplamYazisi = Yazi(Kutu(panel.transform, "Toplam", new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(24f, 22f), new Vector2(0f, 90f)),
                string.Empty, 22, TextAnchor.MiddleLeft, textColor);
            GameObject sat = Dugme(panel.transform, "Sat", new Vector2(1f, 0f), new Vector2(-180f, 52f), new Vector2(300f, 64f), 26, sellColor, SatDugmesi);
            satDugmesi = sat.GetComponent<Button>();
            satYazisi = sat.GetComponentInChildren<Text>();
            satYazisi.fontStyle = FontStyle.Bold;
            Dugme(panel.transform, "Kapat", new Vector2(1f, 0f), new Vector2(-440f, 52f), new Vector2(180f, 56f), 22, buttonColor, () => {
                MobileFeedback.Tap();
                panelRoot.SetActive(false);
            });
            panelRoot.SetActive(false);
        }

        private void Yenile() {
            UnitController oyuncu = Oyuncu;
            griDugmesi.color = gri ? toggleOnColor : buttonColor;
            zayifDugmesi.color = zayif ? toggleOnColor : buttonColor;
            nadirDugmesi.color = nadir ? toggleOnColor : buttonColor;
            adaylar = Adaylar(oyuncu, gri, zayif, nadir);
            foreach (GameObject s in satirlar) {
                Destroy(s);
            }
            satirlar.Clear();
            Dictionary<Currency, int> toplam = new Dictionary<Currency, int>();
            int satilacak = 0;
            for (int i = 0; i < adaylar.Count; i++) {
                InstantiatedItem esya = adaylar[i].Key;
                bool tut = tutulacaklar.Contains(esya.InstanceId);
                KeyValuePair<Currency, int> fiyat = esya.Item.GetSellPrice(esya, oyuncu);
                if (tut == false) {
                    satilacak++;
                    int onceki;
                    toplam.TryGetValue(fiyat.Key, out onceki);
                    toplam[fiyat.Key] = onceki + fiyat.Value;
                }
                Color renk = esya.ItemQuality != null ? esya.ItemQuality.QualityColor : textColor;
                string fiyatYazisi = FiyatYazisi(fiyat.Key, fiyat.Value);
                long kimlik = esya.InstanceId;
                Satir(i, esya.DisplayName, renk, esya.Icon, adaylar[i].Value, fiyatYazisi, tut, () => {
                    MobileFeedback.Tap();
                    if (tutulacaklar.Remove(kimlik) == false) {
                        tutulacaklar.Add(kimlik);
                    }
                    Yenile();
                });
            }
            liste.sizeDelta = new Vector2(0f, adaylar.Count * (SatirBoyu + 6f) + 6f);
            bosYazisi.text = adaylar.Count == 0 ? "Satılacak eşya yok. Seçimleri değiştirebilirsin." : string.Empty;
            List<string> yazilar = new List<string>();
            foreach (KeyValuePair<Currency, int> k in toplam) {
                yazilar.Add(FiyatYazisi(k.Key, k.Value));
            }
            toplamYazisi.text = satilacak > 0 ? satilacak + " eşya  ·  <color=#FFD54A><b>+" + string.Join(", ", yazilar) + "</b></color>" : string.Empty;
            satDugmesi.interactable = satilacak > 0;
            satYazisi.text = satilacak > 0 ? "Sat (" + satilacak + ")" : "Sat";
        }

        private void SatDugmesi() {
            MobileFeedback.Tap();
            List<InstantiatedItem> satilacaklar = new List<InstantiatedItem>();
            foreach (KeyValuePair<InstantiatedItem, string> aday in adaylar) {
                if (tutulacaklar.Contains(aday.Key.InstanceId) == false) {
                    satilacaklar.Add(aday.Key);
                }
            }
            string kazanc;
            Sat(Oyuncu, satilacaklar, out kazanc);
            Yenile();
        }

        private const float SatirBoyu = 60f;

        private void Satir(int i, string ad, Color renk, Sprite simge, string neden, string fiyat, bool tut, UnityEngine.Events.UnityAction tik) {
            GameObject satir = Kutu(liste, "Esya", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(6f, -(i + 1) * (SatirBoyu + 6f)), new Vector2(-6f, -i * (SatirBoyu + 6f) - 6f));
            Image arka = satir.AddComponent<Image>();
            arka.color = tut ? rowKeepColor : rowColor;
            Button b = satir.AddComponent<Button>();
            b.targetGraphic = arka;
            b.onClick.AddListener(tik);
            GameObject simgeKutusu = Kutu(satir.transform, "Simge", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, -24f), new Vector2(56f, 24f));
            Image simgeResmi = simgeKutusu.AddComponent<Image>();
            simgeResmi.sprite = simge;
            simgeResmi.preserveAspect = true;
            simgeResmi.raycastTarget = false;
            simgeResmi.color = simge == null ? new Color(0.3f, 0.25f, 0.2f, 1f) : (tut ? new Color(1f, 1f, 1f, 0.4f) : Color.white);
            string adRengi = ColorUtility.ToHtmlStringRGB(tut ? Color.Lerp(renk, Color.gray, 0.6f) : renk);
            Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.68f, 1f), new Vector2(66f, 0f), Vector2.zero),
                "<color=#" + adRengi + ">" + ad + "</color>  <size=16><color=#B8A890>" + (tut ? "tutulacak" : neden) + "</color></size>",
                21, TextAnchor.MiddleLeft, textColor);
            Yazi(Kutu(satir.transform, "Fiyat", new Vector2(0.68f, 0f), Vector2.one, Vector2.zero, new Vector2(-12f, 0f)),
                tut ? "—" : "+" + fiyat, 19, TextAnchor.MiddleRight, tut ? hintColor : gold);
            satirlar.Add(satir);
        }

        /// <summary>editördeki arayüz önizlemesi (HaritaHazirlik): oyun olmadan örnek eşyalarla</summary>
        private void Onizleme() {
            panelRoot.SetActive(true);
            string[] adlar = { "Kırık Ok Ucu", "Paslı Hançer", "Yırtık Deri Eldiven", "Kurt Postu Başlık", "Bakır Yüzük" };
            string[] nedenler = { "değersiz", "değersiz", "giydiğinden zayıf", "kullanamazsın", "giydiğinden zayıf" };
            string[] fiyatlar = { "2 Bakır Akçe", "3 Bakır Akçe", "14 Bakır Akçe", "22 Bakır Akçe", "1 Gümüş Akçe 5 Bakır Akçe" };
            Color[] renkler = { Color.gray, Color.gray, Color.white, new Color(0.45f, 0.85f, 0.35f), Color.white };
            for (int i = 0; i < adlar.Length; i++) {
                Satir(i, adlar[i], renkler[i], null, nedenler[i], fiyatlar[i], i == 3, () => { });
            }
            liste.sizeDelta = new Vector2(0f, adlar.Length * (SatirBoyu + 6f) + 6f);
            toplamYazisi.text = "4 eşya  ·  <color=#FFD54A><b>+1 Gümüş Akçe 24 Bakır Akçe</b></color>";
            satYazisi.text = "Sat (4)";
            nadirDugmesi.color = buttonColor;
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

        /// <summary>
        /// çanta penceresinin (InventoryPanel) para satırının altına "Sırala" ve "Toplu Sat" düğmelerini ekler.
        /// Panelin dikey yerleşimine katılan bir satırdır; pencere kendi boyunu buna göre ayarlar.
        /// sonra: altına girilecek gösterge (para çubuğu); bulunamazsa ağırlık satırının (InfoRow) altı
        /// </summary>
        public static void CantaDugmeleriniEkle(Transform panel, Transform sonra = null) {
            if (panel == null || panel.Find("CantaAraclari") != null) {
                return;
            }
            Font yaziTipi = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject satir = new GameObject("CantaAraclari", typeof(RectTransform));
            satir.transform.SetParent(panel, false);
            Transform bilgi = sonra != null ? sonra : AltNesne(panel, "InfoRow");
            while (bilgi != null && bilgi.parent != panel) {
                bilgi = bilgi.parent;
            }
            if (bilgi != null && bilgi != satir.transform) {
                satir.transform.SetSiblingIndex(bilgi.GetSiblingIndex() + 1);
            }
            LayoutElement yer = satir.AddComponent<LayoutElement>();
            yer.minHeight = 34f;
            yer.preferredHeight = 34f;
            yer.flexibleHeight = 0f;
            HorizontalLayoutGroup dizi = satir.AddComponent<HorizontalLayoutGroup>();
            dizi.padding = new RectOffset(4, 4, 3, 3);
            dizi.spacing = 6f;
            dizi.childControlWidth = true;
            dizi.childControlHeight = true;
            dizi.childForceExpandWidth = true;
            dizi.childForceExpandHeight = true;
            KucukDugme(satir.transform, yaziTipi, "Sırala", SiralaDugmesi);
            KucukDugme(satir.transform, yaziTipi, "Toplu Sat", () => { MobileFeedback.Tap(); TopluSatisGoster(); });
        }

        private static Transform AltNesne(Transform kok, string ad) {
            foreach (Transform t in kok.GetComponentsInChildren<Transform>(true)) {
                if (t.name == ad) {
                    return t;
                }
            }
            return null;
        }

        private static void KucukDugme(Transform ust, Font yaziTipi, string etiket, UnityEngine.Events.UnityAction tik) {
            GameObject go = new GameObject(etiket, typeof(RectTransform));
            go.transform.SetParent(ust, false);
            Image resim = go.AddComponent<Image>();
            resim.color = sellColor;
            Outline cizgi = go.AddComponent<Outline>();
            cizgi.effectColor = new Color(gold.r, gold.g, gold.b, 0.7f);
            cizgi.effectDistance = new Vector2(1f, -1f);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = resim;
            b.onClick.AddListener(tik);
            GameObject yazi = new GameObject("Yazi", typeof(RectTransform));
            yazi.transform.SetParent(go.transform, false);
            RectTransform rt = yazi.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Text t = yazi.AddComponent<Text>();
            t.font = yaziTipi;
            t.text = etiket;
            t.fontSize = 15;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = textColor;
            t.raycastTarget = false;
        }
    }
}
