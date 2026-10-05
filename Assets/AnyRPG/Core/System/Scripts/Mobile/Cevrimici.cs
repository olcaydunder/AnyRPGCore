using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Çevrimiçi oyunda (GameMode.Network) karakter verisi sunucudadır. Telefonda kurulan kolaylıkların bir kısmı
    /// (armağan, ödül, demirci, toplu satış, sıralama, ışınlanma) karakteri doğrudan telefonda değiştirir; çevrimiçi
    /// oyunda bu değişiklik sunucuya gitmez, karakter bozulur. Bunlar sunucu üzerinden yazılana kadar çevrimiçi oyunda
    /// kapalıdır: Engelle() kısa bir mesaj yazar ve true döner.
    /// </summary>
    public static class Cevrimici {

        private static SystemGameManager oyun = null;

        public static bool Acik {
            get {
                if (oyun == null) {
                    oyun = Object.FindAnyObjectByType<SystemGameManager>();
                }
                return oyun != null && oyun.GameMode == GameMode.Network;
            }
        }

        /// <summary>çevrimiçi oyundaysa özelliği durdurur (mesaj yazar, true döner)</summary>
        public static bool Engelle(string ozellik) {
            if (Acik == false) {
                return false;
            }
            UnitController oyuncu = oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            string mesaj = ozellik + " çevrimiçi oyunda henüz yok (yakında).";
            if (oyuncu != null) {
                OtukenAg.Mesaj(oyuncu, mesaj);
            } else {
                Debug.Log("[Cevrimici] " + mesaj);
            }
            MobileFeedback.Medium();
            return true;
        }
    }
}
