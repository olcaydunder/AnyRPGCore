// hikaye.py yazar; elle değiştirme
using System.Collections.Generic;

namespace Otuken.EditorAraclari {

    /// <summary>ana hikâye: her haritanın görev veren yardımcısı (girişin yanına) ve yeni boss'u (en uzak açıklığa)</summary>
    public static class HikayeVerisi {
        // sahne -> { yardımcı profil (boşsa yok), boss profil (boşsa sahnede zaten var) }
        public static readonly Dictionary<string, string[]> Haritalar = new Dictionary<string, string[]>() {
            { "FeaturesDemoZone", new[] { "", "" } },
            { "UmayTarlalari", new[] { "Destan Ak Ana", "Alkarasi" } },
            { "BoruTepesi", new[] { "Destan Tonga", "Kara Toygar" } },
            { "AkDenizKiyisi", new[] { "Destan Aybars", "Tengiz Kormosu" } },
            { "OrdubalikCarsisi", new[] { "Destan Bilge Kul", "Kara Kuzgun" } },
            { "OrdubalikKenti", new[] { "Destan Kutluk Bey", "Kara Albis" } },
            { "UlukayinOrmani", new[] { "Destan Kayra", "Agu Bey" } },
            { "KoncolosIni", new[] { "Destan Cagri", "" } },
            { "KaganOrdasi", new[] { "Destan Isbara", "Kizil Tamga" } },
            { "KafDagiYolu", new[] { "Destan Ak Dogan", "Alaz Cin" } },
            { "ErgenekonMagarasi", new[] { "Destan Bozkurt Ata", "Demirkiynak" } },
            { "KurganMezarligi", new[] { "Destan Kara Bilge", "" } },
            { "AyDedeKoyu", new[] { "Destan Ay Dede", "Karakura" } },
            { "FeaturesDemoDungeon", new[] { "Destan Er Tostuk", "" } },
            { "TamuZindani", new[] { "Destan Ak Kam", "Erlik Han" } },
        };
    }
}
