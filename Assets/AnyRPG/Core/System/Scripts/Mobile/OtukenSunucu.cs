using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Çevrimiçi sunucuda telefondaki kolaylıkların her oyuncu için yürüyen kısmı (saniyede bir, Sunucu çağırır):
    ///  - günlük görevlerin sayılması (öldürme, taş, harita; ganimet ve sandık kendi olaylarından) ve telefona gönderilmesi
    ///  - seviye ödülleri
    ///  - çevrimdışı kazanç (girişte ara hesaplanır, oyundayken son görülme yazılır)
    ///  - binek (5. seviyede sunucuda da öğrenilir)
    /// Oyuncu birimi doğduktan 4 saniye sonra başlar (eşyalar ve değerler yerine otursun). Harita değiştirince birim
    /// yenilenir; kayıtlar karakter kaydında durduğu için kaldığı yerden sürer.
    /// </summary>
    public static class OtukenSunucu {

        private const float Bekleme = 4f;

        private static readonly Dictionary<UnitController, float> gorulenler = new Dictionary<UnitController, float>();
        private static readonly List<UnitController> gidenler = new List<UnitController>();
        private static readonly HashSet<UnitController> etkinler = new HashSet<UnitController>();
        private static readonly HashSet<UnitController> ilkTurlar = new HashSet<UnitController>();
        private static readonly Dictionary<string, int> hataSayilari = new Dictionary<string, int>();

        public static void Tick(SystemGameManager oyun) {
            if (oyun == null || oyun.PlayerManagerServer == null) {
                return;
            }
            float simdi = Time.realtimeSinceStartup;
            etkinler.Clear();
            foreach (UnitController oyuncu in oyun.PlayerManagerServer.ActiveUnitControllers.Values) {
                if (oyuncu == null || oyuncu.CharacterStats == null || oyuncu.CharacterSaveManager == null) {
                    continue;
                }
                etkinler.Add(oyuncu);
                float ilk;
                if (gorulenler.TryGetValue(oyuncu, out ilk) == false) {
                    gorulenler[oyuncu] = simdi;
                    continue;
                }
                if (simdi - ilk < Bekleme) {
                    continue;
                }
                if (ilkTurlar.Add(oyuncu)) {
                    // girişte bir kez: Kut Dükkânı durumu (kutsama, muska)
                    Calistir("kut durumu", () => KutDukkani.DurumGonder(oyuncu));
                }
                Calistir("günlük görevler", () => GunlukGorevler.SunucuTick(oyuncu));
                Calistir("seviye ödülleri", () => Gelisim.SunucuTick(oyuncu));
                Calistir("çevrimdışı kazanç", () => CevrimdisiKazanc.SunucuTick(oyuncu));
                Calistir("binek", () => Binek.SunucuTick(oyuncu));
            }
            // çıkan ya da harita değiştiren oyuncuların eski birimleri
            gidenler.Clear();
            foreach (UnitController u in gorulenler.Keys) {
                if (etkinler.Contains(u) == false) {
                    gidenler.Add(u);
                }
            }
            // takas, pazar ve hesap silme
            Calistir("takas", Takas.SunucuTick);
            Calistir("pazar", Pazar.SunucuTick);
            Calistir("hesap silme", () => HesapSilme.SunucuTick(oyun));
            Calistir("kayıt budama", Ticaret.KayitlariBuda);
            foreach (UnitController u in gidenler) {
                gorulenler.Remove(u);
                ilkTurlar.Remove(u);
                Calistir("çıkış", () => {
                    GunlukGorevler.SunucuCikti(u);
                    Gelisim.SunucuCikti(u);
                    CevrimdisiKazanc.SunucuCikti(u);
                });
            }
        }

        private static void Calistir(string ad, Action is_) {
            try {
                is_();
            } catch (Exception e) {
                // aynı hata her saniye günlüğü doldurmasın: her iş için ilk 5 hata
                int n;
                hataSayilari.TryGetValue(ad, out n);
                hataSayilari[ad] = n + 1;
                if (n < 5) {
                    Debug.LogError("[Sunucu] " + ad + ": " + e);
                }
            }
        }
    }
}
