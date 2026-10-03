using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif

namespace AnyRPG {

    /// <summary>
    /// Hatırlatma bildirimleri (Unity Mobile Notifications):
    ///  - Oyundan çıkınca ertesi akşam 19.00'a "Günlük armağanın hazır" bildirimi kurulur.
    ///  - 3 gün girilmezse "Ötüken seni bekliyor" hatırlatması.
    /// Oyuna dönünce kurulanlar silinir (oyundayken bildirim gelmez). Android 13+ izni, oyuncu 90 saniye
    /// oynadıktan sonra bir kez sorulur. Seçenekler > Oyun > Bildirimler ile kapatılabilir.
    /// MobileBootstrap saniyede bir Tick, arka plana geçişte ArkaPlan çağırır.
    /// </summary>
    public static class Bildirimler {

        private const string AcikKey = "bildirimler";
        private const string IzinSorulduKey = "bildirim-izni-soruldu";
        private const string KanalId = "otuken-hatirlatma";
        private const int AksamSaati = 19;

        private static bool kanalHazir = false;
        private static float oyundaBeri = -1f;

        public static bool Acik {
            get { return PlayerPrefs.GetInt(AcikKey, 1) == 1; }
            set {
                PlayerPrefs.SetInt(AcikKey, value ? 1 : 0);
                if (value == false) {
                    Temizle();
                } else {
                    PlayerPrefs.DeleteKey(IzinSorulduKey);
                }
            }
        }

        /// <summary>saniyede bir: oyuncu bir süre oynadıktan sonra bildirim iznini bir kez sor</summary>
        public static void Tick(bool oyunda) {
            if (oyunda == false) {
                oyundaBeri = -1f;
                return;
            }
            if (oyundaBeri < 0f) {
                oyundaBeri = Time.unscaledTime;
            }
            if (Acik == false || Time.unscaledTime - oyundaBeri < 90f || PlayerPrefs.GetInt(IzinSorulduKey, 0) == 1) {
                return;
            }
            PlayerPrefs.SetInt(IzinSorulduKey, 1);
            PlayerPrefs.Save();
#if UNITY_ANDROID && !UNITY_EDITOR
            try {
                KanalKur();
                // Android 13 ve üstünde sistem penceresi açılır; eskilerde izin zaten vardır
                new PermissionRequest();
            } catch (Exception e) {
                Debug.LogWarning("Bildirimler: izin istenemedi: " + e.Message);
            }
#endif
        }

        /// <summary>oyuna dönüldü: kurulmuş hatırlatmaları sil</summary>
        public static void OnPlanaGecti() {
            Temizle();
        }

        /// <summary>oyun arka plana geçti ya da kapanıyor: hatırlatmaları kur</summary>
        public static void ArkaPlan() {
            if (Acik == false) {
                return;
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            try {
                KanalKur();
                AndroidNotificationCenter.CancelAllScheduledNotifications();
                DateTime simdi = DateTime.Now;
                DateTime yarinAksam = simdi.Date.AddDays(1).AddHours(AksamSaati);
                Gonder("Günlük armağanın hazır!", "Ötüken Destanı'na gir, bugünün armağanını al. 7 gün üst üste girene en büyük armağan.", yarinAksam);
                Gonder("Ötüken seni bekliyor, kahraman", "Erlik'in yandaşları yeniden toplanıyor. Geçit Taşı'na dokun, yolculuğuna devam et.",
                    simdi.Date.AddDays(3).AddHours(AksamSaati));
            } catch (Exception e) {
                Debug.LogWarning("Bildirimler: kurulamadı: " + e.Message);
            }
#endif
        }

        private static void Temizle() {
#if UNITY_ANDROID && !UNITY_EDITOR
            try {
                AndroidNotificationCenter.CancelAllScheduledNotifications();
                AndroidNotificationCenter.CancelAllDisplayedNotifications();
            } catch (Exception e) {
                Debug.LogWarning("Bildirimler: silinemedi: " + e.Message);
            }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void KanalKur() {
            if (kanalHazir) {
                return;
            }
            AndroidNotificationChannel kanal = new AndroidNotificationChannel() {
                Id = KanalId,
                Name = "Hatırlatmalar",
                Importance = Importance.Default,
                Description = "Günlük armağan ve oyuna dönüş hatırlatmaları",
            };
            AndroidNotificationCenter.RegisterNotificationChannel(kanal);
            kanalHazir = true;
        }

        private static void Gonder(string baslik, string metin, DateTime zaman) {
            AndroidNotification bildirim = new AndroidNotification() {
                Title = baslik,
                Text = metin,
                FireTime = zaman,
            };
            AndroidNotificationCenter.SendNotification(bildirim, KanalId);
        }
#endif
    }
}
