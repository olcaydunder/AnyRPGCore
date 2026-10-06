using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Oyuncu pazarı (çevrimiçi): oyuncu durduğu yerde bir pazar tezgâhı açar, çantasından en çok 8 yuva eşyayı
    /// (yığınlar bütün olarak) kendi koyduğu fiyatla satışa çıkarır. Başının üstünde pazarın adı görünür; yakındaki
    /// (25 m) oyuncular dokunup malları görür ve satın alır. Satış sunucuda olur: para alıcıdan satıcıya, eşya satıcıdan
    /// alıcıya geçer, ticaret.log'a yazılır. Pazar açıkken eşyalar satıcının çantasında kalır; satıcı yürürse, haritayı
    /// değiştirirse ya da oyundan çıkarsa pazar kapanır. Hesaba bağlı eşyalar satılamaz. Pazar adı süzgeçten geçer
    /// (küfür, hakaret, bağlantı olamaz) ve şikâyet edilebilir.
    /// Fiyat yazımı: "5a 20g 3b" (altın, gümüş, bakır); çıplak sayı gümüş sayılır.
    /// </summary>
    public class Pazar : OtukenPencere {

        public const int EnFazlaMal = 8;
        public const int BaslikSiniri = 32;

        // ================================================================ sunucu

        private class Mal {
            public long kimlik;
            public int fiyat;
        }

        private class Dukkan {
            public UnitController sahip;
            public string baslik;
            public Vector3 yer;
            public readonly List<Mal> mallar = new List<Mal>();
        }

        private static readonly Dictionary<UnitController, Dukkan> dukkanlar = new Dictionary<UnitController, Dukkan>();

        /// <summary>bu süreçte sunucuda yapılan satışlar (oyun testi)</summary>
        public static int SunucudaSatilan { get; private set; }

        public static bool SunucudaAcik(UnitController u) {
            return u != null && dukkanlar.ContainsKey(u);
        }

        private static void Sonuc(UnitController u, bool iyi, string mesaj) {
            OtukenAg.Yanitla(u, "pazar-sonuc", (iyi ? "1|" : "0|") + mesaj);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("pazar-ac", (oyuncu, veri) => {
                string[] p = veri.Split('\u001F');
                string baslik = p.Length > 0 ? p[0].Trim() : string.Empty;
                if (baslik.Length == 0) {
                    baslik = oyuncu.DisplayName + " Pazarı";
                }
                if (baslik.Length > BaslikSiniri) {
                    baslik = baslik.Substring(0, BaslikSiniri);
                }
                if (Sozguc.UygunsuzMu(baslik)) {
                    Sonuc(oyuncu, false, "Pazar adı uygunsuz bir kelime ya da bağlantı içeriyor.");
                    return;
                }
                if (Takas.SunucudaAcik(oyuncu)) {
                    Sonuc(oyuncu, false, "Takas sürerken pazar açılamaz.");
                    return;
                }
                Dukkan d = new Dukkan() { sahip = oyuncu, baslik = Ticaret.Duz(baslik), yer = oyuncu.transform.position };
                HashSet<InventorySlot> yuvalar = new HashSet<InventorySlot>();
                if (p.Length > 1) {
                    foreach (string parca in p[1].Split(';')) {
                        string[] kf = parca.Split(':');
                        long kimlik;
                        int fiyat;
                        if (kf.Length < 2 || long.TryParse(kf[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out kimlik) == false
                            || int.TryParse(kf[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out fiyat) == false) {
                            continue;
                        }
                        InstantiatedItem esya = Ticaret.CantadaBul(oyuncu, kimlik);
                        if (esya == null || fiyat <= 0 || yuvalar.Contains(esya.Slot)) {
                            continue;
                        }
                        if (Ticaret.BagliMi(esya)) {
                            Sonuc(oyuncu, false, esya.DisplayName + ": " + Ticaret.BagliNedeni(esya));
                            return;
                        }
                        yuvalar.Add(esya.Slot);
                        d.mallar.Add(new Mal() { kimlik = kimlik, fiyat = Mathf.Min(fiyat, 1000000000) });
                        if (d.mallar.Count >= EnFazlaMal) {
                            break;
                        }
                    }
                }
                if (d.mallar.Count == 0) {
                    Sonuc(oyuncu, false, "Satışa en az bir eşya ve fiyatını koy.");
                    return;
                }
                dukkanlar[oyuncu] = d;
                Ticaret.Defter("PAZAR AÇILDI " + oyuncu.DisplayName + " \"" + d.baslik + "\" " + d.mallar.Count + " mal");
                OtukenAg.Yanitla(oyuncu, "pazar-benim", "1\u001F" + d.baslik);
                Sonuc(oyuncu, true, "Pazarın açıldı: " + d.baslik + ". Yürürsen kapanır.");
            });
            OtukenAg.SunucuIsle("pazar-kapat", (oyuncu, veri) => {
                if (dukkanlar.ContainsKey(oyuncu)) {
                    Kapat(oyuncu, "Pazarını kapattın.");
                }
            });
            OtukenAg.SunucuIsle("pazar-liste", (oyuncu, veri) => {
                List<string> p = new List<string>();
                foreach (Dukkan d in dukkanlar.Values) {
                    if (d.sahip != null && d.sahip.gameObject.scene == oyuncu.gameObject.scene) {
                        float mesafe = Vector3.Distance(d.sahip.transform.position, oyuncu.transform.position);
                        if (mesafe <= 120f) {
                            p.Add(Ticaret.Duz(d.sahip.DisplayName) + "\u001D" + d.baslik + "\u001D" + mesafe.ToString("0", CultureInfo.InvariantCulture)
                                + "\u001D" + d.mallar.Count.ToString(CultureInfo.InvariantCulture));
                        }
                    }
                }
                OtukenAg.Yanitla(oyuncu, "pazar-liste", string.Join("\u001E", p));
            });
            OtukenAg.SunucuIsle("pazar-bak", (oyuncu, veri) => {
                UnitController sahip = Ticaret.OyuncuBul(veri);
                Dukkan d;
                if (sahip == null || dukkanlar.TryGetValue(sahip, out d) == false) {
                    Sonuc(oyuncu, false, "Bu pazar kapanmış.");
                    return;
                }
                OtukenAg.Yanitla(oyuncu, "pazar-mallar", Mallar(d));
            });
            OtukenAg.SunucuIsle("pazar-al", (oyuncu, veri) => {
                string[] p = veri.Split('\u001F');
                long kimlik;
                if (p.Length < 2 || long.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out kimlik) == false) {
                    return;
                }
                UnitController sahip = Ticaret.OyuncuBul(p[0]);
                Dukkan d;
                if (sahip == null || dukkanlar.TryGetValue(sahip, out d) == false) {
                    Sonuc(oyuncu, false, "Bu pazar kapanmış.");
                    return;
                }
                if (sahip == oyuncu) {
                    Sonuc(oyuncu, false, "Kendi pazarından alamazsın.");
                    return;
                }
                if (Ticaret.Yakin(oyuncu, sahip, Ticaret.PazarMesafesi) == false) {
                    Sonuc(oyuncu, false, "Almak için pazara yaklaş (" + Ticaret.PazarMesafesi.ToString("0") + " m).");
                    return;
                }
                Mal mal = d.mallar.Find(m => m.kimlik == kimlik);
                InstantiatedItem esya = mal != null ? Ticaret.CantadaBul(sahip, kimlik) : null;
                if (mal == null || esya == null) {
                    if (mal != null) {
                        d.mallar.Remove(mal);
                    }
                    Sonuc(oyuncu, false, "Bu mal artık satışta değil.");
                    OtukenAg.Yanitla(oyuncu, "pazar-mallar", Mallar(d));
                    return;
                }
                if (Ticaret.Para(oyuncu) < mal.fiyat) {
                    Sonuc(oyuncu, false, "Paran yetmiyor (" + ParaYazisi(mal.fiyat) + ").");
                    return;
                }
                if (oyuncu.CharacterInventoryManager.EmptySlotCount() < 1) {
                    Sonuc(oyuncu, false, "Çantanda boş yer yok.");
                    return;
                }
                List<InstantiatedItem> yigin = Ticaret.Yigin(esya);
                string ad = esya.DisplayName + (yigin.Count > 1 ? " ×" + yigin.Count : string.Empty);
                if (Ticaret.ParaAktar(oyuncu, sahip, mal.fiyat) == false) {
                    Sonuc(oyuncu, false, "Ödeme yapılamadı.");
                    return;
                }
                if (Ticaret.Tasi(sahip, oyuncu, yigin) == false) {
                    // eşya geçmediyse para geri
                    Ticaret.ParaAktar(sahip, oyuncu, mal.fiyat);
                    Sonuc(oyuncu, false, "Satış olmadı, paran geri verildi.");
                    return;
                }
                d.mallar.Remove(mal);
                Ticaret.Kaydet(oyuncu);
                Ticaret.Kaydet(sahip);
                Ticaret.Defter("PAZAR SATIŞ " + sahip.DisplayName + " -> " + oyuncu.DisplayName + ": " + ad + " #" + kimlik + " " + mal.fiyat + " bakır");
                SunucudaSatilan++;
                Sonuc(oyuncu, true, ad + " alındı (" + ParaYazisi(mal.fiyat) + ").");
                OtukenAg.Mesaj(sahip, "<color=#FFD54A>Pazar:</color> " + ad + " satıldı, +" + ParaYazisi(mal.fiyat));
                if (d.mallar.Count == 0) {
                    Kapat(sahip, "Pazarındaki her şey satıldı, pazar kapandı.");
                } else {
                    OtukenAg.Yanitla(oyuncu, "pazar-mallar", Mallar(d));
                }
            });

            // ---------------------------------------------------------------- telefon
            OtukenAg.IstemciDinle("pazar-sonuc", veri => {
                bool iyi = veri.StartsWith("1|");
                string mesaj = veri.Length > 2 ? veri.Substring(2) : veri;
                SonSonuc = mesaj;
                if (iyi) {
                    MobileFeedback.Success();
                }
                if (ornek != null && ornek.Acik) {
                    ornek.Bilgi(mesaj, iyi);
                }
                UnitController oyuncu = Oyuncu;
                if (oyuncu != null) {
                    OtukenAg.Mesaj(oyuncu, "<color=#FFD54A>Pazar:</color> " + mesaj);
                }
                if (iyi && mesaj.Contains(" alındı (")) {
                    AlinanSayisi++;
                }
            });
            OtukenAg.IstemciDinle("pazar-benim", veri => {
                string[] p = veri.Split('\u001F');
                BenimAcik = p[0] == "1";
                if (BenimAcik && ornek != null && ornek.Acik && ornek.kurKipi) {
                    ornek.Kapat();
                }
            });
            OtukenAg.IstemciDinle("pazar-liste", veri => {
                Pazarlar.Clear();
                if (string.IsNullOrEmpty(veri) == false) {
                    foreach (string parca in veri.Split('\u001E')) {
                        string[] p = parca.Split('\u001D');
                        if (p.Length >= 4) {
                            PazarBilgisi b = new PazarBilgisi() { sahip = p[0], baslik = p[1] };
                            float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out b.mesafe);
                            int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out b.malSayisi);
                            if (Engelleme.EngelliMi(b.sahip) == false) {
                                Pazarlar.Add(b);
                            }
                        }
                    }
                }
                ListeGeldi?.Invoke();
            });
            OtukenAg.IstemciDinle("pazar-mallar", veri => {
                Ensure();
                ornek.MallariGoster(veri);
            });
        }

        private static string Mallar(Dukkan d) {
            List<string> p = new List<string>();
            foreach (Mal m in d.mallar) {
                InstantiatedItem esya = Ticaret.CantadaBul(d.sahip, m.kimlik);
                if (esya != null) {
                    p.Add(Ticaret.Tanim(Ticaret.Yigin(esya)) + "\u001D" + m.fiyat.ToString(CultureInfo.InvariantCulture));
                }
            }
            return Ticaret.Duz(d.sahip.DisplayName) + "\u001F" + d.baslik + "\u001F" + string.Join("\u001E", p);
        }

        private static void Kapat(UnitController sahip, string mesaj) {
            Dukkan d;
            if (dukkanlar.TryGetValue(sahip, out d) == false) {
                return;
            }
            dukkanlar.Remove(sahip);
            Ticaret.Defter("PAZAR KAPANDI " + (sahip != null ? sahip.DisplayName : "?") + " (" + mesaj + ")");
            if (Ticaret.Etkin(sahip)) {
                OtukenAg.Yanitla(sahip, "pazar-benim", "0\u001F");
                OtukenAg.Mesaj(sahip, "<color=#FFD54A>Pazar:</color> " + mesaj);
            }
        }

        /// <summary>sunucu, saniyede bir: yürüyen, çıkan satıcının pazarı kapanır</summary>
        public static void SunucuTick() {
            if (dukkanlar.Count == 0) {
                return;
            }
            List<UnitController> kapanacak = new List<UnitController>();
            foreach (Dukkan d in dukkanlar.Values) {
                if (Ticaret.Etkin(d.sahip) == false) {
                    kapanacak.Add(d.sahip);
                } else if (Vector3.Distance(d.sahip.transform.position, d.yer) > 3f || d.sahip.CharacterStats.IsAlive == false) {
                    kapanacak.Add(d.sahip);
                }
            }
            foreach (UnitController u in kapanacak) {
                Kapat(u, "Yürüdüğün için pazarın kapandı.");
            }
        }

        // ================================================================ telefon

        public class PazarBilgisi {
            public string sahip;
            public string baslik;
            public float mesafe;
            public int malSayisi;
        }

        /// <summary>sunucunun son bildirdiği yakındaki pazarlar</summary>
        public static readonly List<PazarBilgisi> Pazarlar = new List<PazarBilgisi>();
        public static event Action ListeGeldi;

        public static bool BenimAcik { get; private set; }
        public static string SonSonuc { get; private set; } = "-";
        public static int AlinanSayisi { get; private set; }

        private static Pazar ornek = null;

        private static void Ensure() {
            if (ornek == null) {
                ornek = Kur<Pazar>("PazarCanvas", 34);
            }
        }

        public static void ListeIste() {
            if (Cevrimici.Acik) {
                OtukenAg.Gonder("pazar-liste");
            }
        }

        public static void Bak(string sahip) {
            OtukenAg.Gonder("pazar-bak", sahip);
        }

        public static void KurGoster() {
            if (Cevrimici.Acik == false) {
                UnitController oyuncu = Oyuncu;
                if (oyuncu != null) {
                    OtukenAg.Mesaj(oyuncu, "Pazar yalnız çevrimiçi oyunda kurulur.");
                }
                return;
            }
            Ensure();
            ornek.KurAc();
        }

        public static void KendiPazariniKapat() {
            OtukenAg.Gonder("pazar-kapat");
        }

        // ---------------------------------------------------------------- pencere

        private Text baslikYazisi = null;
        private Text bilgiYazisi = null;
        private RectTransform liste = null;
        private GameObject kurAlti = null;
        private InputField baslikAlani = null;
        private bool kurKipi = false;
        private string bakilanSahip = string.Empty;

        private class KurSatiri {
            public long kimlik;
            public InputField fiyat;
        }

        private readonly List<KurSatiri> kurSatirlari = new List<KurSatiri>();

        protected override void Kur() {
            PencereKur("Pazar", 1100f, 680f);
            baslikYazisi = Baslik("PAZAR");
            liste = Liste(panel.transform, Vector2.zero, Vector2.one, new Vector2(20f, 170f), new Vector2(-20f, -76f));
            bilgiYazisi = Yazi(Kutu(panel.transform, "Bilgi", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 100f), new Vector2(-24f, 166f)),
                string.Empty, 18, TextAnchor.MiddleLeft, IpucuRengi);

            kurAlti = Kutu(panel.transform, "KurAlti", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 100f));
            baslikAlani = Girdi(kurAlti.transform, "Pazarının adı (ör. Ucuz İksirler)", new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(24f, 24f), new Vector2(560f, 80f), BaslikSiniri);
            Dugme(kurAlti.transform, "Pazarı Aç", new Vector2(1f, 0f), new Vector2(-150f, 52f), new Vector2(240f, 62f), 24, OnayRengi, PazariAc);
        }

        public void Bilgi(string mesaj, bool iyi) {
            bilgiYazisi.text = mesaj;
            bilgiYazisi.color = iyi ? IyiRengi : HataRengi;
        }

        private void KurAc() {
            kurKipi = true;
            kok.SetActive(true);
            kurAlti.SetActive(true);
            baslikYazisi.text = "PAZAR KUR";
            bilgiYazisi.color = IpucuRengi;
            bilgiYazisi.text = "Satacağın eşyaların fiyatını yaz (ör. \"5a 20g\" = 5 Altın 20 Gümüş, \"30\" = 30 Gümüş). Fiyat yazılmayan satılmaz. "
                + "En çok " + EnFazlaMal + " eşya. Pazar açıkken yürürsen kapanır. Gerçek parayla satış yasaktır.";
            ListeyiTemizle(liste);
            kurSatirlari.Clear();
            UnitController oyuncu = Oyuncu;
            if (oyuncu == null) {
                return;
            }
            int i = 0;
            foreach (InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                if (yuva == null || yuva.IsEmpty) {
                    continue;
                }
                InstantiatedItem esya = yuva.InstantiatedItem;
                bool bagli = Ticaret.BagliMi(esya);
                GameObject satir = Satir(liste, i, 60f, bagli ? new Color(0.12f, 0.1f, 0.08f, 0.9f) : SatirRengi, null);
                Simge(satir.transform, esya.Icon, new Vector2(8f, -24f), new Vector2(56f, 24f));
                string renk = esya.ItemQuality != null ? ColorUtility.ToHtmlStringRGB(esya.ItemQuality.QualityColor) : "FFFFFF";
                KeyValuePair<Currency, int> satis = esya.Item.GetSellPrice(esya, oyuncu);
                int oneri = satis.Key != null ? satis.Value * Mathf.Max(1, yuva.Count) * 3 : 0;
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.6f, 1f), new Vector2(66f, 0f), Vector2.zero),
                    "<color=#" + renk + ">" + esya.DisplayName + "</color>" + (yuva.Count > 1 ? " ×" + yuva.Count : string.Empty)
                    + "\n<size=15><color=#B8A890>" + (bagli ? "hesaba bağlı, satılamaz" : "satıcı fiyatı " + ParaYazisi(satis.Value * Mathf.Max(1, yuva.Count))) + "</color></size>",
                    19, TextAnchor.MiddleLeft, YaziRengi);
                if (bagli == false) {
                    InputField fiyat = Girdi(satir.transform, oneri > 0 ? "ör. " + KisaPara(oneri) : "fiyat", new Vector2(0.6f, 0f), new Vector2(1f, 1f),
                        new Vector2(8f, 8f), new Vector2(-8f, -8f), 14);
                    kurSatirlari.Add(new KurSatiri() { kimlik = esya.InstanceId, fiyat = fiyat });
                }
                i++;
            }
            if (i == 0) {
                Yazi(Kutu(liste, "Bos", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -60f), new Vector2(-10f, -10f)),
                    "Çantan boş.", 22, TextAnchor.MiddleCenter, IpucuRengi);
            }
        }

        private static string KisaPara(int bakir) {
            int altin = bakir / 10000;
            int gumus = (bakir / 100) % 100;
            int kalan = bakir % 100;
            string s = string.Empty;
            if (altin > 0) {
                s += altin + "a ";
            }
            if (gumus > 0) {
                s += gumus + "g ";
            }
            if (kalan > 0 || s.Length == 0) {
                s += kalan + "b";
            }
            return s.Trim();
        }

        private void PazariAc() {
            MobileFeedback.Tap();
            List<string> mallar = new List<string>();
            foreach (KurSatiri k in kurSatirlari) {
                int fiyat = ParaOku(k.fiyat.text);
                if (fiyat > 0) {
                    mallar.Add(k.kimlik.ToString(CultureInfo.InvariantCulture) + ":" + fiyat.ToString(CultureInfo.InvariantCulture));
                }
            }
            if (mallar.Count == 0) {
                Bilgi("En az bir eşyanın fiyatını yaz.", false);
                return;
            }
            if (mallar.Count > EnFazlaMal) {
                Bilgi("En çok " + EnFazlaMal + " eşya satılabilir.", false);
                return;
            }
            string baslik = baslikAlani.text.Trim();
            if (Sozguc.UygunsuzMu(baslik)) {
                Bilgi("Pazar adı uygunsuz bir kelime ya da bağlantı içeriyor.", false);
                return;
            }
            OtukenAg.Gonder("pazar-ac", baslik.Replace('\u001F', ' ') + "\u001F" + string.Join(";", mallar));
        }

        private void MallariGoster(string veri) {
            string[] p = veri.Split('\u001F');
            if (p.Length < 3) {
                return;
            }
            kurKipi = false;
            bakilanSahip = p[0];
            kok.SetActive(true);
            kurAlti.SetActive(false);
            baslikYazisi.text = p[1] + "  <size=20><color=#B8A890>· " + p[0] + "</color></size>";
            ListeyiTemizle(liste);
            List<Ticaret.EsyaTanimi> mallar = Ticaret.TanimlariOku(p[2]);
            ilkMal = mallar.Count > 0 ? mallar[0].kimlik : -1;
            UnitController oyuncu = Oyuncu;
            int param = oyuncu != null ? Ticaret.Para(oyuncu) : 0;
            for (int i = 0; i < mallar.Count; i++) {
                Ticaret.EsyaTanimi t = mallar[i];
                GameObject satir = Satir(liste, i, 64f, SatirRengi, null);
                Simge(satir.transform, t.Simge, new Vector2(8f, -26f), new Vector2(60f, 26f));
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.55f, 1f), new Vector2(70f, 0f), Vector2.zero), t.Yazi, 20, TextAnchor.MiddleLeft, YaziRengi);
                Yazi(Kutu(satir.transform, "Fiyat", new Vector2(0.55f, 0f), new Vector2(0.8f, 1f), Vector2.zero, Vector2.zero),
                    ParaYazisi(t.fiyat), 19, TextAnchor.MiddleRight, t.fiyat <= param ? Altin : HataRengi);
                long kimlik = t.kimlik;
                string sahip = bakilanSahip;
                GameObject al = Dugme(satir.transform, "Satın Al", new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(150f, 48f), 19, OnayRengi, () => {
                    MobileFeedback.Tap();
                    OtukenAg.Gonder("pazar-al", sahip + "\u001F" + kimlik.ToString(CultureInfo.InvariantCulture));
                });
                if (sahip == (oyuncu != null ? oyuncu.DisplayName : string.Empty)) {
                    al.GetComponent<Button>().interactable = false;
                }
            }
            if (mallar.Count == 0) {
                Yazi(Kutu(liste, "Bos", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -60f), new Vector2(-10f, -10f)),
                    "Bu pazarda mal kalmamış.", 22, TextAnchor.MiddleCenter, IpucuRengi);
            }
            bilgiYazisi.color = IpucuRengi;
            bilgiYazisi.text = "Paran: " + ParaYazisi(param) + ". Satın alma geri alınamaz. Pazar adı uygunsuzsa Ticaret > Oyuncular'dan şikâyet edebilirsin.";
        }

        // ---------------------------------------------------------------- başların üstündeki pazar adları

        private readonly Dictionary<string, Text> etiketler = new Dictionary<string, Text>();
        private float sonrakiListe = 0f;

        /// <summary>çevrimiçi oyunda yakındaki pazarlar 4 saniyede bir sorulur, adları satıcının başının üstünde görünür</summary>
        public static void Tick(SystemGameManager oyun, bool oyunda) {
            if (oyunda == false || Cevrimici.Acik == false) {
                if (ornek != null) {
                    ornek.EtiketleriGizle();
                }
                return;
            }
            Ensure();
            if (Time.unscaledTime >= ornek.sonrakiListe) {
                ornek.sonrakiListe = Time.unscaledTime + 4f;
                ListeIste();
            }
        }

        private void EtiketleriGizle() {
            foreach (Text t in etiketler.Values) {
                if (t != null) {
                    t.transform.parent.gameObject.SetActive(false);
                }
            }
        }

        private readonly List<UnitController> oyuncular = new List<UnitController>();
        private float sonrakiArama = 0f;

        private void LateUpdate() {
            if (Cevrimici.Acik == false || Pazarlar.Count == 0) {
                if (etiketler.Count > 0) {
                    EtiketleriGizle();
                }
                return;
            }
            Camera kamera = Camera.main;
            if (kamera == null) {
                return;
            }
            if (Time.unscaledTime >= sonrakiArama) {
                sonrakiArama = Time.unscaledTime + 2f;
                oyuncular.Clear();
                foreach (UnitController u in FindObjectsByType<UnitController>(FindObjectsSortMode.None)) {
                    if (u != null && u.UnitControllerMode == UnitControllerMode.Player) {
                        oyuncular.Add(u);
                    }
                }
            }
            HashSet<string> gorunen = new HashSet<string>();
            foreach (PazarBilgisi b in Pazarlar) {
                UnitController satici = oyuncular.Find(u => u != null && u.DisplayName == b.sahip);
                if (satici == null) {
                    continue;
                }
                Vector3 ekran = kamera.WorldToScreenPoint(satici.transform.position + Vector3.up * 2.7f);
                if (ekran.z <= 0f) {
                    continue;
                }
                Text etiket;
                if (etiketler.TryGetValue(b.sahip, out etiket) == false || etiket == null) {
                    etiket = EtiketKur(b.sahip);
                    etiketler[b.sahip] = etiket;
                }
                etiket.text = b.baslik;
                RectTransform rt = etiket.transform.parent as RectTransform;
                rt.gameObject.SetActive(true);
                Canvas tuval = GetComponent<Canvas>();
                float olcek = tuval != null ? tuval.scaleFactor : 1f;
                rt.anchoredPosition = new Vector2(ekran.x / olcek, ekran.y / olcek);
                gorunen.Add(b.sahip);
            }
            foreach (KeyValuePair<string, Text> e in etiketler) {
                if (e.Value != null && gorunen.Contains(e.Key) == false) {
                    e.Value.transform.parent.gameObject.SetActive(false);
                }
            }
        }

        private Text EtiketKur(string sahip) {
            GameObject go = Kutu(transform, "Etiket_" + sahip, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            go.transform.SetAsFirstSibling();
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240f, 40f);
            rt.pivot = new Vector2(0.5f, 0f);
            Image arka = go.AddComponent<Image>();
            arka.color = new Color(0.25f, 0.15f, 0.05f, 0.85f);
            Outline cizgi = go.AddComponent<Outline>();
            cizgi.effectColor = Altin;
            cizgi.effectDistance = new Vector2(1f, -1f);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = arka;
            b.onClick.AddListener(() => {
                MobileFeedback.Tap();
                Bak(sahip);
            });
            Text t = Yazi(Kutu(go.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(6f, 0f), new Vector2(-6f, 0f)), string.Empty, 18, TextAnchor.MiddleCenter, Altin);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        public override void Kapat() {
            kurKipi = false;
            base.Kapat();
        }

        // ---------------------------------------------------------------- ağ botu

        /// <summary>ağ botu: çantadaki ilk satılabilir eşyayla pazar açar</summary>
        public static bool TestIcinAc(int fiyat) {
            UnitController oyuncu = Oyuncu;
            if (oyuncu == null) {
                return false;
            }
            foreach (InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                if (yuva != null && yuva.IsEmpty == false && Ticaret.BagliMi(yuva.InstantiatedItem) == false) {
                    OtukenAg.Gonder("pazar-ac", "Deneme Pazarı\u001F" + yuva.InstantiatedItem.InstanceId.ToString(CultureInfo.InvariantCulture)
                        + ":" + fiyat.ToString(CultureInfo.InvariantCulture));
                    return true;
                }
            }
            return false;
        }

        /// <summary>ağ botu: pazarın ilk malını satın alır (mallar geldiyse)</summary>
        public static void TestIcinAl(string sahip, long kimlik) {
            OtukenAg.Gonder("pazar-al", sahip + "\u001F" + kimlik.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>ağ botu: son bakılan pazarın ilk malı (kimlik), yoksa -1</summary>
        public static long TestIcinIlkMal {
            get { return ornek != null ? ornek.ilkMal : -1; }
        }

        private long ilkMal = -1;
    }
}
