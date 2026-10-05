using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Çevrimiçi oyunda telefondaki kolaylıkların (günlük armağan, günlük görevler, seviye ödülleri, Demirci, toplu satış,
    /// çanta sıralama, çevrimdışı kazanç) sunucuyla konuştuğu tek kanal. Çevrimiçi oyunda karakter sunucudadır: ödül,
    /// satış, güçlendirme gibi her değişiklik sunucuda yapılır, sonucu AnyRPG'nin kendi ağ olaylarıyla (para, eşya, tecrübe,
    /// mesaj) telefona gelir.
    ///   istemci: OtukenAg.Gonder("armagan-al", "") → sunucu: SunucuIsle("armagan-al", (oyuncu, veri) => ...)
    ///   sunucu : OtukenAg.Yanitla(oyuncu, "armagan", "...") → istemci: IstemciDinle("armagan", veri => ...)
    /// Taşıyıcı FishNet eklentisindedir (FishNetClientConnector: OtukenIstegiServer / OtukenYanitiClient);
    /// AnyRPG çekirdeği eklentiyi tanımadığı için iki temsilciyle bağlanır.
    /// </summary>
    public static class OtukenAg {

        /// <summary>istemci → sunucu (FishNet eklentisi kurar)</summary>
        public static Action<string, string> IstemciGonderici = null;

        /// <summary>sunucu → bir hesabın istemcisi (FishNet eklentisi kurar)</summary>
        public static Action<int, string, string> SunucuGonderici = null;

        private static readonly Dictionary<string, Action<string>> istemciDinleyicileri = new Dictionary<string, Action<string>>();
        private static readonly Dictionary<string, Action<UnitController, string>> sunucuIsleyicileri = new Dictionary<string, Action<UnitController, string>>();

        private static SystemGameManager oyun = null;

        public static SystemGameManager Oyun {
            get {
                if (oyun == null) {
                    oyun = UnityEngine.Object.FindAnyObjectByType<SystemGameManager>();
                }
                return oyun;
            }
        }

        /// <summary>bu süreç çevrimiçi oyunun sunucusu mu</summary>
        public static bool Sunucuda {
            get {
                SystemGameManager o = Oyun;
                return o != null && o.NetworkManagerServer != null && o.NetworkManagerServer.ServerModeActive;
            }
        }

        public static void IstemciDinle(string islem, Action<string> dinleyici) {
            istemciDinleyicileri[islem] = dinleyici;
        }

        public static void SunucuIsle(string islem, Action<UnitController, string> isleyici) {
            sunucuIsleyicileri[islem] = isleyici;
        }

        /// <summary>istemci: sunucuya istek (çevrimiçi değilse ya da kanal kurulmadıysa false)</summary>
        public static bool Gonder(string islem, string veri = "") {
            if (IstemciGonderici == null || Cevrimici.Acik == false) {
                return false;
            }
            try {
                IstemciGonderici(islem, veri ?? string.Empty);
                return true;
            } catch (Exception e) {
                Debug.LogWarning("[OtukenAg] gönderilemedi (" + islem + "): " + e.Message);
                return false;
            }
        }

        /// <summary>sunucu: oyuncunun telefonuna</summary>
        public static void Yanitla(UnitController oyuncu, string islem, string veri) {
            if (SunucuGonderici == null || oyuncu == null) {
                return;
            }
            SystemGameManager o = Oyun;
            int hesap = o != null && o.PlayerManagerServer != null ? o.PlayerManagerServer.GetAccountIdFromUnitController(oyuncu) : -1;
            if (hesap < 0) {
                return;
            }
            try {
                SunucuGonderici(hesap, islem, veri ?? string.Empty);
            } catch (Exception e) {
                Debug.LogWarning("[OtukenAg] yanıt gönderilemedi (" + islem + "): " + e.Message);
            }
        }

        /// <summary>FishNet eklentisi: bir istemciden istek geldi</summary>
        public static void SunucuyaGeldi(int hesap, string islem, string veri) {
            SystemGameManager o = Oyun;
            UnitController oyuncu = null;
            if (o != null && o.PlayerManagerServer != null) {
                o.PlayerManagerServer.ActiveUnitControllers.TryGetValue(hesap, out oyuncu);
            }
            if (oyuncu == null) {
                Debug.LogWarning("[OtukenAg] " + hesap + " numaralı hesabın oyunda karakteri yok (" + islem + ")");
                return;
            }
            Action<UnitController, string> isleyici;
            if (sunucuIsleyicileri.TryGetValue(islem, out isleyici) == false) {
                Debug.LogWarning("[OtukenAg] bilinmeyen istek: " + islem);
                return;
            }
            try {
                isleyici(oyuncu, veri ?? string.Empty);
            } catch (Exception e) {
                Debug.LogError("[OtukenAg] " + islem + " işlenemedi: " + e);
            }
        }

        /// <summary>FishNet eklentisi: sunucudan yanıt geldi</summary>
        public static void IstemciyeGeldi(string islem, string veri) {
            Action<string> dinleyici;
            if (istemciDinleyicileri.TryGetValue(islem, out dinleyici) == false) {
                Debug.LogWarning("[OtukenAg] bilinmeyen yanıt: " + islem);
                return;
            }
            try {
                dinleyici(veri ?? string.Empty);
            } catch (Exception e) {
                Debug.LogError("[OtukenAg] " + islem + " yanıtı işlenemedi: " + e);
            }
        }

        /// <summary>
        /// oyuncunun mesaj akışına yazar. Sunucuda ağ üzerinden telefona gider; çevrimiçi istemcide de görünür
        /// (AnyRPG'nin WriteMessageFeedMessage'ı çevrimiçi istemcide mesajları yutar: "mesajlar sunucudan gelir").
        /// </summary>
        public static void Mesaj(UnitController oyuncu, string metin) {
            if (oyuncu == null || oyuncu.UnitEventController == null || string.IsNullOrEmpty(metin)) {
                return;
            }
            oyuncu.UnitEventController.NotifyOnWriteMessageFeedMessage(metin);
        }
    }

    /// <summary>
    /// Karakter başına kalıcı küçük kayıtlar (günlük armağan serisi, günlük görev ilerlemesi, alınan seviye ödülleri,
    /// son görülme...). Tek oyunculu oyunda telefonda (PlayerPrefs, anahtar + karakter adı: eski kayıtlarla aynı);
    /// çevrimiçi sunucuda karakter kaydının içinde (CharacterSaveData.OtukenVerisi, "anahtar=değer" satırları), böylece
    /// karakterle birlikte sunucuda saklanır.
    /// </summary>
    public static class OtukenVeri {

        private static readonly Dictionary<CharacterSaveData, Dictionary<string, string>> onbellek = new Dictionary<CharacterSaveData, Dictionary<string, string>>();

        private static CharacterSaveData Kayit(UnitController oyuncu) {
            return oyuncu != null && oyuncu.CharacterSaveManager != null ? oyuncu.CharacterSaveManager.SaveData : null;
        }

        private static Dictionary<string, string> Sozluk(CharacterSaveData kayit) {
            Dictionary<string, string> d;
            if (onbellek.TryGetValue(kayit, out d)) {
                return d;
            }
            if (onbellek.Count > 300) {
                onbellek.Clear();
            }
            d = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(kayit.OtukenVerisi) == false) {
                foreach (string satir in kayit.OtukenVerisi.Split('\n')) {
                    int esit = satir.IndexOf('=');
                    if (esit > 0) {
                        d[satir.Substring(0, esit)] = satir.Substring(esit + 1);
                    }
                }
            }
            onbellek[kayit] = d;
            return d;
        }

        public static string Oku(UnitController oyuncu, string anahtar, string varsayilan = "") {
            if (oyuncu == null) {
                return varsayilan;
            }
            if (OtukenAg.Sunucuda) {
                CharacterSaveData kayit = Kayit(oyuncu);
                string deger;
                if (kayit != null && Sozluk(kayit).TryGetValue(anahtar, out deger)) {
                    return deger;
                }
                return varsayilan;
            }
            return PlayerPrefs.GetString(anahtar + oyuncu.DisplayName, varsayilan);
        }

        public static void Yaz(UnitController oyuncu, string anahtar, string deger) {
            if (oyuncu == null) {
                return;
            }
            deger = (deger ?? string.Empty).Replace('\n', ' ');
            if (OtukenAg.Sunucuda) {
                CharacterSaveData kayit = Kayit(oyuncu);
                if (kayit == null) {
                    return;
                }
                Dictionary<string, string> d = Sozluk(kayit);
                string eski;
                if (d.TryGetValue(anahtar, out eski) && eski == deger) {
                    return;
                }
                d[anahtar] = deger;
                List<string> satirlar = new List<string>();
                foreach (KeyValuePair<string, string> k in d) {
                    satirlar.Add(k.Key + "=" + k.Value);
                }
                kayit.OtukenVerisi = string.Join("\n", satirlar);
                // karakter kaydı kirlendi: sunucu birazdan dosyaya yazar (PlayerCharacterMonitor)
                oyuncu.UnitEventController.NotifyOnSaveDataUpdated();
                return;
            }
            PlayerPrefs.SetString(anahtar + oyuncu.DisplayName, deger);
        }

        /// <summary>sayı (tek oyunculu oyunda eski kayıtlar PlayerPrefs'te sayı olarak durur)</summary>
        public static int OkuSayi(UnitController oyuncu, string anahtar, int varsayilan) {
            if (oyuncu == null) {
                return varsayilan;
            }
            if (OtukenAg.Sunucuda) {
                int sayi;
                return int.TryParse(Oku(oyuncu, anahtar, string.Empty), System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out sayi) ? sayi : varsayilan;
            }
            return PlayerPrefs.GetInt(anahtar + oyuncu.DisplayName, varsayilan);
        }

        public static void YazSayi(UnitController oyuncu, string anahtar, int deger) {
            if (oyuncu == null) {
                return;
            }
            if (OtukenAg.Sunucuda) {
                Yaz(oyuncu, anahtar, deger.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            }
            PlayerPrefs.SetInt(anahtar + oyuncu.DisplayName, deger);
        }

        public static void Kaydet() {
            if (OtukenAg.Sunucuda == false) {
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// bugünün tarihi (yyyyMMdd): tek oyunculu oyunda telefonun saati, sunucuda Türkiye saati (UTC+3),
        /// böylece günlük işler bütün oyuncular için gece yarısı yenilenir
        /// </summary>
        public static DateTime Simdi {
            get { return OtukenAg.Sunucuda ? DateTime.UtcNow.AddHours(3) : DateTime.Now; }
        }

        public static string GunAnahtari(DateTime tarih) {
            return tarih.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string Bugun {
            get { return GunAnahtari(Simdi); }
        }
    }
}
