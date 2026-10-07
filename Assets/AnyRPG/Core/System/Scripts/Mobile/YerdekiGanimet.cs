using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Yerdeki ganimet (Ötüken, Metin2 tarzı):
    ///  - Canavar ölünce eşyaları cesedin çevresine yere düşer (LootableCharacterComponent.HandleBeforeDie); para ve görev
    ///    eşyası doğrudan çantaya/keseye geçer.
    ///  - Yerdeki eşya 3 dakika durur, sonra kaybolur; o süre içinde HERKES alabilir.
    ///  - Otomatik toplama (Seçenekler > Oyun > Otomatik ganimet): 2,6 m'ye gelen eşya kendiliğinden çantaya girer;
    ///    Oto Av açıkken karakter 15 m içindeki eşyaya yürür.
    ///  - Çevrimiçinde alma sunucuda yapılır ("yerden-al" + eşya kimliği); çevrimdışında telefonda.
    /// Oyuncunun "At" ile yere bıraktığı eşyalar eskisi gibi kalıcıdır (süresiz).
    /// </summary>
    public static class YerdekiGanimet {

        public const float Sure = 180f;
        public const float ToplamaMesafesi = 2.6f;
        public const float AvMesafesi = 15f;
        public const string VarsayilanModel = "Bag";

        private class Sureli {
            public DroppedItemComponent bilesen;
            public float bitis;
        }

        // sunucu (ve tek oyunculu oyun): süreli canavar ganimetleri
        private static readonly List<Sureli> sureliler = new List<Sureli>();
        // her iki tarafta: sahnedeki yer eşyaları
        private static readonly HashSet<DroppedItemComponent> yerdekiler = new HashSet<DroppedItemComponent>();
        private static readonly Dictionary<DroppedItemComponent, float> istenen = new Dictionary<DroppedItemComponent, float>();

        public static int YerdekiSayisi { get { return yerdekiler.Count; } }
        public static int AlinanSayisi { get; private set; }
        public static int DusenSayisi { get; private set; }
        public static int KaybolanSayisi { get; private set; }
        // telefon/bot: görülen en çok yer eşyası ve gönderilen alma isteği (ağ botu raporu)
        public static int EnCokGorulen { get; private set; }
        public static int IstekSayisi { get; private set; }
        private static float sonrakiRapor = 0f;
        private static int raporlananDusen = -1;

        public static void Kaydol(DroppedItemComponent c) {
            if (c != null) {
                yerdekiler.Add(c);
            }
        }

        public static void Birak(DroppedItemComponent c) {
            if (c == null) {
                return;
            }
            yerdekiler.Remove(c);
            istenen.Remove(c);
            sureliler.RemoveAll(s => s.bilesen == c);
        }

        /// <summary>yere düşürülebilir mi (para ve görev başlatan eşya doğrudan alınır)</summary>
        public static bool YereDuser(InstantiatedItem esya) {
            return esya != null && esya.Item != null && (esya is InstantiatedCurrencyItem) == false && (esya is InstantiatedQuestStartItem) == false;
        }

        // ---------------------------------------------------------------- sunucu

        /// <summary>canavarın ganimetini cesedin çevresine saçar (her eşya ayrı)</summary>
        public static void CanavardanDusur(UnitController olu, UnitController sahip, List<InstantiatedItem> esyalar) {
            if (olu == null || sahip == null || sahip.CharacterInventoryManager == null || esyalar == null) {
                return;
            }
            Vector3 merkez = olu.transform.position;
            float aciBasi = Random.Range(0f, 360f);
            for (int i = 0; i < esyalar.Count; i++) {
                float aci = (aciBasi + i * 137.5f) * Mathf.Deg2Rad;
                float r = 0.6f + 0.35f * i;
                Vector3 yon = new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci));
                InteractableBase n = null;
                try {
                    n = sahip.CharacterInventoryManager.YereBirak(new List<InstantiatedItem>() { esyalar[i] }, merkez + yon * r * 0.3f, yon, 0.6f, false, olu.gameObject.scene);
                } catch (System.Exception e) {
                    Debug.LogWarning("[YerdekiGanimet] yere düşürülemedi: " + e.Message);
                }
                DroppedItemComponent c = n != null ? n.GetFirstInteractableOption(typeof(DroppedItemComponent)) as DroppedItemComponent : null;
                if (c != null) {
                    sureliler.Add(new Sureli() { bilesen = c, bitis = Time.time + Sure });
                    DusenSayisi++;
                }
            }
        }

        // ---------------------------------------------------------------- cevherler (Demirci +6..+9)

        private static readonly string[] cevherler = { "Demir Cevheri", "Gumus Cevheri", "Altin Cevheri", "Gok Demiri" };
        // hangi haritadan (HaritaSeviyeleri.Sira sırası) itibaren düşer ve sıradan canavarda yüzde şansı
        private static readonly int[] cevherHaritasi = { 2, 5, 8, 11 };
        private static readonly float[] cevherSansi = { 4f, 2.5f, 1.5f, 0.8f };

        /// <summary>
        /// canavarın bıraktığı cevher (en çok biri; boss en iyisinden bir tane, %35 ihtimalle bir alt basamaktan bir tane daha).
        /// Seçkin canavarlarda şans 2,5 kat, Ganimet Bereketi/Boss Avı etkinliğinde 1,5–2 kat.
        /// </summary>
        public static List<InstantiatedItem> CevherAt(UnitController olu, UnitController sahip) {
            List<InstantiatedItem> liste = new List<InstantiatedItem>();
            if (olu == null || sahip == null || sahip.CharacterInventoryManager == null) {
                return liste;
            }
            int harita = System.Array.IndexOf(HaritaSeviyeleri.Sira, olu.gameObject.scene.name);
            int enIyi = -1;
            for (int i = 0; i < cevherHaritasi.Length; i++) {
                if (harita >= cevherHaritasi[i]) {
                    enIyi = i;
                }
            }
            if (enIyi < 0) {
                return liste;
            }
            string dayaniklilik = olu.BaseCharacter != null && olu.BaseCharacter.UnitToughness != null ? olu.BaseCharacter.UnitToughness.ResourceName : string.Empty;
            List<string> adlar = new List<string>();
            if (HaritaSeviyeleri.BossMu(dayaniklilik)) {
                adlar.Add(cevherler[enIyi]);
                if (Random.value < 0.35f) {
                    adlar.Add(cevherler[Mathf.Max(0, enIyi - 1)]);
                }
            } else {
                float carpan = (HaritaSeviyeleri.SeckinMi(dayaniklilik) ? 2.5f : 1f) * Etkinlikler.GanimetCarpani(sahip);
                for (int i = enIyi; i >= 0; i--) {
                    if (Random.value * 100f < cevherSansi[i] * carpan) {
                        adlar.Add(cevherler[i]);
                        break;
                    }
                }
            }
            foreach (string ad in adlar) {
                InstantiatedItem esya = sahip.CharacterInventoryManager.GetNewInstantiatedItem(ad);
                if (esya != null) {
                    liste.Add(esya);
                }
            }
            return liste;
        }

        /// <summary>sunucu (ve tek oyunculu oyun) saniyede bir: süresi dolan ganimet kaybolur</summary>
        public static void SunucuTick() {
            if (Time.time >= sonrakiRapor) {
                sonrakiRapor = Time.time + 60f;
                if (DusenSayisi != raporlananDusen && OtukenAg.Sunucuda) {
                    raporlananDusen = DusenSayisi;
                    Debug.Log("[Sunucu] yerdeki ganimet: düşen " + DusenSayisi + ", alınan " + AlinanSayisi + ", kaybolan " + KaybolanSayisi + ", yerde " + sureliler.Count);
                }
            }
            if (sureliler.Count == 0) {
                return;
            }
            float t = Time.time;
            for (int i = sureliler.Count - 1; i >= 0; i--) {
                Sureli s = sureliler[i];
                if (s.bilesen == null || s.bilesen.EsyaSayisi == 0) {
                    sureliler.RemoveAt(i);
                    continue;
                }
                if (t < s.bitis) {
                    continue;
                }
                sureliler.RemoveAt(i);
                try {
                    s.bilesen.Despawn();
                    KaybolanSayisi++;
                } catch (System.Exception e) {
                    Debug.LogWarning("[YerdekiGanimet] kaldırılamadı: " + e.Message);
                }
            }
        }

        /// <summary>oyuncu yerdeki eşyayı alır (menzil sunucuda denetlenir); alınan sayısı</summary>
        public static int Al(UnitController oyuncu, DroppedItemComponent c) {
            if (oyuncu == null || c == null || c.EsyaSayisi == 0 || c.Nesne == null) {
                return 0;
            }
            if (oyuncu.CharacterStats == null || oyuncu.CharacterStats.IsAlive == false) {
                return 0;
            }
            if (YatayUzaklik(oyuncu.transform.position, c.Nesne.transform.position) > ToplamaMesafesi + 1.5f) {
                return 0;
            }
            if (oyuncu.CharacterInventoryManager.EmptySlotCount() == 0) {
                oyuncu.WriteMessageFeedMessage("Çanta dolu! Yerdeki eşyayı alamadın.");
                return 0;
            }
            List<InstantiatedItem> once = new List<InstantiatedItem>(c.Esyalar);
            c.DropLoot(oyuncu);
            int alinan = 0;
            foreach (InstantiatedItem esya in once) {
                if (c.Esyalar.Contains(esya)) {
                    continue;
                }
                alinan++;
                AlinanSayisi++;
                string ileti = Ganimet.PickupMessage(esya);
                if (string.IsNullOrEmpty(ileti) == false) {
                    oyuncu.WriteMessageFeedMessage(ileti);
                }
                if (OtukenAg.Sunucuda) {
                    GunlukGorevler.SunucuBildir(oyuncu, GunlukGorevTuru.Ganimet);
                } else {
                    Ganimet.OnLooted(esya);
                }
            }
            return alinan;
        }

        private static float YatayUzaklik(Vector3 a, Vector3 b) {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("yerden-al", (oyuncu, veri) => {
                long kimlik;
                if (long.TryParse(veri, NumberStyles.Integer, CultureInfo.InvariantCulture, out kimlik) == false) {
                    return;
                }
                foreach (DroppedItemComponent c in yerdekiler) {
                    if (c != null && c.IlkKimlik == kimlik) {
                        Al(oyuncu, c);
                        return;
                    }
                }
            });
        }

        // ---------------------------------------------------------------- telefon (ve ağ botu)

        private static readonly List<DroppedItemComponent> silinecek = new List<DroppedItemComponent>();

        /// <summary>
        /// otomatik toplama (saniyede bir; tek oyunculu oyunda süre denetimini de yapar). Yakındaki en çok 3 eşyayı ister.
        /// </summary>
        public static void IstemciTick(SystemGameManager oyun) {
            if (oyun == null) {
                return;
            }
            if (oyun.GameMode == GameMode.Local) {
                SunucuTick();
            }
            UnitController oyuncu = oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu == null || oyuncu.CharacterStats == null || oyuncu.CharacterStats.IsAlive == false || Ganimet.AutoLoot == false) {
                return;
            }
            EnCokGorulen = Mathf.Max(EnCokGorulen, yerdekiler.Count);
            if (yerdekiler.Count == 0 || oyuncu.CharacterInventoryManager.EmptySlotCount() == 0) {
                return;
            }
            silinecek.Clear();
            int istek = 0;
            Vector3 konum = oyuncu.transform.position;
            foreach (DroppedItemComponent c in yerdekiler) {
                if (c == null || c.Nesne == null || c.EsyaSayisi == 0) {
                    silinecek.Add(c);
                    continue;
                }
                if (c.Nesne.gameObject.scene != oyuncu.gameObject.scene) {
                    continue;
                }
                if (YatayUzaklik(konum, c.Nesne.transform.position) > ToplamaMesafesi) {
                    continue;
                }
                float son;
                if (istenen.TryGetValue(c, out son) && Time.unscaledTime - son < 1.2f) {
                    continue;
                }
                istenen[c] = Time.unscaledTime;
                if (oyun.GameMode == GameMode.Local) {
                    Al(oyuncu, c);
                } else {
                    OtukenAg.Gonder("yerden-al", c.IlkKimlik.ToString(CultureInfo.InvariantCulture));
                }
                IstekSayisi++;
                if (++istek >= 3) {
                    break;
                }
            }
            foreach (DroppedItemComponent c in silinecek) {
                yerdekiler.Remove(c);
                istenen.Remove(c);
            }
        }

        /// <summary>Oto Av: oyuncuya en yakın, 15 m içindeki yer eşyası (yoksa null)</summary>
        public static InteractableBase EnYakin(UnitController oyuncu, float enCok) {
            if (oyuncu == null) {
                return null;
            }
            InteractableBase en = null;
            float enYakin = enCok;
            Vector3 konum = oyuncu.transform.position;
            foreach (DroppedItemComponent c in yerdekiler) {
                if (c == null || c.Nesne == null || c.EsyaSayisi == 0 || c.Nesne.gameObject.scene != oyuncu.gameObject.scene) {
                    continue;
                }
                float u = YatayUzaklik(konum, c.Nesne.transform.position);
                if (u < enYakin) {
                    enYakin = u;
                    en = c.Nesne;
                }
            }
            return en;
        }
    }
}
