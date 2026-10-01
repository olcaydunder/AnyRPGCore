#!/usr/bin/env python3
"""Bozkır ganimeti: düşmanlardan düşen satılık ganimet eşyaları ve ganimet tabloları.

Ganimet tablosu düzeni (ağırlıklı gruplar, garantili düşüş, nadirlik katmanları) GitHub'daki açık kaynak
Unity ganimet tablosu projelerindeki yaygın düzene göre kuruldu; AnyRPG'nin kendi LootTable sistemi bunları
zaten desteklediği için ek kütüphane gerekmedi.

1. EŞYALAR (Item/Ganimet): gri "değersiz" süprüntüden turuncu "efsanevi" hazineye 10 eşya. Hiçbiri kullanılmaz;
   satıcıya satılır. Satıcı penceresindeki "Değersizleri Sat" düğmesi gri olanların hepsini tek seferde satar.
2. TABLOLAR (LootTable/Ganimet):
   "Bozkir Ganimeti" - her düşmana eklenir:  %50 bir değersiz eşya, %4-11 kupa (kurt dişi, pençe, tüy),
                                             %5 nadir/destansı (gök taşı, altın sikke, tamga yüzüğü),
                                             %0,3 efsanevi Ergenekon Demiri.
   "Ulu Ganimet"     - boss'lara eklenir (Ulu Evren, Yelbegen, Tepegöz): her seferinde bir değerli hazine ve bir kese.
3. DÜŞMANLAR: elle yapılmış birimlere (Kara Yek, Albastı, Tepegöz) tablo burada eklenir; "Dusman" klasöründekilere
   yaratiklar.py ekler.

Tekrar çalıştırılabilir; dosya GUID'leri addan türetilir.
Kullanım (depo kök klasöründen):
    python3 "Tools~/dunya/ganimet.py"
    python3 "Tools~/dunya/yaratiklar.py"      (Dusman birimlerinin ganimet listesi)
    python3 "Tools~/tr-icerik/uygula.py"      (eşya adları ve açıklamaları icerik.json'dan)
"""
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame"
ICONS = ROOT / "Assets/AnyRPG/Core/Content/Images/Icons"
ICERIK = ROOT / "Tools~/tr-icerik/icerik.json"
NS = uuid.UUID("3f6a1c54-8d2e-4b7a-9c1f-5e0d2b7a4c93")

# (resourceName, dosya, nitelik, satış değeri (bakır), simge, ağırlık, Türkçe ad, açıklama)
# satış değeri: satıcının ödediği para. basePrice = değer / (nitelik satış çarpanı x satıcı çarpanı 0,25)
ESYALAR = [
    ("Kirik Ok Ucu", "KirikOkUcuItem", "Poor", 30, "Equipment/Weapon/Bow/MedievalArrow", 0.1,
     "Kırık Ok Ucu", "Bir çatışmadan kalma, ucu körelmiş bir temren. Değersiz; satıcılar birkaç bakır verir."),
    ("Pasli Kemer Tokasi", "PasliKemerTokasiItem", "Poor", 40, "Item/Prop/LeatherCollar", 0.1,
     "Paslı Kemer Tokası", "Bir yağmacının kemerinden kopmuş paslı bir toka. Değersiz; satıcıya ver."),
    ("Catlak Boncuk", "CatlakBoncukItem", "Poor", 25, "Item/Minerals/GemChromatic", 0.1,
     "Çatlak Boncuk", "Rengi solmuş, ortasından çatlamış bir boncuk. Değersiz; satıcıya ver."),
    ("Kurt Disi", "KurtDisiItem", "Common", 100, "Ability/AbilityBoneSpikes", 0.1,
     "Kurt Dişi", "Sivri bir kurt dişi. Obalarda uğur getirsin diye boyna asılır; tüccarlar iyi para verir."),
    ("Kurt Pencesi", "KurtPencesiItem", "UnCommon", 300, "Ability/AbilityClaw3", 0.1,
     "Kurt Pençesi", "Bozkır kurdunun keskin pençesi. Avcılar arasında yiğitlik nişanıdır."),
    ("Kartal Tuyu", "KartalTuyuItem", "UnCommon", 400, "Ability/AbilityFeatherWhite", 0.1,
     "Kartal Tüyü", "Gökte süzülen bir kartalın tüyü. Kamlar ayinlerinde kullanır, iyi fiyata alırlar."),
    ("Gok Tasi Parcasi", "GokTasiParcasiItem", "Rare", 1500, "Ability/AbilityMeteor", 0.5,
     "Gök Taşı Parçası", "Tengri'nin gökten attığı demirli taş. Demirciler altın değerinde sayar."),
    ("Eski Altin Sikke", "EskiAltinSikkeItem", "Rare", 2000, "Item/Coin/CoinGoldOld", 0.1,
     "Eski Altın Sikke", "Üzerindeki tamga silinmiş, eski bir hanlıktan kalma altın sikke."),
    ("Altin Tamga Yuzugu", "AltinTamgaYuzuguItem", "Epic", 6000, "Equipment/Accessory/RingYellow", 0.1,
     "Altın Tamga Yüzüğü", "Bir boy beyinin tamgasını taşıyan mühür yüzüğü. Tüccarlar avuç dolusu gümüş verir."),
    ("Ergenekon Demiri", "ErgenekonDemiriItem", "Legendary", 30000, "Item/Minerals/IngotRedDark", 1.0,
     "Ergenekon Demiri", "Demir dağı eriten kutlu ocaktan kalma demir. Destanlarda anılır, paha biçilmez."),
]

SATIS_CARPANI = {"Poor": 0.25, "Common": 0.5, "UnCommon": 2.0, "Rare": 2.5, "Epic": 5.0, "Legendary": 10.0}
SATICI_CARPANI = 0.25

TABLOLAR = {
    # (garantili, grup şansı, en çok, [(eşya, ağırlık ya da % şans)])
    # garantili grupta ağırlıkla bir eşya seçilir; garantisiz grupta her eşya kendi şansıyla ayrı ayrı zar atar
    "Bozkir Ganimeti": [
        (True, 50, 1, [("Kirik Ok Ucu", 40), ("Pasli Kemer Tokasi", 30), ("Catlak Boncuk", 30)]),
        (False, 100, 0, [("Kurt Disi", 10), ("Kurt Pencesi", 4), ("Kartal Tuyu", 3)]),
        (True, 5, 1, [("Gok Tasi Parcasi", 45), ("Eski Altin Sikke", 45), ("Altin Tamga Yuzugu", 10)]),
        (False, 1, 0, [("Ergenekon Demiri", 29)]),
    ],
    "Ulu Ganimet": [
        (True, 100, 1, [("Gok Tasi Parcasi", 35), ("Eski Altin Sikke", 35), ("Altin Tamga Yuzugu", 25), ("Ergenekon Demiri", 5)]),
        (True, 100, 1, [("Altin Kese", 50), ("Dolu Akce Kesesi", 50)]),
        (False, 100, 0, [("Kurt Pencesi", 30), ("Kartal Tuyu", 30)]),
    ],
}

# elle yapılmış düşman birimleri: dosya -> eklenecek tablolar
ELLE_BIRIMLER = {
    "KaraYekUnit.asset": ["Bozkir Ganimeti"],
    "EnemyMinionUnit.asset": ["Bozkir Ganimeti"],
    "EnemyBossUnit.asset": ["Bozkir Ganimeti", "Ulu Ganimet"],
}


def guid(name):
    return uuid.uuid5(NS, name).hex


def write_meta(path, folder=False):
    meta = Path(str(path) + ".meta")
    g = guid("meta:" + path.relative_to(ROOT).as_posix())
    if folder:
        text = (f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        text = (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
                "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if not meta.exists() or meta.read_text() != text:
        meta.write_text(text)


def icon_ref(icon):
    meta = (ICONS / (icon + ".png.meta")).read_text(encoding="utf-8")
    assert re.search(r"^  spriteMode: 1\s*$", meta, re.M) and re.search(r"^  textureType: 8\s*$", meta, re.M), icon
    g = re.search(r"^guid: (\w+)", meta, re.M).group(1)
    return f"{{fileID: 21300000, guid: {g}, type: 3}}"


def sub(pattern, repl, text):
    new, n = re.subn(pattern, lambda m: repl, text, count=1, flags=re.M)
    assert n == 1, pattern
    return new


def write_items():
    folder = RES / "Item/Ganimet"
    folder.mkdir(exist_ok=True)
    write_meta(folder, folder=True)
    src = (RES / "Item/Gathering/Skinning/AnimalHideItem.asset").read_text(encoding="utf-8")
    for name, file_name, quality, value, icon, weight, _, _ in ESYALAR:
        base_price = round(value / (SATIS_CARPANI[quality] * SATICI_CARPANI))
        t = src
        t = sub(r"^  m_Name: .*$", f"  m_Name: {file_name}", t)
        t = sub(r"^  resourceName: .*$", f"  resourceName: {name}", t)
        # the Turkish name and text come from Tools~/tr-icerik (run uygula.py after this script)
        t = sub(r"^  displayName: .*$", "  displayName: ", t)
        t = sub(r"^  icon: .*$", f"  icon: {icon_ref(icon)}", t)
        t = sub(r"^  description: .*$", "  description: ", t)
        t = sub(r"^  stackSize: .*$", "  stackSize: 20", t)
        t = sub(r"^  itemQuality: .*$", f"  itemQuality: {quality}", t)
        t = sub(r"^  itemLevel: .*$", "  itemLevel: 1", t)
        t = sub(r"^  currencyName: .*$", "  currencyName: Copper", t)
        t = sub(r"^  dynamicCurrencyAmount: .*$", "  dynamicCurrencyAmount: 0", t)
        t = sub(r"^  pricePerLevel: .*$", "  pricePerLevel: 0", t)
        t = sub(r"^  basePrice: .*$", f"  basePrice: {base_price}", t)
        t = sub(r"^  weight: .*$", f"  weight: {weight}", t)
        t = sub(r"^  itemPickupPrefabProfileName: .*$", "  itemPickupPrefabProfileName: ", t)
        path = folder / (file_name + ".asset")
        path.write_text(t, encoding="utf-8")
        write_meta(path)
    print(f"{len(ESYALAR)} ganimet eşyası yazıldı")


def write_loot_tables(known_items):
    folder = RES / "LootTable/Ganimet"
    folder.mkdir(exist_ok=True)
    write_meta(folder, folder=True)
    header = (RES / "LootTable/PotionsLoot.asset").read_text(encoding="utf-8")
    header = header[:header.index("  resourceName:")]
    for name, groups in TABLOLAR.items():
        file_name = name.replace(" ", "") + "Loot"
        lines = [header.replace("  m_Name: PotionsLoot\n", f"  m_Name: {file_name}\n"),
                 f"  resourceName: {name}\n", "  displayName: \n", "  icon: {fileID: 0}\n", "  iconBackgroundImage: {fileID: 0}\n",
                 "  description: \n", "  useRegionalDescription: 0\n", "  resourceDescriptionProfile: \n", "  optionalOverride: 0\n",
                 "  ignoreGlobalDropLimit: 1\n", "  dropLimit: 0\n", "  lootGroups:\n"]
        for guaranteed, chance, limit, loot in groups:
            lines += [f"  - guaranteedDrop: {1 if guaranteed else 0}\n", f"    groupChance: {chance}\n",
                      f"    dropLimit: {limit}\n", "    uniqueLimit: 0\n", "    ignoreGlobalDropLimit: 1\n", "    loot:\n"]
            for item, weight in loot:
                if item not in known_items:
                    raise ValueError(f"{name}: bilinmeyen eşya {item}")
                lines += [f"    - itemName: {item}\n", f"      dropChance: {weight}\n", "      minDrops: 1\n", "      maxDrops: 1\n",
                          "      matchItemRestrictions: 0\n", "      prerequisiteConditions: []\n"]
        path = folder / (file_name + ".asset")
        path.write_text("".join(lines), encoding="utf-8")
        write_meta(path)
    print(f"{len(TABLOLAR)} ganimet tablosu yazıldı")


def add_to_units():
    for file_name, tables in ELLE_BIRIMLER.items():
        path = RES / "UnitProfile" / file_name
        raw = path.read_bytes().decode("utf-8")
        crlf = "\r\n" in raw
        text = raw.replace("\r\n", "\n")
        m = re.search(r"^        lootTableNames:\n((?:        - .*\n)*)", text, re.M)
        assert m, file_name
        names = [line[10:] for line in m.group(1).splitlines()]
        new_names = names + [t for t in tables if t not in names]
        block = "        lootTableNames:\n" + "".join(f"        - {n}\n" for n in new_names)
        text = text[:m.start()] + block + text[m.end():]
        if crlf:
            text = text.replace("\n", "\r\n")
        path.write_bytes(text.encode("utf-8"))
    print(f"{len(ELLE_BIRIMLER)} elle yapılmış birime ganimet eklendi")


def write_names():
    spec = json.loads(ICERIK.read_text(encoding="utf-8"))
    items = spec.setdefault("Item", {})
    for name, _, _, _, _, _, ad, aciklama in ESYALAR:
        items[name] = {"ad": ad, "aciklama": aciklama}
    ICERIK.write_text(json.dumps(spec, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("eşya adları icerik.json'a yazıldı")


def item_names():
    names = set()
    for f in (RES / "Item").rglob("*.asset"):
        m = re.search(r"^  resourceName: (.*)$", f.read_text(encoding="utf-8"), re.M)
        if m:
            names.add(m.group(1).strip())
    return names


def main():
    write_items()
    write_loot_tables(item_names())
    add_to_units()
    write_names()


if __name__ == "__main__":
    main()
