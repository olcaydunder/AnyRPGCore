using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Kut Dükkânı: oyun içi satın almalar.
    ///  - Kut, Google Play üzerinden gerçek parayla alınan oyun parasıdır (Odeme). Satın alındığı karaktere yüklenir.
    ///  - Kut ile alınanlar ve Kut'un kendisi hesaba bağlıdır: takas edilemez, pazarda satılamaz, gerçek paraya ya da
    ///    oyun parasına (akçe) çevrilemez. Böylece gerçek parayla eşya ticareti ve iade dolandırıcılığı önlenir.
    ///  - Rastgele ödül (sandık, çark) satılmaz. Demirci Kutsaması yalnız şansı belli bir puan artırır; şanslar Demirci
    ///    penceresinde ve burada açıkça yazar (Google Play'in şans oranı bildirme kuralı).
    /// Ürünler:
    ///  - Demirci Kutsaması: sonraki yükseltme denemesinin başarı şansı +15 puan (deneme başına bir kutsama gider)
    ///  - Deneyim Muskası: 1 saat boyunca öldürmelerden %50 fazla tecrübe (üst üste alınırsa süre eklenir)
    ///  - Ulu Heybe: 24 gözlü heybe (hesaba bağlı)
    /// Çevrimiçi oyunda alışveriş sunucuda yapılır ("kut-al"); kutsama sayısı ve muska süresi karakter kaydında durur
    /// (OtukenVeri). Tek oyunculu oyunda aynı kurallar telefonda işler.
    /// </summary>
    public class KutDukkani : OtukenPencere {

        public const string KutParasi = "Kut";
        public const string UluHeybe = "Ulu Heybe";
        public const int KutsamaEki = 15;
        public const float MuskaCarpani = 1.5f;

        private const string KutsamaAnahtari = "kut-kutsama";
        private const string MuskaAnahtari = "kut-muska-bitis";

        public class Urun {
            public string kod;
            public string ad;
            public string aciklama;
            public int fiyat;
        }

        public static readonly Urun[] Urunler = {
            new Urun() { kod = "kutsama1", ad = "Demirci Kutsaması", fiyat = 40,
                aciklama = "Sonraki yükseltmenin başarı şansı +" + KutsamaEki + " puan (ör. +9'da %25 → %40). Başarı garanti değildir." },
            new Urun() { kod = "kutsama5", ad = "Demirci Kutsaması ×5", fiyat = 180,
                aciklama = "5 kutsama; her yükseltme denemesinde biri kullanılır (+" + KutsamaEki + " puan şans)." },
            new Urun() { kod = "muska", ad = "Deneyim Muskası (1 saat)", fiyat = 120,
                aciklama = "1 saat boyunca öldürmelerden %50 fazla tecrübe. Üst üste alınırsa süre eklenir." },
            new Urun() { kod = "heybe", ad = "Ulu Heybe (24 göz)", fiyat = 300,
                aciklama = "24 gözlü büyük heybe. Hesaba bağlıdır, takas edilemez." },
        };

        public static Currency Kut {
            get {
                SystemGameManager o = OtukenAg.Oyun;
                return o != null ? o.SystemDataFactory.GetResource<Currency>(KutParasi) : null;
            }
        }

        public static int KutMiktari(UnitController oyuncu) {
            Currency kut = Kut;
            return oyuncu != null && kut != null ? oyuncu.CharacterCurrencyManager.GetCurrencyAmount(kut) : 0;
        }

        // ---------------------------------------------------------------- kurallar (sunucuda, tek oyunculu oyunda telefonda)

        public static int KutsamaSayisi(UnitController oyuncu) {
            return OtukenVeri.OkuSayi(oyuncu, KutsamaAnahtari, 0);
        }

        /// <summary>Demirci: kutsama varsa birini harcar ve true döner</summary>
        public static bool KutsamaHarca(UnitController oyuncu) {
            int n = KutsamaSayisi(oyuncu);
            if (n <= 0) {
                return false;
            }
            OtukenVeri.YazSayi(oyuncu, KutsamaAnahtari, n - 1);
            OtukenVeri.Kaydet();
            return true;
        }

        private static long Unix {
            get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }
        }

        /// <summary>muskanın kalan süresi (saniye)</summary>
        public static int MuskaKalan(UnitController oyuncu) {
            long bitis;
            long.TryParse(OtukenVeri.Oku(oyuncu, MuskaAnahtari, "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out bitis);
            return (int)Math.Max(0, bitis - Unix);
        }

        /// <summary>öldürme tecrübesinin çarpanı (PlayerManagerServer.HandleKillEvent)</summary>
        public static float TecrubeCarpani(UnitController oyuncu) {
            return oyuncu != null && MuskaKalan(oyuncu) > 0 ? MuskaCarpani : 1f;
        }

        /// <summary>Kut ile ürün alır; olmazsa nedenini döndürür (olursa null)</summary>
        public static string Al(UnitController oyuncu, string kod) {
            Urun urun = Array.Find(Urunler, u => u.kod == kod);
            Currency kut = Kut;
            if (oyuncu == null || urun == null || kut == null) {
                return "Ürün bulunamadı.";
            }
            if (KutMiktari(oyuncu) < urun.fiyat) {
                return "Yeterli Kut yok (" + urun.fiyat + " gerekli).";
            }
            if (kod == "heybe" && oyuncu.CharacterInventoryManager.EmptySlotCount() < 1) {
                return "Çantanda boş yer yok.";
            }
            if (oyuncu.CharacterCurrencyManager.SpendCurrency(kut, urun.fiyat) == false) {
                return "Ödeme yapılamadı.";
            }
            switch (kod) {
                case "kutsama1":
                    OtukenVeri.YazSayi(oyuncu, KutsamaAnahtari, KutsamaSayisi(oyuncu) + 1);
                    break;
                case "kutsama5":
                    OtukenVeri.YazSayi(oyuncu, KutsamaAnahtari, KutsamaSayisi(oyuncu) + 5);
                    break;
                case "muska":
                    OtukenVeri.Yaz(oyuncu, MuskaAnahtari, (Unix + MuskaKalan(oyuncu) + 3600).ToString(CultureInfo.InvariantCulture));
                    break;
                case "heybe":
                    InstantiatedItem heybe = oyuncu.CharacterInventoryManager.GetNewInstantiatedItem(UluHeybe);
                    if (heybe == null || oyuncu.CharacterInventoryManager.AddItem(heybe, false) == false) {
                        oyuncu.CharacterCurrencyManager.AddCurrency(kut, urun.fiyat);
                        return "Heybe verilemedi, Kut geri yüklendi.";
                    }
                    break;
            }
            OtukenVeri.Kaydet();
            Ticaret.Kaydet(oyuncu);
            OtukenAg.Mesaj(oyuncu, "<color=#FFD54A>Kut Dükkânı: " + urun.ad + " alındı.</color>");
            return null;
        }

        /// <summary>"kutsama|muskaKalanSn"</summary>
        private static string Durum(UnitController oyuncu) {
            return KutsamaSayisi(oyuncu).ToString(CultureInfo.InvariantCulture) + "|" + MuskaKalan(oyuncu).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>sunucu: kutsama ve muska durumunu oyuncunun telefonuna gönderir</summary>
        public static void DurumGonder(UnitController oyuncu) {
            if (OtukenAg.Sunucuda) {
                OtukenAg.Yanitla(oyuncu, "kut-durum", Durum(oyuncu));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("kut-al", (oyuncu, kod) => {
                string hata = Al(oyuncu, kod);
                Debug.Log("[Sunucu] " + oyuncu.DisplayName + " Kut Dükkânı " + kod + ": " + (hata ?? "alındı"));
                OtukenAg.Yanitla(oyuncu, "kut-sonuc", hata == null ? "1|" + kod : "0|" + hata);
                DurumGonder(oyuncu);
            });
            OtukenAg.SunucuIsle("kut-durum", (oyuncu, veri) => DurumGonder(oyuncu));
            OtukenAg.IstemciDinle("kut-durum", veri => {
                string[] p = veri.Split('|');
                int k;
                int m;
                if (p.Length >= 2 && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out k)
                    && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out m)) {
                    istemciKutsama = k;
                    istemciMuskaBitis = Time.realtimeSinceStartup + m;
                }
                if (ornek != null && ornek.Acik) {
                    ornek.Yenile();
                }
            });
            OtukenAg.IstemciDinle("kut-sonuc", veri => {
                bool iyi = veri.StartsWith("1|");
                string mesaj = veri.Substring(Math.Min(2, veri.Length));
                if (iyi) {
                    AlinanSayisi++;
                    MobileFeedback.Success();
                    Urun u = Array.Find(Urunler, x => x.kod == mesaj);
                    mesaj = (u != null ? u.ad : mesaj) + " alındı.";
                }
                SonSonuc = mesaj;
                if (ornek != null && ornek.Acik) {
                    ornek.Bilgi(mesaj, iyi);
                }
            });
        }

        // ================================================================ telefon

        private static int istemciKutsama = 0;
        private static float istemciMuskaBitis = 0f;

        public static int AlinanSayisi { get; private set; }
        public static string SonSonuc { get; private set; } = "-";

        /// <summary>telefonda bilinen kutsama sayısı (çevrimiçinde sunucunun son bildirdiği)</summary>
        public static int IstemciKutsama {
            get {
                if (Cevrimici.Acik) {
                    return istemciKutsama;
                }
                return KutsamaSayisi(Oyuncu);
            }
        }

        public static int IstemciMuskaKalan {
            get {
                if (Cevrimici.Acik) {
                    return Mathf.Max(0, Mathf.RoundToInt(istemciMuskaBitis - Time.realtimeSinceStartup));
                }
                return MuskaKalan(Oyuncu);
            }
        }

        /// <summary>Demirci yükseltmeden sonra çevrimiçinde kutsama sayısını düşürür (sunucu ayrıca bildirir)</summary>
        public static void IstemciKutsamaDus() {
            if (istemciKutsama > 0) {
                istemciKutsama--;
            }
        }

        private static KutDukkani ornek = null;

        public static void Goster() {
            if (ornek == null) {
                ornek = Kur<KutDukkani>("KutDukkaniCanvas", 33);
            }
            ornek.Ac();
        }

        private Text kutYazisi = null;
        private Text durumYazisi = null;
        private Text bilgiYazisi = null;
        private RectTransform paketListesi = null;
        private RectTransform urunListesi = null;

        protected override void Kur() {
            PencereKur("KutDukkani", 1180f, 700f);
            Baslik("KUT DÜKKÂNI");
            kutYazisi = Yazi(Kutu(panel.transform, "Kut", new Vector2(0.35f, 1f), new Vector2(1f, 1f), new Vector2(0f, -62f), new Vector2(-130f, -8f)),
                string.Empty, 26, TextAnchor.MiddleRight, Altin);
            Yazi(Kutu(panel.transform, "PaketBaslik", new Vector2(0f, 1f), new Vector2(0.42f, 1f), new Vector2(24f, -104f), new Vector2(0f, -68f)),
                "Kut al  <size=16><color=#B8A890>(Google Play ile ödenir)</color></size>", 22, TextAnchor.MiddleLeft, Altin);
            paketListesi = Liste(panel.transform, new Vector2(0f, 0f), new Vector2(0.42f, 1f), new Vector2(20f, 150f), new Vector2(-10f, -108f));
            Yazi(Kutu(panel.transform, "UrunBaslik", new Vector2(0.42f, 1f), new Vector2(1f, 1f), new Vector2(10f, -104f), new Vector2(-24f, -68f)),
                "Kut ile al", 22, TextAnchor.MiddleLeft, Altin);
            urunListesi = Liste(panel.transform, new Vector2(0.42f, 0f), new Vector2(1f, 1f), new Vector2(10f, 150f), new Vector2(-20f, -108f));
            durumYazisi = Yazi(Kutu(panel.transform, "Durum", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 104f), new Vector2(-24f, 146f)),
                string.Empty, 19, TextAnchor.MiddleLeft, YaziRengi);
            bilgiYazisi = Yazi(Kutu(panel.transform, "Bilgi", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 10f), new Vector2(-24f, 100f)),
                string.Empty, 16, TextAnchor.MiddleLeft, IpucuRengi);
            Odeme.Degisti += () => {
                if (Acik) {
                    Yenile();
                }
            };
            Reklam.Degisti += () => {
                if (Acik == false) {
                    return;
                }
                Yenile();
                if (Reklam.SonSonuc != sonReklamSonucu) {
                    sonReklamSonucu = Reklam.SonSonuc;
                    Bilgi(Reklam.SonSonuc, Reklam.SonSonuc.StartsWith("+"));
                }
            };
        }

        private string sonReklamSonucu = "-";

        public void Bilgi(string mesaj, bool iyi) {
            bilgiYazisi.text = mesaj;
            bilgiYazisi.color = iyi ? IyiRengi : HataRengi;
        }

        private void Ac() {
            kok.SetActive(true);
            Odeme.Baslat();
            Reklam.Baslat();
            Reklam.DurumIste();
            sonReklamSonucu = Reklam.SonSonuc;
            if (Cevrimici.Acik) {
                OtukenAg.Gonder("kut-durum");
            }
            Yenile();
            bilgiYazisi.color = IpucuRengi;
            bilgiYazisi.text = "Kut ve Kut ile alınanlar hesabına bağlıdır: takas edilemez, satılamaz, gerçek paraya çevrilemez. Kut, satın aldığın karaktere yüklenir. "
                + "Satın alma Google Play ile yapılır; iade ve geri ödeme Google Play kurallarına bağlıdır. 18 yaşından küçüksen ebeveyninin iznini al.";
        }

        public void Yenile() {
            UnitController oyuncu = Oyuncu;
            kutYazisi.text = "Kut: " + KutMiktari(oyuncu);
            int muska = IstemciMuskaKalan;
            durumYazisi.text = "Kutsama: " + IstemciKutsama + "    Deneyim Muskası: "
                + (muska > 0 ? (muska / 60) + " dk kaldı" : "yok");

            ListeyiTemizle(paketListesi);
            int i = 0;
            if (Reklam.Kurulu) {
                // ödüllü reklam: izleyene Kut (günde sınırlı)
                int kalan = Reklam.IstemciKalan(Reklam.Kut);
                GameObject satir = Satir(paketListesi, i, 64f, SatirRengi, null);
                Simge(satir.transform, Kut != null ? Kut.Icon : null, new Vector2(8f, -26f), new Vector2(60f, 26f));
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.62f, 1f), new Vector2(70f, 0f), Vector2.zero),
                    "<b>+" + Reklam.KutOdulu + " Kut</b>\n<size=15><color=#B8A890>Reklam izle · bugün " + kalan + "/" + Reklam.Sinir(Reklam.Kut) + "</color></size>",
                    20, TextAnchor.MiddleLeft, YaziRengi);
                bool olur = kalan > 0 && Reklam.Hazir;
                GameObject izle = Dugme(satir.transform, kalan <= 0 ? "Yarın" : (Reklam.Hazir ? "İzle" : "Bekle"), new Vector2(1f, 0.5f), new Vector2(-90f, 0f),
                    new Vector2(160f, 50f), 20, olur ? VurguRengi : DugmeRengi, () => {
                        MobileFeedback.Tap();
                        string hata = Reklam.KutIcinIzle();
                        if (hata != null) {
                            Bilgi(hata, false);
                        }
                    });
                izle.GetComponent<Button>().interactable = olur;
                i++;
            }
            foreach (Odeme.Paket paket in Odeme.Paketler) {
                if (paket.kut <= 0) {
                    // depo gözü Depo penceresinde satılır
                    continue;
                }
                Odeme.Paket p = paket;
                GameObject satir = Satir(paketListesi, i, 64f, SatirRengi, null);
                Simge(satir.transform, Kut != null ? Kut.Icon : null, new Vector2(8f, -26f), new Vector2(60f, 26f));
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.55f, 1f), new Vector2(70f, 0f), Vector2.zero),
                    "<b>" + p.kut + " Kut</b>", 22, TextAnchor.MiddleLeft, YaziRengi);
                string fiyat = Odeme.Fiyat(p.kod);
                GameObject al = Dugme(satir.transform, string.IsNullOrEmpty(fiyat) ? "—" : fiyat, new Vector2(1f, 0.5f), new Vector2(-90f, 0f),
                    new Vector2(160f, 50f), 20, OnayRengi, () => {
                        MobileFeedback.Tap();
                        string hata = Odeme.SatinAl(p.kod);
                        if (hata != null) {
                            Bilgi(hata, false);
                        }
                    });
                al.GetComponent<Button>().interactable = string.IsNullOrEmpty(fiyat) == false;
                i++;
            }
            if (Odeme.Hazir == false || Odeme.FiyatSayisi == 0) {
                Yazi(Kutu(paketListesi, "Durum", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, -i * 70f - 120f), new Vector2(-10f, -i * 70f - 10f)),
                    Odeme.DurumYazisi, 17, TextAnchor.UpperLeft, IpucuRengi);
            }

            ListeyiTemizle(urunListesi);
            int kut = KutMiktari(oyuncu);
            for (int j = 0; j < Urunler.Length; j++) {
                Urun u = Urunler[j];
                GameObject satir = Satir(urunListesi, j, 84f, SatirRengi, null);
                Yazi(Kutu(satir.transform, "Ad", Vector2.zero, new Vector2(0.72f, 1f), new Vector2(12f, 4f), new Vector2(0f, -4f)),
                    "<b>" + u.ad + "</b>\n<size=15><color=#B8A890>" + u.aciklama + "</color></size>", 19, TextAnchor.MiddleLeft, YaziRengi);
                GameObject al = Dugme(satir.transform, u.fiyat + " Kut", new Vector2(1f, 0.5f), new Vector2(-82f, 0f), new Vector2(144f, 54f), 20,
                    kut >= u.fiyat ? OnayRengi : DugmeRengi, () => {
                        MobileFeedback.Tap();
                        UrunAl(u.kod);
                    });
                al.GetComponent<Button>().interactable = kut >= u.fiyat;
            }
        }

        private void UrunAl(string kod) {
            if (Cevrimici.Acik) {
                OtukenAg.Gonder("kut-al", kod);
                return;
            }
            string hata = Al(Oyuncu, kod);
            if (hata == null) {
                AlinanSayisi++;
                MobileFeedback.Success();
                Urun u = Array.Find(Urunler, x => x.kod == kod);
                Bilgi((u != null ? u.ad : kod) + " alındı.", true);
            } else {
                Bilgi(hata, false);
            }
            Yenile();
        }

        // ---------------------------------------------------------------- ağ botu

        public static void TestIcinUrunAl(string kod) {
            if (Cevrimici.Acik) {
                OtukenAg.Gonder("kut-al", kod);
            } else {
                Al(Oyuncu, kod);
            }
        }
    }
}
