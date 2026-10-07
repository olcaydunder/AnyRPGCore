using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Bölge etkinlikleri (Ötüken). Takvim Türkiye saatine göre hesaplanır, sunucuya ya da ağa gerek yoktur: telefon,
    /// çevrimdışı oyun ve sunucu aynı saatte aynı etkinliği bulur.
    ///  - Her 2 saatte bir (00, 02, 04 ... 22) 45 dakikalık bir bölge etkinliği; bölge ve tür güne ve saate göre döner:
    ///      Çifte Tecrübe    : o bölgede canavarlardan 2 kat tecrübe
    ///      Ganimet Bereketi : o bölgede eşya düşme şansı 2 kat
    ///      Canavar İstilası : canavarlar 2,5 kat hızlı yeniden doğar, tecrübe 1,5 kat
    ///      Boss Avı         : bölgenin boss'u 10 dakika yerine 2 dakikada yeniden doğar, ganimet 1,5 kat
    ///  - Cumartesi ve pazar 20:00–23:00 "Bozkır Şöleni": bütün bölgelerde tecrübe 1,5 kat
    /// Uygulandığı yerler: PlayerManagerServer.HandleKillEvent (tecrübe), LootTableState (ganimet),
    /// UnitSpawnNode.ProcessRespawn (yeniden doğma). Telefonda başlayınca ileti ve HUD'da afiş (MobileHud).
    /// </summary>
    public static class Etkinlikler {

        public enum Tur { CifteTecrube, GanimetBereketi, CanavarIstilasi, BossAvi, BozkirSoleni }

        public struct Etkinlik {
            public Tur tur;
            public string sahne;        // boşsa bütün bölgeler
            public DateTime baslangic;  // Türkiye saati
            public DateTime bitis;

            public string Ad {
                get { return Etkinlikler.Ad(tur); }
            }

            public string Yer {
                get { return string.IsNullOrEmpty(sahne) ? "Bütün bölgeler" : HaritaSeviyeleri.Ad(sahne); }
            }

            public string Aciklama {
                get { return Etkinlikler.Aciklama(tur); }
            }
        }

        public const int AralikSaat = 2;
        public const int SureDakika = 45;
        private static readonly DateTime Baslangic = new DateTime(2026, 1, 1);

        public static DateTime TurkiyeSaati {
            get { return DateTime.UtcNow.AddHours(3); }
        }

        public static string Ad(Tur tur) {
            switch (tur) {
                case Tur.CifteTecrube: return "Çifte Tecrübe";
                case Tur.GanimetBereketi: return "Ganimet Bereketi";
                case Tur.CanavarIstilasi: return "Canavar İstilası";
                case Tur.BossAvi: return "Boss Avı";
                default: return "Bozkır Şöleni";
            }
        }

        public static string Aciklama(Tur tur) {
            switch (tur) {
                case Tur.CifteTecrube: return "Canavarlardan 2 kat tecrübe";
                case Tur.GanimetBereketi: return "Eşya düşme şansı 2 kat";
                case Tur.CanavarIstilasi: return "Canavarlar çok hızlı doğar, tecrübe 1,5 kat";
                case Tur.BossAvi: return "Boss 2 dakikada bir doğar, ganimet 1,5 kat";
                default: return "Her yerde tecrübe 1,5 kat";
            }
        }

        private static uint Karistir(uint x) {
            x ^= x >> 16;
            x *= 0x7feb352d;
            x ^= x >> 15;
            x *= 0x846ca68b;
            x ^= x >> 16;
            return x;
        }

        /// <summary>verilen başlangıç saatindeki (çift saat) bölge etkinliği</summary>
        public static Etkinlik Hesapla(DateTime dilimBasi) {
            int gun = (int)(dilimBasi.Date - Baslangic).TotalDays;
            int dilim = dilimBasi.Hour / AralikSaat;
            uint h = Karistir((uint)(gun * 31 + dilim * 7 + 12345));
            Etkinlik e = new Etkinlik();
            e.tur = (Tur)(h % 4u);
            // bölge: günde 12 dilim farklı bölgelere dağılsın (ard arda aynı bölge gelmez)
            int bolge = (int)((gun * 5 + dilim * 4 + (int)(h >> 8) % 3) % HaritaSeviyeleri.Sira.Length);
            e.sahne = HaritaSeviyeleri.Sira[bolge];
            e.baslangic = new DateTime(dilimBasi.Year, dilimBasi.Month, dilimBasi.Day, dilim * AralikSaat, 0, 0);
            e.bitis = e.baslangic.AddMinutes(SureDakika);
            return e;
        }

        private static bool SoleneDenk(DateTime t, out Etkinlik e) {
            e = new Etkinlik();
            if ((t.DayOfWeek == DayOfWeek.Saturday || t.DayOfWeek == DayOfWeek.Sunday) && t.Hour >= 20 && t.Hour < 23) {
                e.tur = Tur.BozkirSoleni;
                e.sahne = string.Empty;
                e.baslangic = t.Date.AddHours(20);
                e.bitis = t.Date.AddHours(23);
                return true;
            }
            return false;
        }

        /// <summary>şu an süren etkinlikler (bölge etkinliği ve hafta sonu şöleni)</summary>
        public static List<Etkinlik> Suren(DateTime t) {
            List<Etkinlik> liste = new List<Etkinlik>();
            Etkinlik bolge = Hesapla(t.Date.AddHours(t.Hour - t.Hour % AralikSaat));
            if (t >= bolge.baslangic && t < bolge.bitis) {
                liste.Add(bolge);
            }
            Etkinlik solen;
            if (SoleneDenk(t, out solen)) {
                liste.Add(solen);
            }
            return liste;
        }

        public static List<Etkinlik> Suren() {
            return Suren(TurkiyeSaati);
        }

        /// <summary>sıradaki bölge etkinliği</summary>
        public static Etkinlik Siradaki(DateTime t) {
            DateTime dilim = t.Date.AddHours(t.Hour - t.Hour % AralikSaat);
            Etkinlik e = Hesapla(dilim);
            if (t >= e.baslangic) {
                e = Hesapla(dilim.AddHours(AralikSaat));
            }
            return e;
        }

        /// <summary>bugünün bölge etkinlikleri (kılavuz ve etkinlik penceresi)</summary>
        public static List<Etkinlik> Gun(DateTime gun) {
            List<Etkinlik> liste = new List<Etkinlik>();
            for (int s = 0; s < 24; s += AralikSaat) {
                liste.Add(Hesapla(gun.Date.AddHours(s)));
            }
            return liste;
        }

        private static float Carpan(string sahne, Tur tur, float deger) {
            float c = 1f;
            foreach (Etkinlik e in Suren()) {
                if (e.tur == tur && (string.IsNullOrEmpty(e.sahne) || e.sahne == sahne)) {
                    c = Mathf.Max(c, deger);
                }
            }
            return c;
        }

        private static string Sahne(UnitController uc) {
            return uc != null ? uc.gameObject.scene.name : string.Empty;
        }

        /// <summary>öldürme tecrübesi çarpanı</summary>
        public static float TecrubeCarpani(UnitController oyuncu) {
            string sahne = Sahne(oyuncu);
            return Mathf.Max(Carpan(sahne, Tur.CifteTecrube, 2f), Carpan(sahne, Tur.CanavarIstilasi, 1.5f), Carpan(sahne, Tur.BozkirSoleni, 1.5f));
        }

        /// <summary>eşya düşme çarpanı</summary>
        public static float GanimetCarpani(UnitController oyuncu) {
            string sahne = Sahne(oyuncu);
            return Mathf.Max(Carpan(sahne, Tur.GanimetBereketi, 2f), Carpan(sahne, Tur.BossAvi, 1.5f));
        }

        /// <summary>yeniden doğma süresinin çarpanı (boss düğümleri: 5 dakikadan uzun süreli olanlar)</summary>
        public static float DogmaCarpani(string sahne, int sure) {
            float c = 1f;
            if (Carpan(sahne, Tur.CanavarIstilasi, 2f) > 1f) {
                c = 0.4f;
            }
            if (sure >= 300 && Carpan(sahne, Tur.BossAvi, 2f) > 1f) {
                c = Mathf.Min(c, 0.2f);
            }
            return c;
        }

        // ---------------------------------------------------------------- telefon: duyuru ve afiş

        private static readonly HashSet<string> duyurulan = new HashSet<string>();

        /// <summary>HUD afişi: süren etkinlik ("Çifte Tecrübe · Börü Tepesi · 23 dk") ya da sıradaki ("20:00 Boss Avı")</summary>
        public static string AfisYazisi() {
            DateTime t = TurkiyeSaati;
            List<Etkinlik> suren = Suren(t);
            if (suren.Count > 0) {
                Etkinlik e = suren[0];
                int kalan = Mathf.Max(1, (int)Math.Ceiling((e.bitis - t).TotalMinutes));
                return "ETKİNLİK: " + e.Ad + " · " + e.Yer + " · " + kalan + " dk";
            }
            Etkinlik s = Siradaki(t);
            return "Sıradaki etkinlik " + s.baslangic.ToString("HH:mm") + ": " + s.Ad + " · " + s.Yer;
        }

        public static bool Suruyor {
            get { return Suren().Count > 0; }
        }

        /// <summary>MobileBootstrap saniyede bir: etkinlik başlayınca oyuncuya bir kez yazar</summary>
        public static void Tick(SystemGameManager oyun) {
            if (oyun == null || oyun.PlayerManagerClient == null || oyun.PlayerManagerClient.UnitController == null) {
                return;
            }
            foreach (Etkinlik e in Suren()) {
                string anahtar = e.tur + "|" + e.sahne + "|" + e.baslangic.ToString("yyyyMMddHH");
                if (duyurulan.Add(anahtar) == false) {
                    continue;
                }
                string metin = "<color=#FFD54A>ETKİNLİK BAŞLADI:</color> " + e.Ad + " · " + e.Yer + " (" + e.Aciklama + ", "
                    + e.bitis.ToString("HH:mm") + "'e kadar)";
                OtukenAg.Mesaj(oyun.PlayerManagerClient.UnitController, metin);
                oyun.MessageLogClient?.WriteSystemMessage(metin);
            }
        }
    }
}
