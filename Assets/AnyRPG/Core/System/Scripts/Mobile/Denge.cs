namespace AnyRPG {

    /// <summary>
    /// Canavar gücü dengesi: haritalar seviye basamaklı olduğundan (HaritaSeviyeleri) o haritanın seviyesindeki
    /// bir oyuncu, başlangıç donanımıyla bile o haritanın canavarlarını yenebilmeli. Ayar, otomatik oyun testindeki
    /// "savaş denemesi"yle ölçülür (Assets/Otuken/Editor/OyunTesti.cs: her haritada o haritanın en düşük
    /// seviyesinde, hilesiz, otomatik avla en yakın canavarla dövüş; sonra en yakın Ötüken Taşı kırılır).
    ///  - CanavarHasari: canavarların oyuncuya vurduğu hasarın çarpanı
    ///  - CanavarCani  : canavarların canının çarpanı (Ötüken Taşı kendi dayanıklılığıyla ayrıca ayarlı)
    /// Acemi koruması (5. seviyeye kadar %40 az hasar, Gelisim) bunlara ek olarak geçerlidir.
    /// </summary>
    public static class Denge {

        // ölçüm (0.1.48, çarpanlar 0.8 / 0.9 iken): aynı seviyedeki canavarla dövüş başlangıç donanımıyla ~60 sn sürüyor,
        // oyuncu canının %50-80'ini kaybediyordu; 2-3'lü kampta ölüyordu. Hedef: tek canavar ~10-15 sn, canın ~%10'u.
        // 0.1.49 (0.5 / 0.2): tek canavar 10-20 sn, çoğunda can %80-100 kaldı; kalabalık kampta ve zindan girişinde
        // ölümler sürdü -> hasar 0.4
        // 0.1.53: oyuncunun canı da yanlışlıkla x0.2 idi (düzeltildi) -> oyuncu artık 5 kat dayanıklı; hasar 0.5
        // 0.1.60: Börü Tepesi'nde (5. sv, Ağulu Körmös) ve Tamu'da (can %4) zorlandı -> 0.45
        public const float CanavarHasari = 0.45f;
        public const float CanavarCani = 0.2f;

        /// <summary>hedefin aldığı hasarın çarpanı (oyuncuya canavar vurduysa CanavarHasari)</summary>
        public static float HasarCarpani(UnitController hedef, IAbilityCaster kaynak) {
            if (hedef == null || hedef.UnitControllerMode != UnitControllerMode.Player) {
                return 1f;
            }
            UnitController saldiran = kaynak as UnitController;
            if (saldiran == null || saldiran.UnitControllerMode != UnitControllerMode.AI) {
                return 1f;
            }
            return CanavarHasari;
        }

        // ---------------------------------------------------------------- ölçüm: oyuncuların verdiği ve aldığı hasar
        // (oyun testi ve çevrimiçi sunucu günlüğü okur; çevrimiçi savaşın çevrimdışıyla aynı işleyip işlemediğini görmek için)

        public class HasarSayaci {
            public int verilenVurus;
            public int verilenToplam;
            public int sonVerilen;
            public int alinanVurus;
            public int alinanToplam;
            public float ilk = -1f;
            public float son;
            // çevrimiçi: telefondan gelen beceri istekleri ve sunucuda başlayabilenler (oto av becerileri ölçümü)
            public int beceriIstegi;
            public int beceriBasladi;
            public string sonBeceri = string.Empty;
            public string sonReddedilen = string.Empty;
        }

        private static readonly System.Collections.Generic.Dictionary<UnitController, HasarSayaci> sayaclar =
            new System.Collections.Generic.Dictionary<UnitController, HasarSayaci>();

        private static HasarSayaci Sayac(UnitController oyuncu) {
            HasarSayaci s;
            if (sayaclar.TryGetValue(oyuncu, out s) == false) {
                if (sayaclar.Count > 200) {
                    // uzun süre açık sunucuda çıkan oyuncuların sayaçları birikmesin
                    sayaclar.Clear();
                }
                s = new HasarSayaci();
                sayaclar[oyuncu] = s;
            }
            return s;
        }

        /// <summary>CharacterCombat.TakeDamageCommon her hasarda çağırır</summary>
        public static void HasarKaydet(UnitController hedef, IAbilityCaster kaynak, int hasar) {
            UnitController saldiran = kaynak as UnitController;
            if (hedef == null || saldiran == null || hasar <= 0) {
                return;
            }
            if (saldiran.UnitControllerMode == UnitControllerMode.Player && hedef.UnitControllerMode == UnitControllerMode.AI) {
                HasarSayaci s = Sayac(saldiran);
                s.verilenVurus++;
                s.verilenToplam += hasar;
                s.sonVerilen = hasar;
                if (s.ilk < 0f) {
                    s.ilk = UnityEngine.Time.time;
                }
                s.son = UnityEngine.Time.time;
            } else if (hedef.UnitControllerMode == UnitControllerMode.Player && saldiran.UnitControllerMode == UnitControllerMode.AI) {
                HasarSayaci s = Sayac(hedef);
                s.alinanVurus++;
                s.alinanToplam += hasar;
            }
        }

        /// <summary>FishNetUnitController.HandleBeginAbilityServer: oyuncunun beceri isteği sunucuda başladı mı</summary>
        public static void BeceriIstegi(UnitController oyuncu, string beceri, bool basladi) {
            if (oyuncu == null) {
                return;
            }
            HasarSayaci s = Sayac(oyuncu);
            s.beceriIstegi++;
            s.sonBeceri = beceri;
            if (basladi) {
                s.beceriBasladi++;
            } else {
                s.sonReddedilen = beceri;
            }
        }

        public static HasarSayaci Olcum(UnitController oyuncu) {
            return oyuncu != null ? Sayac(oyuncu) : new HasarSayaci();
        }

        public static void OlcumuSifirla(UnitController oyuncu) {
            if (oyuncu != null) {
                sayaclar.Remove(oyuncu);
            }
        }

        public static string OlcumYazisi(UnitController oyuncu) {
            HasarSayaci s = Olcum(oyuncu);
            float sure = s.ilk >= 0f ? UnityEngine.Mathf.Max(0.1f, s.son - s.ilk) : 0f;
            return "verdiği " + s.verilenVurus + " vuruş/" + s.verilenToplam + " hasar (son " + s.sonVerilen
                + (s.verilenVurus > 1 ? ", " + (s.verilenVurus / sure * 60f).ToString("0") + " vuruş/dk" : string.Empty)
                + "), aldığı " + s.alinanVurus + " vuruş/" + s.alinanToplam + " hasar"
                + (s.beceriIstegi > 0 ? ", beceri isteği " + s.beceriIstegi + " (başlayan " + s.beceriBasladi + ", son " + s.sonBeceri
                    + (s.sonReddedilen.Length > 0 ? ", son reddedilen " + s.sonReddedilen : string.Empty) + ")" : string.Empty);
        }

        /// <summary>birimin can (ve diğer kaynak) çarpanı: canavarlar CanavarCani, Ötüken Taşı ve oyuncular 1</summary>
        public static float CanCarpani(UnitController birim) {
            if (birim == null || OtukenTasi.TasMi(birim)) {
                return 1f;
            }
            // seviye, birimin kipi (oyuncu/yapay zekâ) atanmadan hesaplanır: kip doğma isteğinden okunur
            // (0.1.49-0.1.52'de oyuncuların canı da yanlışlıkla %20'ye iniyordu, çevrimiçi canavarlarınki hiç inmiyordu)
            UnitControllerMode kip = birim.UnitControllerMode;
            if (birim.CharacterRequestData != null && birim.CharacterRequestData.characterConfigurationRequest != null) {
                kip = birim.CharacterRequestData.characterConfigurationRequest.unitControllerMode;
            }
            return kip == UnitControllerMode.AI ? CanavarCani : 1f;
        }
    }
}
