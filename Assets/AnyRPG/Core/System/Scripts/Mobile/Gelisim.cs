using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Gelişim (mobil RPG'lerde yaygın olan ilerleme yardımcıları):
    ///  - Güç Puanı: karakterin gücünü tek sayıyla gösterir (temel değerler + zırh + hasar + seviye); artınca bildirilir.
    ///  - Seviye ödülleri: belirli seviyelere ulaşınca bir kez verilen armağanlar (karakter başına PlayerPrefs'te).
    ///  - Daha iyi eşya: çantaya giren eşya kuşanılandan güçlüyse "Kuşan" kartı çıkar.
    ///  - Acemi koruması: 5. seviyeye kadar oyuncu daha az hasar alır (CharacterCombat.TakeDamageCommon).
    /// Kartlar sol sütunun sağında, oyunu durdurmadan görünür. MobileBootstrap saniyede bir Tick çağırır. Kodla kurulur.
    /// </summary>
    public class Gelisim : MonoBehaviour {

        public const string CanvasName = "GelisimCanvas";
        private const int SortingOrder = 27;
        private const string OdulAnahtari = "seviye-odulu-";

        public const int AcemiSeviyesi = 5;
        public const float AcemiHasarCarpani = 0.6f;

        private static readonly Color gold = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color panelColor = new Color(0.09f, 0.07f, 0.05f, 0.94f);
        private static readonly Color equipColor = new Color(0.32f, 0.5f, 0.2f, 1f);
        private static readonly Color buttonColor = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        private static readonly Color textColor = new Color(0.96f, 0.92f, 0.84f, 1f);

        private struct Odul {
            public int seviye;
            public string yazi;
            public string para;
            public int paraMiktari;
            public string esya;
            public int esyaSayisi;
            public string para2;
            public int para2Miktari;

            public Odul(int seviye, string yazi, string para, int paraMiktari, string esya, int esyaSayisi, string para2 = null, int para2Miktari = 0) {
                this.seviye = seviye;
                this.yazi = yazi;
                this.para = para;
                this.paraMiktari = paraMiktari;
                this.esya = esya;
                this.esyaSayisi = esyaSayisi;
                this.para2 = para2;
                this.para2Miktari = para2Miktari;
            }
        }

        // kaynak adları (görünen adlar değil)
        private static readonly Odul[] oduller = {
            new Odul(2, "5 Şifa İksiri", null, 0, "Health Potion", 5),
            new Odul(3, "20 Gümüş Akçe", "Silver", 20, null, 0),
            new Odul(4, "3 Mana İksiri + 10 Gümüş Akçe", "Silver", 10, "Mana Potion", 3),
            new Odul(5, "Gök Taşı Parçası + 25 Gümüş Akçe (acemi koruması bitti)", "Silver", 25, Demirci.TasEsyasi, 1),
            new Odul(7, "40 Gümüş Akçe + 5 Şifa İksiri", "Silver", 40, "Health Potion", 5),
            new Odul(10, "2 Gök Taşı Parçası + 1 Altın", "Gold", 1, Demirci.TasEsyasi, 2),
            new Odul(12, "80 Gümüş Akçe + 5 Mana İksiri", "Silver", 80, "Mana Potion", 5),
            new Odul(15, "3 Gök Taşı Parçası + 2 Altın", "Gold", 2, Demirci.TasEsyasi, 3),
            new Odul(20, "5 Gök Taşı Parçası + 5 Altın", "Gold", 5, Demirci.TasEsyasi, 5),
            new Odul(25, "6 Gök Taşı Parçası + 8 Altın", "Gold", 8, Demirci.TasEsyasi, 6),
            new Odul(30, "8 Gök Taşı Parçası + 12 Altın", "Gold", 12, Demirci.TasEsyasi, 8),
        };

        private class Kart {
            public string baslik;
            public string yazi;
            public Sprite simge;
            public InstantiatedEquipment esya;
            public float sure;
        }

        private static Gelisim instance = null;
        private static SystemGameManager oyun = null;
        private static string karakter = string.Empty;
        private static float karakterZamani = -1f;
        private static int sonGuc = -1;
        private static readonly HashSet<long> bilinenler = new HashSet<long>();
        private static bool bilinenHazir = false;
        private static readonly Queue<Kart> kuyruk = new Queue<Kart>();
        private static bool cantaUyarisi = false;

        /// <summary>oyun testi okur</summary>
        public static int OneriSayisi = 0;
        public static int VerilenOdulSayisi = 0;
        public static string SonOdul = string.Empty;

        private Font font = null;
        private GameObject kartKoku = null;
        private Text baslikYazisi = null;
        private Text kartYazisi = null;
        private Image simgeResmi = null;
        private GameObject kusanDugmesi = null;
        private Kart gosterilen = null;
        private float kapanis = 0f;

        // ---------------------------------------------------------------- güç puanı

        /// <summary>temel değerler + zırh/2 + hasar×2 + seviye×10</summary>
        public static int GucPuani(UnitController oyuncu) {
            if (oyuncu == null || oyuncu.CharacterStats == null) {
                return 0;
            }
            CharacterStats st = oyuncu.CharacterStats;
            float toplam = st.Level * 10f;
            foreach (Stat s in st.PrimaryStats.Values) {
                toplam += s.CurrentValue;
            }
            Stat deger;
            if (st.SecondaryStats.TryGetValue(SecondaryStatType.Armor, out deger)) {
                toplam += deger.CurrentValue * 0.5f;
            }
            if (st.SecondaryStats.TryGetValue(SecondaryStatType.Damage, out deger)) {
                toplam += deger.CurrentValue * 2f;
            }
            if (st.SecondaryStats.TryGetValue(SecondaryStatType.PhysicalDamage, out deger)) {
                toplam += deger.CurrentValue * 2f;
            }
            if (st.SecondaryStats.TryGetValue(SecondaryStatType.SpellDamage, out deger)) {
                toplam += deger.CurrentValue * 2f;
            }
            return Mathf.RoundToInt(toplam);
        }

        /// <summary>eşyanın kaba değeri: kuşanınca kazandırdıkları (karşılaştırma için)</summary>
        public static float EsyaDegeri(InstantiatedEquipment esya, UnitController oyuncu) {
            if (esya == null || esya.Equipment == null || oyuncu == null) {
                return 0f;
            }
            int seviye = oyuncu.CharacterStats.Level;
            Equipment e = esya.Equipment;
            float toplam = 0f;
            foreach (ItemPrimaryStatNode dugum in e.PrimaryStats) {
                toplam += e.GetPrimaryStatModifier(dugum.StatName, seviye, oyuncu.BaseCharacter);
            }
            toplam += e.GetArmorModifier(seviye) * 0.5f;
            foreach (ItemSecondaryStatNode dugum in esya.SecondaryStats) {
                toplam += e.GetSecondaryStatAddModifier(esya.SecondaryStats, dugum.SecondaryStat, seviye) * 2f;
            }
            Weapon silah = e as Weapon;
            if (silah != null) {
                toplam += silah.GetDamagePerSecond(seviye) * 2f;
            }
            int basamak = Demirci.Seviye(esya);
            if (basamak > 0) {
                switch (Demirci.KazancTuru(esya)) {
                    case Demirci.Kazanc.Hasar:
                        toplam += Demirci.HasarBonusu(basamak, seviye) * 2f;
                        break;
                    case Demirci.Kazanc.Zirh:
                        toplam += Demirci.ZirhBonusu(basamak, seviye) * 0.5f;
                        break;
                    default:
                        toplam += Demirci.StatBonusu(basamak, seviye) * e.PrimaryStats.Count;
                        break;
                }
            }
            return toplam;
        }

        /// <summary>
        /// eşya kuşanılabiliyorsa ve uyduğu yuvalardan biri boşsa ya da oradakinden güçlüyse, kazanacağı değer; değilse -1
        /// </summary>
        public static float DahaIyiMi(InstantiatedEquipment esya, UnitController oyuncu) {
            if (esya == null || esya.Equipment == null || esya.Equipment.EquipmentSlotType == null || oyuncu.CharacterEquipmentManager == null) {
                return -1f;
            }
            if (esya.Equipment.CanEquip(esya.GetItemLevel(oyuncu.CharacterStats.Level), oyuncu) == false) {
                return -1f;
            }
            List<EquipmentSlotProfile> yuvalar = esya.Equipment.EquipmentSlotType.GetCompatibleSlotProfiles();
            if (yuvalar == null || yuvalar.Count == 0) {
                return -1f;
            }
            float yeni = EsyaDegeri(esya, oyuncu);
            float enZayif = float.MaxValue;
            foreach (EquipmentSlotProfile yuva in yuvalar) {
                EquipmentInventorySlot kusanili;
                if (yuva == null || oyuncu.CharacterEquipmentManager.CurrentEquipment.TryGetValue(yuva, out kusanili) == false) {
                    continue;
                }
                float eski = kusanili == null || kusanili.InstantiatedEquipment == null ? 0f : EsyaDegeri(kusanili.InstantiatedEquipment, oyuncu);
                enZayif = Mathf.Min(enZayif, eski);
            }
            if (enZayif == float.MaxValue) {
                return -1f;
            }
            float fark = yeni - enZayif;
            return fark >= 1f ? fark : -1f;
        }

        // ---------------------------------------------------------------- akış

        /// <summary>MobileBootstrap saniyede bir çağırır</summary>
        public static void Tick(SystemGameManager systemGameManager, bool oyunda) {
            oyun = systemGameManager;
            UnitController oyuncu = oyunda && systemGameManager != null && systemGameManager.PlayerManagerClient != null
                ? systemGameManager.PlayerManagerClient.UnitController : null;
            if (oyuncu == null || oyuncu.CharacterStats == null) {
                karakterZamani = -1f;
                bilinenHazir = false;
                return;
            }
            if (oyuncu.DisplayName != karakter || karakterZamani < 0f) {
                karakter = oyuncu.DisplayName;
                karakterZamani = Time.unscaledTime;
                sonGuc = -1;
                bilinenHazir = false;
                cantaUyarisi = false;
            }
            // yükleme bitsin (eşyalar, değerler yerine otursun)
            if (Time.unscaledTime - karakterZamani < 4f) {
                return;
            }
            SeviyeOdulleri(oyuncu);
            EsyalaraBak(oyuncu);
            GucuIzle(oyuncu);
            if (instance != null) {
                instance.KartiYonet();
            } else if (kuyruk.Count > 0) {
                Ensure();
                instance.KartiYonet();
            }
        }

        private static void GucuIzle(UnitController oyuncu) {
            int guc = GucPuani(oyuncu);
            if (sonGuc >= 0 && guc > sonGuc) {
                oyuncu.WriteMessageFeedMessage("<color=#FFD54A>Güç Puanı " + guc + " (+" + (guc - sonGuc) + ")</color>");
            }
            sonGuc = guc;
        }

        public static int AlinanSeviye(string ad) {
            return PlayerPrefs.GetInt(OdulAnahtari + ad, 1);
        }

        private static void SeviyeOdulleri(UnitController oyuncu) {
            int alinan = AlinanSeviye(karakter);
            int seviye = oyuncu.CharacterStats.Level;
            if (seviye <= alinan) {
                return;
            }
            foreach (Odul odul in oduller) {
                if (odul.seviye <= alinan || odul.seviye > seviye) {
                    continue;
                }
                if (odul.esya != null && oyuncu.CharacterInventoryManager.EmptySlotCount() == 0) {
                    if (cantaUyarisi == false) {
                        cantaUyarisi = true;
                        oyuncu.WriteMessageFeedMessage("<color=#FF9A7A>Seviye ödülün bekliyor: çantanda yer aç.</color>");
                    }
                    return;
                }
                Ver(oyuncu, odul);
                alinan = odul.seviye;
                PlayerPrefs.SetInt(OdulAnahtari + karakter, alinan);
            }
            PlayerPrefs.SetInt(OdulAnahtari + karakter, seviye);
            PlayerPrefs.Save();
        }

        private static void Ver(UnitController oyuncu, Odul odul) {
            if (odul.para != null) {
                Currency para = oyun.SystemDataFactory.GetResource<Currency>(odul.para);
                if (para != null) {
                    oyuncu.CharacterCurrencyManager.AddCurrency(para, odul.paraMiktari);
                }
            }
            Sprite simge = null;
            if (odul.esya != null) {
                for (int i = 0; i < odul.esyaSayisi; i++) {
                    InstantiatedItem esya = oyuncu.CharacterInventoryManager.GetNewInstantiatedItem(odul.esya);
                    if (esya == null) {
                        break;
                    }
                    simge = esya.Icon;
                    if (oyuncu.CharacterInventoryManager.AddItem(esya, false) == false) {
                        break;
                    }
                    // çantaya giren ödül "daha iyi eşya" sayılmasın
                    bilinenler.Add(esya.InstanceId);
                }
            }
            VerilenOdulSayisi++;
            SonOdul = odul.seviye + ": " + odul.yazi;
            oyuncu.WriteMessageFeedMessage("<color=#FFD54A>Seviye " + odul.seviye + " ödülü: " + odul.yazi + "</color>");
            MobileFeedback.Success();
            kuyruk.Enqueue(new Kart() { baslik = "SEVİYE " + odul.seviye + " ÖDÜLÜ", yazi = odul.yazi, simge = simge, sure = 6f });
        }

        private static void EsyalaraBak(UnitController oyuncu) {
            if (oyuncu.CharacterInventoryManager == null) {
                return;
            }
            List<long> simdi = new List<long>();
            List<InstantiatedEquipment> yeniler = new List<InstantiatedEquipment>();
            foreach (InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                if (yuva == null || yuva.IsEmpty) {
                    continue;
                }
                foreach (InstantiatedItem esya in yuva.InstantiatedItems.Values) {
                    simdi.Add(esya.InstanceId);
                    if (bilinenHazir && bilinenler.Contains(esya.InstanceId) == false && esya is InstantiatedEquipment) {
                        yeniler.Add(esya as InstantiatedEquipment);
                    }
                }
            }
            if (oyuncu.CharacterEquipmentManager != null) {
                foreach (EquipmentInventorySlot yuva in oyuncu.CharacterEquipmentManager.CurrentEquipment.Values) {
                    if (yuva != null && yuva.InstantiatedEquipment != null) {
                        simdi.Add(yuva.InstantiatedEquipment.InstanceId);
                    }
                }
            }
            bilinenler.Clear();
            foreach (long kimlik in simdi) {
                bilinenler.Add(kimlik);
            }
            bilinenHazir = true;
            foreach (InstantiatedEquipment esya in yeniler) {
                float fark = DahaIyiMi(esya, oyuncu);
                if (fark <= 0f) {
                    continue;
                }
                OneriSayisi++;
                kuyruk.Enqueue(new Kart() {
                    baslik = "DAHA İYİ EŞYA",
                    yazi = esya.DisplayName + "\n<size=17><color=#9FE08A>kuşanılandan güçlü (+" + Mathf.CeilToInt(fark) + ")</color></size>",
                    simge = esya.Icon,
                    esya = esya,
                    sure = 15f
                });
            }
        }

        // ---------------------------------------------------------------- kart

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
            instance = canvasObject.AddComponent<Gelisim>();
            instance.Build();
        }

        private void Build() {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // sol sütunun (Harita/Işınlan) sağında, hareket çubuğunun üstünde
            kartKoku = Kutu(transform, "Kart", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(206f, 96f), new Vector2(576f, 262f));
            kartKoku.AddComponent<Image>().color = panelColor;
            Outline cizgi = kartKoku.AddComponent<Outline>();
            cizgi.effectColor = gold;
            cizgi.effectDistance = new Vector2(2f, -2f);
            GameObject simge = Kutu(kartKoku.transform, "Simge", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, -36f), new Vector2(84f, 36f));
            simgeResmi = simge.AddComponent<Image>();
            simgeResmi.preserveAspect = true;
            simgeResmi.raycastTarget = false;
            baslikYazisi = Yazi(Kutu(kartKoku.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(96f, -42f), new Vector2(-44f, -6f)),
                string.Empty, 20, gold);
            baslikYazisi.fontStyle = FontStyle.Bold;
            kartYazisi = Yazi(Kutu(kartKoku.transform, "Yazi", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(96f, 50f), new Vector2(-12f, -42f)),
                string.Empty, 19, textColor);
            kusanDugmesi = Dugme(kartKoku.transform, "Kuşan", new Vector2(1f, 0f), new Vector2(-80f, 28f), new Vector2(140f, 44f), equipColor, Kusan);
            Dugme(kartKoku.transform, "X", new Vector2(1f, 1f), new Vector2(-22f, -22f), new Vector2(36f, 36f), buttonColor, Gec);
            kartKoku.SetActive(false);
        }

        private void KartiYonet() {
            if (gosterilen != null && Time.unscaledTime < kapanis) {
                return;
            }
            gosterilen = null;
            if (kuyruk.Count == 0) {
                kartKoku.SetActive(false);
                return;
            }
            gosterilen = kuyruk.Dequeue();
            baslikYazisi.text = gosterilen.baslik;
            kartYazisi.text = gosterilen.yazi;
            simgeResmi.sprite = gosterilen.simge;
            simgeResmi.enabled = gosterilen.simge != null;
            kusanDugmesi.SetActive(gosterilen.esya != null);
            kapanis = Time.unscaledTime + gosterilen.sure;
            kartKoku.SetActive(true);
            MobileFeedback.Light();
        }

        /// <summary>editördeki arayüz önizlemesi (HaritaHazirlik): örnek "daha iyi eşya" kartı</summary>
        private void Onizleme() {
            gosterilen = new Kart() { baslik = "DAHA İYİ EŞYA", yazi = "Kurt Dişi Kılıç\n<size=17><color=#9FE08A>kuşanılandan güçlü (+12)</color></size>", sure = 15f };
            baslikYazisi.text = gosterilen.baslik;
            kartYazisi.text = gosterilen.yazi;
            simgeResmi.enabled = true;
            simgeResmi.color = new Color(0.45f, 0.4f, 0.32f, 1f);
            kusanDugmesi.SetActive(true);
            kartKoku.SetActive(true);
        }

        private void Kusan() {
            MobileFeedback.Tap();
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            InstantiatedEquipment esya = gosterilen != null ? gosterilen.esya : null;
            if (oyuncu != null && esya != null && esya.Slot != null && esya.Slot.InstantiatedItems.ContainsValue(esya)) {
                oyuncu.CharacterInventoryManager.RequestUseItem(esya.Slot);
            }
            Gec();
        }

        private void Gec() {
            gosterilen = null;
            kapanis = 0f;
            KartiYonet();
        }

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

        private Text Yazi(GameObject hedef, string icerik, int boyut, Color renk) {
            Text t = hedef.AddComponent<Text>();
            t.font = font;
            t.text = icerik;
            t.fontSize = boyut;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = renk;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        private GameObject Dugme(Transform ust, string etiket, Vector2 anchor, Vector2 konum, Vector2 boyut, Color renk, UnityEngine.Events.UnityAction tik) {
            GameObject go = Kutu(ust, etiket, anchor, anchor, Vector2.zero, Vector2.zero);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = konum;
            rt.sizeDelta = boyut;
            Image resim = go.AddComponent<Image>();
            resim.color = renk;
            Button b = go.AddComponent<Button>();
            b.targetGraphic = resim;
            b.onClick.AddListener(tik);
            Text t = Yazi(Kutu(go.transform, "Yazi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), etiket, 20, textColor);
            t.alignment = TextAnchor.MiddleCenter;
            t.fontStyle = FontStyle.Bold;
            return go;
        }
    }
}
