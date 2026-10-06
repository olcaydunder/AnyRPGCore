using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Sol sütundaki "Ticaret" düğmesinin penceresi:
    ///  - Yakındaki oyuncular: Takas teklif et, Şikâyet Et, Engelle
    ///  - Yakındaki pazarlar: pazarın adı, satıcı, uzaklık; "Bak" ile mallar
    ///  - Pazar Kur / Pazarımı Kapat, Kut Dükkânı, engellenen oyuncular (engel kaldırma)
    /// Tek oyunculu oyunda yalnız Kut Dükkânı vardır (takas ve pazar çevrimiçi oyunda).
    /// </summary>
    public class TicaretPenceresi : OtukenPencere {

        private static TicaretPenceresi ornek = null;

        public static void Goster() {
            if (ornek == null) {
                ornek = Kur<TicaretPenceresi>("TicaretCanvas", 32);
            }
            ornek.Ac(false);
        }

        public static void EngellenenleriGoster() {
            if (ornek == null) {
                ornek = Kur<TicaretPenceresi>("TicaretCanvas", 32);
            }
            ornek.Ac(true);
        }

        private RectTransform oyuncuListesi = null;
        private RectTransform pazarListesi = null;
        private Text oyuncuBasligi = null;
        private Text pazarBasligi = null;
        private Text bilgiYazisi = null;
        private GameObject pazarDugmesi = null;
        private GameObject nedenKoku = null;
        private Text nedenBasligi = null;
        private string sikayetEdilen = string.Empty;
        private bool engelKipi = false;
        private float sonrakiYenileme = 0f;

        protected override void Kur() {
            PencereKur("Ticaret", 1180f, 700f);
            Baslik("TİCARET");
            pazarDugmesi = Dugme(panel.transform, "Pazar Kur", new Vector2(0f, 1f), new Vector2(130f, -104f), new Vector2(220f, 54f), 21, VurguRengi, () => {
                MobileFeedback.Tap();
                if (Pazar.BenimAcik) {
                    Pazar.KendiPazariniKapat();
                } else {
                    Kapat();
                    Pazar.KurGoster();
                }
            });
            Dugme(panel.transform, "Kut Dükkânı", new Vector2(0f, 1f), new Vector2(370f, -104f), new Vector2(220f, 54f), 21, new Color(0.55f, 0.4f, 0.1f, 1f), () => {
                MobileFeedback.Tap();
                Kapat();
                KutDukkani.Goster();
            });
            Dugme(panel.transform, "Engellenenler", new Vector2(0f, 1f), new Vector2(610f, -104f), new Vector2(220f, 54f), 21, DugmeRengi, () => {
                MobileFeedback.Tap();
                engelKipi = !engelKipi;
                Yenile();
            });
            oyuncuBasligi = Yazi(Kutu(panel.transform, "OyuncuBaslik", new Vector2(0f, 1f), new Vector2(0.55f, 1f), new Vector2(24f, -170f), new Vector2(0f, -136f)),
                "Yakındaki oyuncular", 22, TextAnchor.MiddleLeft, Altin);
            pazarBasligi = Yazi(Kutu(panel.transform, "PazarBaslik", new Vector2(0.55f, 1f), new Vector2(1f, 1f), new Vector2(10f, -170f), new Vector2(-24f, -136f)),
                "Yakındaki pazarlar", 22, TextAnchor.MiddleLeft, Altin);
            oyuncuListesi = Liste(panel.transform, new Vector2(0f, 0f), new Vector2(0.55f, 1f), new Vector2(20f, 70f), new Vector2(-10f, -174f));
            pazarListesi = Liste(panel.transform, new Vector2(0.55f, 0f), new Vector2(1f, 1f), new Vector2(10f, 70f), new Vector2(-20f, -174f));
            bilgiYazisi = Yazi(Kutu(panel.transform, "Bilgi", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 8f), new Vector2(-24f, 66f)),
                string.Empty, 17, TextAnchor.MiddleLeft, IpucuRengi);

            // şikâyet nedeni seçimi
            nedenKoku = Kutu(panel.transform, "Neden", Vector2.zero, Vector2.one, new Vector2(10f, 10f), new Vector2(-10f, -10f));
            nedenKoku.AddComponent<Image>().color = new Color(0.05f, 0.04f, 0.03f, 0.98f);
            nedenBasligi = Yazi(Kutu(nedenKoku.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -60f), new Vector2(-140f, -8f)),
                string.Empty, 26, TextAnchor.MiddleLeft, Altin);
            Dugme(nedenKoku.transform, "Vazgeç", new Vector2(1f, 1f), new Vector2(-70f, -34f), new Vector2(110f, 46f), 20, DugmeRengi, () => {
                MobileFeedback.Tap();
                nedenKoku.SetActive(false);
            });
            for (int i = 0; i < Engelleme.Nedenler.Length; i++) {
                string neden = Engelleme.Nedenler[i];
                Dugme(nedenKoku.transform, neden, new Vector2(0.5f, 1f), new Vector2(0f, -110f - i * 66f), new Vector2(620f, 56f), 21, DugmeRengi, () => {
                    MobileFeedback.Tap();
                    nedenKoku.SetActive(false);
                    Engelleme.SikayetEt(sikayetEdilen, neden);
                    Bilgi(sikayetEdilen + " şikâyet edildi (" + neden + "). İstersen engelleyebilirsin.", true);
                });
            }
            nedenKoku.SetActive(false);

            Pazar.ListeGeldi += () => {
                if (Acik) {
                    Yenile();
                }
            };
        }

        public void Bilgi(string mesaj, bool iyi) {
            bilgiYazisi.text = mesaj;
            bilgiYazisi.color = iyi ? IyiRengi : HataRengi;
        }

        private void Ac(bool engeller) {
            kok.SetActive(true);
            engelKipi = engeller;
            nedenKoku.SetActive(false);
            Pazar.ListeIste();
            Yenile();
            bilgiYazisi.color = IpucuRengi;
            bilgiYazisi.text = Cevrimici.Acik
                ? "Takas için oyuncunun 15 m yakınında ol. Gerçek parayla alışveriş yasaktır; dolandırıcılık, küfür ve taciz için Şikâyet Et'i kullan."
                : "Takas ve pazar çevrimiçi oyunda yapılır. Kut Dükkânı her yerde açık.";
        }

        private void Update() {
            if (Acik && Time.unscaledTime >= sonrakiYenileme) {
                sonrakiYenileme = Time.unscaledTime + 3f;
                Pazar.ListeIste();
                Yenile();
            }
        }

        private void Yenile() {
            DugmeYazisi(pazarDugmesi, Pazar.BenimAcik ? "Pazarımı Kapat" : "Pazar Kur");
            pazarDugmesi.GetComponent<Button>().interactable = Cevrimici.Acik;
            ListeyiTemizle(oyuncuListesi);
            ListeyiTemizle(pazarListesi);
            if (engelKipi) {
                EngellenenleriYaz();
                return;
            }
            oyuncuBasligi.text = "Yakındaki oyuncular";
            pazarBasligi.text = "Yakındaki pazarlar";
            UnitController ben = Oyuncu;
            if (ben == null || Cevrimici.Acik == false) {
                return;
            }
            List<KeyValuePair<float, UnitController>> yakindakiler = new List<KeyValuePair<float, UnitController>>();
            foreach (UnitController u in FindObjectsByType<UnitController>(FindObjectsSortMode.None)) {
                if (u == null || u == ben || u.UnitControllerMode != UnitControllerMode.Player) {
                    continue;
                }
                float mesafe = Vector3.Distance(u.transform.position, ben.transform.position);
                if (mesafe <= 60f) {
                    yakindakiler.Add(new KeyValuePair<float, UnitController>(mesafe, u));
                }
            }
            yakindakiler.Sort((a, b) => a.Key.CompareTo(b.Key));
            int i = 0;
            foreach (KeyValuePair<float, UnitController> k in yakindakiler) {
                string ad = k.Value.DisplayName;
                bool engelli = Engelleme.EngelliMi(ad);
                GameObject satir = Satir(oyuncuListesi, i, 64f, SatirRengi, null);
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.36f, 1f), new Vector2(12f, 0f), Vector2.zero),
                    "<b>" + ad + "</b>\n<size=15><color=#B8A890>" + k.Key.ToString("0") + " m" + (engelli ? " · engelli" : string.Empty) + "</color></size>",
                    19, TextAnchor.MiddleLeft, YaziRengi);
                GameObject takas = Dugme(satir.transform, "Takas", new Vector2(0.36f, 0.5f), new Vector2(66f, 0f), new Vector2(118f, 48f), 18, OnayRengi, () => {
                    MobileFeedback.Tap();
                    Takas.Iste(ad);
                    Bilgi(ad + " oyuncusuna takas teklif edildi.", true);
                });
                takas.GetComponent<Button>().interactable = engelli == false && k.Key <= Ticaret.TakasMesafesi;
                Dugme(satir.transform, "Şikâyet Et", new Vector2(0.36f, 0.5f), new Vector2(194f, 0f), new Vector2(126f, 48f), 17, DugmeRengi, () => {
                    MobileFeedback.Tap();
                    sikayetEdilen = ad;
                    nedenBasligi.text = ad + " oyuncusunu neden şikâyet ediyorsun?";
                    nedenKoku.SetActive(true);
                    nedenKoku.transform.SetAsLastSibling();
                });
                Dugme(satir.transform, engelli ? "Engeli Kaldır" : "Engelle", new Vector2(0.36f, 0.5f), new Vector2(330f, 0f), new Vector2(134f, 48f), 17, TehlikeRengi, () => {
                    MobileFeedback.Tap();
                    if (engelli) {
                        Engelleme.Kaldir(ad);
                    } else {
                        Engelleme.Engelle(ad);
                    }
                    Yenile();
                });
                i++;
            }
            if (i == 0) {
                Yazi(Kutu(oyuncuListesi, "Bos", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -60f), new Vector2(-10f, -10f)),
                    "Yakında oyuncu yok.", 20, TextAnchor.MiddleCenter, IpucuRengi);
            }

            int j = 0;
            foreach (Pazar.PazarBilgisi b in Pazar.Pazarlar) {
                string sahip = b.sahip;
                GameObject satir = Satir(pazarListesi, j, 64f, SatirRengi, null);
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.68f, 1f), new Vector2(12f, 0f), Vector2.zero),
                    "<b>" + b.baslik + "</b>\n<size=15><color=#B8A890>" + sahip + " · " + b.malSayisi + " mal · " + b.mesafe.ToString("0") + " m</color></size>",
                    18, TextAnchor.MiddleLeft, YaziRengi);
                Dugme(satir.transform, "Bak", new Vector2(1f, 0.5f), new Vector2(-66f, 0f), new Vector2(110f, 48f), 19, OnayRengi, () => {
                    MobileFeedback.Tap();
                    Kapat();
                    Pazar.Bak(sahip);
                });
                j++;
            }
            if (j == 0) {
                Yazi(Kutu(pazarListesi, "Bos", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -60f), new Vector2(-10f, -10f)),
                    "Yakında açık pazar yok.", 20, TextAnchor.MiddleCenter, IpucuRengi);
            }
        }

        private void EngellenenleriYaz() {
            oyuncuBasligi.text = "Engellediğin oyuncular";
            pazarBasligi.text = string.Empty;
            int i = 0;
            foreach (string ad in new List<string>(Engelleme.Engellenenler)) {
                string a = ad;
                GameObject satir = Satir(oyuncuListesi, i, 60f, SatirRengi, null);
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.6f, 1f), new Vector2(12f, 0f), Vector2.zero), a, 20, TextAnchor.MiddleLeft, YaziRengi);
                Dugme(satir.transform, "Engeli Kaldır", new Vector2(1f, 0.5f), new Vector2(-90f, 0f), new Vector2(160f, 46f), 18, DugmeRengi, () => {
                    MobileFeedback.Tap();
                    Engelleme.Kaldir(a);
                    Yenile();
                });
                i++;
            }
            if (i == 0) {
                Yazi(Kutu(oyuncuListesi, "Bos", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -60f), new Vector2(-10f, -10f)),
                    "Engellediğin oyuncu yok.", 20, TextAnchor.MiddleCenter, IpucuRengi);
            }
        }

        public override void Kapat() {
            if (nedenKoku != null) {
                nedenKoku.SetActive(false);
            }
            base.Kapat();
        }
    }
}
