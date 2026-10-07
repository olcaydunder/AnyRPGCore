using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {
    public class UserAccountService : ConfiguredClass {

        /// <summary>
        /// username, userAccount
        /// </summary>
        private Dictionary<string, UserAccount> userAccounts = new Dictionary<string, UserAccount>();

        // game manager references
        private ServerDataService serverDataService = null;

        public override void Configure(SystemGameManager systemGameManager) {
            base.Configure(systemGameManager);
            //networkManagerServer.OnStartServer += ProcessStartServer;
            networkManagerServer.OnStopServer += HandleStopServer;
        }

        public override void SetGameManagerReferences() {
            base.SetGameManagerReferences();
            serverDataService = systemGameManager.ServerDataService;
        }

        public void ProcessStartServer() {
            //Debug.Log("UserAccountService.HandleStartServer()");

            LoadAllUserAccounts();
        }

        private void HandleStopServer() {
            //Debug.Log("UserAccountService.HandleStopServer()");

            ClearAllUserAccounts();
        }

        private void ClearAllUserAccounts() {
            //Debug.Log("UserAccountService.ClearAllUserAccounts()");

            userAccounts.Clear();
        }

        private void LoadAllUserAccounts() {
            //Debug.Log("UserAccountService.LoadAllUserAccounts()");
            serverDataService.LoadAllUserAccounts();
        }

        public void ProcessLoadAllUserAccounts(List<UserAccount> accountList) {
            foreach (UserAccount userAccount in accountList) {
                //UserAccount userAccount = JsonUtility.FromJson<UserAccount>(jsonString);
                if (userAccount.Id == -1) {
                    Debug.LogWarning($"UserAccountService.LoadAllUserAccounts(): User account with name {userAccount.UserName} has invalid id of -1.  This account will be skipped.");
                    continue;
                }
                //Debug.Log($"Loaded user account: {userAccount.UserName}");
                if (userAccounts.ContainsKey(userAccount.UserName)) {
                    Debug.LogWarning($"UserAccountService.LoadAllUserAccounts(): Duplicate user account name {userAccount.UserName} found.  This account will be skipped.");
                    continue;
                }
                //Debug.Log($"UserAccountService.LoadAllUserAccounts(): Adding user account {userAccount.UserName} with id {userAccount.Id} to in memory lookup.");
                userAccounts.Add(userAccount.UserName, userAccount);
            }
        }

        /// <summary>
        /// check if username exists
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        /// <summary>Ötüken: hesabı kimliğinden bulur (hesap silme)</summary>
        public UserAccount HesapBul(int id) {
            foreach (UserAccount hesap in userAccounts.Values) {
                if (hesap.Id == id) {
                    return hesap;
                }
            }
            return null;
        }

        /// <summary>Ötüken: silinen hesabı bellekten çıkarır (ad yeniden alınabilir)</summary>
        public void HesapKaldir(int id) {
            UserAccount hesap = HesapBul(id);
            if (hesap != null) {
                userAccounts.Remove(hesap.UserName);
            }
        }

        /// <summary>Ötüken: Google Play Oyun Hizmetleri oyuncu kimliğine bağlı hesap (yoksa null)</summary>
        public UserAccount GoogleHesabi(string googleId) {
            if (string.IsNullOrEmpty(googleId)) {
                return null;
            }
            foreach (UserAccount hesap in userAccounts.Values) {
                if (hesap.GoogleId == googleId) {
                    return hesap;
                }
            }
            return null;
        }

        public bool AccountExists(string userName) {
            //Debug.Log($"UserAccountService.AccountExists({userName})");
            
            return userAccounts.ContainsKey(userName);
        }

        /// <summary>
        /// get user account by username
        /// </summary>
        /// <param name="userName"></param>
        /// <returns></returns>
        public UserAccount GetUserAccount(string userName) {
            //Debug.Log($"UserAccountService.GetUserAccount({userName})");

            if (userAccounts.ContainsKey(userName)) {
                //Debug.Log($"UserAccountService.GetUserAccount({userName}) return id: {userAccounts[userName].Id}");
                return userAccounts[userName];
            }

            return null;
        }

        public UserAccount CreateNewAccount(string username, string password) {
            //Debug.Log($"UserAccountService.CreateNewAccount({username}, {password})");

            // check if username is not taken
            if (AccountExists(username)) {
                return null;
            }

            // create new account
            // Ötüken: AnyRPG şifreyi hiç koymuyordu (boş şifrenin özeti saklanıyordu); hesap ilk girişte açılıyor, ikinci
            // girişte "şifre yanlış" ile reddediliyordu. ProvideSaltAndHash buradaki düz şifreyi tuzlayıp özetine çevirir.
            UserAccount userAccount = new UserAccount() {
                Id = serverDataService.GetNewAccountId(),
                UserName = username,
                PasswordHash = password ?? string.Empty,
            };

            // populate salt and hash values
            AuthenticationHelpers.ProvideSaltAndHash(userAccount);

            // add user account to storage
            serverDataService.SaveAccount(userAccount);

            // add to in memory lookup
            userAccounts.Add(username, userAccount);

            return userAccount;
        }

        

    }

}
