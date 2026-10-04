using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Metin2 tarzı otomatik av ve otomatik iksir ayarları.
    /// Av, ekrandaki "Oto Av" düğmesiyle açılıp kapanır (her oturum kapalı başlar); açıkken karakter yakındaki
    /// düşmanları sırayla bulur, yanına koşar, saldırır ve ölenlerin ganimetini toplar. Hareket çubuğuna dokununca
    /// kısa bir süre durur, oyuncu yönetir. İşi PlayerController.HandleAutoHunt yapar.
    /// Otomatik iksir: can belirlenen oranın altına inince çantadaki can iksiri içilir (PlayerController.HandleAutoPotion).
    /// Oto av becerileri (Metin2'deki oto av gibi): dövüşte öğrenilmiş saldırı becerileri bekleme süreleri dolunca kullanılır
    /// (PlayerController.TryAutoSkill); Seçenekler > Oyun'dan kapatılabilir.
    /// </summary>
    public static class OtomatikAv {

        private const string IksirKey = "otomatik-iksir";
        private const string BeceriKey = "oto-av-beceri";

        /// <summary>bu oturumda oto avın kullandığı beceri sayısı (oyun testi okur)</summary>
        public static int BeceriSayisi = 0;

        public static bool BeceriKullan {
            get { return PlayerPrefs.GetInt(BeceriKey, 1) == 1; }
            set { PlayerPrefs.SetInt(BeceriKey, value ? 1 : 0); }
        }

        private static bool acik = false;

        public static event System.Action Degisti = delegate { };

        public static bool Acik { get { return acik; } }

        public static void Degistir() {
            acik = !acik;
            Degisti();
        }

        public static void Kapat() {
            if (acik) {
                acik = false;
                Degisti();
            }
        }

        /// <summary>0: kapalı, 1: can %30'un altında, 2: can %50'nin altında</summary>
        public static int IksirSecimi {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(IksirKey, 1), 0, 2); }
            set { PlayerPrefs.SetInt(IksirKey, Mathf.Clamp(value, 0, 2)); }
        }

        public static float IksirEsigi {
            get {
                switch (IksirSecimi) {
                    case 1: return 0.3f;
                    case 2: return 0.5f;
                    default: return 0f;
                }
            }
        }
    }
}
