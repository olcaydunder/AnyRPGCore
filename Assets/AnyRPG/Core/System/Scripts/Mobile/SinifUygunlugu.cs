using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Sınıfa özel eşya (Ötüken):
    ///  - Uygun: eşya oyuncunun sınıfına uygun mu (seviyeye bakmadan): silah türü, zırh sınıfı, sınıf listesi
    ///  - DusmeCarpani: canavardan düşerken sınıfa uygun silah/zırh 2,5 kat sık, uygun olmayan yarı yarıya düşer
    ///    (LootTableState; takılar ve öteki eşyalar değişmez)
    ///  - Aciklama: çantadaki eşyanın üstünde uygunluk, saldırı gücü/zırh ve kuşanılı eşyayla fark (yalnız telefonda)
    /// </summary>
    public static class SinifUygunlugu {

        public const float UygunCarpan = 2.5f;
        public const float UygunsuzCarpan = 0.5f;

        /// <summary>eşyada sınıf kısıtı var mı (silah türü, zırh sınıfı ya da sınıf listesi)</summary>
        public static bool Kisitli(Item esya) {
            if (esya == null) {
                return false;
            }
            if (esya.CharacterClassRequirementList != null && esya.CharacterClassRequirementList.Count > 0) {
                return true;
            }
            Weapon silah = esya as Weapon;
            if (silah != null) {
                return silah.RequireWeaponSkill && silah.WeaponSkill != null;
            }
            Armor zirh = esya as Armor;
            if (zirh != null) {
                return zirh.RequireArmorClass && zirh.ArmorClass != null;
            }
            return false;
        }

        /// <summary>eşya bu karakterin sınıfına uygun mu (seviye hariç)</summary>
        public static bool Uygun(Item esya, UnitController oyuncu) {
            if (esya == null || oyuncu == null || oyuncu.BaseCharacter == null) {
                return true;
            }
            if (esya.CharacterClassRequirementIsMet(oyuncu.BaseCharacter) == false) {
                return false;
            }
            Equipment donanim = esya as Equipment;
            if (donanim != null && donanim.CapabilityConsumerSupported(oyuncu.BaseCharacter) == false) {
                return false;
            }
            return true;
        }

        /// <summary>canavar ganimetinde ağırlık çarpanı (sınıfa göre)</summary>
        public static float DusmeCarpani(Item esya, UnitController oyuncu) {
            if (oyuncu == null || oyuncu.UnitControllerMode != UnitControllerMode.Player || Kisitli(esya) == false) {
                return 1f;
            }
            return Uygun(esya, oyuncu) ? UygunCarpan : UygunsuzCarpan;
        }

        /// <summary>bu eşyayı kullanabilen sınıfların adları ("Alp, Batur")</summary>
        public static string KullananSiniflar(Item esya, SystemDataFactory veri) {
            if (esya == null || veri == null) {
                return string.Empty;
            }
            List<string> adlar = new List<string>();
            foreach (CharacterClass sinif in veri.GetResourceList<CharacterClass>()) {
                if (sinif == null) {
                    continue;
                }
                if (esya.CharacterClassRequirementList != null && esya.CharacterClassRequirementList.Count > 0
                    && esya.CharacterClassRequirementList.Contains(sinif) == false) {
                    continue;
                }
                CapabilityProps yetenek = sinif.GetFilteredCapabilities(null);
                Weapon silah = esya as Weapon;
                if (silah != null && silah.RequireWeaponSkill && silah.WeaponSkill != null
                    && (yetenek == null || yetenek.WeaponSkillList.Contains(silah.WeaponSkill) == false)) {
                    continue;
                }
                Armor zirh = esya as Armor;
                if (zirh != null && zirh.RequireArmorClass && zirh.ArmorClass != null
                    && (yetenek == null || yetenek.ArmorClassList.Contains(zirh.ArmorClass.ResourceName) == false)) {
                    continue;
                }
                adlar.Add(sinif.DisplayName);
            }
            return string.Join(", ", adlar);
        }

        // ---------------------------------------------------------------- açıklama (telefon)

        public static float SaldiriGucu(InstantiatedEquipment esya, int seviye) {
            Weapon silah = esya != null ? esya.Equipment as Weapon : null;
            if (silah == null) {
                return 0f;
            }
            float guc = silah.GetDamagePerSecond(seviye, esya.ItemQuality);
            int basamak = Demirci.Seviye(esya);
            if (basamak > 0 && Demirci.KazancTuru(esya) == Demirci.Kazanc.Hasar) {
                guc += Demirci.HasarBonusu(basamak, seviye);
            }
            return guc;
        }

        public static float ZirhDegeri(InstantiatedEquipment esya, int seviye) {
            if (esya == null || esya.Equipment == null) {
                return 0f;
            }
            float zirh = esya.Equipment.GetArmorModifier(seviye, esya.ItemQuality);
            int basamak = Demirci.Seviye(esya);
            if (basamak > 0 && Demirci.KazancTuru(esya) == Demirci.Kazanc.Zirh) {
                zirh += Demirci.ZirhBonusu(basamak, seviye);
            }
            return zirh;
        }

        /// <summary>aynı yuva türünde kuşanılı en zayıf eşya (yoksa null; yuva boşsa da null)</summary>
        public static InstantiatedEquipment Kusanili(InstantiatedEquipment esya, UnitController oyuncu, out bool bosYuva) {
            bosYuva = false;
            if (esya == null || esya.Equipment == null || esya.Equipment.EquipmentSlotType == null || oyuncu == null
                || oyuncu.CharacterEquipmentManager == null) {
                return null;
            }
            List<EquipmentSlotProfile> yuvalar = esya.Equipment.EquipmentSlotType.GetCompatibleSlotProfiles();
            if (yuvalar == null) {
                return null;
            }
            InstantiatedEquipment enZayif = null;
            float enZayifDeger = float.MaxValue;
            foreach (EquipmentSlotProfile yuva in yuvalar) {
                EquipmentInventorySlot kusanili;
                if (yuva == null || oyuncu.CharacterEquipmentManager.CurrentEquipment.TryGetValue(yuva, out kusanili) == false) {
                    continue;
                }
                if (kusanili == null || kusanili.InstantiatedEquipment == null) {
                    bosYuva = true;
                    return null;
                }
                if (kusanili.InstantiatedEquipment == esya) {
                    return esya;
                }
                float d = Gelisim.EsyaDegeri(kusanili.InstantiatedEquipment, oyuncu);
                if (d < enZayifDeger) {
                    enZayifDeger = d;
                    enZayif = kusanili.InstantiatedEquipment;
                }
            }
            return enZayif;
        }

        private static string Fark(float fark) {
            int f = Mathf.RoundToInt(fark);
            if (f > 0) {
                return "<color=#7CE07C>+" + f + "</color>";
            }
            if (f < 0) {
                return "<color=#FF7A6E>" + f + "</color>";
            }
            return "<color=#C8C0B0>±0</color>";
        }

        /// <summary>çantadaki eşyanın açıklamasına eklenen satırlar</summary>
        public static string Aciklama(InstantiatedEquipment esya, UnitController oyuncu, SystemDataFactory veri) {
            if (esya == null || esya.Equipment == null || oyuncu == null || oyuncu.CharacterStats == null) {
                return string.Empty;
            }
            int seviye = oyuncu.CharacterStats.Level;
            List<string> satirlar = new List<string>();
            if (Kisitli(esya.Equipment)) {
                if (Uygun(esya.Equipment, oyuncu)) {
                    satirlar.Add("<color=#7CE07C>Sınıfına uygun</color>");
                } else {
                    satirlar.Add("<color=#FF7A6E>× Sınıfına uygun değil</color> <size=85%>(" + KullananSiniflar(esya.Equipment, veri) + ")</size>");
                }
            }
            int esyaSeviyesi = esya.GetItemLevel(seviye);
            if (esyaSeviyesi > seviye) {
                satirlar.Add("<color=#FF7A6E>" + esyaSeviyesi + ". seviye gerekir</color>");
            }
            bool silah = esya.Equipment is Weapon;
            float guc = silah ? SaldiriGucu(esya, seviye) : ZirhDegeri(esya, seviye);
            string ad = silah ? "Saldırı gücü" : "Zırh";
            if (guc > 0f) {
                satirlar.Add(ad + ": <b>" + Mathf.RoundToInt(guc) + "</b>");
            }
            bool bos;
            InstantiatedEquipment kusanili = Kusanili(esya, oyuncu, out bos);
            if (kusanili == esya) {
                satirlar.Add("<color=#FFD54A>Şu an kuşanılı</color>");
            } else if (bos) {
                satirlar.Add("Yuva boş: kuşanırsan " + Fark(guc > 0f ? guc : Gelisim.EsyaDegeri(esya, oyuncu)) + " kazanırsın");
            } else if (kusanili != null) {
                float eski = silah ? SaldiriGucu(kusanili, seviye) : ZirhDegeri(kusanili, seviye);
                float genelFark = Gelisim.EsyaDegeri(esya, oyuncu) - Gelisim.EsyaDegeri(kusanili, oyuncu);
                string satir = "Kuşanılı " + kusanili.DisplayName + ": ";
                if (guc > 0f || eski > 0f) {
                    satir += ad.ToLowerInvariant() + " " + Mathf.RoundToInt(eski) + " → " + Mathf.RoundToInt(guc) + " (" + Fark(guc - eski) + ")";
                } else {
                    satir += "güç " + Fark(genelFark);
                }
                satirlar.Add(satir);
                if ((guc > 0f || eski > 0f) && Mathf.Abs(genelFark - (guc - eski) * (silah ? 2f : 0.5f)) >= 1f) {
                    satirlar.Add("Toplam güç farkı: " + Fark(genelFark));
                }
            }
            return satirlar.Count == 0 ? string.Empty : "\n" + string.Join("\n", satirlar);
        }
    }
}
