using System;
using System.Collections;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Ayrılmış oyun sunucusu (çevrimiçi oyun, AnyMMO + FishNet). Oyun "-sunucu" ile başlatılınca (Linux sunucu
    /// derlemesi: ./OtukenSunucu.x86_64 -batchmode -nographics -sunucu -port 7770) oyun yöneticisi ve ana menü
    /// yüklenir yüklenmez FishNet sunucusu MMO kipinde açılır; telefonlar "Çevrimiçi" ile bağlanır.
    /// Hesaplar ve karakterler sunucudaki dosyalarda tutulur (SystemConfigurationManager.ServerBackend = File).
    /// Dakikada bir durum satırı yazar (sunucudaki günlükte "[Sunucu]").
    /// </summary>
    public class Sunucu : MonoBehaviour {

        public const ushort VarsayilanPort = 7770;

        public static bool Etkin {
            get {
#if UNITY_SERVER
                return true;
#else
                return Array.IndexOf(Environment.GetCommandLineArgs(), "-sunucu") >= 0;
#endif
            }
        }

        public static ushort Port {
            get {
                string[] a = Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-port");
                ushort port;
                if (i >= 0 && i + 1 < a.Length && ushort.TryParse(a[i + 1], out port)) {
                    return port;
                }
                return VarsayilanPort;
            }
        }

        /// <summary>sunucu açıldı mı (oyun testi okur)</summary>
        public static bool Basladi { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Kur() {
            if (Etkin == false || Application.isEditor) {
                return;
            }
            // görüntü yok: işlemciyi boşa yormasın
            Application.targetFrameRate = 30;
            QualitySettings.vSyncCount = 0;
            AudioListener.volume = 0f;
            GameObject go = new GameObject("Sunucu");
            DontDestroyOnLoad(go);
            go.AddComponent<Sunucu>();
            Debug.Log("[Sunucu] ayrılmış sunucu kipi, port " + Port);
        }

        /// <summary>editördeki oyun testi de aynı yolla sunucu açabilsin</summary>
        public static void Baslat(SystemGameManager oyun, ushort port) {
            oyun.NetworkManagerServer.SetServerMode(NetworkServerMode.MMO);
            oyun.NetworkManagerServer.SetServerPort(port);
            oyun.NetworkManagerServer.StartServer();
            Basladi = true;
            Debug.Log("[Sunucu] başladı: MMO kipi, port " + port + ", istemci sürümü " + oyun.SystemConfigurationManager.ClientVersion);
        }

        private IEnumerator Start() {
            float baslangic = Time.realtimeSinceStartup;
            SystemGameManager oyun = null;
            while (true) {
                oyun = FindAnyObjectByType<SystemGameManager>();
                if (oyun != null && oyun.NetworkManagerServer != null && oyun.LevelManagerClient != null && oyun.LevelManagerClient.IsMainMenu()) {
                    break;
                }
                if (Time.realtimeSinceStartup - baslangic > 180f) {
                    Debug.LogError("[Sunucu] oyun yöneticisi 3 dakikada hazır olmadı; sunucu açılamadı");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
            }
            // ana menü kendini kursun
            yield return new WaitForSecondsRealtime(2f);
            try {
                Baslat(oyun, Port);
            } catch (Exception e) {
                Debug.LogError("[Sunucu] başlatılamadı: " + e);
                yield break;
            }
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
