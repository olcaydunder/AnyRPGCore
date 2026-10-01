using System.Collections.Generic;

namespace AnyRPG {

    /// <summary>
    /// Turkish labels for names that the engine also uses as internal keys (appearance groups and options, stats, armor classes,
    /// graphics quality levels).
    /// Only the text shown on screen is translated; the keys stay unchanged so saves and equipment keep working.
    /// </summary>
    public static class TurkishText {

        private static readonly Dictionary<string, string> words = new Dictionary<string, string>() {
            { "None", "Yok" },
            // primary stats (keys of CharacterStats.PrimaryStats) and secondary stat types
            { "Stamina", "Dayanıklılık" },
            { "Strength", "Güç" },
            { "Intellect", "Zekâ" },
            { "Agility", "Çeviklik" },
            { "MovementSpeed", "Hareket Hızı" },
            { "Accuracy", "İsabet" },
            { "CriticalStrike", "Kritik Vuruş" },
            { "Speed", "Hız" },
            { "Damage", "Hasar" },
            { "PhysicalDamage", "Fiziksel Hasar" },
            { "SpellDamage", "Büyü Hasarı" },
            { "CarryWeight", "Taşıma" },
            // armor classes
            { "Cloth", "Bez" },
            { "Leather", "Deri" },
            { "Plate", "Plaka" },
            // graphics quality levels (ProjectSettings/QualitySettings)
            { "Very Low", "Çok Düşük" },
            { "Low", "Düşük" },
            { "Medium", "Orta" },
            { "High", "Yüksek" },
            { "Very High", "Çok Yüksek" },
            { "Ultra", "En Yüksek" },
            { "Head", "Yüz" },
            { "Hair", "Saç" },
            { "Body", "Beden" },
            { "Hands", "Eller" },
            { "Feet", "Ayaklar" },
            { "Armor", "Zırh" },
            { "Helmet", "Miğfer" },
            { "Gloves", "Eldiven" },
            { "Boots", "Çizme" },
            { "Bracers", "Bileklik" },
            { "Clothes", "Giysi" },
            { "Blacksmith", "Demirci Giysisi" },
            { "InnKeeper", "Hancı Giysisi" },
            { "Default Body", "Varsayılan Beden" },
            { "Default Feet", "Varsayılan Ayaklar" },
            { "Default Hands", "Varsayılan Eller" },
            { "Heavy Armor", "Ağır Zırh" },
            { "Medium Armor", "Orta Zırh" },
            { "Light Armor", "Hafif Zırh" },
            { "Heavy Helmet", "Ağır Miğfer" },
            { "Medium Helmet", "Orta Miğfer" },
            { "Light Helmet", "Hafif Miğfer" },
            { "Heavy Gloves", "Ağır Eldiven" },
            { "Medium Gloves", "Orta Eldiven" },
            { "Light Gloves", "Hafif Eldiven" },
            { "Heavy Boots", "Ağır Çizme" },
            { "Medium Boots", "Orta Çizme" },
            { "Light Boots", "Hafif Çizme" },
            { "Male", "Erkek" },
            { "Female", "Kadın" },
        };

        private static readonly Dictionary<string, string> prefixes = new Dictionary<string, string>() {
            { "Hair ", "Saç " },
            { "Head ", "Yüz " },
        };

        public static string Translate(string text) {
            if (string.IsNullOrEmpty(text)) {
                return text;
            }
            string translated;
            if (words.TryGetValue(text, out translated)) {
                return translated;
            }
            foreach (KeyValuePair<string, string> prefix in prefixes) {
                if (text.StartsWith(prefix.Key)) {
                    return prefix.Value + text.Substring(prefix.Key.Length);
                }
            }
            return text;
        }
    }
}
