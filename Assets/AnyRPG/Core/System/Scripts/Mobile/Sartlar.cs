using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Kullanım şartları, gizlilik politikası ve oyun kuralları (web sayfaları zootopiayazilim.com'da) ve açık kaynak
    /// lisansları.
    ///  - İlk açılışta (ve şartların sürümü değişince) ana menünün önünde "Kabul Ediyorum" penceresi çıkar; kabul edilmeden
    ///    oyuna geçilmez (Google Play'in kullanıcı içeriği kuralı: sohbet, pazar adı gibi içerikten önce şartlar kabul edilir).
    ///  - Seçenekler > Hesap: sayfalar, açık kaynak lisansları, engellenenler, hesap silme.
    /// Botlarda, sunucuda ve editördeki oyun testinde pencere çıkmaz.
    /// </summary>
    public class Sartlar : OtukenPencere {

        public const string Site = "https://zootopiayazilim.com";
        // aynı sitede başka uygulamalar da var: bu oyunun sayfaları "otuken-" önekli Shopify sayfalarıdır (Tools~/yasal/sayfalar.py)
        public const string Sayfalar = Site + "/pages/otuken-";
        public const string GizlilikAdresi = Sayfalar + "gizlilik";
        public const string KullanimSartlariAdresi = Sayfalar + "kullanim-sartlari";
        public const string OyunKurallariAdresi = Sayfalar + "oyun-kurallari";
        public const string HesapSilmeAdresi = Sayfalar + "hesap-silme";

        // şartlar değişince artırılır: herkes yeniden kabul eder
        private const int Surum = 3;   // 2: isteğe bağlı ödüllü reklamlar (AdMob); 3: Google Play Games ile giriş
        private const string Anahtar = "sartlar-kabul";

        public static bool KabulEdildi {
            get { return PlayerPrefs.GetInt(Anahtar, 0) >= Surum; }
        }

        private static Sartlar ornek = null;

        /// <summary>MobileBootstrap, saniyede bir: kabul edilmediyse pencere açılır</summary>
        public static void Tick() {
            if (KabulEdildi || Application.isEditor || Application.isBatchMode || AgBotu.Etkin) {
                return;
            }
            if (ornek == null) {
                ornek = Kur<Sartlar>("SartlarCanvas", 60);
            }
            if (ornek.Acik == false) {
                ornek.kok.SetActive(true);
            }
        }

        protected override void Kur() {
            PencereKur("Sartlar", 920f, 640f);
            Text baslik = Yazi(Kutu(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -66f), new Vector2(-24f, -10f)),
                "HOŞ GELDİN", 32, TextAnchor.MiddleCenter, Altin);
            baslik.fontStyle = FontStyle.Bold;
            Yazi(Kutu(panel.transform, "Metin", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(36f, -300f), new Vector2(-36f, -76f)),
                "Oynamaya başlamadan önce lütfen aşağıdaki belgeleri oku. \"Kabul Ediyorum\"a dokunarak Kullanım Şartları'nı ve Oyun Kuralları'nı "
                + "kabul etmiş, Gizlilik Politikası'nı okumuş olursun.\n\n"
                + "Kısaca: küfür, hakaret, taciz, dolandırıcılık ve hile yasaktır; gerçek para karşılığı eşya, oyun parası ya da hesap satılamaz. "
                + "Uygunsuz davranışları oyundaki \"Şikâyet Et\" ile bildirebilir, oyuncuları engelleyebilirsin.\n"
                + "Oyunda yalnız senin isteğinle açılan ödüllü reklamlar (Google AdMob) vardır.",
                20, TextAnchor.UpperLeft, YaziRengi);
            Dugme(panel.transform, "Kullanım Şartları", new Vector2(0.5f, 0f), new Vector2(-290f, 230f), new Vector2(270f, 58f), 20, DugmeRengi,
                () => { MobileFeedback.Tap(); Application.OpenURL(KullanimSartlariAdresi); });
            Dugme(panel.transform, "Gizlilik Politikası", new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(270f, 58f), 20, DugmeRengi,
                () => { MobileFeedback.Tap(); Application.OpenURL(GizlilikAdresi); });
            Dugme(panel.transform, "Oyun Kuralları", new Vector2(0.5f, 0f), new Vector2(290f, 230f), new Vector2(270f, 58f), 20, DugmeRengi,
                () => { MobileFeedback.Tap(); Application.OpenURL(OyunKurallariAdresi); });
            Dugme(panel.transform, "Kabul Ediyorum", new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(420f, 76f), 28, OnayRengi, () => {
                MobileFeedback.Success();
                PlayerPrefs.SetInt(Anahtar, Surum);
                PlayerPrefs.Save();
                Kapat();
            });
            // başlıktaki "Kapat" düğmesi yok: kabul edilmeden geçilmez
        }

        // ================================================================ açık kaynak lisansları

        public static void LisanslariGoster() {
            Lisanslar.Goster();
        }
    }
}
