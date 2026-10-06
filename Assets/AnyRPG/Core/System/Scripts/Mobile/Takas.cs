using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Oyuncular arası takas (çevrimiçi). Bir oyuncu yakınındaki (15 m) oyuncuya takas teklif eder; o kabul edince iki
    /// tarafın da takas penceresi açılır. Her taraf çantasından en çok 9 yuva eşya (yığınlar bütün olarak) ve para koyar.
    /// Bir şey değişince iki tarafın onayı düşer; ikisi de onaylayınca sunucu son kez denetler (eşyalar hâlâ çantada mı,
    /// para yetiyor mu, çantada yer var mı, hesaba bağlı eşya var mı) ve hepsini birden değiştirir. Takas geri alınmaz,
    /// sunucu her takası ticaret.log'a yazar. Uzaklaşan, haritayı değiştiren ya da oyundan çıkan olursa takas kapanır.
    /// Engellenen oyuncunun teklifi gösterilmeden reddedilir.
    /// </summary>
    public class Takas : OtukenPencere {

        public const int EnFazlaYigin = 9;

        // ================================================================ sunucu

        private class Taraf {
            public UnitController oyuncu;
            public readonly List<long> esyalar = new List<long>();
            public int para;
            public bool onay;
        }

        private class Oturum {
            public Taraf a;
            public Taraf b;
            public float baslangic;

            public Taraf Ben(UnitController u) {
                return a.oyuncu == u ? a : b;
            }

            public Taraf Karsi(UnitController u) {
                return a.oyuncu == u ? b : a;
            }
        }

        private static readonly Dictionary<UnitController, Oturum> oturumlar = new Dictionary<UnitController, Oturum>();
        private static readonly Dictionary<UnitController, KeyValuePair<UnitController, float>> davetler = new Dictionary<UnitController, KeyValuePair<UnitController, float>>();

        /// <summary>bu süreçte sunucuda tamamlanan takaslar (oyun testi)</summary>
        public static int SunucudaTamamlanan { get; private set; }

        private static float Simdi {
            get { return Time.realtimeSinceStartup; }
        }

        private static void Hata(UnitController u, string mesaj) {
            OtukenAg.Yanitla(u, "takas-bilgi", "0|" + mesaj);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("takas-iste", (oyuncu, ad) => {
                UnitController hedef = Ticaret.OyuncuBul(ad);
                if (hedef == null || hedef == oyuncu) {
                    Hata(oyuncu, "Oyuncu bulunamadı.");
                    return;
                }
                if (Ticaret.Yakin(oyuncu, hedef, Ticaret.TakasMesafesi) == false) {
                    Hata(oyuncu, "Takas için " + hedef.DisplayName + " oyuncusunun yanına git (" + Ticaret.TakasMesafesi.ToString("0") + " m).");
                    return;
                }
                if (oturumlar.ContainsKey(oyuncu) || oturumlar.ContainsKey(hedef) || Pazar.SunucudaAcik(oyuncu) || Pazar.SunucudaAcik(hedef)) {
                    Hata(oyuncu, "Şu an takas yapılamıyor (biriniz başka bir takasta ya da pazarda).");
                    return;
                }
                davetler[hedef] = new KeyValuePair<UnitController, float>(oyuncu, Simdi);
                OtukenAg.Yanitla(hedef, "takas-davet", oyuncu.DisplayName);
                OtukenAg.Yanitla(oyuncu, "takas-bilgi", "1|" + hedef.DisplayName + " oyuncusuna takas teklif edildi, yanıtı bekleniyor.");
            });
            OtukenAg.SunucuIsle("takas-cevap", (oyuncu, veri) => {
                KeyValuePair<UnitController, float> d;
                if (davetler.TryGetValue(oyuncu, out d) == false) {
                    return;
                }
                davetler.Remove(oyuncu);
                UnitController eden = d.Key;
                if (veri != "1") {
                    if (Ticaret.Etkin(eden)) {
                        Hata(eden, oyuncu.DisplayName + " takası kabul etmedi.");
                    }
                    return;
                }
                if (Simdi - d.Value > 40f || Ticaret.Etkin(eden) == false || Ticaret.Yakin(oyuncu, eden, Ticaret.TakasMesafesi * 1.5f) == false
                    || oturumlar.ContainsKey(eden) || oturumlar.ContainsKey(oyuncu)) {
                    Hata(oyuncu, "Takas teklifinin süresi geçti.");
                    return;
                }
                Oturum o = new Oturum() { a = new Taraf() { oyuncu = eden }, b = new Taraf() { oyuncu = oyuncu }, baslangic = Simdi };
                oturumlar[eden] = o;
                oturumlar[oyuncu] = o;
                DurumGonder(o);
            });
            OtukenAg.SunucuIsle("takas-koy", (oyuncu, veri) => {
                Oturum o;
                long kimlik;
                if (oturumlar.TryGetValue(oyuncu, out o) == false || long.TryParse(veri, NumberStyles.Integer, CultureInfo.InvariantCulture, out kimlik) == false) {
                    return;
                }
                Taraf ben = o.Ben(oyuncu);
                InstantiatedItem esya = Ticaret.CantadaBul(oyuncu, kimlik);
                if (esya == null) {
                    Hata(oyuncu, "Eşya çantanda bulunamadı.");
                    return;
                }
                if (Ticaret.BagliMi(esya)) {
                    Hata(oyuncu, Ticaret.BagliNedeni(esya));
                    return;
                }
                foreach (InstantiatedItem e in Ticaret.Yigin(esya)) {
                    if (ben.esyalar.Contains(e.InstanceId)) {
                        return;
                    }
                }
                if (ben.esyalar.Count >= EnFazlaYigin) {
                    Hata(oyuncu, "Bir takasa en çok " + EnFazlaYigin + " yuva eşya konur.");
                    return;
                }
                ben.esyalar.Add(esya.InstanceId);
                OnaylariDusur(o);
                DurumGonder(o);
            });
            OtukenAg.SunucuIsle("takas-cikar", (oyuncu, veri) => {
                Oturum o;
                long kimlik;
                if (oturumlar.TryGetValue(oyuncu, out o) == false || long.TryParse(veri, NumberStyles.Integer, CultureInfo.InvariantCulture, out kimlik) == false) {
                    return;
                }
                if (o.Ben(oyuncu).esyalar.Remove(kimlik)) {
                    OnaylariDusur(o);
                    DurumGonder(o);
                }
            });
            OtukenAg.SunucuIsle("takas-para", (oyuncu, veri) => {
                Oturum o;
                int bakir;
                if (oturumlar.TryGetValue(oyuncu, out o) == false || int.TryParse(veri, NumberStyles.Integer, CultureInfo.InvariantCulture, out bakir) == false) {
                    return;
                }
                bakir = Mathf.Clamp(bakir, 0, Ticaret.Para(oyuncu));
                o.Ben(oyuncu).para = bakir;
                OnaylariDusur(o);
                DurumGonder(o);
            });
            OtukenAg.SunucuIsle("takas-onay", (oyuncu, veri) => {
                Oturum o;
                if (oturumlar.TryGetValue(oyuncu, out o) == false) {
                    return;
                }
                o.Ben(oyuncu).onay = veri != "0";
                if (o.a.onay && o.b.onay) {
                    Tamamla(o);
                } else {
                    DurumGonder(o);
                }
            });
            OtukenAg.SunucuIsle("takas-iptal", (oyuncu, veri) => {
                Oturum o;
                if (oturumlar.TryGetValue(oyuncu, out o)) {
                    Bitir(o, "0|" + oyuncu.DisplayName + " takası iptal etti.");
                }
                davetler.Remove(oyuncu);
            });

            // ---------------------------------------------------------------- telefon
            OtukenAg.IstemciDinle("takas-davet", veri => {
                if (Engelleme.EngelliMi(veri)) {
                    // engellenen oyuncunun teklifi gösterilmez
                    OtukenAg.Gonder("takas-cevap", "0");
                    return;
                }
                if (OtoKabul) {
                    OtukenAg.Gonder("takas-cevap", "1");
                    return;
                }
                Ensure();
                ornek.DavetGoster(veri);
            });
            OtukenAg.IstemciDinle("takas-durum", veri => {
                Ensure();
                ornek.DurumGoster(veri);
            });
            OtukenAg.IstemciDinle("takas-bilgi", veri => {
                bool iyi = veri.StartsWith("1|");
                string mesaj = veri.Length > 2 ? veri.Substring(2) : veri;
                SonBilgi = mesaj;
                if (ornek != null && ornek.Acik) {
                    ornek.Bilgi(mesaj, iyi);
                } else {
                    MesajYaz(mesaj);
                }
            });
            OtukenAg.IstemciDinle("takas-bitti", veri => {
                bool iyi = veri.StartsWith("1|");
                string mesaj = veri.Length > 2 ? veri.Substring(2) : veri;
                SonBilgi = mesaj;
                if (iyi) {
                    TamamlananSayisi++;
                    MobileFeedback.Success();
                }
                if (ornek != null) {
                    ornek.Kapat();
                }
                MesajYaz(mesaj);
            });
        }

        private static void OnaylariDusur(Oturum o) {
            o.a.onay = false;
            o.b.onay = false;
        }

        private static List<List<InstantiatedItem>> Yiginlar(Taraf t, out string hata) {
            hata = null;
            List<List<InstantiatedItem>> liste = new List<List<InstantiatedItem>>();
            foreach (long kimlik in t.esyalar) {
                InstantiatedItem esya = Ticaret.CantadaBul(t.oyuncu, kimlik);
                if (esya == null) {
                    hata = t.oyuncu.DisplayName + " oyuncusunun koyduğu bir eşya artık çantasında değil.";
                    return null;
                }
                if (Ticaret.BagliMi(esya)) {
                    hata = Ticaret.BagliNedeni(esya);
                    return null;
                }
                liste.Add(Ticaret.Yigin(esya));
            }
            return liste;
        }

        private static void DurumGonder(Oturum o) {
            OtukenAg.Yanitla(o.a.oyuncu, "takas-durum", Durum(o, o.a.oyuncu));
            OtukenAg.Yanitla(o.b.oyuncu, "takas-durum", Durum(o, o.b.oyuncu));
        }

        private static string Durum(Oturum o, UnitController kime) {
            Taraf ben = o.Ben(kime);
            Taraf karsi = o.Karsi(kime);
            return Ticaret.Duz(karsi.oyuncu.DisplayName) + "\u001F" + Tanimlar(ben) + "\u001F" + Tanimlar(karsi) + "\u001F"
                + ben.para.ToString(CultureInfo.InvariantCulture) + "\u001F" + karsi.para.ToString(CultureInfo.InvariantCulture) + "\u001F"
                + (ben.onay ? "1" : "0") + "\u001F" + (karsi.onay ? "1" : "0");
        }

        private static string Tanimlar(Taraf t) {
            List<string> p = new List<string>();
            foreach (long kimlik in t.esyalar) {
                InstantiatedItem esya = Ticaret.CantadaBul(t.oyuncu, kimlik);
                if (esya != null) {
                    p.Add(Ticaret.Tanim(Ticaret.Yigin(esya)));
                }
            }
            return string.Join("\u001E", p);
        }

        private static void Tamamla(Oturum o) {
            UnitController a = o.a.oyuncu;
            UnitController b = o.b.oyuncu;
            if (Ticaret.Etkin(a) == false || Ticaret.Etkin(b) == false || Ticaret.Yakin(a, b, Ticaret.TakasMesafesi * 1.5f) == false) {
                Bitir(o, "0|Takas olmadı: oyuncular birbirinden uzak.");
                return;
            }
            string hata;
            List<List<InstantiatedItem>> aVerir = Yiginlar(o.a, out hata);
            List<List<InstantiatedItem>> bVerir = hata == null ? Yiginlar(o.b, out hata) : null;
            if (hata != null) {
                OnaylariDusur(o);
                Hata(a, hata);
                Hata(b, hata);
                DurumGonder(o);
                return;
            }
            if (a.CharacterInventoryManager.EmptySlotCount() < bVerir.Count || b.CharacterInventoryManager.EmptySlotCount() < aVerir.Count) {
                OnaylariDusur(o);
                string yer = "Takas olmadı: çantada yeterli boş yer yok.";
                Hata(a, yer);
                Hata(b, yer);
                DurumGonder(o);
                return;
            }
            if (Ticaret.Para(a) < o.a.para || Ticaret.Para(b) < o.b.para) {
                OnaylariDusur(o);
                string para = "Takas olmadı: konan para artık yok.";
                Hata(a, para);
                Hata(b, para);
                DurumGonder(o);
                return;
            }
            foreach (List<InstantiatedItem> y in aVerir) {
                Ticaret.Tasi(a, b, y);
            }
            foreach (List<InstantiatedItem> y in bVerir) {
                Ticaret.Tasi(b, a, y);
            }
            Ticaret.ParaAktar(a, b, o.a.para);
            Ticaret.ParaAktar(b, a, o.b.para);
            Ticaret.Kaydet(a);
            Ticaret.Kaydet(b);
            Ticaret.Defter("TAKAS " + a.DisplayName + " -> " + b.DisplayName + ": " + Ticaret.Ozet(aVerir) + " + " + o.a.para + " bakır | "
                + b.DisplayName + " -> " + a.DisplayName + ": " + Ticaret.Ozet(bVerir) + " + " + o.b.para + " bakır");
            SunucudaTamamlanan++;
            Bitir(o, "1|Takas tamamlandı.");
        }

        private static void Bitir(Oturum o, string mesaj) {
            oturumlar.Remove(o.a.oyuncu);
            oturumlar.Remove(o.b.oyuncu);
            if (Ticaret.Etkin(o.a.oyuncu)) {
                OtukenAg.Yanitla(o.a.oyuncu, "takas-bitti", mesaj);
            }
            if (Ticaret.Etkin(o.b.oyuncu)) {
                OtukenAg.Yanitla(o.b.oyuncu, "takas-bitti", mesaj);
            }
        }

        public static bool SunucudaAcik(UnitController u) {
            return u != null && oturumlar.ContainsKey(u);
        }

        /// <summary>sunucu, saniyede bir: uzaklaşan, çıkan, uzun süren takaslar kapanır; eski teklifler silinir</summary>
        public static void SunucuTick() {
            if (oturumlar.Count > 0) {
                List<Oturum> kapanacak = new List<Oturum>();
                foreach (Oturum o in oturumlar.Values) {
                    if (kapanacak.Contains(o)) {
                        continue;
                    }
                    if (Ticaret.Etkin(o.a.oyuncu) == false || Ticaret.Etkin(o.b.oyuncu) == false
                        || Ticaret.Yakin(o.a.oyuncu, o.b.oyuncu, Ticaret.TakasMesafesi * 2f) == false || Simdi - o.baslangic > 600f) {
                        kapanacak.Add(o);
                    }
                }
                foreach (Oturum o in kapanacak) {
                    Bitir(o, "0|Takas kapandı (uzaklaşıldı, oyundan çıkıldı ya da süre doldu).");
                }
            }
            if (davetler.Count > 0) {
                List<UnitController> eski = new List<UnitController>();
                foreach (KeyValuePair<UnitController, KeyValuePair<UnitController, float>> d in davetler) {
                    if (Simdi - d.Value.Value > 60f || Ticaret.Etkin(d.Key) == false) {
                        eski.Add(d.Key);
                    }
                }
                foreach (UnitController u in eski) {
                    davetler.Remove(u);
                }
            }
        }

        // ================================================================ telefon

        /// <summary>ağ botu: gelen takas teklifleri kendiliğinden kabul edilir</summary>
        public static bool OtoKabul = false;

        public static int TamamlananSayisi { get; private set; }
        public static string SonBilgi { get; private set; } = "-";

        private static Takas ornek = null;

        private static void Ensure() {
            if (ornek == null) {
                ornek = Kur<Takas>("TakasCanvas", 34);
            }
        }

        private static void MesajYaz(string mesaj) {
            UnitController oyuncu = Oyuncu;
            if (oyuncu != null) {
                OtukenAg.Mesaj(oyuncu, "<color=#FFD54A>Takas:</color> " + mesaj);
            }
        }

        /// <summary>yakındaki oyuncuya takas teklif eder</summary>
        public static void Iste(string ad) {
            if (Cevrimici.Acik == false) {
                MesajYaz("Takas yalnız çevrimiçi oyunda yapılır.");
                return;
            }
            OtukenAg.Gonder("takas-iste", ad);
        }

        public static bool AcikMi {
            get { return ornek != null && ornek.Acik; }
        }

        // ---------------------------------------------------------------- pencere

        private GameObject davetKoku = null;
        private Text davetYazisi = null;
        private string davetEden = string.Empty;

        private Text baslik = null;
        private RectTransform benimListem = null;
        private RectTransform karsiListe = null;
        private Text benimParam = null;
        private Text karsiPara = null;
        private Text karsiOnayYazisi = null;
        private Text bilgiYazisi = null;
        private InputField paraAlani = null;
        private GameObject onayDugmesi = null;
        private GameObject secimKoku = null;
        private RectTransform secimListesi = null;

        private bool benOnay = false;
        private List<Ticaret.EsyaTanimi> benimEsyalarim = new List<Ticaret.EsyaTanimi>();

        protected override void Kur() {
            PencereKur("Takas", 1180f, 680f);
            baslik = Baslik("TAKAS");

            // iki sütun
            Yazi(Kutu(panel.transform, "BenBaslik", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(24f, -104f), new Vector2(-10f, -70f)),
                "Senin teklifin  <size=16><color=#B8A890>(çıkarmak için dokun)</color></size>", 22, TextAnchor.MiddleLeft, Altin);
            karsiOnayYazisi = Yazi(Kutu(panel.transform, "KarsiBaslik", new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(10f, -104f), new Vector2(-24f, -70f)),
                string.Empty, 22, TextAnchor.MiddleLeft, Altin);
            benimListem = Liste(panel.transform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(20f, 210f), new Vector2(-10f, -108f));
            karsiListe = Liste(panel.transform, new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(10f, 210f), new Vector2(-20f, -108f));
            benimParam = Yazi(Kutu(panel.transform, "BenimPara", new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(24f, 170f), new Vector2(-10f, 205f)),
                string.Empty, 20, TextAnchor.MiddleLeft, YaziRengi);
            karsiPara = Yazi(Kutu(panel.transform, "KarsiPara", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(10f, 170f), new Vector2(-24f, 205f)),
                string.Empty, 20, TextAnchor.MiddleLeft, YaziRengi);

            // alt sıra: eşya ekle, para, onayla, iptal
            Dugme(panel.transform, "Eşya Ekle", new Vector2(0f, 0f), new Vector2(110f, 130f), new Vector2(180f, 56f), 20, DugmeRengi, () => {
                MobileFeedback.Tap();
                SecimGoster();
            });
            paraAlani = Girdi(panel.transform, "para (ör. 5g 20b)", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(214f, 102f), new Vector2(424f, 158f), 16);
            Dugme(panel.transform, "Parayı Koy", new Vector2(0f, 0f), new Vector2(510f, 130f), new Vector2(160f, 56f), 20, DugmeRengi, () => {
                MobileFeedback.Tap();
                OtukenAg.Gonder("takas-para", ParaOku(paraAlani.text).ToString(CultureInfo.InvariantCulture));
            });
            onayDugmesi = Dugme(panel.transform, "Onayla", new Vector2(1f, 0f), new Vector2(-330f, 130f), new Vector2(200f, 60f), 22, OnayRengi, () => {
                MobileFeedback.Tap();
                OtukenAg.Gonder("takas-onay", benOnay ? "0" : "1");
            });
            Dugme(panel.transform, "İptal", new Vector2(1f, 0f), new Vector2(-120f, 130f), new Vector2(180f, 60f), 22, TehlikeRengi, () => {
                MobileFeedback.Tap();
                OtukenAg.Gonder("takas-iptal");
                Kapat();
            });
            bilgiYazisi = Yazi(Kutu(panel.transform, "Bilgi", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 14f), new Vector2(-24f, 92f)),
                string.Empty, 18, TextAnchor.MiddleLeft, IpucuRengi);

            // eşya seçimi (çantadan)
            secimKoku = Kutu(panel.transform, "Secim", Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            secimKoku.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.03f, 0.98f);
            Yazi(Kutu(secimKoku.transform, "SecimBaslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -60f), new Vector2(-140f, -8f)),
                "Takasa koyacağın eşyayı seç", 26, TextAnchor.MiddleLeft, Altin);
            Dugme(secimKoku.transform, "Geri", new Vector2(1f, 1f), new Vector2(-70f, -34f), new Vector2(110f, 46f), 20, DugmeRengi, () => {
                MobileFeedback.Tap();
                secimKoku.SetActive(false);
            });
            secimListesi = Liste(secimKoku.transform, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -68f));
            secimKoku.SetActive(false);

            // davet
            davetKoku = Kutu(transform, "Davet", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-330f, -230f), new Vector2(330f, -60f));
            davetKoku.AddComponent<Image>().color = PanelRengi;
            Outline cizgi = davetKoku.AddComponent<Outline>();
            cizgi.effectColor = Altin;
            cizgi.effectDistance = new Vector2(2f, -2f);
            davetYazisi = Yazi(Kutu(davetKoku.transform, "Yazi", new Vector2(0f, 0.45f), Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, -10f)),
                string.Empty, 24, TextAnchor.MiddleCenter, YaziRengi);
            Dugme(davetKoku.transform, "Kabul Et", new Vector2(0.5f, 0f), new Vector2(-200f, 44f), new Vector2(180f, 56f), 22, OnayRengi, () => {
                MobileFeedback.Tap();
                davetKoku.SetActive(false);
                OtukenAg.Gonder("takas-cevap", "1");
            });
            Dugme(davetKoku.transform, "Reddet", new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(180f, 56f), 22, DugmeRengi, () => {
                MobileFeedback.Tap();
                davetKoku.SetActive(false);
                OtukenAg.Gonder("takas-cevap", "0");
            });
            Dugme(davetKoku.transform, "Engelle", new Vector2(0.5f, 0f), new Vector2(200f, 44f), new Vector2(180f, 56f), 22, TehlikeRengi, () => {
                MobileFeedback.Tap();
                davetKoku.SetActive(false);
                OtukenAg.Gonder("takas-cevap", "0");
                Engelleme.Engelle(davetEden);
            });
            davetKoku.SetActive(false);
        }

        private void DavetGoster(string eden) {
            davetEden = eden;
            davetYazisi.text = "<b>" + eden + "</b> seninle takas yapmak istiyor.";
            davetKoku.SetActive(true);
            davetKoku.transform.SetAsLastSibling();
            MobileFeedback.Medium();
            davetSayaci++;
            StartCoroutine(DavetiKapat(davetSayaci));
        }

        private int davetSayaci = 0;

        private System.Collections.IEnumerator DavetiKapat(int sayac) {
            yield return new WaitForSecondsRealtime(30f);
            if (sayac == davetSayaci && davetKoku != null && davetKoku.activeSelf) {
                davetKoku.SetActive(false);
                OtukenAg.Gonder("takas-cevap", "0");
            }
        }

        public void Bilgi(string mesaj, bool iyi) {
            bilgiYazisi.text = mesaj;
            bilgiYazisi.color = iyi ? IyiRengi : HataRengi;
        }

        private void DurumGoster(string veri) {
            string[] p = veri.Split('\u001F');
            if (p.Length < 7) {
                return;
            }
            kok.SetActive(true);
            if (davetKoku.activeSelf) {
                davetKoku.SetActive(false);
            }
            string karsiAd = p[0];
            baslik.text = "TAKAS  ·  " + karsiAd;
            benimEsyalarim = Ticaret.TanimlariOku(p[1]);
            List<Ticaret.EsyaTanimi> onunkiler = Ticaret.TanimlariOku(p[2]);
            int benimPara;
            int onunPara;
            int.TryParse(p[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out benimPara);
            int.TryParse(p[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out onunPara);
            benOnay = p[5] == "1";
            bool onunOnayi = p[6] == "1";

            ListeyiTemizle(benimListem);
            for (int i = 0; i < benimEsyalarim.Count; i++) {
                Ticaret.EsyaTanimi t = benimEsyalarim[i];
                long kimlik = t.kimlik;
                EsyaSatiri(benimListem, i, t, () => {
                    MobileFeedback.Tap();
                    OtukenAg.Gonder("takas-cikar", kimlik.ToString(CultureInfo.InvariantCulture));
                });
            }
            ListeyiTemizle(karsiListe);
            for (int i = 0; i < onunkiler.Count; i++) {
                EsyaSatiri(karsiListe, i, onunkiler[i], null);
            }
            benimParam.text = "Para: <color=#FFD54A>" + ParaYazisi(benimPara) + "</color>";
            karsiPara.text = "Para: <color=#FFD54A>" + ParaYazisi(onunPara) + "</color>";
            karsiOnayYazisi.text = karsiAd + " teklifi  " + (onunOnayi ? "<color=#8CE070>(onayladı)</color>" : "<size=16><color=#B8A890>(onay bekleniyor)</color></size>");
            DugmeYazisi(onayDugmesi, benOnay ? "Onayı Geri Al" : "Onayla");
            onayDugmesi.GetComponent<Image>().color = benOnay ? VurguRengi : OnayRengi;
            Bilgi(benOnay && onunOnayi ? "Takas yapılıyor..." :
                "İkiniz de onaylayınca takas olur ve geri alınamaz. Bir şey değişirse onaylar düşer. Gerçek para karşılığı eşya, para ya da hesap satmak yasaktır.",
                true);
            bilgiYazisi.color = IpucuRengi;
        }

        private void EsyaSatiri(RectTransform liste, int i, Ticaret.EsyaTanimi t, UnityEngine.Events.UnityAction tik) {
            GameObject satir = Satir(liste, i, 56f, SatirRengi, tik);
            Simge(satir.transform, t.Simge, new Vector2(8f, -22f), new Vector2(52f, 22f));
            Yazi(Kutu(satir.transform, "Ad", Vector2.zero, Vector2.one, new Vector2(62f, 0f), new Vector2(-8f, 0f)), t.Yazi, 20, TextAnchor.MiddleLeft, YaziRengi);
        }

        private void SecimGoster() {
            ListeyiTemizle(secimListesi);
            UnitController oyuncu = Oyuncu;
            if (oyuncu == null) {
                return;
            }
            int i = 0;
            HashSet<long> konanlar = new HashSet<long>();
            foreach (Ticaret.EsyaTanimi t in benimEsyalarim) {
                konanlar.Add(t.kimlik);
            }
            foreach (InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                if (yuva == null || yuva.IsEmpty) {
                    continue;
                }
                InstantiatedItem esya = yuva.InstantiatedItem;
                bool konmus = false;
                foreach (long k in yuva.InstantiatedItems.Keys) {
                    if (konanlar.Contains(k)) {
                        konmus = true;
                    }
                }
                if (konmus) {
                    continue;
                }
                bool bagli = Ticaret.BagliMi(esya);
                long kimlik = esya.InstanceId;
                GameObject satir = Satir(secimListesi, i, 56f, bagli ? new Color(0.12f, 0.1f, 0.08f, 0.9f) : SatirRengi, bagli ? (UnityEngine.Events.UnityAction)null : () => {
                    MobileFeedback.Tap();
                    secimKoku.SetActive(false);
                    OtukenAg.Gonder("takas-koy", kimlik.ToString(CultureInfo.InvariantCulture));
                });
                Simge(satir.transform, esya.Icon, new Vector2(8f, -22f), new Vector2(52f, 22f));
                string renk = esya.ItemQuality != null ? ColorUtility.ToHtmlStringRGB(esya.ItemQuality.QualityColor) : "FFFFFF";
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, Vector2.one, new Vector2(62f, 0f), new Vector2(-8f, 0f)),
                    "<color=#" + renk + ">" + esya.DisplayName + "</color>" + (yuva.Count > 1 ? " ×" + yuva.Count : string.Empty)
                    + (bagli ? "  <size=16><color=#B8A890>hesaba bağlı</color></size>" : string.Empty), 20, TextAnchor.MiddleLeft, YaziRengi);
                i++;
            }
            if (i == 0) {
                Yazi(Kutu(secimListesi, "Bos", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -60f), new Vector2(-10f, -10f)),
                    "Çantanda takasa konabilecek eşya yok.", 22, TextAnchor.MiddleCenter, IpucuRengi);
            }
            secimKoku.SetActive(true);
            secimKoku.transform.SetAsLastSibling();
        }

        public override void Kapat() {
            if (secimKoku != null) {
                secimKoku.SetActive(false);
            }
            base.Kapat();
        }

        // ---------------------------------------------------------------- ağ botu

        /// <summary>ağ botu: açık takasa çantadaki ilk takas edilebilir eşyayı koyar; koyduysa true</summary>
        public static bool TestIcinEsyaKoy() {
            UnitController oyuncu = Oyuncu;
            if (oyuncu == null) {
                return false;
            }
            foreach (InventorySlot yuva in oyuncu.CharacterInventoryManager.InventorySlots) {
                if (yuva != null && yuva.IsEmpty == false && Ticaret.BagliMi(yuva.InstantiatedItem) == false) {
                    OtukenAg.Gonder("takas-koy", yuva.InstantiatedItem.InstanceId.ToString(CultureInfo.InvariantCulture));
                    return true;
                }
            }
            return false;
        }

        public static void TestIcinOnayla() {
            OtukenAg.Gonder("takas-onay", "1");
        }
    }
}
