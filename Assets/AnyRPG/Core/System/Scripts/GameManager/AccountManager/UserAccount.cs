using System;

namespace AnyRPG {

    [Serializable]
    public class UserAccount {
        public int Id;
        public string UserName;
        public string PasswordHash;
        public string Salt;
        public string Email;
        public string Phone;
        /// <summary>Ötüken: bağlı Google Play Oyun Hizmetleri oyuncu kimliği (yoksa boş; GoogleGiris)</summary>
        public string GoogleId;

        public UserAccount() {
            UserName = string.Empty;
            PasswordHash = string.Empty;
            Salt = string.Empty;
            Email = string.Empty;
            Phone = string.Empty;
            GoogleId = string.Empty;
        }
    }
}

