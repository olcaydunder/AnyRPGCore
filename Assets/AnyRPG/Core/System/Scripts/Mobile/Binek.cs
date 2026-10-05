using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Binek: Türk efsanelerindeki ejderha Evren. Oyuncu 5. seviyeye gelince kendiliğinden öğrenilir
    /// (oyunun kendi "Dragon Mount" yeteneği, Evren birimi). Sol sütundaki "Binek" düğmesi öğrenilince çıkar:
    /// dokununca binilir, binekteyken dokununca inilir. Zindanlarda (sahne ayarı allowMount kapalı) binilemez.
    /// Başka bir yetenek kullanınca oyun bineği kendisi indirir.
    /// Çevrimiçi oyunda yetenek hem sunucuda (SunucuTick: yeteneği sunucu tanısın, kayda girsin) hem telefonda öğrenilir;
    /// binme isteği AnyRPG'nin kendi yetenek isteğiyle sunucuya gider, inme isteği durum etkisini kaldırma isteğiyle.
    /// </summary>
    public static class Binek {

        public const string YetenekAdi = "Dragon Mount";
        public const int GerekenSeviye = 5;

        private static AbilityProperties yetenek = null;

        /// <summary>oyuncu bineği öğrendi mi (HUD düğmesi görünür)</summary>
        public static bool Var { get; private set; }

        /// <summary>şu an binekte mi</summary>
        public static bool Binili { get; private set; }

        private static SystemGameManager oyun = null;

        /// <summary>MobileBootstrap saniyede bir çağırır</summary>
        public static void Tick(SystemGameManager systemGameManager, bool oyunda) {
            oyun = systemGameManager;
            UnitController oyuncu = oyunda && systemGameManager != null && systemGameManager.PlayerManagerClient != null
                ? systemGameManager.PlayerManagerClient.UnitController : null;
            if (oyuncu == null || oyuncu.CharacterAbilityManager == null || oyuncu.CharacterStats == null) {
                Var = false;
                Binili = false;
                return;
            }
            AbilityProperties binek = Yetenek();
            if (binek == null) {
                Var = false;
                return;
            }
            Binili = oyuncu.IsMounted;
            if (oyuncu.CharacterAbilityManager.HasAbility(binek)) {
                Var = true;
                return;
            }
            Var = false;
            if (oyuncu.CharacterStats.Level >= GerekenSeviye && oyuncu.CharacterStats.IsAlive) {
                if (oyuncu.CharacterAbilityManager.LearnAbility(binek)) {
                    Var = true;
                    OtukenAg.Mesaj(oyuncu, "<color=#FFD54A>Evren seni seçti! Artık ejderhana binebilirsin: soldaki Binek düğmesine dokun.</color>");
                    MobileFeedback.Success();
                }
            }
        }

        /// <summary>çevrimiçi sunucu: 5. seviyeye gelen oyuncu bineği sunucuda da öğrenir (OtukenSunucu)</summary>
        public static void SunucuTick(UnitController oyuncu) {
            if (oyuncu.CharacterAbilityManager == null || oyuncu.CharacterStats == null || oyuncu.CharacterStats.Level < GerekenSeviye
                || oyuncu.CharacterStats.IsAlive == false) {
                return;
            }
            AbilityProperties binek = Yetenek();
            if (binek != null && oyuncu.CharacterAbilityManager.HasAbility(binek) == false && oyuncu.CharacterAbilityManager.LearnAbility(binek)) {
                Debug.Log("[Sunucu] " + oyuncu.DisplayName + " bineği öğrendi");
            }
        }

        private static AbilityProperties Yetenek() {
            if (oyun == null) {
                oyun = OtukenAg.Oyun;
            }
            if (yetenek == null && oyun != null && oyun.SystemDataFactory != null) {
                Ability kaynak = oyun.SystemDataFactory.GetResource<Ability>(YetenekAdi);
                if (kaynak != null) {
                    yetenek = kaynak.AbilityProperties;
                }
            }
            return yetenek;
        }

        /// <summary>HUD'daki Binek düğmesi: bin ya da in</summary>
        public static void Degistir() {
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            AbilityProperties binek = Yetenek();
            if (oyuncu == null || binek == null) {
                return;
            }
            if (oyuncu.IsMounted) {
                if (Cevrimici.Acik) {
                    // çevrimiçi: binek etkisini sunucu kaldırır
                    foreach (StatusEffectNode etki in oyuncu.CharacterStats.StatusEffects.Values) {
                        if (etki.StatusEffect is MountEffectProperties) {
                            oyuncu.CharacterStats.RequestCancelStatusEffect(etki);
                            break;
                        }
                    }
                } else {
                    oyuncu.CancelMountEffects();
                }
                Binili = false;
                return;
            }
            if (oyuncu.CharacterAbilityManager.HasAbility(binek) == false) {
                OtukenAg.Mesaj(oyuncu, "Binek " + GerekenSeviye + ". seviyede gelir.");
                return;
            }
            SceneNode sahne = oyun.LevelManagerClient != null ? oyun.LevelManagerClient.GetActiveSceneNode() : null;
            if (sahne != null && sahne.AllowMount == false) {
                OtukenAg.Mesaj(oyuncu, "Burada bineğe binilemez.");
                return;
            }
            if (oyuncu.CharacterCombat != null && oyuncu.CharacterCombat.GetInCombat()) {
                OtukenAg.Mesaj(oyuncu, "Savaşta bineğe binilemez.");
                return;
            }
            OtomatikAv.Kapat();
            oyuncu.CharacterAbilityManager.BeginAbility(binek, true);
        }
    }
}
