using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Depo (Ötüken): AnyRPG'nin banka yuvaları üstüne kurulu, her yerden açılan depo.
    ///  - 10 göz ücretsiz (defaultBankSlots); Google Play'den "depo_10" her alındığında 10 göz eklenir:
    ///    sunucu karakterin depo bağ düğümlerinden boş olana 10 gözlü "Depo Sandığı" takar (banka çantası olarak
    ///    kaydedilir, AnyRPG eşitler). En çok 99 sandık: 10 + 990 = 1000 göz = 20 sayfa × 50 göz.
    ///  - Sandıklar hesaba bağlıdır: çıkarılamaz, taşınamaz, takas/pazar dışı (FishNetUnitController, Ticaret).
    ///  - Pencere: solda depo (sayfa başına 50 göz), sağda çanta (sayfa başına 40 göz); eşyaya dokununca öbür tarafa geçer.
    /// </summary>
    public class Depo : OtukenPencere {

        public const string SandikAdi = "Depo Sandigi";
        public const string UrunKodu = "depo_10";
        public const int SandikGozu = 10;
        public const int SayfaGozu = 50;
        public const int EnCokSayfa = 20;
        public const int CantaSayfaGozu = 40;

        public static bool SandikMi(InstantiatedItem esya) {
            return esya != null && esya.Item != null && esya.Item.ResourceName == SandikAdi;
        }

        public static int GozSayisi(UnitController oyuncu) {
            return oyuncu != null && oyuncu.CharacterInventoryManager != null ? oyuncu.CharacterInventoryManager.BankSlots.Count : 0;
        }

        // ---------------------------------------------------------------- sunucu (ve tek oyunculu oyun)

        /// <summary>
        /// karaktere göz ekler (10'ar); eklenen göz sayısını döndürür (depo doluysa 0).
        /// Çevrimiçinde sunucuda, tek oyunculu oyunda telefonda çağrılır.
        /// </summary>
        public static int KareEkle(UnitController oyuncu, int goz) {
            if (oyuncu == null || oyuncu.CharacterInventoryManager == null) {
                return 0;
            }
            CharacterInventoryManager env = oyuncu.CharacterInventoryManager;
            int eklenen = 0;
            for (int i = 0; i < goz / SandikGozu; i++) {
                BagNode bos = null;
                foreach (BagNode d in env.BankNodes) {
                    if (d != null && d.InstantiatedBag == null) {
                        bos = d;
                        break;
                    }
                }
                if (bos == null) {
                    break;
                }
                InstantiatedBag sandik = env.GetNewInstantiatedItem(SandikAdi) as InstantiatedBag;
                if (sandik == null) {
                    Debug.LogError("[Depo] '" + SandikAdi + "' çanta eşyası bulunamadı");
                    break;
                }
                env.AddBag(sandik, bos);
                eklenen += SandikGozu;
            }
            if (eklenen > 0) {
                Ticaret.Kaydet(oyuncu);
                Ticaret.Defter("DEPO " + oyuncu.DisplayName + " +" + eklenen + " göz (" + env.BankSlots.Count + ")");
            }
            return eklenen;
        }

        /// <summary>depoya daha göz eklenebilir mi</summary>
        public static bool YerVar(UnitController oyuncu) {
            if (oyuncu == null || oyuncu.CharacterInventoryManager == null) {
                return false;
            }
            foreach (BagNode d in oyuncu.CharacterInventoryManager.BankNodes) {
                if (d != null && d.InstantiatedBag == null) {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- pencere

        private static Depo ornek = null;

        private class Goz {
            public GameObject go;
            public Image arka;
            public Image simge;
            public Text sayi;
            public InventorySlot yuva;
        }

        private readonly List<Goz> depoGozleri = new List<Goz>();
        private readonly List<Goz> cantaGozleri = new List<Goz>();
        private int depoSayfasi = 0;
        private int cantaSayfasi = 0;
        private Text depoBilgi = null;
        private Text cantaBilgi = null;
        private Text durum = null;
        private GameObject satinAlDugmesi = null;
        private float sonraki = 0f;

        public static void Goster() {
            if (ornek == null) {
                ornek = Kur<Depo>("DepoCanvas", 33);
            }
            Odeme.Baslat();
            ornek.kok.SetActive(true);
            ornek.Yenile();
        }

        protected override void Kur() {
            PencereKur("Depo", 1340f, 720f);
            Baslik("DEPO");
            durum = Yazi(Kutu(panel.transform, "Durum", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(200f, -62f), new Vector2(-460f, -8f)),
                string.Empty, 18, TextAnchor.MiddleLeft, IpucuRengi);
            satinAlDugmesi = Dugme(panel.transform, "10 Göz Al", new Vector2(1f, 1f), new Vector2(-300f, -34f), new Vector2(260f, 46f), 19, OnayRengi, SatinAl);

            // depo: 10 x 5
            depoBilgi = Yazi(Kutu(panel.transform, "DepoBilgi", new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(24f, -100f), new Vector2(0f, -70f)),
                string.Empty, 20, TextAnchor.MiddleLeft, Altin);
            Izgara(depoGozleri, SayfaGozu, 10, new Vector2(24f, -106f), true);
            Sayfalayici(new Vector2(24f + 5 * 62f, -106f - 5 * 62f - 34f), -1, () => DepoSayfa(-1), () => DepoSayfa(1));

            // çanta: 8 x 5
            cantaBilgi = Yazi(Kutu(panel.transform, "CantaBilgi", new Vector2(0.55f, 1f), new Vector2(1f, 1f), new Vector2(20f, -100f), new Vector2(-24f, -70f)),
                string.Empty, 20, TextAnchor.MiddleLeft, Altin);
            float cantaX = 24f + 10 * 62f + 60f;
            Izgara(cantaGozleri, CantaSayfaGozu, 8, new Vector2(cantaX, -106f), false);
            Sayfalayici(new Vector2(cantaX + 4 * 62f, -106f - 5 * 62f - 34f), 1, () => CantaSayfa(-1), () => CantaSayfa(1));

            Yazi(Kutu(panel.transform, "Bilgi", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 12f), new Vector2(-24f, 80f)),
                "Eşyaya dokun: depodaki çantaya, çantadaki depoya geçer. 10 göz ücretsiz; Google Play'den her alımda 10 göz eklenir "
                + "(bir sayfa 50 göz, en çok 20 sayfa). Depo Sandıkları hesaba bağlıdır.", 16, TextAnchor.MiddleLeft, IpucuRengi);
        }

        private void Izgara(List<Goz> liste, int adet, int sutun, Vector2 solUst, bool depo) {
            const float boy = 56f;
            const float aralik = 6f;
            for (int i = 0; i < adet; i++) {
                int sira = i;
                Vector2 k = solUst + new Vector2((i % sutun) * (boy + aralik), -(i / sutun) * (boy + aralik));
                GameObject go = Kutu(panel.transform, (depo ? "Depo" : "Canta") + i, new Vector2(0f, 1f), new Vector2(0f, 1f), k + new Vector2(0f, -boy), k + new Vector2(boy, 0f));
                Goz g = new Goz() { go = go };
                g.arka = go.AddComponent<Image>();
                g.arka.color = SatirRengi;
                Button b = go.AddComponent<Button>();
                b.targetGraphic = g.arka;
                b.onClick.AddListener(() => Dokun(depo, sira));
                GameObject s = Kutu(go.transform, "Simge", Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
                g.simge = s.AddComponent<Image>();
                g.simge.preserveAspect = true;
                g.simge.raycastTarget = false;
                g.sayi = Yazi(Kutu(go.transform, "Sayi", Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-4f, -2f)), string.Empty, 15, TextAnchor.LowerRight, YaziRengi);
                liste.Add(g);
            }
        }

        private void Sayfalayici(Vector2 merkez, int taraf, UnityEngine.Events.UnityAction geri, UnityEngine.Events.UnityAction ileri) {
            Dugme(panel.transform, "‹", new Vector2(0f, 1f), merkez + new Vector2(-150f, 0f), new Vector2(70f, 44f), 26, DugmeRengi, () => { MobileFeedback.Tap(); geri(); });
            Dugme(panel.transform, "›", new Vector2(0f, 1f), merkez + new Vector2(150f, 0f), new Vector2(70f, 44f), 26, DugmeRengi, () => { MobileFeedback.Tap(); ileri(); });
        }

        private void DepoSayfa(int fark) {
            int sayfalar = Mathf.Clamp(Mathf.CeilToInt(GozSayisi(Oyuncu) / (float)SayfaGozu), 1, EnCokSayfa);
            depoSayfasi = Mathf.Clamp(depoSayfasi + fark, 0, sayfalar - 1);
            Yenile();
        }

        private void CantaSayfa(int fark) {
            UnitController oyuncu = Oyuncu;
            int toplam = oyuncu != null ? oyuncu.CharacterInventoryManager.InventorySlots.Count : 0;
            int sayfalar = Mathf.Max(1, Mathf.CeilToInt(toplam / (float)CantaSayfaGozu));
            cantaSayfasi = Mathf.Clamp(cantaSayfasi + fark, 0, sayfalar - 1);
            Yenile();
        }

        private void Dokun(bool depo, int sira) {
            Goz g = (depo ? depoGozleri : cantaGozleri)[sira];
            UnitController oyuncu = Oyuncu;
            if (g.yuva == null || g.yuva.IsEmpty || oyuncu == null) {
                return;
            }
            MobileFeedback.Tap();
            if (depo) {
                if (oyuncu.CharacterInventoryManager.EmptySlotCount(false) == 0) {
                    Bilgi("Çantan dolu.");
                    return;
                }
                oyuncu.CharacterInventoryManager.RequestMoveFromBankToInventory(g.yuva);
            } else {
                if (oyuncu.CharacterInventoryManager.EmptySlotCount(true) == 0) {
                    Bilgi("Depon dolu. 10 göz alabilirsin.");
                    return;
                }
                oyuncu.CharacterInventoryManager.RequestMoveFromInventoryToBank(g.yuva);
            }
            sonraki = Time.unscaledTime + 0.25f;
        }

        private float hataBitis = 0f;

        private void Bilgi(string metin) {
            durum.text = metin;
            durum.color = HataRengi;
            hataBitis = Time.unscaledTime + 3f;
        }

        private void SatinAl() {
            MobileFeedback.Tap();
            if (Depo.YerVar(Oyuncu) == false) {
                Bilgi("Depo en büyük boyutunda (1000 göz).");
                return;
            }
            string hata = Odeme.SatinAl(UrunKodu);
            if (hata != null) {
                Bilgi(hata);
            }
        }

        private void Update() {
            if (Acik && Time.unscaledTime >= sonraki) {
                Yenile();
            }
        }

        private static void Doldur(Goz g, InventorySlot yuva, bool kilitli) {
            g.yuva = yuva;
            if (kilitli) {
                g.arka.color = new Color(0.05f, 0.04f, 0.03f, 0.9f);
                g.simge.enabled = false;
                g.sayi.text = string.Empty;
                return;
            }
            g.arka.color = SatirRengi;
            InstantiatedItem esya = yuva != null && yuva.IsEmpty == false ? yuva.InstantiatedItem : null;
            Sprite sp = esya != null ? esya.Icon : null;
            g.simge.sprite = sp;
            g.simge.enabled = sp != null;
            int n = yuva != null ? yuva.Count : 0;
            g.sayi.text = n > 1 ? n.ToString() : string.Empty;
        }

        private void Yenile() {
            sonraki = Time.unscaledTime + 0.5f;
            UnitController oyuncu = Oyuncu;
            if (oyuncu == null || oyuncu.CharacterInventoryManager == null) {
                return;
            }
            CharacterInventoryManager env = oyuncu.CharacterInventoryManager;
            List<InventorySlot> depo = env.BankSlots;
            int depoSayfalar = Mathf.Clamp(Mathf.CeilToInt(depo.Count / (float)SayfaGozu), 1, EnCokSayfa);
            depoSayfasi = Mathf.Clamp(depoSayfasi, 0, depoSayfalar - 1);
            int bos = env.EmptySlotCount(true);
            depoBilgi.text = "Depo · sayfa " + (depoSayfasi + 1) + "/" + depoSayfalar + " · " + (depo.Count - bos) + "/" + depo.Count + " göz dolu";
            for (int i = 0; i < depoGozleri.Count; i++) {
                int sira = depoSayfasi * SayfaGozu + i;
                Doldur(depoGozleri[i], sira < depo.Count ? depo[sira] : null, sira >= depo.Count);
            }

            List<InventorySlot> canta = env.InventorySlots;
            int cantaSayfalar = Mathf.Max(1, Mathf.CeilToInt(canta.Count / (float)CantaSayfaGozu));
            cantaSayfasi = Mathf.Clamp(cantaSayfasi, 0, cantaSayfalar - 1);
            cantaBilgi.text = "Çanta · sayfa " + (cantaSayfasi + 1) + "/" + cantaSayfalar;
            for (int i = 0; i < cantaGozleri.Count; i++) {
                int sira = cantaSayfasi * CantaSayfaGozu + i;
                bool var = sira < canta.Count;
                cantaGozleri[i].go.SetActive(var);
                if (var) {
                    Doldur(cantaGozleri[i], canta[sira], false);
                }
            }

            string fiyat = Odeme.Fiyat(UrunKodu);
            DugmeYazisi(satinAlDugmesi, fiyat != null ? "10 Göz Al · " + fiyat : "10 Göz Al");
            if (Time.unscaledTime > hataBitis) {
                durum.color = IpucuRengi;
                durum.text = depo.Count + " göz (" + depo.Count / SayfaGozu + " tam sayfa)";
            }
        }
    }
}
