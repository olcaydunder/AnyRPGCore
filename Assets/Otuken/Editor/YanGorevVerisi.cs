// yan_gorevler.py yazar; elle değiştirme
using System.Collections.Generic;

namespace Otuken.EditorAraclari {

    /// <summary>yan görevler: her haritanın üç görev vereni (girişin çevresine, HaritaDoldur koyar)</summary>
    public static class YanGorevVerisi {
        public static readonly Dictionary<string, string[]> Haritalar = new Dictionary<string, string[]>() {
            { "FeaturesDemoZone", new[] { "Yan Avci Batur", "Yan Otaci Ay Hatun", "Yan Yilkici Erdem" } },
            { "UmayTarlalari", new[] { "Yan Ekinci Bayindir", "Yan Ebe Tolun", "Yan Degirmenci Tarhan" } },
            { "BoruTepesi", new[] { "Yan Kurtcu Asena", "Yan Gozcu Tutuk", "Yan Ok Ustasi Kinik" } },
            { "AkDenizKiyisi", new[] { "Yan Balikci Yalcin", "Yan Tuzcu Ece", "Yan Kayikci Oguz" } },
            { "OrdubalikCarsisi", new[] { "Yan Kervanci Tekin", "Yan Kumasci Gokce", "Yan Bekci Turgut" } },
            { "OrdubalikKenti", new[] { "Yan Yazici Bengu", "Yan Subasi Alp Er", "Yan Ascibasi Kutlu" } },
            { "UlukayinOrmani", new[] { "Yan Oduncu Cagatay", "Yan Otaci Borte", "Yan Tuzakci Temur" } },
            { "KoncolosIni", new[] { "Yan Madenci Demir", "Yan Iz Surucu Alaca", "Yan Atesci Oktay" } },
            { "KaganOrdasi", new[] { "Yan Tug Tasiyan Bumin", "Yan Atci Saruca", "Yan Okcubasi Inal" } },
            { "KafDagiYolu", new[] { "Yan Dagci Kartal", "Yan Yol Bekcisi Tomris", "Yan Kervan Basi Ilgaz" } },
            { "ErgenekonMagarasi", new[] { "Yan Korukcu Celik", "Yan Demir Ustasi Arslan", "Yan Kazmaci Aybike" } },
            { "KurganMezarligi", new[] { "Yan Yugcu Ayaz Ana", "Yan Balbal Ustasi Tonguc", "Yan Kam Kizi Altun" } },
            { "AyDedeKoyu", new[] { "Yan Gece Bekcisi Yildiz", "Yan Koy Agasi Sungur", "Yan Ozan Bayat" } },
            { "FeaturesDemoDungeon", new[] { "Yan Alp Kilic", "Yan Kam Ana Ulduz", "Yan Er Ulug" } },
            { "TamuZindani", new[] { "Yan Ak Kiz Aycin", "Yan Gok Alp Tengiz", "Yan Kut Bekcisi Ertug" } },
        };
    }
}
