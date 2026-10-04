using System;
using System.Collections;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Ayrılmış oyun sunucusu (çevrimiçi oyun, AnyMMO + FishNet). AnyRPG, oyun batch mode'da açılıp ana menü
    /// yüklenince sunucuyu kendisi başlatır (LevelManagerClient; port "--serverport", kip "--servermode", varsayılan
    /// 7770 ve MMO). Linux sunucu derlemesi: ./OtukenSunucu.x86_64 -batchmode -nographics --serverport 7770 --servermode MMO
    /// Bu sınıf yalnız kimin sunucu olacağına karar verir (editördeki oyun testi ve istemci botları olmaz) ve sunucu
    /// açılınca "[Sunucu] başladı" ile dakikada bir durum satırı yazar. Hesaplar ve karakterler sunucudaki dosyalarda
    /// tutulur (SystemConfigurationManager.ServerBackend = File).
    /// </summary>
    public class Sunucu : MonoBehaviour {

        public const ushort VarsayilanPort = 7770;

        /// <summary>batch mode'daki bu süreç sunucu mu (editör ve istemci botu değil)</summary>
        public static bool BatchSunucuOlsun {
            get { return Application.isEditor == false && AgBotu.Etkin == false; }
        }

        /// <summary>sunucu açıldı mı</summary>
        public static bool Basladi { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Kur() {
            if (Application.isBatchMode == false || BatchSunucuOlsun == false) {
                return;
            }
            // görüntü yok: işlemciyi boşa yormasın
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            AudioListener.volume = 0f;
            GameObject go = new GameObject("Sunucu");
            DontDestroyOnLoad(go);
            go.AddComponent<Sunucu>();
            Debug.Log("[Sunucu] ayrılmış sunucu kipi");
        }

        private IEnumerator Start() {
            float baslangic = Time.realtimeSinceStartup;
            SystemGameManager oyun = null;
            while (true) {
                oyun = FindAnyObjectByType<SystemGameManager>();
                if (oyun != null && oyun.NetworkManagerServer != null && oyun.NetworkManagerServer.ServerModeActive) {
                    break;
                }
                if (Time.realtimeSinceStartup - baslangic > 180f) {
                    Debug.LogError("[Sunucu] 3 dakikada açılmadı");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            Basladi = true;
            Debug.Log("[Sunucu] başladı: " + oyun.NetworkManagerServer.ServerMode + " kipi, port " + oyun.NetworkManagerServer.GetServerPort()
                + ", istemci sürümü " + oyun.SystemConfigurationManager.ClientVersion + ", kayıtlar " + Application.persistentDataPath);
            while (true) {
                yield return new WaitForSecondsRealtime(60f);
                try {
                    int oyuncu = oyun.AuthenticationService != null ? oyun.AuthenticationService.LoggedInAccounts.Count : -1;
                    Debug.Log("[Sunucu] açık: " + (oyun.NetworkManagerServer.ServerModeActive ? "evet" : "HAYIR") + ", bağlı oyuncu " + oyuncu
                        + ", çalışma " + (int)(Time.realtimeSinceStartup / 60f) + " dk");
                } catch (Exception e) {
                    Debug.LogWarning("[Sunucu] durum yazılamadı: " + e.Message);
                }
            }
        }
    }
}
