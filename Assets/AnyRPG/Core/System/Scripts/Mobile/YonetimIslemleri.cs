using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Yönetim panelinin yetkileri (hepsi sunucunun ana iş parçacığında çalışır; YonetimPaneli.AnaIstekte):
    ///  - oyuncu ayrıntısı: hesap, oyun süresi, bütün karakterleri (diskten), çevrimiçiyse canlı karakter: seviye,
    ///    tecrübe, can, harita ve konum, paralar, çanta, depo, kuşanılanlar, görevler, son sohbetleri, oturumları
    ///  - işlemler: eşya ver / sil, akçe ve Kut ekle / ayarla, tecrübe ver, seviye ayarla, iyileştir, dirilt, ışınla,
    ///    özel mesaj, sustur, at, yasakla, şifre sıfırla, Google bağını kaldır, hesabı sil
    ///  - çevrimdışı oyuncuya verilenler "bekleyen" olarak saklanır, oyuncu girince kendiliğinden verilir
    ///  - herkese hediye (çevrimiçi herkese), yeni hesap açma
    ///  - her işlem yonetim-islemleri.log'a yazılır (panelde "İşlemler")
    /// </summary>
    public static class YonetimIslemleri {

        // ================================================================ JSON

        public class Js {
            private readonly StringBuilder sb = new StringBuilder();
            private readonly Stack<bool> ilk = new Stack<bool>();

            private void Ayir() {
                if (ilk.Count > 0) {
                    if (ilk.Peek()) {
                        ilk.Pop();
                        ilk.Push(false);
                    } else {
                        sb.Append(',');
                    }
                }
            }

            private void Ad(string ad) {
                Ayir();
                if (ad != null) {
                    sb.Append(Esc(ad)).Append(':');
                }
            }

            public Js Nesne(string ad = null) { Ad(ad); sb.Append('{'); ilk.Push(true); return this; }
            public Js Dizi(string ad = null) { Ad(ad); sb.Append('['); ilk.Push(true); return this; }
            public Js NesneBitir() { ilk.Pop(); sb.Append('}'); return this; }
            public Js DiziBitir() { ilk.Pop(); sb.Append(']'); return this; }
            public Js Y(string ad, string d) { Ad(ad); sb.Append(Esc(d)); return this; }
            public Js S(string ad, long d) { Ad(ad); sb.Append(d.ToString(CultureInfo.InvariantCulture)); return this; }
            public Js O(string ad, float d) { Ad(ad); sb.Append(d.ToString("0.##", CultureInfo.InvariantCulture)); return this; }
            public Js B(string ad, bool d) { Ad(ad); sb.Append(d ? "true" : "false"); return this; }
            public Js Ham(string ad, string json) { Ad(ad); sb.Append(json); return this; }
            public override string ToString() { return sb.ToString(); }

            public static string Esc(string s) {
                if (s == null) {
                    return "\"\"";
                }
                StringBuilder b = new StringBuilder(s.Length + 2);
                b.Append('"');
                foreach (char c in s) {
                    switch (c) {
                        case '"': b.Append("\\\""); break;
                        case '\\': b.Append("\\\\"); break;
                        case '\n': b.Append("\\n"); break;
                        case '\r': break;
                        case '\t': b.Append("\\t"); break;
                        default:
                            if (c < 0x20) {
                                b.Append("\\u").Append(((int)c).ToString("x4"));
                            } else {
                                b.Append(c);
                            }
                            break;
                    }
                }
                return b.Append('"').ToString();
            }
        }

        public static string Hata(string mesaj) {
            return "{\"hata\":" + Js.Esc(mesaj) + "}";
        }

        public static string Tamam(string mesaj) {
            return "{\"tamam\":true,\"mesaj\":" + Js.Esc(mesaj) + "}";
        }

        // ================================================================ istekler

        [Serializable]
        public class Istek {
            public int hesap = -1;
            public string tur = string.Empty;
            public string ad = string.Empty;
            public long miktar = 0;
            public string metin = string.Empty;
        }

        [Serializable]
        private class Bekleyen {
            public int hesap;
            public string tur = string.Empty;
            public string ad = string.Empty;
            public long miktar;
            public string metin = string.Empty;
            public long zaman;
        }

        [Serializable]
        private class BekleyenDosyasi {
            public List<Bekleyen> liste = new List<Bekleyen>();
        }

        private static BekleyenDosyasi bekleyenler = null;
        private static readonly Dictionary<int, DateTime> susturulanlar = new Dictionary<int, DateTime>();
        private static bool okundu = false;
        private static readonly object kilit = new object();

        private static string Klasor { get { return Application.persistentDataPath; } }
        private static string BekleyenYolu { get { return Path.Combine(Klasor, "bekleyen-hediyeler.json"); } }
        private static string SusturmaYolu { get { return Path.Combine(Klasor, "susturmalar.txt"); } }
        private static string IslemDefteri { get { return Path.Combine(Klasor, "yonetim-islemleri.log"); } }

        private static void Oku() {
            if (okundu) {
                return;
            }
            okundu = true;
            try {
                if (File.Exists(BekleyenYolu)) {
                    bekleyenler = JsonUtility.FromJson<BekleyenDosyasi>(File.ReadAllText(BekleyenYolu));
                }
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] bekleyenler okunamadı: " + e.Message);
            }
            if (bekleyenler == null) {
                bekleyenler = new BekleyenDosyasi();
            }
            try {
                if (File.Exists(SusturmaYolu)) {
                    foreach (string satir in File.ReadAllLines(SusturmaYolu)) {
                        string[] p = satir.Split(' ');
                        int no;
                        long bitis;
                        if (p.Length >= 2 && int.TryParse(p[0], out no) && long.TryParse(p[1], out bitis)) {
                            susturulanlar[no] = DateTimeOffset.FromUnixTimeSeconds(bitis).UtcDateTime;
                        }
                    }
                }
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] susturmalar okunamadı: " + e.Message);
            }
        }

        private static void BekleyenleriYaz() {
            try {
                File.WriteAllText(BekleyenYolu, JsonUtility.ToJson(bekleyenler));
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] bekleyenler yazılamadı: " + e.Message);
            }
        }

        private static void SusturmalariYaz() {
            try {
                StringBuilder sb = new StringBuilder();
                lock (kilit) {
                    foreach (KeyValuePair<int, DateTime> kv in susturulanlar) {
                        sb.Append(kv.Key).Append(' ').Append(new DateTimeOffset(kv.Value).ToUnixTimeSeconds()).Append('\n');
                    }
                }
                File.WriteAllText(SusturmaYolu, sb.ToString());
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] susturmalar yazılamadı: " + e.Message);
            }
        }

        /// <summary>sohbet: susturulmuş mu (MessageLogServer); susturulmuşsa kalan dakika</summary>
        public static bool Susturuldu(int hesap, out int kalanDk) {
            kalanDk = 0;
            lock (kilit) {
                DateTime bitis;
                if (susturulanlar.TryGetValue(hesap, out bitis) == false) {
                    return false;
                }
                if (bitis <= DateTime.UtcNow) {
                    susturulanlar.Remove(hesap);
                    return false;
                }
                kalanDk = Mathf.Max(1, Mathf.CeilToInt((float)(bitis - DateTime.UtcNow).TotalMinutes));
                return true;
            }
        }

        /// <summary>sohbet sunucusu: susturulmuşsa iletiyi durdurur, oyuncuya kalan süreyi yazar</summary>
        public static bool SohbetEngeli(SystemGameManager o, int hesap) {
            int kalan;
            if (Susturuldu(hesap, out kalan) == false) {
                return false;
            }
            try {
                o.MessageLogServer.WriteSystemMessage(hesap, "Sohbetin yönetici tarafından kapatıldı (" + kalan + " dk kaldı).");
            } catch (Exception) {
            }
            return true;
        }

        public static void Defter(string ip, string islem, string sonuc) {
            try {
                File.AppendAllText(IslemDefteri, DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    + " | " + ip + " | " + islem + " | " + sonuc + "\n", Encoding.UTF8);
            } catch (Exception) {
            }
            Debug.Log("[Sunucu] yönetim: " + islem + " → " + sonuc);
        }

        // ================================================================ saniyede bir

        private static float sonrakiBekleyen = 0f;

        /// <summary>YonetimPaneli.SunucuTick: bekleyen hediyeler girmiş oyunculara verilir</summary>
        public static void Tick(SystemGameManager o) {
            Oku();
            if (Time.realtimeSinceStartup < sonrakiBekleyen || bekleyenler.liste.Count == 0) {
                return;
            }
            sonrakiBekleyen = Time.realtimeSinceStartup + 3f;
            bool degisti = false;
            foreach (Bekleyen b in bekleyenler.liste.ToArray()) {
                UnitController u = Canli(o, b.hesap);
                if (u == null || u.CharacterInventoryManager == null || u.CharacterStats == null
                    || YonetimPaneli.OturumSaniyesi(b.hesap) < 8) {
                    continue;
                }
                string sonuc;
                try {
                    sonuc = Uygula(o, u, b.tur, b.ad, b.miktar, b.metin, true);
                } catch (Exception e) {
                    sonuc = "hata: " + e.Message;
                }
                bekleyenler.liste.Remove(b);
                degisti = true;
                Defter("sunucu", "bekleyen " + b.tur + " " + b.ad + " " + b.miktar + " → #" + b.hesap + " " + u.DisplayName, sonuc);
            }
            if (degisti) {
                BekleyenleriYaz();
            }
        }

        private static UnitController Canli(SystemGameManager o, int hesap) {
            UnitController u;
            if (o != null && o.PlayerManagerServer != null && o.PlayerManagerServer.ActiveUnitControllers.TryGetValue(hesap, out u)) {
                return u;
            }
            return null;
        }

        // ================================================================ işlemler

        private static readonly HashSet<string> bekletilebilir = new HashSet<string>() {
            "esya-ver", "para-ekle", "para-ayarla", "kut-ekle", "kut-ayarla", "tecrube-ver", "seviye-ayarla", "mesaj"
        };

        /// <summary>tek oyuncuya işlem; sonuç JSON</summary>
        public static string Islem(SystemGameManager o, Istek i, string ip) {
            Oku();
            UserAccount hesap = o.UserAccountService.HesapBul(i.hesap);
            if (hesap == null) {
                return Hata("Hesap bulunamadı (#" + i.hesap + ").");
            }
            string hedef = hesap.UserName + " (#" + hesap.Id + ")";
            string tur = (i.tur ?? string.Empty).Trim();
            string ad = (i.ad ?? string.Empty).Trim();
            string metin = (i.metin ?? string.Empty).Trim();
            string sonuc;
            switch (tur) {
                case "at":
                    if (Canli(o, hesap.Id) == null) {
                        return Hata("Oyuncu oyunda değil.");
                    }
                    o.NetworkManagerServer.KickPlayer(hesap.Id);
                    sonuc = "oyundan atıldı";
                    break;
                case "sustur":
                    lock (kilit) {
                        if (i.miktar <= 0) {
                            susturulanlar.Remove(hesap.Id);
                        } else {
                            susturulanlar[hesap.Id] = DateTime.UtcNow.AddMinutes(Math.Min(i.miktar, 60L * 24 * 365));
                        }
                    }
                    SusturmalariYaz();
                    sonuc = i.miktar <= 0 ? "susturma kaldırıldı" : i.miktar + " dk susturuldu";
                    UnitController su = Canli(o, hesap.Id);
                    if (su != null) {
                        OtukenAg.Mesaj(su, "<color=#FF9A7A>" + (i.miktar <= 0 ? "Sohbetin yeniden açıldı." : "Sohbetin " + i.miktar + " dakika kapatıldı.") + "</color>");
                    }
                    break;
                case "sifre":
                    if (metin.Length < 6) {
                        return Hata("Yeni şifre en az 6 karakter olmalı.");
                    }
                    if (string.IsNullOrEmpty(hesap.Salt)) {
                        AuthenticationHelpers.ProvideSaltAndHash(hesap);
                    }
                    hesap.PasswordHash = AuthenticationHelpers.ComputeHash(metin, hesap.Salt);
                    o.ServerDataService.SaveAccount(hesap);
                    sonuc = "şifre değiştirildi";
                    break;
                case "google-ayir":
                    if (string.IsNullOrEmpty(hesap.GoogleId)) {
                        return Hata("Bu hesap Google'a bağlı değil.");
                    }
                    hesap.GoogleId = string.Empty;
                    o.ServerDataService.SaveAccount(hesap);
                    sonuc = "Google bağı kaldırıldı";
                    break;
                case "hesap-sil":
                    if (metin != hesap.UserName) {
                        return Hata("Onay için hesap adını aynen yaz.");
                    }
                    HesapSilme.YoneticiSil(hesap.Id, hesap.UserName);
                    sonuc = "silme sırasına kondu (oyundaysa çıkarılır, 15 sn sonra silinir)";
                    break;
                case "bekleyen-sil":
                    int silinen = bekleyenler.liste.RemoveAll(b => b.hesap == hesap.Id);
                    BekleyenleriYaz();
                    sonuc = silinen + " bekleyen silindi";
                    break;
                case "esya-sil": {
                        UnitController u = Canli(o, hesap.Id);
                        if (u == null) {
                            return Hata("Eşya silmek için oyuncu oyunda olmalı.");
                        }
                        long no;
                        if (long.TryParse(ad, NumberStyles.Integer, CultureInfo.InvariantCulture, out no) == false) {
                            return Hata("Eşya numarası geçersiz.");
                        }
                        InstantiatedItem e = o.SystemItemManager.GetExistingInstantiatedItem(no);
                        string eAd = e != null ? e.DisplayName : "#" + no;
                        u.CharacterInventoryManager.DeleteItem(no);
                        u.UnitEventController.NotifyOnSaveDataUpdated();
                        sonuc = eAd + " silindi";
                        break;
                    }
                default: {
                        UnitController u = Canli(o, hesap.Id);
                        if (u == null) {
                            if (bekletilebilir.Contains(tur) == false) {
                                return Hata("Bu işlem için oyuncu oyunda olmalı.");
                            }
                            string denetim = Denetle(o, tur, ad, i.miktar, metin);
                            if (denetim != null) {
                                return Hata(denetim);
                            }
                            bekleyenler.liste.Add(new Bekleyen() {
                                hesap = hesap.Id, tur = tur, ad = ad, miktar = i.miktar, metin = metin,
                                zaman = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            });
                            BekleyenleriYaz();
                            sonuc = "oyuncu çevrimdışı: girince verilecek";
                        } else {
                            sonuc = Uygula(o, u, tur, ad, i.miktar, metin, false);
                            if (sonuc.StartsWith("!")) {
                                Defter(ip, tur + " " + ad + " " + i.miktar + " → " + hedef, sonuc);
                                return Hata(sonuc.Substring(1));
                            }
                        }
                        break;
                    }
            }
            Defter(ip, tur + (ad.Length > 0 ? " " + ad : string.Empty) + (i.miktar != 0 ? " " + i.miktar : string.Empty) + " → " + hedef, sonuc);
            return Tamam(sonuc);
        }

        /// <summary>çevrimiçi herkese aynı işlem</summary>
        public static string Herkese(SystemGameManager o, Istek i, string ip) {
            string tur = (i.tur ?? string.Empty).Trim();
            if (tur != "esya-ver" && tur != "para-ekle" && tur != "kut-ekle" && tur != "tecrube-ver" && tur != "iyilestir" && tur != "mesaj") {
                return Hata("Herkese yalnız eşya, akçe, Kut, tecrübe, iyileştirme ve mesaj verilebilir.");
            }
            string denetim = Denetle(o, tur, i.ad, i.miktar, i.metin);
            if (denetim != null) {
                return Hata(denetim);
            }
            int n = 0, hata = 0;
            foreach (UnitController u in new List<UnitController>(o.PlayerManagerServer.ActiveUnitControllers.Values)) {
                if (u == null) {
                    continue;
                }
                string s = Uygula(o, u, tur, (i.ad ?? string.Empty).Trim(), i.miktar, (i.metin ?? string.Empty).Trim(), true);
                if (s.StartsWith("!")) {
                    hata++;
                } else {
                    n++;
                }
            }
            string sonuc = n + " oyuncuya verildi" + (hata > 0 ? ", " + hata + " oyuncuya verilemedi" : string.Empty);
            Defter(ip, "HERKESE " + tur + " " + i.ad + " " + i.miktar, sonuc);
            return Tamam(sonuc);
        }

        /// <summary>işlemin değerleri geçerli mi (null: geçerli)</summary>
        private static string Denetle(SystemGameManager o, string tur, string ad, long miktar, string metin) {
            switch (tur) {
                case "esya-ver":
                    if (EsyaBul(o, ad) == null) {
                        return "Böyle bir eşya yok: " + ad;
                    }
                    if (miktar < 1 || miktar > 200) {
                        return "Adet 1-200 arası olmalı.";
                    }
                    return null;
                case "para-ekle":
                case "kut-ekle":
                case "tecrube-ver":
                    if (miktar == 0 || Math.Abs(miktar) > 1000000000L) {
                        return "Miktar geçersiz.";
                    }
                    if (tur == "tecrube-ver" && miktar < 0) {
                        return "Tecrübe eksi olamaz.";
                    }
                    return null;
                case "para-ayarla":
                case "kut-ayarla":
                    return miktar < 0 || miktar > 2000000000L ? "Miktar geçersiz." : null;
                case "seviye-ayarla":
                    return miktar < 1 || miktar > 200 ? "Seviye 1-200 arası olmalı." : null;
                case "mesaj":
                    return string.IsNullOrEmpty(metin) ? "Mesaj boş." : null;
                case "iyilestir":
                    return null;
                default:
                    return "Bilinmeyen işlem: " + tur;
            }
        }

        /// <summary>çevrimiçi oyuncuya işlemi uygular; "!" ile başlayan sonuç hatadır</summary>
        private static string Uygula(SystemGameManager o, UnitController u, string tur, string ad, long miktar, string metin, bool bildir) {
            string denetim = tur == "dirilt" || tur == "isinla" ? null : Denetle(o, tur, ad, miktar, metin);
            if (denetim != null) {
                return "!" + denetim;
            }
            string sonuc;
            string bildirim = null;
            switch (tur) {
                case "esya-ver": {
                        Item esyaTuru = EsyaBul(o, ad);
                        int verilen = 0;
                        for (int k = 0; k < miktar; k++) {
                            InstantiatedItem e = u.CharacterInventoryManager.GetNewInstantiatedItem(esyaTuru.ResourceName);
                            if (e == null) {
                                break;
                            }
                            if (u.CharacterInventoryManager.AddItem(e, false) == false) {
                                break;
                            }
                            o.ServerDataService.CreateItemInstance(e);
                            verilen++;
                        }
                        if (verilen == 0) {
                            return "!Eşya verilemedi (çanta dolu olabilir).";
                        }
                        sonuc = verilen + " × " + esyaTuru.DisplayName + " verildi" + (verilen < miktar ? " (çanta doldu)" : string.Empty);
                        bildirim = "Yönetici sana " + verilen + " × " + esyaTuru.DisplayName + " gönderdi.";
                        break;
                    }
                case "para-ekle":
                case "para-ayarla": {
                        Currency bakir = Ticaret.Bakir;
                        if (bakir == null) {
                            return "!Akçe bulunamadı.";
                        }
                        int simdi = Ticaret.Para(u);
                        long fark = tur == "para-ekle" ? miktar : miktar - simdi;
                        if (fark > 0) {
                            u.CharacterCurrencyManager.AddCurrency(bakir, (int)Math.Min(fark, int.MaxValue - (long)simdi));
                        } else if (fark < 0) {
                            u.CharacterCurrencyManager.SpendCurrency(bakir, (int)Math.Min(-fark, simdi));
                        }
                        sonuc = "akçe: " + OtukenPencere.ParaYazisi(simdi) + " → " + OtukenPencere.ParaYazisi(Ticaret.Para(u));
                        if (fark > 0) {
                            bildirim = "Yönetici sana " + OtukenPencere.ParaYazisi((int)fark) + " gönderdi.";
                        }
                        break;
                    }
                case "kut-ekle":
                case "kut-ayarla": {
                        Currency kut = KutDukkani.Kut;
                        if (kut == null) {
                            return "!Kut bulunamadı.";
                        }
                        int simdi = KutDukkani.KutMiktari(u);
                        long fark = tur == "kut-ekle" ? miktar : miktar - simdi;
                        if (fark > 0) {
                            u.CharacterCurrencyManager.AddCurrency(kut, (int)Math.Min(fark, int.MaxValue - (long)simdi));
                        } else if (fark < 0) {
                            u.CharacterCurrencyManager.SpendCurrency(kut, (int)Math.Min(-fark, simdi));
                        }
                        sonuc = "Kut: " + simdi + " → " + KutDukkani.KutMiktari(u);
                        if (fark > 0) {
                            bildirim = "Yönetici sana " + fark + " Kut gönderdi.";
                        }
                        Ticaret.Kaydet(u);
                        break;
                    }
                case "tecrube-ver": {
                        int once = u.CharacterStats.Level;
                        u.CharacterStats.GainExperience((int)Math.Min(miktar, int.MaxValue));
                        sonuc = miktar + " tecrübe verildi (seviye " + once + " → " + u.CharacterStats.Level + ")";
                        bildirim = "Yönetici sana " + miktar + " tecrübe gönderdi.";
                        break;
                    }
                case "seviye-ayarla": {
                        int once = u.CharacterStats.Level;
                        u.CharacterStats.SetLevel((int)miktar);
                        // yeni seviyenin başından başlasın (eski tecrübe yeni seviyenin eşiğini aşmasın)
                        u.CharacterStats.SetXP(0);
                        u.UnitEventController.NotifyOnGainXP(0, 0);
                        u.UnitEventController.NotifyOnSaveDataUpdated();
                        sonuc = "seviye " + once + " → " + u.CharacterStats.Level;
                        bildirim = "Seviyen yönetici tarafından " + u.CharacterStats.Level + " yapıldı.";
                        break;
                    }
                case "iyilestir":
                    if (u.CharacterStats.IsAlive == false) {
                        return "!Oyuncu ölü; önce dirilt.";
                    }
                    u.CharacterStats.SetResourceAmountsToMaximum();
                    sonuc = "can ve mana dolduruldu";
                    bildirim = "Yönetici seni iyileştirdi.";
                    break;
                case "dirilt":
                    if (u.CharacterStats.IsAlive) {
                        return "!Oyuncu zaten hayatta.";
                    }
                    u.CharacterStats.Revive();
                    sonuc = "diriltildi";
                    bildirim = "Yönetici seni diriltti.";
                    break;
                case "isinla": {
                        if (Array.IndexOf(IsinlanmaPenceresi.SahneAdlari, ad) < 0) {
                            return "!Bilinmeyen harita: " + ad;
                        }
                        if (u.CharacterStats.IsAlive == false) {
                            return "!Ölü oyuncu ışınlanamaz; önce dirilt.";
                        }
                        IsinlanmaPenceresi.SunucudaIsinla(o, u, ad);
                        sonuc = HaritaSeviyeleri.Ad(ad) + " haritasına ışınlandı";
                        bildirim = "Yönetici seni " + HaritaSeviyeleri.Ad(ad) + " diyarına ışınladı.";
                        break;
                    }
                case "mesaj":
                    OtukenAg.Mesaj(u, "<color=#7FD7FF><b>[Yönetici]</b> " + Temizle(metin) + "</color>");
                    sonuc = "mesaj gönderildi";
                    break;
                default:
                    return "!Bilinmeyen işlem: " + tur;
            }
            // oyuncu ne aldığını görsün
            if (bildirim != null) {
                OtukenAg.Mesaj(u, "<color=#FFD54A>" + bildirim + "</color>");
            }
            return sonuc;
        }

        private static string Temizle(string metin) {
            string m = metin.Length > 300 ? metin.Substring(0, 300) : metin;
            return m.Replace("<", "‹").Replace(">", "›");
        }

        // ================================================================ hesap aç

        public static string HesapAc(SystemGameManager o, string ad, string sifre, string ip) {
            ad = (ad ?? string.Empty).Trim();
            sifre = sifre ?? string.Empty;
            if (ad.Length < 3 || ad.Length > 20 || Regex.IsMatch(ad, "^[A-Za-z0-9_çğıöşüÇĞİÖŞÜ]+$") == false) {
                return Hata("Kullanıcı adı 3-20 karakter olmalı; harf, rakam ve _ kullanılabilir.");
            }
            if (sifre.Length < 6) {
                return Hata("Şifre en az 6 karakter olmalı.");
            }
            if (o.UserAccountService.AccountExists(ad)) {
                return Hata("Bu kullanıcı adı alınmış.");
            }
            UserAccount yeni = o.UserAccountService.CreateNewAccount(ad, sifre);
            if (yeni == null) {
                return Hata("Hesap açılamadı.");
            }
            Defter(ip, "hesap aç " + ad, "#" + yeni.Id);
            YonetimPaneli.HesaplariYenile();
            return Tamam(ad + " hesabı açıldı (#" + yeni.Id + ").");
        }

        // ================================================================ eşya listesi

        private static string esyaJson = null;
        private static Dictionary<string, Item> esyaDizini = null;

        private static void EsyalariHazirla(SystemGameManager o) {
            if (esyaDizini != null) {
                return;
            }
            esyaDizini = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
            List<Item> hepsi = new List<Item>(o.SystemDataFactory.GetResourceList<Item>());
            hepsi.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
            Js js = new Js().Dizi();
            foreach (Item e in hepsi) {
                if (e == null || string.IsNullOrEmpty(e.ResourceName)) {
                    continue;
                }
                esyaDizini[e.ResourceName] = e;
                if (string.IsNullOrEmpty(e.DisplayName) == false && esyaDizini.ContainsKey(e.DisplayName) == false) {
                    esyaDizini[e.DisplayName] = e;
                }
                js.Nesne().Y("ad", e.ResourceName).Y("gorunen", e.DisplayName).Y("tur", e.GetType().Name).NesneBitir();
            }
            esyaJson = js.DiziBitir().ToString();
        }

        private static Item EsyaBul(SystemGameManager o, string ad) {
            EsyalariHazirla(o);
            Item e;
            return string.IsNullOrEmpty(ad) == false && esyaDizini.TryGetValue(ad.Trim(), out e) ? e : null;
        }

        public static string EsyaListesi(SystemGameManager o) {
            EsyalariHazirla(o);
            return esyaJson;
        }

        public static string Haritalar() {
            Js js = new Js().Dizi();
            foreach (string s in IsinlanmaPenceresi.SahneAdlari) {
                js.Nesne().Y("sahne", s).Y("ad", HaritaSeviyeleri.Ad(s)).Y("seviye", HaritaSeviyeleri.Yazi(s)).NesneBitir();
            }
            return js.DiziBitir().ToString();
        }

        public static string Bekleyenler() {
            Oku();
            Js js = new Js().Dizi();
            foreach (Bekleyen b in bekleyenler.liste) {
                js.Nesne().S("hesap", b.hesap).Y("tur", b.tur).Y("ad", b.ad).S("miktar", b.miktar).Y("metin", b.metin)
                    .Y("zaman", DateTimeOffset.FromUnixTimeSeconds(b.zaman).UtcDateTime.AddHours(3).ToString("dd.MM HH:mm", CultureInfo.InvariantCulture))
                    .NesneBitir();
            }
            return js.DiziBitir().ToString();
        }

        // ================================================================ oyuncu ayrıntısı

        public static string OyuncuJson(SystemGameManager o, int hesapNo) {
            Oku();
            UserAccount hesap = o.UserAccountService.HesapBul(hesapNo);
            if (hesap == null) {
                return Hata("Hesap bulunamadı.");
            }
            UnitController u = Canli(o, hesapNo);
            Js js = new Js().Nesne();
            js.Nesne("hesap").S("no", hesap.Id).Y("ad", hesap.UserName).B("google", string.IsNullOrEmpty(hesap.GoogleId) == false)
                .B("yasak", YonetimPaneli.YasakliMi(hesap.Id));
            int kalan;
            js.Y("sustur", Susturuldu(hesap.Id, out kalan) ? kalan + " dk" : string.Empty);
            YonetimPaneli.SureBilgisi(hesap.Id, js);
            LoggedInAccount giris;
            if (o.AuthenticationService.LoggedInAccounts.TryGetValue(hesap.Id, out giris) && giris != null) {
                js.Y("ip", giris.ipAddress ?? string.Empty);
            }
            js.NesneBitir();
            js.B("cevrimici", u != null);
            if (u != null) {
                CanliKarakter(o, u, js);
            }
            Karakterler(o, hesap.Id, js, u != null ? u.DisplayName : null);
            js.Ham("oturumlar", YonetimPaneli.OturumlarJson(hesap.Id, 30));
            js.Dizi("bekleyen");
            foreach (Bekleyen b in bekleyenler.liste) {
                if (b.hesap == hesap.Id) {
                    js.Nesne().Y("tur", b.tur).Y("ad", b.ad).S("miktar", b.miktar).NesneBitir();
                }
            }
            js.DiziBitir();
            js.NesneBitir();
            return js.ToString();
        }

        private static void CanliKarakter(SystemGameManager o, UnitController u, Js js) {
            CharacterStats st = u.CharacterStats;
            js.Nesne("canli");
            js.Y("karakter", u.DisplayName);
            js.Y("sinif", u.BaseCharacter != null && u.BaseCharacter.CharacterClass != null ? u.BaseCharacter.CharacterClass.DisplayName : string.Empty);
            js.Y("uzmanlik", u.BaseCharacter != null && u.BaseCharacter.ClassSpecialization != null ? u.BaseCharacter.ClassSpecialization.DisplayName : string.Empty);
            js.S("seviye", st.Level).S("tecrube", st.CurrentXP);
            try {
                js.S("gereken", LevelEquations.GetXPNeededForLevel(st.Level, o.SystemConfigurationManager));
            } catch (Exception) {
                js.S("gereken", 0);
            }
            js.S("can", st.CurrentPrimaryResource).S("canMax", st.MaxPrimaryResource).B("olu", st.IsAlive == false);
            js.Y("harita", HaritaSeviyeleri.Ad(u.gameObject.scene.name)).Y("sahne", u.gameObject.scene.name);
            Vector3 p = u.transform.position;
            js.Y("konum", p.x.ToString("0", CultureInfo.InvariantCulture) + ", " + p.y.ToString("0", CultureInfo.InvariantCulture) + ", " + p.z.ToString("0", CultureInfo.InvariantCulture));
            js.Y("hedef", u.Target != null ? u.Target.DisplayName : string.Empty);
            js.B("binek", u.IsMounted);
            js.B("savasta", u.CharacterCombat != null && u.CharacterCombat.GetInCombat());
            js.S("kut", KutDukkani.KutMiktari(u)).S("bakir", Ticaret.Para(u)).Y("para", OtukenPencere.ParaYazisi(Ticaret.Para(u)));
            js.Dizi("paralar");
            foreach (KeyValuePair<string, CurrencyNode> kv in u.CharacterCurrencyManager.CurrencyList) {
                js.Nesne().Y("ad", kv.Value.currency != null ? kv.Value.currency.DisplayName : kv.Key).S("miktar", kv.Value.Amount).NesneBitir();
            }
            js.DiziBitir();
            CharacterInventoryManager env = u.CharacterInventoryManager;
            js.S("cantaBos", env.EmptySlotCount()).S("cantaToplam", env.InventorySlots.Count);
            js.S("depoBos", env.EmptySlotCount(true)).S("depoToplam", env.BankSlots.Count);
            Yuvalar(env.InventorySlots, js, "canta");
            Yuvalar(env.BankSlots, js, "depo");
            js.Dizi("kusanilan");
            if (u.CharacterEquipmentManager != null) {
                foreach (KeyValuePair<EquipmentSlotProfile, EquipmentInventorySlot> kv in u.CharacterEquipmentManager.CurrentEquipment) {
                    InstantiatedEquipment e = kv.Value != null ? kv.Value.InstantiatedEquipment : null;
                    if (e == null) {
                        continue;
                    }
                    js.Nesne().Y("yuva", kv.Key != null ? kv.Key.DisplayName : "?").Y("ad", e.DisplayName).S("no", e.InstanceId)
                        .Y("kalite", e.ItemQuality != null ? e.ItemQuality.DisplayName : string.Empty).NesneBitir();
                }
            }
            js.DiziBitir();
            js.Dizi("gorevler");
            int biten = 0;
            if (u.CharacterQuestLog != null) {
                foreach (KeyValuePair<string, Quest> kv in u.CharacterQuestLog.Quests) {
                    if (kv.Value == null) {
                        continue;
                    }
                    string durum;
                    try {
                        durum = kv.Value.IsComplete(u) ? "tamamlandı (teslim bekliyor)" : "sürüyor";
                    } catch (Exception) {
                        durum = "sürüyor";
                    }
                    js.Nesne().Y("ad", kv.Value.DisplayName).Y("durum", durum).NesneBitir();
                }
                foreach (KeyValuePair<string, QuestSaveData> kv in u.CharacterQuestLog.QuestSaveDataDictionary) {
                    if (kv.Value != null && kv.Value.TurnedIn) {
                        biten++;
                    }
                }
            }
            js.DiziBitir();
            js.S("bitenGorev", biten);
            js.Ham("sohbet", SonSohbet(u.DisplayName, 25));
            js.NesneBitir();
        }

        private static void Yuvalar(List<InventorySlot> yuvalar, Js js, string ad) {
            js.Dizi(ad);
            for (int k = 0; k < yuvalar.Count; k++) {
                InventorySlot y = yuvalar[k];
                if (y == null || y.IsEmpty || y.InstantiatedItem == null) {
                    continue;
                }
                InstantiatedItem e = y.InstantiatedItem;
                js.Nesne().S("yuva", k + 1).Y("ad", e.DisplayName).S("adet", y.Count).S("no", e.InstanceId)
                    .Y("kalite", e.ItemQuality != null ? e.ItemQuality.DisplayName : string.Empty).NesneBitir();
            }
            js.DiziBitir();
        }

        /// <summary>hesabın diskteki bütün karakterleri (çevrimdışı görünüm)</summary>
        private static void Karakterler(SystemGameManager o, int hesapNo, Js js, string canliAd) {
            js.Dizi("karakterler");
            try {
                string oyunAdi = Regex.Replace(o.SystemConfigurationManager.GameName ?? string.Empty, "[^a-zA-Z0-9]", "");
                string klasor = Path.Combine(Application.persistentDataPath, oyunAdi, "Online", "PlayerCharacters", hesapNo.ToString(CultureInfo.InvariantCulture));
                if (Directory.Exists(klasor)) {
                    foreach (string dosya in Directory.GetFiles(klasor, "*.json")) {
                        CharacterSaveData k;
                        try {
                            k = JsonUtility.FromJson<CharacterSaveData>(File.ReadAllText(dosya));
                        } catch (Exception) {
                            continue;
                        }
                        if (k == null) {
                            continue;
                        }
                        js.Nesne().S("no", k.CharacterId).Y("ad", k.CharacterName).S("seviye", k.CharacterLevel).S("tecrube", k.CurrentExperience)
                            .Y("sinif", k.CharacterClass).Y("harita", HaritaSeviyeleri.Ad(k.CurrentScene)).B("olu", k.IsDead)
                            .Y("konum", k.PlayerLocationX.ToString("0", CultureInfo.InvariantCulture) + ", " + k.PlayerLocationY.ToString("0", CultureInfo.InvariantCulture)
                                + ", " + k.PlayerLocationZ.ToString("0", CultureInfo.InvariantCulture))
                            .Y("kayit", k.DataSavedOn).B("oyunda", canliAd != null && canliAd == k.CharacterName);
                        js.Dizi("paralar");
                        foreach (CurrencySaveData c in k.CurrencySaveData) {
                            js.Nesne().Y("ad", c.CurrencyName).S("miktar", c.Amount).NesneBitir();
                        }
                        js.DiziBitir();
                        KayitYuvalari(o, k.InventorySlotSaveData, js, "canta");
                        KayitYuvalari(o, k.BankSlotSaveData, js, "depo");
                        js.Dizi("kusanilan");
                        foreach (EquipmentInventorySlotSaveData e in k.EquipmentSaveData) {
                            if (e == null || e.HasItem == false) {
                                continue;
                            }
                            InstantiatedItem ie = o.SystemItemManager.GetExistingInstantiatedItem(e.ItemInstanceId);
                            js.Nesne().Y("ad", ie != null ? ie.DisplayName : "(kayıp eşya #" + e.ItemInstanceId + ")").NesneBitir();
                        }
                        js.DiziBitir();
                        int aktif = 0, biten = 0;
                        foreach (QuestSaveData q in k.QuestSaveData) {
                            if (q == null) {
                                continue;
                            }
                            if (q.TurnedIn) {
                                biten++;
                            } else if (q.InLog) {
                                aktif++;
                            }
                        }
                        js.S("aktifGorev", aktif).S("bitenGorev", biten);
                        js.NesneBitir();
                    }
                }
            } catch (Exception e) {
                Debug.LogWarning("[Yonetim] karakterler okunamadı: " + e.Message);
            }
            js.DiziBitir();
        }

        private static void KayitYuvalari(SystemGameManager o, List<InventorySlotSaveData> yuvalar, Js js, string ad) {
            js.Dizi(ad);
            for (int k = 0; k < yuvalar.Count; k++) {
                InventorySlotSaveData y = yuvalar[k];
                if (y == null || y.ItemInstanceIds == null || y.ItemInstanceIds.Count == 0) {
                    continue;
                }
                InstantiatedItem e = o.SystemItemManager.GetExistingInstantiatedItem(y.ItemInstanceIds[0]);
                js.Nesne().S("yuva", k + 1).Y("ad", e != null ? e.DisplayName : "(kayıp eşya #" + y.ItemInstanceIds[0] + ")")
                    .S("adet", y.ItemInstanceIds.Count).NesneBitir();
            }
            js.DiziBitir();
        }

        /// <summary>sohbet.log'un sonundan bu karakterin son satırları</summary>
        private static string SonSohbet(string karakter, int adet) {
            Js js = new Js().Dizi();
            try {
                string yol = Path.Combine(Application.persistentDataPath, "sohbet.log");
                if (File.Exists(yol) && string.IsNullOrEmpty(karakter) == false) {
                    string arama = " " + karakter + ": ";
                    List<string> bulunan = new List<string>();
                    foreach (string satir in YonetimPaneli.SonSatirlarDisari(yol, 3000)) {
                        if (satir.Contains(arama)) {
                            bulunan.Add(satir);
                        }
                    }
                    for (int k = bulunan.Count - 1, n = 0; k >= 0 && n < adet; k--, n++) {
                        js.Y(null, bulunan[k]);
                    }
                }
            } catch (Exception) {
            }
            return js.DiziBitir().ToString();
        }
    }
}
