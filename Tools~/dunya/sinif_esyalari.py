#!/usr/bin/env python3
"""
Sınıfa özel silah ve zırh (Ötüken):
  - Silahlar silah becerisi ister (requireWeaponSkill: 1): sınıfın weaponSkills listesindeki türleri kullanabilir
      Alp: kılıç, balta, gürz (tek/iki el), kalkan · Batur: pençe, mızrak/teber · Akıncı: hançer
      Okçu: yay, arbalet · Kam ve Otacı: asa, değnek, grimuar
  - Zırhlar zırh sınıfı ister (requireArmorClass: 1): Alp ağır (Plate), Batur/Akıncı/Okçu deri, Kam/Otacı kumaş
  - Takılar (kolye, yüzük) herkese açık
Kısıt yalnız oyuncuya uygulanır (Equipment.SinifKisitiUygulanir); canavarlar kendi donanımını giyer.
Çalıştırma: python3 "Tools~/dunya/sinif_esyalari.py"  (tekrar çalıştırmak zararsızdır)
"""
import re
from pathlib import Path

KOK = Path(__file__).resolve().parents[2] / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame"
ESYA = KOK / "Item/Equipment"
SINIF = KOK / "CharacterClass"

SINIF_SILAHLARI = {
    "WarriorCharacterClass.asset": ["One Hand Sword", "Two Hand Sword", "One Hand Mace", "Two Hand Mace",
                                    "One Hand Axe", "Two Hand Axe", "Shield"],
    "FighterCharacterClass.asset": ["Fist", "Polearm"],
    "ThiefCharacterClass.asset": ["Dagger"],
    "ArcherCharacterClass.asset": ["Bow", "Crossbow"],
    "MageCharacterClass.asset": ["Staff", "Wand"],
    "PriestCharacterClass.asset": ["Staff", "Wand"],
}
GRIMUAR_SINIFLARI = ["Mage", "Priest"]


def sinif_silahlari():
    for dosya, silahlar in SINIF_SILAHLARI.items():
        p = SINIF / dosya
        s = p.read_text(encoding="utf-8")
        m = re.search(r"^(    weaponSkills:\n)((?:    - .*\n)*)", s, re.M)
        assert m, dosya
        yeni = m.group(1) + "".join(f"    - {w}\n" for w in silahlar)
        s = s[:m.start()] + yeni + s[m.end():]
        p.write_text(s, encoding="utf-8")


def esyalar():
    silah = zirh = grimuar = 0
    for p in ESYA.rglob("*.asset"):
        s = p.read_text(encoding="utf-8")
        eski = s
        tur = re.search(r"^  weaponType: ?(.*)$", s, re.M)
        if tur is not None:
            if tur.group(1).strip():
                s = re.sub(r"^  requireWeaponSkill: \d", "  requireWeaponSkill: 1", s, flags=re.M)
                silah += 1
            elif "Grimoire" in p.name:
                liste = "".join(f"\n  - {c}" for c in GRIMUAR_SINIFLARI)
                s = re.sub(r"^  characterClassRequirementList:.*(?:\n  - .*)*", "  characterClassRequirementList:" + liste, s, flags=re.M)
                grimuar += 1
        if re.search(r"^  armorClassName: \S", s, re.M):
            s = re.sub(r"^  requireArmorClass: \d", "  requireArmorClass: 1", s, flags=re.M)
            zirh += 1
        if s != eski:
            p.write_text(s, encoding="utf-8")
    print(f"silah {silah}, zırh {zirh}, grimuar {grimuar}")


if __name__ == "__main__":
    sinif_silahlari()
    esyalar()
