using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Oyuncu engelleme ve şikâyet (Google Play'in kullanıcı içeriği kuralı: oyuncular uygunsuz içeriği ve kullanıcıları
    /// bildirebilmeli ve engelleyebilmeli).
    ///  - Engelle: engellenen oyuncunun sohbet yazıları (pencerede ve başının üstünde) gösterilmez, takas teklifleri
    ///    kendiliğinden reddedilir, pazarı listede görünmez. Liste telefonda saklanır; Ticaret penceresinden kaldırılır.
    ///  - Şikâyet Et: neden seçilir; şikâyet geliştiriciye (hata panosuna "Oyuncu şikâyeti" olarak) ve sunucuya gider.
    ///    Sunucu şikâyeti, şikâyet edilenin son sohbet satırlarıyla birlikte sikayetler.log'a yazar (inceleme için).
    /// Sohbet sunucuda Sozguc ile süzülür (küfür, hakaret ve bağlantılar yıldızlanır) ve son satırlar bellekte tutulur.
    /// </summary>
    public static class Engelleme {

        private const string Anahtar = "engellenenler";

        private static HashSet<string> engellenenler = null;

        private static HashSet<string> Liste {
            get {
                if (engellenenler == null) {
                    engellenenler = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string ad in PlayerPrefs.GetString(Anahtar, string.Empty).Split('\n')) {
                        if (ad.Trim().Length > 0) {
                            engellenenler.Add(ad.Trim());
                        }
                    }
                }
                return engellenenler;
            }
        }

        public static IEnumerable<string> Engellenenler {
            get { return Liste; }
        }

        public static bool EngelliMi(string ad) {
            return string.IsNullOrEmpty(ad) == false && Liste.Contains(ad.Trim());
        }

        public static void Engelle(string ad) {
            if (string.IsNullOrWhiteSpace(ad)) {
                return;
            }
            Liste.Add(ad.Trim());
            Yaz();
            Mesaj(ad + " engellendi: yazıları gösterilmeyecek, takas teklifleri reddedilecek.");
        }

        public static void Kaldir(string ad) {
            if (Liste.Remove((ad ?? string.Empty).Trim())) {
                Yaz();
                Mesaj(ad + " oyuncusunun engeli kaldırıldı.");
            }
        }

        private static void Yaz() {
            PlayerPrefs.SetString(Anahtar, string.Join("\n", Liste));
            PlayerPrefs.Save();
        }

        private static void Mesaj(string metin) {
            SystemGameManager o = OtukenAg.Oyun;
            UnitController oyuncu = o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            if (oyuncu != null) {
                OtukenAg.Mesaj(oyuncu, metin);
            }
        }

        /// <summary>sohbet satırı "Ad: yazı" engellenen birinden mi</summary>
        public static bool SohbetEngelli(string satir) {
            if (string.IsNullOrEmpty(satir) || Liste.Count == 0) {
                return false;
            }
            int iki = satir.IndexOf(':');
            return iki > 0 && EngelliMi(satir.Substring(0, iki));
        }

        public static readonly string[] Nedenler = {
            "Küfür ya da hakaret", "Taciz ya da tehdit", "Uygunsuz ad ya da pazar başlığı", "Dolandırıcılık ya da gerçek parayla satış",
            "Hile ya da hata kullanımı", "Spam ya da reklam", "Başka"
        };

        /// <summary>şikâyet: geliştiriciye ve sunucuya</summary>
        public static void SikayetEt(string ad, string neden) {
            string harita = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool gitti = HataBildirici.OyuncuSikayeti("Şikâyet edilen: " + ad + "\nneden: " + neden + "\nharita: " + harita);
            OtukenAg.Gonder("sikayet", ad + "\u001F" + neden);
            Mesaj(gitti || Cevrimici.Acik ? "Şikâyetin alındı, incelenecek. Teşekkürler." : "Şikâyet gönderilemedi; internet bağlantını denetle.");
        }

        // ================================================================ sunucu

        private class SohbetSatiri {
            public string ad;
            public string yazi;
            public DateTime zaman;
        }

        private static readonly List<SohbetSatiri> sonSohbet = new List<SohbetSatiri>();
        private static string sohbetDefteri = null;

        /// <summary>sunucu: süzülmüş sohbet satırını belleğe (son 300) ve sohbet.log'a yazar</summary>
        public static void SohbetKaydet(string ad, string yazi) {
            sonSohbet.Add(new SohbetSatiri() { ad = ad, yazi = yazi, zaman = DateTime.UtcNow });
            if (sonSohbet.Count > 300) {
                sonSohbet.RemoveRange(0, sonSohbet.Count - 300);
            }
            try {
                if (sohbetDefteri == null) {
                    sohbetDefteri = Path.Combine(Application.persistentDataPath, "sohbet.log");
                }
                File.AppendAllText(sohbetDefteri, DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                    + " " + ad + ": " + yazi + "\n", Encoding.UTF8);
            } catch (Exception) {
                // sohbet kaydı olmadan da sürer
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("sikayet", (oyuncu, veri) => {
                string[] p = veri.Split('\u001F');
                string ad = p.Length > 0 ? p[0] : "?";
                string neden = p.Length > 1 ? p[1] : "?";
                StringBuilder s = new StringBuilder();
                s.Append(DateTime.UtcNow.AddHours(3).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append(" ŞİKÂYET ").Append(oyuncu.DisplayName).Append(" -> ").Append(ad).Append(": ").Append(neden).Append('\n');
                int adet = 0;
                for (int i = sonSohbet.Count - 1; i >= 0 && adet < 15; i--) {
                    if (string.Equals(sonSohbet[i].ad, ad, StringComparison.OrdinalIgnoreCase)) {
                        s.Append("    ").Append(sonSohbet[i].zaman.AddHours(3).ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append(' ')
                            .Append(sonSohbet[i].yazi).Append('\n');
                        adet++;
                    }
                }
                try {
                    File.AppendAllText(Path.Combine(Application.persistentDataPath, "sikayetler.log"), s.ToString(), Encoding.UTF8);
                } catch (Exception e) {
                    Debug.LogWarning("[Engelleme] şikâyet yazılamadı: " + e.Message);
                }
                Debug.Log("[Sunucu] " + s.ToString().TrimEnd());
            });
        }
    }
}
