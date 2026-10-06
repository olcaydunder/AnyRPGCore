using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Oyuncular arası ticaretin (takas ve pazar) sunucudaki ortak işleri. Çevrimiçi oyunda her şey sunucuda olur:
    /// eşya ve para sunucudaki karakterler arasında taşınır, sonucu AnyRPG'nin ağ olayları (çanta yuvası, para)
    /// telefonlara götürür.
    ///  - eşya kimliği ile çantadaki yuvasını bulmak; bir yuvadaki yığının hepsini taşımak (aynı eşya nesneleri taşınır,
    ///    kimlikleri değişmez: karakter kayıtlarına öyle girer)
    ///  - hesaba bağlı eşyalar takas edilemez, pazarda satılamaz (Kut ile alınanlar, görev eşyaları)
    ///  - para bakır cinsinden (1 Altın = 100 Gümüş Akçe = 10000 Bakır Akçe)
    ///  - her takas ve satış sunucuda ticaret.log dosyasına yazılır (anlaşmazlık ve dolandırıcılık incelemesi için)
    /// </summary>
    public static class Ticaret {

        public const float TakasMesafesi = 15f;
        public const float PazarMesafesi = 25f;

        // hesaba bağlı (takas ve pazar dışı) eşyalar: Kut Dükkânı'ndan alınanlar
        private static readonly HashSet<string> bagliEsyalar = new HashSet<string>() { KutDukkani.UluHeybe };

        public static Currency Bakir {
            get {
                SystemGameManager o = OtukenAg.Oyun;
                return o != null ? o.SystemDataFactory.GetResource<Currency>("Copper") : null;
            }
        }

        public static bool BagliMi(InstantiatedItem esya) {
            if (esya == null || esya.Item == null) {
                return true;
            }
            if (bagliEsyalar.Contains(esya.Item.ResourceName)) {
                return true;
            }
            // görev eşyaları ve paranın kendisi
            return esya.Item is QuestStartItem || esya.Item is CurrencyItem;
        }

        public static string BagliNedeni(InstantiatedItem esya) {
            if (esya != null && esya.Item != null && bagliEsyalar.Contains(esya.Item.ResourceName)) {
                return "Kut Dükkânı'ndan alınan eşyalar hesaba bağlıdır, takas edilemez.";
            }
            return "Bu eşya takas edilemez.";
        }

        /// <summary>çantadaki (kuşanılmamış) eşyayı kimliğinden bulur</summary>
        public static InstantiatedItem CantadaBul(UnitController oyuncu, long kimlik) {
            if (oyuncu == null || oyuncu.CharacterInventoryManager == null) {
                return null;
            }
            foreach (InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                if (yuva == null || yuva.IsEmpty) {
                    continue;
                }
                InstantiatedItem esya;
                if (yuva.InstantiatedItems.TryGetValue(kimlik, out esya)) {
                    return esya;
                }
            }
            return null;
        }

        /// <summary>eşyanın yuvasındaki bütün yığın (en az kendisi)</summary>
        public static List<InstantiatedItem> Yigin(InstantiatedItem esya) {
            List<InstantiatedItem> liste = new List<InstantiatedItem>();
            if (esya == null) {
                return liste;
            }
            if (esya.Slot != null) {
                liste.AddRange(esya.Slot.InstantiatedItems.Values);
            }
            if (liste.Contains(esya) == false) {
                liste.Add(esya);
            }
            return liste;
        }

        /// <summary>bir yığını bir oyuncudan ötekine taşır (sunucu). Alanın çantasında yer yoksa false (hiçbir şey değişmez)</summary>
        public static bool Tasi(UnitController veren, UnitController alan, List<InstantiatedItem> yigin) {
            if (veren == null || alan == null || yigin == null || yigin.Count == 0) {
                return false;
            }
            if (alan.CharacterInventoryManager.EmptySlotCount() < 1) {
                return false;
            }
            foreach (InstantiatedItem esya in yigin) {
                if (esya.Slot != null) {
                    esya.Slot.RemoveItem(esya);
                } else {
                    veren.CharacterInventoryManager.RemoveInventoryItem(esya);
                }
                // alanın telefonu bu eşya nesnesini tanısın (kimlik ve ad, demirci basamağı, rastgele değerler)
                alan.UnitEventController.NotifyOnGetNewInstantiatedItem(esya);
                if (alan.CharacterInventoryManager.AddItem(esya, false, false) == false) {
                    // olmamalı (yer bakıldı); eşya kaybolmasın: verene geri
                    veren.CharacterInventoryManager.AddItem(esya, false, false);
                    Debug.LogError("[Ticaret] " + esya.DisplayName + " " + alan.DisplayName + " çantasına konamadı, " + veren.DisplayName + " oyuncusuna geri verildi");
                    return false;
                }
            }
            // telefonda eski adla kalmasın (demirci basamağı adda)
            foreach (InstantiatedItem esya in yigin) {
                OtukenAg.Yanitla(alan, "esya-adi", esya.InstanceId.ToString(CultureInfo.InvariantCulture) + "|" + esya.DisplayName);
            }
            return true;
        }

        public static int Para(UnitController oyuncu) {
            Currency bakir = Bakir;
            return oyuncu != null && bakir != null ? oyuncu.CharacterCurrencyManager.GetBaseCurrencyValue(bakir) : 0;
        }

        /// <summary>bakır cinsinden para aktarır; verende yeterli yoksa false</summary>
        public static bool ParaAktar(UnitController veren, UnitController alan, int bakir) {
            if (bakir <= 0) {
                return true;
            }
            Currency para = Bakir;
            if (para == null || Para(veren) < bakir) {
                return false;
            }
            if (veren.CharacterCurrencyManager.SpendCurrency(para, bakir) == false) {
                return false;
            }
            alan.CharacterCurrencyManager.AddCurrency(para, bakir);
            return true;
        }

        /// <summary>sunucudaki oyuncu (ada göre, büyük/küçük harf ayrımsız)</summary>
        public static UnitController OyuncuBul(string ad) {
            SystemGameManager o = OtukenAg.Oyun;
            if (o == null || o.PlayerManagerServer == null || string.IsNullOrEmpty(ad)) {
                return null;
            }
            foreach (UnitController u in o.PlayerManagerServer.ActiveUnitControllers.Values) {
                if (u != null && string.Equals(u.DisplayName, ad, StringComparison.OrdinalIgnoreCase)) {
                    return u;
                }
            }
            return null;
        }

        public static bool Yakin(UnitController a, UnitController b, float mesafe) {
            return a != null && b != null && a.gameObject.scene == b.gameObject.scene
                && Vector3.Distance(a.transform.position, b.transform.position) <= mesafe;
        }

        public static bool Etkin(UnitController u) {
            SystemGameManager o = OtukenAg.Oyun;
            if (u == null || o == null || o.PlayerManagerServer == null) {
                return false;
            }
            foreach (UnitController x in o.PlayerManagerServer.ActiveUnitControllers.Values) {
                if (x == u) {
                    return true;
                }
            }
            return false;
        }

        public static void Kaydet(UnitController u) {
            if (u != null && u.UnitEventController != null) {
                u.UnitEventController.NotifyOnSaveDataUpdated();
            }
        }

        // ---------------------------------------------------------------- telefon: eşya bilgisi

        /// <summary>takas ve pazar listeleri için bir yığının tanımı: kimlik, kaynak adı, görünen ad, adet</summary>
        public static string Tanim(List<InstantiatedItem> yigin) {
            InstantiatedItem e = yigin[0];
            return e.InstanceId.ToString(CultureInfo.InvariantCulture) + "\u001D" + e.Item.ResourceName + "\u001D" + Duz(e.DisplayName)
                + "\u001D" + yigin.Count.ToString(CultureInfo.InvariantCulture)
                + "\u001D" + (e.ItemQuality != null ? ColorUtility.ToHtmlStringRGB(e.ItemQuality.QualityColor) : "FFFFFF");
        }

        public static string Duz(string s) {
            return (s ?? string.Empty).Replace('\u001D', ' ').Replace('\u001E', ' ').Replace('\u001F', ' ').Replace('|', '/');
        }

        public class EsyaTanimi {
            public long kimlik;
            public string kaynak;
            public string ad;
            public int adet;
            public Color renk = Color.white;
            public int fiyat;

            public Sprite Simge {
                get {
                    SystemGameManager o = OtukenAg.Oyun;
                    Item item = o != null ? o.SystemDataFactory.GetResource<Item>(kaynak) : null;
                    return item != null ? item.Icon : null;
                }
            }

            public string Yazi {
                get { return "<color=#" + ColorUtility.ToHtmlStringRGB(renk) + ">" + ad + "</color>" + (adet > 1 ? " ×" + adet : string.Empty); }
            }
        }

        public static EsyaTanimi TanimOku(string s) {
            string[] p = s.Split('\u001D');
            if (p.Length < 5) {
                return null;
            }
            EsyaTanimi t = new EsyaTanimi();
            long.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out t.kimlik);
            t.kaynak = p[1];
            t.ad = p[2];
            int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out t.adet);
            Color renk;
            if (ColorUtility.TryParseHtmlString("#" + p[4], out renk)) {
                t.renk = renk;
            }
            if (p.Length > 5) {
                int.TryParse(p[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out t.fiyat);
            }
            return t;
        }

        public static List<EsyaTanimi> TanimlariOku(string s) {
            List<EsyaTanimi> liste = new List<EsyaTanimi>();
            if (string.IsNullOrEmpty(s)) {
                return liste;
            }
            foreach (string parca in s.Split('\u001E')) {
                EsyaTanimi t = TanimOku(parca);
                if (t != null) {
                    liste.Add(t);
                }
            }
            return liste;
        }

        // ---------------------------------------------------------------- kayıt defteri

        private static string defterYolu = null;

        /// <summary>sunucu: ticaret.log'a bir satır (zaman, olay)</summary>
        public static void Defter(string satir) {
            try {
                if (defterYolu == null) {
                    defterYolu = Path.Combine(Application.persistentDataPath, "ticaret.log");
                }
                File.AppendAllText(defterYolu, DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " " + satir + "\n", Encoding.UTF8);
            } catch (Exception e) {
                Debug.LogWarning("[Ticaret] defter yazılamadı: " + e.Message);
            }
            Debug.Log("[Ticaret] " + satir);
        }

        public static string Ozet(List<List<InstantiatedItem>> yiginlar) {
            List<string> p = new List<string>();
            foreach (List<InstantiatedItem> y in yiginlar) {
                if (y.Count > 0) {
                    p.Add(y[0].DisplayName + (y.Count > 1 ? " x" + y.Count : string.Empty) + " #" + y[0].InstanceId);
                }
            }
            return p.Count == 0 ? "-" : string.Join(", ", p);
        }

        // ---------------------------------------------------------------- telefon: ad yenileme

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.IstemciDinle("esya-adi", veri => {
                int ayrac = veri.IndexOf('|');
                long kimlik;
                SystemGameManager o = OtukenAg.Oyun;
                if (ayrac < 0 || o == null || long.TryParse(veri.Substring(0, ayrac), NumberStyles.Integer, CultureInfo.InvariantCulture, out kimlik) == false) {
                    return;
                }
                InstantiatedItem esya;
                if (o.SystemItemManager.InstantiatedItems.TryGetValue(kimlik, out esya)) {
                    esya.DisplayName = veri.Substring(ayrac + 1);
                }
            });
        }
    }
}
