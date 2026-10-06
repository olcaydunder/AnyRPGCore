using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace AnyRPG {

    /// <summary>
    /// Google Play satın almasının doğrulanması (sunucuda; tek oyunculu oyunda telefonda). Google her satın almanın
    /// bilgisini (JSON) uygulamanın lisans anahtarıyla eşleşen gizli anahtarla imzalar (SHA1withRSA). Play Console'daki
    /// "Para kazanma ayarları > Lisanslama" sayfasındaki Base64 genel anahtar ile imza denetlenir: sahte ya da değiştirilmiş
    /// satın alma kabul edilmez. Genel anahtar gizli değildir.
    /// Unity'ye bağlı değildir (dotnet ile denenebilir).
    /// </summary>
    public static class OdemeDogrulama {

        public class SatinAlma {
            public string orderId = string.Empty;
            public string packageName = string.Empty;
            public string productId = string.Empty;
            public string purchaseToken = string.Empty;
            public int purchaseState = -1;
            public int quantity = 1;
        }

        /// <summary>
        /// imzayı ve alanları denetler. hata null ise geçerli. beklenenPaket boşsa paket adı denetlenmez.
        /// </summary>
        public static SatinAlma Dogrula(string json, string imza, string genelAnahtar, string beklenenPaket, out string hata) {
            hata = null;
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(imza)) {
                hata = "Satın alma bilgisi eksik.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(genelAnahtar)) {
                hata = "Ödeme doğrulaması henüz kurulmadı.";
                return null;
            }
            try {
                RSAParameters p = GenelAnahtarOku(Convert.FromBase64String(genelAnahtar.Trim()));
                using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider()) {
                    rsa.ImportParameters(p);
                    if (rsa.VerifyData(Encoding.UTF8.GetBytes(json), "SHA1", Convert.FromBase64String(imza.Trim())) == false) {
                        hata = "Satın alma imzası geçersiz.";
                        return null;
                    }
                }
            } catch (Exception e) {
                hata = "Satın alma doğrulanamadı: " + e.Message;
                return null;
            }
            SatinAlma s = Oku(json);
            if (string.IsNullOrEmpty(beklenenPaket) == false && s.packageName != beklenenPaket) {
                hata = "Satın alma bu oyuna ait değil.";
                return null;
            }
            if (s.purchaseState != 0) {
                hata = s.purchaseState == 2 ? "Ödeme henüz onaylanmadı (bekliyor)." : "Satın alma tamamlanmamış.";
                return null;
            }
            if (string.IsNullOrEmpty(s.purchaseToken) || string.IsNullOrEmpty(s.productId)) {
                hata = "Satın alma bilgisi eksik.";
                return null;
            }
            if (s.quantity < 1) {
                s.quantity = 1;
            }
            return s;
        }

        /// <summary>Google'ın düz JSON'ından alanlar (imza denetiminden sonra; iç içe yapı yok)</summary>
        public static SatinAlma Oku(string json) {
            SatinAlma s = new SatinAlma();
            s.orderId = Yazi(json, "orderId");
            s.packageName = Yazi(json, "packageName");
            s.productId = Yazi(json, "productId");
            s.purchaseToken = Yazi(json, "purchaseToken");
            s.purchaseState = Sayi(json, "purchaseState", 0);
            s.quantity = Sayi(json, "quantity", 1);
            return s;
        }

        private static string Yazi(string json, string alan) {
            Match m = Regex.Match(json, "\"" + alan + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            return m.Success ? Regex.Unescape(m.Groups[1].Value) : string.Empty;
        }

        private static int Sayi(string json, string alan, int varsayilan) {
            Match m = Regex.Match(json, "\"" + alan + "\"\\s*:\\s*(-?\\d+)");
            int n;
            return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : varsayilan;
        }

        // ---------------------------------------------------------------- X.509 SubjectPublicKeyInfo (DER) → RSA

        private static RSAParameters GenelAnahtarOku(byte[] der) {
            int i = 0;
            Bekle(der, ref i, 0x30);
            Uzunluk(der, ref i);
            // AlgorithmIdentifier
            Bekle(der, ref i, 0x30);
            int algUzunluk = Uzunluk(der, ref i);
            i += algUzunluk;
            // BIT STRING
            Bekle(der, ref i, 0x03);
            Uzunluk(der, ref i);
            if (der[i++] != 0x00) {
                throw new FormatException("anahtar biçimi");
            }
            // RSAPublicKey
            Bekle(der, ref i, 0x30);
            Uzunluk(der, ref i);
            byte[] modul = Tamsayi(der, ref i);
            byte[] us = Tamsayi(der, ref i);
            return new RSAParameters() { Modulus = modul, Exponent = us };
        }

        private static void Bekle(byte[] d, ref int i, byte etiket) {
            if (i >= d.Length || d[i] != etiket) {
                throw new FormatException("anahtar biçimi");
            }
            i++;
        }

        private static int Uzunluk(byte[] d, ref int i) {
            int b = d[i++];
            if (b < 0x80) {
                return b;
            }
            int n = b & 0x7F;
            int uzunluk = 0;
            for (int k = 0; k < n; k++) {
                uzunluk = (uzunluk << 8) | d[i++];
            }
            return uzunluk;
        }

        private static byte[] Tamsayi(byte[] d, ref int i) {
            Bekle(d, ref i, 0x02);
            int uzunluk = Uzunluk(d, ref i);
            int bas = i;
            i += uzunluk;
            // baştaki işaret sıfırı atılır
            while (uzunluk > 1 && d[bas] == 0x00) {
                bas++;
                uzunluk--;
            }
            byte[] sonuc = new byte[uzunluk];
            Array.Copy(d, bas, sonuc, 0, uzunluk);
            return sonuc;
        }
    }
}
