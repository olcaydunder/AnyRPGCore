using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Etkinlikler penceresi (HUD'daki etkinlik afişi ya da Menü > Etkinlikler): süren etkinlik, bugünün takvimi
    /// (her 2 saatte bir 45 dakikalık bölge etkinliği) ve hafta sonu Bozkır Şöleni. Takvim Etkinlikler'den gelir.
    /// </summary>
    public class EtkinlikPenceresi : OtukenPencere {

        private static EtkinlikPenceresi ornek = null;
        private Text durumYazisi = null;
        private RectTransform liste = null;
        private float sonraki = 0f;

        public static void Goster() {
            if (ornek == null) {
                ornek = Kur<EtkinlikPenceresi>("EtkinlikCanvas", 34);
            }
            ornek.kok.SetActive(true);
            ornek.Yenile();
        }

        protected override void Kur() {
            PencereKur("Etkinlikler", 900f, 640f);
            Baslik("BÖLGE ETKİNLİKLERİ");
            durumYazisi = Yazi(Kutu(panel.transform, "Durum", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -150f), new Vector2(-24f, -70f)),
                string.Empty, 21, TextAnchor.UpperLeft, YaziRengi);
            liste = Liste(panel.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20f, 70f), new Vector2(-20f, -160f));
            Yazi(Kutu(panel.transform, "Bilgi", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 10f), new Vector2(-24f, 64f)),
                "Her 2 saatte bir bir bölgede 45 dakikalık etkinlik olur (Türkiye saati). Cumartesi ve pazar 20:00–23:00 Bozkır Şöleni: "
                + "her yerde tecrübe 1,5 kat. Etkinliğe katılmak için o bölgede avlanman yeter.", 16, TextAnchor.MiddleLeft, IpucuRengi);
        }

        private void Update() {
            if (Acik && Time.unscaledTime >= sonraki) {
                Yenile();
            }
        }

        private void Yenile() {
            sonraki = Time.unscaledTime + 5f;
            DateTime t = Etkinlikler.TurkiyeSaati;
            List<Etkinlikler.Etkinlik> suren = Etkinlikler.Suren(t);
            string durum;
            if (suren.Count > 0) {
                List<string> satirlar = new List<string>();
                foreach (Etkinlikler.Etkinlik e in suren) {
                    int kalan = Mathf.Max(1, (int)Math.Ceiling((e.bitis - t).TotalMinutes));
                    satirlar.Add("<color=#FFD54A><b>ŞİMDİ: " + e.Ad + "</b></color> · " + e.Yer + " — " + e.Aciklama + " (" + kalan + " dk kaldı)");
                }
                durum = string.Join("\n", satirlar);
            } else {
                Etkinlikler.Etkinlik s = Etkinlikler.Siradaki(t);
                int dk = Mathf.Max(1, (int)Math.Ceiling((s.baslangic - t).TotalMinutes));
                durum = "Şu an etkinlik yok. Sıradaki: <b>" + s.baslangic.ToString("HH:mm") + " " + s.Ad + "</b> · " + s.Yer + " (" + dk + " dk sonra)";
            }
            durumYazisi.text = durum;

            ListeyiTemizle(liste);
            int i = 0;
            foreach (Etkinlikler.Etkinlik e in Etkinlikler.Gun(t)) {
                bool simdi = t >= e.baslangic && t < e.bitis;
                bool gecti = t >= e.bitis;
                GameObject satir = Satir(liste, i++, 46f, simdi ? SeciliRengi : SatirRengi, null);
                Color renk = gecti ? new Color(0.6f, 0.58f, 0.54f, 1f) : YaziRengi;
                Yazi(Kutu(satir.transform, "Saat", new Vector2(0f, 0f), new Vector2(0.14f, 1f), new Vector2(12f, 0f), Vector2.zero),
                    e.baslangic.ToString("HH:mm") + "–" + e.bitis.ToString("HH:mm"), 18, TextAnchor.MiddleLeft, renk);
                Yazi(Kutu(satir.transform, "Ad", new Vector2(0.15f, 0f), new Vector2(0.42f, 1f), Vector2.zero, Vector2.zero),
                    "<b>" + e.Ad + "</b>", 19, TextAnchor.MiddleLeft, simdi ? Altin : renk);
                Yazi(Kutu(satir.transform, "Yer", new Vector2(0.42f, 0f), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero),
                    e.Yer, 18, TextAnchor.MiddleLeft, renk);
                Yazi(Kutu(satir.transform, "Aciklama", new Vector2(0.66f, 0f), new Vector2(1f, 1f), Vector2.zero, new Vector2(-10f, 0f)),
                    e.Aciklama, 16, TextAnchor.MiddleLeft, gecti ? renk : IpucuRengi);
            }
        }
    }
}
