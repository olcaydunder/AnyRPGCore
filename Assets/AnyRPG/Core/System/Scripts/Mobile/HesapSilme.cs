using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Hesabı ve bütün verileri silme (Google Play'in hesap silme kuralı: hesap açılan uygulamada hesap oyunun içinden
    /// silinebilmeli; ayrıca web sayfasından da istenebilir, bkz. Sartlar.HesapSilmeAdresi).
    /// Çevrimiçi: Seçenekler > Hesap > "Hesabımı sil" → şifre ve "SİL" yazılır → sunucu şifreyi denetler, oyuncuyu
    /// oyundan çıkarır; karakterin son kaydı diske yazıldıktan sonra hesap dosyası, bütün karakterler, posta, açık artırma
    /// ve arkadaş kayıtları silinir, adlar yeniden alınabilir olur. Geri alınamaz.
    /// Tek oyunculu oyunun kayıtları yalnız telefondadır: karakter seçme ekranındaki "Sil" ile ya da uygulama verisini
    /// temizleyerek silinir.
    /// </summary>
    public class HesapSilme : OtukenPencere {

        // ================================================================ sunucu

        private class Bekleyen {
            public int hesap;
            public string ad;
            public float zaman;
        }

        private static readonly List<Bekleyen> bekleyenler = new List<Bekleyen>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AgKur() {
            OtukenAg.SunucuIsle("hesap-sil", (oyuncu, sifre) => {
                SystemGameManager o = OtukenAg.Oyun;
                int hesapNo = o.PlayerManagerServer.GetAccountIdFromUnitController(oyuncu);
                UserAccount hesap = hesapNo >= 0 ? o.UserAccountService.HesapBul(hesapNo) : null;
                if (hesap == null) {
                    OtukenAg.Yanitla(oyuncu, "hesap-sil", "0|Hesap bulunamadı.");
                    return;
                }
                // Google Play Oyun Hizmetleri'ne bağlı hesapta (Google ile girilir, şifresi bilinmez) şifre sorulmaz:
                // istek zaten bu hesapla girmiş oyuncudan geliyor
                bool googleHesabi = string.IsNullOrEmpty(hesap.GoogleId) == false && string.IsNullOrEmpty(sifre);
                if (googleHesabi == false && AuthenticationHelpers.ComputeHash(sifre ?? string.Empty, hesap.Salt) != hesap.PasswordHash) {
                    OtukenAg.Yanitla(oyuncu, "hesap-sil", "0|Şifre yanlış.");
                    return;
                }
                if (bekleyenler.Exists(b => b.hesap == hesapNo) == false) {
                    bekleyenler.Add(new Bekleyen() { hesap = hesapNo, ad = hesap.UserName, zaman = Time.realtimeSinceStartup });
                }
                Ticaret.Defter("HESAP SİLME İSTENDİ " + hesap.UserName + " (#" + hesapNo + ") karakter " + oyuncu.DisplayName);
                OtukenAg.Yanitla(oyuncu, "hesap-sil", "1|Hesabın ve bütün karakterlerin siliniyor. Oyundan çıkarılıyorsun.");
            });
            OtukenAg.IstemciDinle("hesap-sil", veri => {
                bool iyi = veri.StartsWith("1|");
                string mesaj = veri.Substring(Math.Min(2, veri.Length));
                if (ornek != null) {
                    ornek.Bilgi(mesaj, iyi);
                }
                if (iyi) {
                    SilindiMi = true;
                    MobileFeedback.Success();
                    if (ornek != null) {
                        ornek.StartCoroutine(ornek.Cik());
                    }
                }
            });
        }

        /// <summary>sunucu, saniyede bir: oyuncu çıktıktan ve kayıtlar yazıldıktan sonra hesap silinir</summary>
        public static void SunucuTick(SystemGameManager o) {
            if (bekleyenler.Count == 0 || o == null) {
                return;
            }
            float simdi = Time.realtimeSinceStartup;
            foreach (Bekleyen b in bekleyenler.ToArray()) {
                bool oyunda = o.PlayerManagerServer.ActiveUnitControllers.ContainsKey(b.hesap);
                if (oyunda && simdi - b.zaman > 3f && simdi - b.zaman < 4.5f) {
                    try {
                        o.NetworkManagerServer.KickPlayer(b.hesap);
                    } catch (Exception e) {
                        Debug.LogWarning("[HesapSilme] çıkarılamadı: " + e.Message);
                    }
                }
                if (oyunda || simdi - b.zaman < 15f) {
                    continue;
                }
                try {
                    List<int> karakterler = o.ServerDataService.HesabiSil(b.hesap);
                    if (karakterler == null) {
                        // diske yazma sürüyor: sonra
                        continue;
                    }
                    o.PlayerCharacterService.KarakterleriUnut(b.hesap, karakterler);
                    o.UserAccountService.HesapKaldir(b.hesap);
                    Ticaret.Defter("HESAP SİLİNDİ " + b.ad + " (#" + b.hesap + "), " + karakterler.Count + " karakter");
                } catch (Exception e) {
                    Debug.LogError("[HesapSilme] " + b.ad + " silinemedi: " + e);
                }
                bekleyenler.Remove(b);
            }
        }

        // ================================================================ telefon

        public static bool SilindiMi { get; private set; }

        private static HesapSilme ornek = null;

        public static void Goster() {
            if (ornek == null) {
                ornek = Kur<HesapSilme>("HesapSilmeCanvas", 40);
            }
            ornek.Ac();
        }

        private Text aciklama = null;
        private Text bilgiYazisi = null;
        private InputField sifreAlani = null;
        private InputField onayAlani = null;
        private GameObject silDugmesi = null;

        protected override void Kur() {
            PencereKur("HesapSilme", 900f, 620f);
            Baslik("HESABIMI SİL");
            aciklama = Yazi(Kutu(panel.transform, "Aciklama", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -300f), new Vector2(-28f, -76f)),
                string.Empty, 20, TextAnchor.UpperLeft, YaziRengi);
            sifreAlani = Girdi(panel.transform, "Hesap şifren", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 226f), new Vector2(-28f, 282f), 64,
                InputField.ContentType.Password);
            onayAlani = Girdi(panel.transform, "Onaylamak için büyük harfle SİL yaz", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 158f), new Vector2(-28f, 214f), 8);
            silDugmesi = Dugme(panel.transform, "Hesabımı ve verilerimi kalıcı olarak sil", new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(560f, 64f), 22,
                TehlikeRengi, Sil);
            bilgiYazisi = Yazi(Kutu(panel.transform, "Bilgi", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(28f, 12f), new Vector2(-28f, 68f)),
                string.Empty, 18, TextAnchor.MiddleCenter, IpucuRengi);
        }

        public void Bilgi(string mesaj, bool iyi) {
            bilgiYazisi.text = mesaj;
            bilgiYazisi.color = iyi ? IyiRengi : HataRengi;
        }

        private void Ac() {
            kok.SetActive(true);
            sifreAlani.text = string.Empty;
            onayAlani.text = string.Empty;
            bool cevrimici = Cevrimici.Acik && Oyuncu != null;
            sifreAlani.gameObject.SetActive(cevrimici);
            onayAlani.gameObject.SetActive(cevrimici);
            silDugmesi.SetActive(cevrimici);
            bilgiYazisi.text = string.Empty;
            if (cevrimici) {
                aciklama.text = "Çevrimiçi hesabın, bütün karakterlerin, eşyaların, paran, Kut bakiyen ve posta kutun <b>kalıcı olarak silinir</b>. "
                    + "Bu işlem geri alınamaz; satın alınmış Kut iade edilmez (iade için Google Play'e başvurabilirsin).\n\n"
                    + "Güvenlik ve dolandırıcılık incelemesi için tutulan ticaret ve ödeme kayıtları yasal süre boyunca saklanır, sonra silinir. "
                    + "Ayrıntılar: " + Sartlar.HesapSilmeAdresi;
            } else {
                aciklama.text = "Tek oyunculu oyunun kayıtları yalnız bu telefonda durur: karakter seçme ekranında karakteri seçip \"Sil\" ile silebilir "
                    + "ya da telefonun Ayarlar > Uygulamalar bölümünden oyunun verisini temizleyebilirsin.\n\n"
                    + "Çevrimiçi hesabını silmek için çevrimiçi oyuna gir ve bu pencereyi yeniden aç. Oyuna giremiyorsan silme isteğini web sayfasından gönder:\n"
                    + Sartlar.HesapSilmeAdresi;
            }
        }

        private void Sil() {
            MobileFeedback.Tap();
            if (onayAlani.text.Trim() != "SİL" && onayAlani.text.Trim().ToUpperInvariant() != "SIL") {
                Bilgi("Onaylamak için SİL yaz.", false);
                return;
            }
            // Google Play ile girenin şifresi yoktur: oturum Google'la doğrulandı, sunucu Google'a bağlı hesapta şifre sormaz
            bool google = Oyun != null && Oyun.NetworkManagerClient != null && Oyun.NetworkManagerClient.Username == GoogleGiris.KullaniciAdi;
            if (string.IsNullOrEmpty(sifreAlani.text) && google == false) {
                Bilgi("Şifreni yaz.", false);
                return;
            }
            if (OtukenAg.Gonder("hesap-sil", sifreAlani.text) == false) {
                Bilgi("Sunucuya ulaşılamadı.", false);
                return;
            }
            Bilgi("Siliniyor...", true);
        }

        private System.Collections.IEnumerator Cik() {
            yield return new WaitForSecondsRealtime(2.5f);
            SystemGameManager o = Oyun;
            try {
                if (o != null && o.NetworkManagerClient != null) {
                    o.NetworkManagerClient.RequestDisconnect();
                }
            } catch (Exception e) {
                Debug.LogWarning("[HesapSilme] bağlantı kesilemedi: " + e.Message);
            }
            Kapat();
        }
    }
}
