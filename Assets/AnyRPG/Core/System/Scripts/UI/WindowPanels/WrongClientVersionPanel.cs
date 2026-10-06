using TMPro;
using UnityEngine;

namespace AnyRPG {
    public class WrongClientVersionPanel : WindowPanel {

        [Header("Wrong Client Version")]

        /*
        [SerializeField]
        private HighlightButton okButton = null;
        */

        [SerializeField]
        private TMP_Text versionMessage = null;

        // game manager references
        private UIManager uIManager = null;
        private NetworkManagerClient networkManagerClient = null;

        public override void Configure(SystemGameManager systemGameManager) {
            base.Configure(systemGameManager);

            //noButton.Configure(systemGameManager);
            //yesButton.Configure(systemGameManager);
        }

        public override void SetGameManagerReferences() {
            base.SetGameManagerReferences();
            uIManager = systemGameManager.UIManager;
            networkManagerClient = systemGameManager.NetworkManagerClient;
        }

        public override void ProcessOpenWindowNotification() {
            base.ProcessOpenWindowNotification();
            networkManagerClient.OnClientVersionFailure += HandleClientVersionFailure;
        }

        public override void ReceiveClosedWindowNotification() {
            base.ReceiveClosedWindowNotification();
            networkManagerClient.OnClientVersionFailure -= HandleClientVersionFailure;
        }

        // Ötüken: Google Play sürümünde güncelleme yalnız Play'den (politika gereği başka yerden APK gösterilmez)
        private bool PlayAdresi {
            get {
                string adres = systemConfigurationManager.ClientDownloadUrl;
                return string.IsNullOrEmpty(adres) == false && adres.Contains("play.google.com");
            }
        }

        public void HandleClientVersionFailure(string requiredClientVersion) {
            if (PlayAdresi) {
                versionMessage.text = "Oyunun yeni sürümü çıktı.\nGoogle Play'den güncelleyip yeniden gir.";
            } else {
                versionMessage.text = $"Oyunun yeni sürümü çıktı ({requiredClientVersion}).\nİndir: {systemConfigurationManager.ClientDownloadUrl}";
            }
        }

        public void ConfirmAction() {
            //Debug.Log("DisconnectedPanelController.ConfirmAction()");
            uIManager.wrongClientVersionWindow.CloseWindow();
            if (PlayAdresi && Application.platform == RuntimePlatform.Android) {
                Application.OpenURL("market://details?id=" + Application.identifier);
            }
        }

    }

}