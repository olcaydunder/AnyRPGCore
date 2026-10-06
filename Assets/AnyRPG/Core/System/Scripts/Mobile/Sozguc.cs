using System.Collections.Generic;
using System.Text;

namespace AnyRPG {

    /// <summary>
    /// Oyuncuların yazdıklarının süzgeci (Google Play'in kullanıcı içeriği kuralı: uygunsuz içerik engellenir):
    ///  - Temizle: sohbet ve pazar başlığındaki küfür, hakaret ve bağlantılar yıldızlanır
    ///  - UygunAd: karakter adı ve pazar başlığı küfür, hakaret, bağlantı ve yetkili/kurum taklidi (admin, GM, destek,
    ///    Google...) içeremez
    /// Karşılaştırma yalın biçimde yapılır: Türkçe harfler sadeleşir (ş→s, ı→i...), rakamla yazılmış harfler (0→o, 3→e,
    /// 4→a...) çevrilir, tekrarlanan harfler teke iner. Kısa kökler (göt, sik, piç...) yalnız tek başına kelime olunca
    /// sayılır ("götürmek", "sıkı", "epik", "nazik", "normal" yanlışlıkla yakalanmasın); uzun kökler kelimenin içinde de yakalanır.
    /// </summary>
    public static class Sozguc {

        // kelimenin içinde de yakalanan kökler (yalın biçimde)
        private static readonly string[] kokler = {
            "orospu", "orspu", "yarrak", "amcik", "amck", "amina", "aminakoy", "amink", "pezevenk", "siktir", "siker", "sikey",
            "sikim", "sikiy", "yavsak", "kahpe", "gavat", "kaltak", "surtuk", "dalyarrak", "ananis", "ananiz", "bacini", "kaske",
            "puust", "serefsiz", "gerizekali", "dangalak", "fuck", "shit", "bitch", "cunt", "pussy", "faggot", "whore", "slut",
            "porn", "hitler", "terorist"
        };

        private static string[] yalinKokler = null;

        // yalnız tek başına kelime olunca yakalananlar (kısa ya da başka kelimelerin içinde geçebilenler)
        private static readonly HashSet<string> kelimeler = new HashSet<string>() {
            "amk", "aq", "amq", "sik", "sk", "got", "pic", "oc", "mk", "ibne", "ipne", "bok", "kevase", "pust", "mal", "salak", "aptal",
            "dick", "niger", "niga", "nazi", "isid", "isis", "rape", "sex", "seks", "fag", "yarak", "anan"
        };

        // karakter adı ve pazar başlığında yasak (yetkili ve kurum taklidi, bağlantı)
        private static readonly string[] adYasaklari = {
            "admin", "yonetici", "moderator", "moderatr", "gamemaster", "destek", "yetkili", "sistem", "system", "resmi", "official",
            "google", "playstore", "anyrpg", "metin2", "zootopia", "support", "staff", "developer", "gelistirici", "http", "www"
        };

        private static readonly HashSet<string> adKelimeYasaklari = new HashSet<string>() { "gm", "mod", "gs" };

        /// <summary>yalın biçim: küçük harf, Türkçe harfler sade, rakam/işaret harfleri çevrilmiş, tekrarlar tek</summary>
        public static string Yalin(string metin) {
            if (string.IsNullOrEmpty(metin)) {
                return string.Empty;
            }
            StringBuilder s = new StringBuilder(metin.Length);
            char onceki = '\0';
            foreach (char c0 in metin) {
                char c = Cevir(c0);
                if (c == onceki && char.IsLetter(c)) {
                    continue;
                }
                s.Append(c);
                onceki = c;
            }
            return s.ToString();
        }

        private static char Cevir(char c) {
            switch (c) {
                case 'ı': case 'I': case 'İ': case 'i': case 'î': case 'ì': case 'í': case '1': case '!': case '|': return 'i';
                case 'ş': case 'Ş': case '$': case '5': return 's';
                case 'ç': case 'Ç': return 'c';
                case 'ğ': case 'Ğ': return 'g';
                case 'ü': case 'Ü': case 'û': return 'u';
                case 'ö': case 'Ö': case '0': return 'o';
                case 'â': case 'Â': case '4': case '@': return 'a';
                case '3': return 'e';
                case '7': return 't';
                case '8': return 'b';
                default: return char.ToLowerInvariant(c);
            }
        }

        private static List<KeyValuePair<int, int>> Kelimeler(string metin) {
            // (başlangıç, uzunluk) — harf ve rakamlardan oluşan parçalar
            List<KeyValuePair<int, int>> sonuc = new List<KeyValuePair<int, int>>();
            int bas = -1;
            for (int i = 0; i <= metin.Length; i++) {
                bool harf = i < metin.Length && (char.IsLetterOrDigit(metin[i]) || metin[i] == '$' || metin[i] == '@' || metin[i] == '!');
                if (harf && bas < 0) {
                    bas = i;
                } else if (harf == false && bas >= 0) {
                    sonuc.Add(new KeyValuePair<int, int>(bas, i - bas));
                    bas = -1;
                }
            }
            return sonuc;
        }

        private static bool KelimeUygunsuz(string kelime) {
            string y = Yalin(kelime);
            if (y.Length == 0) {
                return false;
            }
            if (kelimeler.Contains(y)) {
                return true;
            }
            if (yalinKokler == null) {
                string[] liste = new string[kokler.Length];
                for (int i = 0; i < kokler.Length; i++) {
                    liste[i] = Yalin(kokler[i]);
                }
                yalinKokler = liste;
            }
            foreach (string k in yalinKokler) {
                if (y.Contains(k)) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>uygunsuz kelimeleri ve bağlantıları yıldızlar (sohbet, pazar başlığı)</summary>
        public static string Temizle(string metin) {
            if (string.IsNullOrEmpty(metin)) {
                return metin ?? string.Empty;
            }
            char[] c = metin.ToCharArray();
            foreach (KeyValuePair<int, int> k in Kelimeler(metin)) {
                string kelime = metin.Substring(k.Key, k.Value);
                if (KelimeUygunsuz(kelime)) {
                    for (int i = k.Key; i < k.Key + k.Value; i++) {
                        c[i] = '*';
                    }
                }
            }
            string sonuc = new string(c);
            // bağlantılar (dışarıya çağıran reklam, dolandırıcılık)
            string kucuk = sonuc.ToLowerInvariant();
            foreach (string b in new[] { "http://", "https://", "www." }) {
                int i = kucuk.IndexOf(b, System.StringComparison.Ordinal);
                while (i >= 0) {
                    int son = i;
                    while (son < sonuc.Length && char.IsWhiteSpace(sonuc[son]) == false) {
                        son++;
                    }
                    sonuc = sonuc.Substring(0, i) + new string('*', son - i) + sonuc.Substring(son);
                    kucuk = sonuc.ToLowerInvariant();
                    i = kucuk.IndexOf(b, System.StringComparison.Ordinal);
                }
            }
            return sonuc;
        }

        public static bool UygunsuzMu(string metin) {
            return Temizle(metin) != metin;
        }

        /// <summary>karakter adı ve pazar başlığı için: uygunsuzsa neden döner, uygunsa null</summary>
        public static string AdSorunu(string ad) {
            if (string.IsNullOrWhiteSpace(ad)) {
                return "Ad boş olamaz.";
            }
            if (UygunsuzMu(ad)) {
                return "Bu ad uygunsuz bir kelime içeriyor.";
            }
            string y = Yalin(ad);
            string bitisik = new string(System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(y, char.IsLetterOrDigit)));
            foreach (string yasak in adYasaklari) {
                if (bitisik.Contains(yasak)) {
                    return "Bu ad yetkili, kurum ya da bağlantı taklidi gibi görünüyor.";
                }
            }
            foreach (KeyValuePair<int, int> k in Kelimeler(ad)) {
                if (adKelimeYasaklari.Contains(Yalin(ad.Substring(k.Key, k.Value)))) {
                    return "Bu ad yetkili taklidi gibi görünüyor.";
                }
            }
            return null;
        }
    }
}
