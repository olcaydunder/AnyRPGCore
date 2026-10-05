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
        public const float CanavarHasari = 0.5f;
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

        /// <summary>birimin can (ve diğer kaynak) çarpanı: canavarlar CanavarCani, Ötüken Taşı ve oyuncular 1</summary>
        public static float CanCarpani(UnitController birim) {
            if (birim == null || birim.UnitControllerMode != UnitControllerMode.AI || OtukenTasi.TasMi(birim)) {
                return 1f;
            }
            return CanavarCani;
        }
    }
}
