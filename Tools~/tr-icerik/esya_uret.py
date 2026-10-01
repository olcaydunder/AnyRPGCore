#!/usr/bin/env python3
"""Esya (Item) adlarinin Turkcelerini kurala gore uretip icerik.json'a ekler.

Oyundaki 400'den fazla esya renk + malzeme + parca kalibiyla adlandirilmis
("Brown Cloth Robe", "Iron Plate Helm" ...). Bu betik o kaliplari Turkceye
cevirir. icerik.json'da elle yazilmis bir Item girdisi varsa ona dokunmaz.

Kullanim (depo kok klasorunden):
    python3 "Tools~/tr-icerik/esya_uret.py"
    python3 "Tools~/tr-icerik/uygula.py"
"""
import json
import re
from pathlib import Path

HERE = Path(__file__).parent
ROOT = HERE.parents[1]
GAME = ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame"

RENK = {
    "Blue": "Mavi", "Brown": "Kahverengi", "Cyan": "Turkuaz", "Gray": "Gri", "Green": "Yeşil",
    "Red": "Kırmızı", "Violet": "Mor", "Yellow": "Sarı", "Orange": "Turuncu", "White": "Beyaz",
    "Black": "Siyah", "Purple": "Mor",
}
MADEN = {
    "Bronze": "Tunç", "Copper": "Bakır", "Gold": "Altın", "Iron": "Demir", "Silver": "Gümüş",
    "Steel": "Çelik", "Tin": "Kalay",
}
AGAC = {"Ash": "Dişbudak", "Elm": "Karaağaç", "Spruce": "Ladin"}

BEZ = {
    "Bracers": "Bez Bileklik", "Gloves": "Bez Eldiven", "Hood": "Başlık", "Mantle": "Omuz Atkısı",
    "Pants": "Şalvar", "Robe": "Kaftan", "Sash": "Kuşak", "Shoes": "Çarık",
}
DERI = {
    "Armor": "Deri Zırh", "Belt": "Deri Kemer", "Boots": "Deri Çizme", "Bracers": "Deri Bileklik",
    "Gloves": "Deri Eldiven", "Headband": "Alınlık", "Pants": "Deri Şalvar", "Shoulders": "Deri Omuzluk",
}
PLAKA = {
    "Armor": "Zırh", "Boots": "Zırhlı Çizme", "Bracers": "Kolluk", "Girdle": "Bel Zırhı",
    "Gloves": "Zırhlı Eldiven", "Helm": "Tulga", "Legs": "Dizlik", "Pauldrons": "Omuz Zırhı",
}
SILAH = {
    "Axe": "Balta", "Claw": "Pençe", "Dagger": "Hançer", "Great Axe": "Büyük Balta", "Halberd": "Teber",
    "Mace": "Gürz", "One Hand Sword": "Kılıç", "Sabre": "Pala", "Spear": "Mızrak", "Staff": "Asa",
    "Two Hand Sword": "İki Elli Kılıç", "War Hammer": "Savaş Çekici", "Club": "Sopa",
}
ORTACAG = {
    "Bow": "Yay", "Crossbow": "Arbalet", "Dagger": "Hançer", "Grimoire": "Bitig",
    "One Hand Axe": "Balta", "One Hand Mace": "Gürz", "One Hand Sword": "Kılıç", "Shield": "Kalkan",
    "Staff": "Asa", "Two Hand Axe": "İki Elli Balta", "Two Hand Mace": "İki Elli Gürz",
    "Two Hand Sword": "İki Elli Kılıç", "Wand": "Değnek",
}
TEK = {
    "8 Slot Bag": ("8 Gözlü Heybe", "Küçük bir yol heybesi."),
    "10 Slot Bag": ("10 Gözlü Heybe", "Sırt çantası kadar olmasa da geniş bir heybe."),
    "12 Slot Bag": ("12 Gözlü Heybe", "Bolca eşya alan sağlam bir heybe."),
    "Backpack": ("Sırt Torbası", "Büyük bir sırt torbası."),
    "Animal Hide": ("Hayvan Postu", "Tabaklanınca deri olur."),
    "Leather": ("Tabaklanmış Deri", "Zırh ve giysi yapımında kullanılır."),
    "Coal": ("Kömür", "Demirci ocağını besler."),
    "Flax": ("Keten Otu", "Keten kumaş yapımında kullanılır."),
    "Hemp": ("Kenevir", "Kâğıt ve ip yapımında kullanılır."),
    "Linen": ("Keten Kumaş", "Terziler giysi yapımında kullanır."),
    "Flour": ("Un", "Ekmek yapımında kullanılır."),
    "Bread": ("Ekmek", "Taze pişmiş ekmek."),
    "Cheese": ("Peynir", "Kokusu ağır ama doyurucu."),
    "Ham": ("Pastırma", "Kurutulmuş et."),
    "Sausage": ("Sucuk", "İyice pişmiş."),
    "Milk": ("Süt", "Taze sağılmış."),
    "Raw Fish": ("Çiğ Balık", "Balık kokuyor."),
    "Vial Of Water": ("Su Şişesi", "Damıtılmış kaynak suyu."),
    "Parchment Paper": ("Parşömen", "Tomar yapımında kullanılır."),
    "Health Potion": ("Şifa İksiri", "İç beni."),
    "Mana Potion": ("Mana İksiri", "İç beni."),
    "Scroll Of Agility": ("Çeviklik Tomarı", None),
    "Scroll Of Intellect": ("Zekâ Tomarı", None),
    "Scroll Of Stamina": ("Dayanıklılık Tomarı", None),
    "Scroll Of Strength": ("Güç Tomarı", None),
    "Logging Axe": ("Oduncu Baltası", None),
}

BOS_ACIKLAMA = re.compile(r"^(very \w+|basic .*|a simple .*|two hand \w+|sword|staff|axe|a shield)$", re.I)


def ad_uret(n):
    """resourceName -> (Turkce ad, Turkce aciklama ya da None)"""
    if n in TEK:
        return TEK[n]
    m = re.fullmatch(r"Level (\d+) (\w+) Necklace", n)
    if m and m.group(2) in RENK:
        return f"{RENK[m.group(2)]} Kolye ({m.group(1)}. Seviye)", "Değerli taşlarla süslü bir kolye."
    m = re.fullmatch(r"(\w+) (Cloth|Leather|Plate) (.+)", n)
    if m:
        on, malzeme, parca = m.groups()
        tablo = {"Cloth": BEZ, "Leather": DERI, "Plate": PLAKA}[malzeme]
        sifat = "Efsanevi" if on == "Legendary" else RENK.get(on) or MADEN.get(on)
        if sifat and parca in tablo:
            aciklama = {"Cloth": "Hafif bez giysi.", "Leather": "Esnek deri giysi.", "Plate": "Ağır maden zırh."}[malzeme]
            if on == "Legendary":
                aciklama = "Destanlarda anılan bir parça."
            return f"{sifat} {tablo[parca]}", aciklama
    m = re.fullmatch(r"Basic (?:(%s) )?(.+)" % "|".join(MADEN), n)
    if m and m.group(2) in SILAH:
        if m.group(1):
            return f"{MADEN[m.group(1)]} {SILAH[m.group(2)]}", f"{MADEN[m.group(1)]} dövme bir silah."
        return f"Basit {SILAH[m.group(2)]}", "Sade, işini gören bir silah."
    m = re.fullmatch(r"(Epic |Random )?Medieval (.+?)( Recipe)?", n)
    if m and m.group(2) in ORTACAG:
        temel = ORTACAG[m.group(2)]
        if m.group(1) == "Epic ":
            temel = f"Destansı {temel}"
        elif m.group(1) == "Random ":
            temel = f"Gizemli {temel}"
        else:
            temel = f"Usta İşi {temel}"
        if m.group(3):
            return f"{temel} Tarifi", "Bu tarifi öğrenen ustalar bu eşyayı yapabilir."
        return temel, None
    m = re.fullmatch(r"(\w+) (Ore|Ingot|Key)", n)
    if m and m.group(1) in MADEN:
        maden = MADEN[m.group(1)]
        return {
            "Ore": (f"{maden} Cevheri", "Arıtılması gerekir."),
            "Ingot": (f"{maden} Külçe", f"Bir {maden.lower()} külçesi."),
            "Key": (f"{maden} Anahtar", None),
        }[m.group(2)]
    m = re.fullmatch(r"(\w+) (Log|Lumber)", n)
    if m and m.group(1) in AGAC:
        agac = AGAC[m.group(1)]
        if m.group(2) == "Log":
            return f"{agac} Kütüğü", f"İşlenmemiş {agac.lower()} kütüğü."
        return f"{agac} Kereste", f"Kaba {agac.lower()} kerestesi."
    m = re.fullmatch(r"(\w+) (Dye|Flower|Gem|Crystal|Necklace)", n)
    if m and m.group(1) in RENK:
        renk = RENK[m.group(1)]
        return {
            "Dye": (f"{renk} Boya", "Terziler kumaş boyamakta kullanır."),
            "Flower": (f"{renk} Çiçek", "Boya yapımında kullanılır."),
            "Gem": (f"{renk} Mücevher", f"Parlatılmış {renk.lower()} bir mücevher."),
            "Crystal": (f"{renk} Kristal", "Kesilmemiş ham bir kristal."),
            "Necklace": (f"{renk} Kolye", "Değerli taşlarla süslü bir kolye."),
        }[m.group(2)]
    return None


def main():
    spec_path = HERE / "icerik.json"
    spec = json.loads(spec_path.read_text(encoding="utf-8"))
    elle = spec.get("Item", {})
    uretilen, atlanan = {}, []
    for f in sorted((GAME / "Item").rglob("*.asset")):
        text = f.read_text(encoding="utf-8")
        m = re.search(r"^  resourceName: (.*)$", text, re.M)
        if not m:
            continue
        n = m.group(1).strip()
        if n in elle or n in uretilen:
            continue
        sonuc = ad_uret(n)
        if sonuc is None:
            atlanan.append(n)
            continue
        ad, aciklama = sonuc
        girdi = {"ad": ad}
        eski = re.search(r"^  description: ?(.*)$", text, re.M).group(1).strip()
        if aciklama is not None:
            girdi["aciklama"] = aciklama
        elif BOS_ACIKLAMA.match(eski):
            girdi["aciklama"] = ""
        uretilen[n] = girdi
    birlesik = dict(sorted(uretilen.items()))
    birlesik.update(elle)
    spec["Item"] = birlesik
    spec_path.write_text(json.dumps(spec, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(uretilen)} eşya adı üretildi, {len(elle)} elle yazılmış korundu")
    print("çevrilmeyen:", ", ".join(sorted(set(atlanan))))


if __name__ == "__main__":
    main()
