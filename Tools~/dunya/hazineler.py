#!/usr/bin/env python3
"""Ötüken Yaylası ve Erlik'in Mağarası'na ganimet kaynakları ekler.

1. AKÇE KESELERİ: ganimetten alınınca parası doğrudan cebe geçen eşyalar (CurrencyItem).
2. GANİMET TABLOLARI: "Hazine Sandigi" ve "Buyuk Hazine Sandigi".
3. SANDIKLAR: iki yeni toplanabilir sandık prefab'ı (Ekmek prefab'ının kopyası, sandık modeliyle).
4. ADLAR: toplama noktalarının (maden, ot, ağaç, balık, tezgâh...) görünen adları Türkçe.
5. YERLEŞTİRME: sandıklar kamplara ve dağ doruklarına, yiyecekler kampların yanına, otlar çayırlara,
   maden damarları dağ yamaçlarına, ağaçlar ormanlara. Her nokta NavMesh'te ulaşılabilir, düz bir yüzeyde.

Tekrar çalıştırılabilir: "TRL_" ile başlayan önceki düğümleri silip yeniden yazar; dosya GUID'leri addan türetilir.
Kullanım (depo kök klasöründen):
    python3 "Tools~/dunya/hazineler.py"
    python3 "Tools~/tr-icerik/uygula.py"     (akçe kesesi adları)
"""
import hashlib
import json
import math
import re
import sys
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from navmesh import NavMesh  # noqa: E402
import yaratiklar  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame"
RES = GAME / "Resources/FeaturesDemoGame"
INTERACTABLE = GAME / "Prefab/Spawnable/Interactable"
NS = uuid.UUID("0c8b6f3e-2a71-4f5d-8e4b-9d2f1a7c5e60")
PREFIX = "TRL_"
MASK = 0x7FFFFFFFFFFFFFFF

q = lambda s: json.dumps(s, ensure_ascii=True)


def guid(name):
    return uuid.uuid5(NS, name).hex


def write_meta(path, kind="native"):
    meta = Path(str(path) + ".meta")
    g = guid("meta:" + path.relative_to(ROOT).as_posix())
    if kind == "folder":
        text = (f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    elif kind == "prefab":
        text = (f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
                "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        text = (f"fileFormatVersion: 2\nguid: {g}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
                "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    if not meta.exists() or meta.read_text() != text:
        meta.write_text(text)
    return g


def item_names():
    names = set()
    for f in (RES / "Item").rglob("*.asset"):
        m = re.search(r"^  resourceName: (.*)$", f.read_text(encoding="utf-8"), re.M)
        if m:
            names.add(m.group(1).strip())
    return names


# ---------------------------------------------------------------- 1. akçe keseleri

KESELER = [
    # (resourceName, dosya adı, para birimi, miktar)
    ("Akce Kesesi", "AkceKesesiItem", "Silver", 3),
    ("Dolu Akce Kesesi", "DoluAkceKesesiItem", "Silver", 12),
    ("Altin Kese", "AltinKeseItem", "Gold", 1),
]


def write_purses():
    src = (RES / "Item/Consumable/GainCurrencyItem.asset").read_text(encoding="utf-8")
    for name, file_name, currency, amount in KESELER:
        t = re.sub(r"^  m_Name: .*$", f"  m_Name: {file_name}", src, count=1, flags=re.M)
        t = re.sub(r"^  resourceName: .*$", f"  resourceName: {name}", t, count=1, flags=re.M)
        t = re.sub(r"^  gainCurrencyName: .*$", f"  gainCurrencyName: {currency}", t, count=1, flags=re.M)
        t = re.sub(r"^  gainCurrencyAmount: .*$", f"  gainCurrencyAmount: {amount}", t, count=1, flags=re.M)
        t = re.sub(r"^  stackSize: .*$", "  stackSize: 20", t, count=1, flags=re.M)
        # the Turkish name and text come from Tools~/tr-icerik (run uygula.py after this script)
        t = re.sub(r"^  displayName: .*$", "  displayName: ", t, count=1, flags=re.M)
        t = re.sub(r"^  description: .*$", "  description: ", t, count=1, flags=re.M)
        path = RES / "Item/Consumable" / (file_name + ".asset")
        path.write_text(t, encoding="utf-8")
        write_meta(path)
    print(f"{len(KESELER)} akçe kesesi yazıldı")


# ---------------------------------------------------------------- 2. ganimet tabloları

RANDOM_WEAPONS = ["Bow", "Crossbow", "Dagger", "Grimoire", "One Hand Axe", "One Hand Mace", "One Hand Sword",
                  "Shield", "Staff", "Two Hand Axe", "Two Hand Mace", "Two Hand Sword", "Wand"]
COLORS = ["Red", "Blue", "Green", "Yellow", "Violet", "Cyan"]

TABLOLAR = {
    "Hazine Sandigi": [
        # (garantili, grup şansı, en çok, aynısından en çok, sınıfa uygun olsun, [(eşya, ağırlık/şans)])
        (True, 100, 1, 0, False, [("Akce Kesesi", 70), ("Dolu Akce Kesesi", 25), ("Altin Kese", 5)]),
        (True, 100, 2, 1, False, [("Health Potion", 30), ("Mana Potion", 20), ("Bread", 15), ("Cheese", 15),
                                  ("Scroll Of Strength", 5), ("Scroll Of Agility", 5), ("Scroll Of Intellect", 5),
                                  ("Scroll Of Stamina", 5)]),
        (False, 100, 1, 0, False, [(f"{c} Gem", 6) for c in COLORS]),
        (True, 35, 1, 0, True, [(f"Random Medieval {w}", 10) for w in RANDOM_WEAPONS]),
        (True, 20, 1, 0, False, [("8 Slot Bag", 60), ("10 Slot Bag", 30), ("12 Slot Bag", 10)]),
        (True, 15, 1, 0, False, [(f"{c} Necklace", 10) for c in COLORS]),
    ],
    "Buyuk Hazine Sandigi": [
        (True, 100, 1, 0, False, [("Dolu Akce Kesesi", 60), ("Altin Kese", 40)]),
        (True, 100, 1, 0, False, [("Akce Kesesi", 100)]),
        (True, 100, 2, 0, False, [("Health Potion", 50), ("Mana Potion", 50)]),
        (True, 70, 1, 0, True, [(f"Random Medieval {w}", 10) for w in RANDOM_WEAPONS]),
        (True, 35, 1, 0, True, [(f"Epic Medieval {w}", 10) for w in RANDOM_WEAPONS if w not in ("Crossbow",)]
         + [("Epic Medieval Crossbow", 10)]),
        (True, 50, 1, 0, False, [(f"{c} Necklace", 10) for c in COLORS] + [(f"{c} Gem", 10) for c in COLORS]),
        (True, 25, 1, 0, False, [(f"Epic Medieval {w} Recipe", 10) for w in RANDOM_WEAPONS]),
        (True, 30, 1, 0, False, [("10 Slot Bag", 60), ("12 Slot Bag", 40)]),
    ],
}


def write_loot_tables(known_items):
    folder = RES / "LootTable/Hazine"
    folder.mkdir(exist_ok=True)
    write_meta(folder, "folder")
    header = (RES / "LootTable/PotionsLoot.asset").read_text(encoding="utf-8")
    header = header[:header.index("  resourceName:")]
    for name, groups in TABLOLAR.items():
        file_name = name.replace(" ", "") + "Loot"
        lines = [header.replace("  m_Name: PotionsLoot\n", f"  m_Name: {file_name}\n"),
                 f"  resourceName: {name}\n", "  displayName: \n", "  icon: {fileID: 0}\n", "  iconBackgroundImage: {fileID: 0}\n",
                 "  description: \n", "  useRegionalDescription: 0\n", "  resourceDescriptionProfile: \n", "  optionalOverride: 0\n",
                 "  ignoreGlobalDropLimit: 1\n", "  dropLimit: 0\n", "  lootGroups:\n"]
        for guaranteed, chance, limit, unique, restrict, loot in groups:
            lines += [f"  - guaranteedDrop: {1 if guaranteed else 0}\n", f"    groupChance: {chance}\n",
                      f"    dropLimit: {limit}\n", f"    uniqueLimit: {unique}\n", "    ignoreGlobalDropLimit: 1\n", "    loot:\n"]
            for item, weight in loot:
                if item not in known_items:
                    raise ValueError(f"{name}: bilinmeyen eşya {item}")
                lines += [f"    - itemName: {item}\n", f"      dropChance: {weight}\n", "      minDrops: 1\n", "      maxDrops: 1\n",
                          f"      matchItemRestrictions: {1 if restrict else 0}\n", "      prerequisiteConditions: []\n"]
        path = folder / (file_name + ".asset")
        path.write_text("".join(lines), encoding="utf-8")
        write_meta(path)
    print(f"{len(TABLOLAR)} ganimet tablosu yazıldı")


# ---------------------------------------------------------------- 3. sandık prefab'ları

BREAD = INTERACTABLE / "ItemPickup/BreadPickupNode.prefab"
BREAD_INSTANCE = 1046899590447443981
BREAD_GO, BREAD_TR = 8450105383853101254, 9083512783103740540
CHEST_MODELS = {
    # model prefab guid, kök GameObject, kök Transform, kaldırılacak bileşenler (çarpışma kutusu, NavMeshModifier)
    "chest_wood": ("3796395d51f67ee4e91c0cb62a43d276", 1621489815193578654, 2146916067052008996,
                   [-5003272094021600800, 576096477263187010]),
    "chest_1": ("3e223f0799330d54e934fec909b215b9", 2279572903490348690, 1506871713589476392,
                [4876592817256141371, 5109809294553311335]),
}
SANDIKLAR = {
    # dosya adı: (görünen ad, ganimet tablosu, yeniden dolma sn, model, ölçek)
    "HazineSandigi": ("Hazine Sandığı", "Hazine Sandigi", 300, "chest_wood", 1.0),
    "BuyukHazineSandigi": ("Büyük Hazine Sandığı", "Buyuk Hazine Sandigi", 900, "chest_1", 1.3),
}


def write_chest_prefabs():
    src = BREAD.read_text(encoding="utf-8")
    assert "\r\n" not in src
    start = src.index(f"--- !u!1001 &{BREAD_INSTANCE}\n")
    end = src.index("--- !u!", src.index(f"--- !u!4 &{BREAD_TR} stripped\n") + 10)
    guids = {}
    for file_name, (shown, table, timer, model, scale) in SANDIKLAR.items():
        mguid, mgo, mtr, removed = CHEST_MODELS[model]
        new_go = (BREAD_INSTANCE ^ mgo) & MASK
        new_tr = (BREAD_INSTANCE ^ mtr) & MASK

        def mod(target, path, value):
            return (f"    - target: {{fileID: {target}, guid: {mguid},\n        type: 3}}\n"
                    f"      propertyPath: {path}\n      value: {value}\n      objectReference: {{fileID: 0}}\n")
        mods = "".join([
            mod(mgo, "m_Name", model),
            mod(mtr, "m_LocalPosition.x", 0), mod(mtr, "m_LocalPosition.y", 0), mod(mtr, "m_LocalPosition.z", 0),
            mod(mtr, "m_LocalRotation.w", 1), mod(mtr, "m_LocalRotation.x", 0), mod(mtr, "m_LocalRotation.y", 0),
            mod(mtr, "m_LocalRotation.z", 0),
            mod(mtr, "m_LocalScale.x", scale), mod(mtr, "m_LocalScale.y", scale), mod(mtr, "m_LocalScale.z", scale),
        ])
        removed_text = "".join(f"    - {{fileID: {r}, guid: {mguid}, type: 3}}\n" for r in removed)
        block = (f"--- !u!1001 &{BREAD_INSTANCE}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
                 f"  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {{fileID: 8704278066669917025}}\n"
                 f"    m_Modifications:\n{mods}    m_RemovedComponents:\n{removed_text}    m_RemovedGameObjects: []\n"
                 "    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
                 f"  m_SourcePrefab: {{fileID: 100100000, guid: {mguid}, type: 3}}\n"
                 f"--- !u!1 &{new_go} stripped\nGameObject:\n  m_CorrespondingSourceObject: {{fileID: {mgo}, guid: {mguid},\n"
                 f"    type: 3}}\n  m_PrefabInstance: {{fileID: {BREAD_INSTANCE}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
                 f"--- !u!4 &{new_tr} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {mtr}, guid: {mguid},\n"
                 f"    type: 3}}\n  m_PrefabInstance: {{fileID: {BREAD_INSTANCE}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
        t = src[:start] + block + src[end:]

        def sub(a, b, count=1):
            nonlocal t
            if t.count(a) != count:
                raise ValueError(f"{file_name}: {a!r} {t.count(a)} kez")
            t = t.replace(a, b)
        sub("  m_Name: BreadPickupNode\n", f"  m_Name: {file_name}\n")
        sub(f"  - {{fileID: {BREAD_TR}}}\n", f"  - {{fileID: {new_tr}}}\n")
        sub(f"    spawnObject: {{fileID: {BREAD_GO}}}\n", f"    spawnObject: {{fileID: {new_go}}}\n")
        t, n = re.subn(r"^  interactableName: .*$", lambda m: f"  interactableName: {q(shown)}", t, count=1, flags=re.M)
        if n != 1:
            raise ValueError(f"{file_name}: interactableName yok")
        sub("  interactionMaxRange: 2\n", "  interactionMaxRange: 3\n")
        sub("    - Bread Pickup Node\n", f"    - {table}\n")
        sub("    spawnTimer: 60\n", f"    spawnTimer: {timer}\n")
        # the bread sits on a table, so its interaction point is 0.6 m lower; a chest stands on the ground
        sub("      propertyPath: m_LocalPosition.x\n      value: -0.999\n", "      propertyPath: m_LocalPosition.x\n      value: -1.3\n")
        sub("      propertyPath: m_LocalPosition.y\n      value: -0.618\n", "      propertyPath: m_LocalPosition.y\n      value: 0\n")
        sub("      propertyPath: m_LocalPosition.z\n      value: 0.926\n", "      propertyPath: m_LocalPosition.z\n      value: 1.3\n")
        sub("  m_Size: {x: 0.25, y: 0.25, z: 0.25}\n  m_Center: {x: 0, y: 0.125, z: 0}\n",
            f"  m_Size: {{x: {1.6 * scale:.2f}, y: {1.0 * scale:.2f}, z: {1.0 * scale:.2f}}}\n"
            f"  m_Center: {{x: 0, y: {0.5 * scale:.2f}, z: 0}}\n")
        path = INTERACTABLE / "ItemPickup" / (file_name + ".prefab")
        path.write_text(t, encoding="utf-8")
        guids[file_name] = write_meta(path, "prefab")
    print(f"{len(SANDIKLAR)} sandık prefab'ı yazıldı")
    return guids


# ---------------------------------------------------------------- 4. Türkçe adlar

ADLAR = {
    "Blue Crystals": "Mavi Kristal", "Coal Ore": "Kömür Damarı", "Copper Ore": "Bakır Damarı",
    "Cyan Crystals": "Turkuaz Kristal", "Gold Ore": "Altın Damarı", "Green Crystals": "Yeşil Kristal",
    "Iron Ore": "Demir Damarı", "Red Crystals": "Kırmızı Kristal", "Silver Ore": "Gümüş Damarı",
    "Tin Ore": "Kalay Damarı", "Violet Crystals": "Mor Kristal", "Yellow Crystals": "Sarı Kristal",
    "Blue Flower": "Mavi Çiçek", "Brown Flower": "Kahverengi Çiçek", "Cyan Flower": "Turkuaz Çiçek",
    "Flax": "Keten Otu", "Gray Flower": "Gri Çiçek", "Green Flower": "Yeşil Çiçek", "Hemp": "Kenevir",
    "Orange Flower": "Turuncu Çiçek", "Red Flower": "Kırmızı Çiçek", "Violet Flower": "Mor Çiçek",
    "White Flower": "Ak Çiçek", "Yellow Flower": "Sarı Çiçek",
    "Ash Tree": "Dişbudak Ağacı", "Elm Tree": "Karaağaç", "Spruce Tree": "Ladin Ağacı",
    "Fish": "Balık", "Bread": "Ekmek", "Cheese": "Peynir",
    "Alchemy Table": "Simya Tezgâhı", "Blacksmith Forge": "Demirci Ocağı", "Cooking Cauldron": "Aş Kazanı",
    "Crafting Table": "Zanaat Tezgâhı", "Mail": "Posta Kutusu", "Candle": "Mum",
    "Storage Chest": "Sandık", "Treasure Chest": "Hazine Sandığı",
}


def translate_interactables():
    changed = 0
    for path in INTERACTABLE.rglob("*.prefab"):
        raw = path.read_bytes().decode("utf-8")
        t = re.sub(r"^(  interactableName: )(.*)$",
                   lambda m: m.group(1) + (q(ADLAR[m.group(2).strip()]) if m.group(2).strip() in ADLAR else m.group(2)),
                   raw, flags=re.M)
        t = re.sub(r"^(    interactionPanelTitle: )(Storage Chest)$", lambda m: m.group(1) + q("Sandık"), t, flags=re.M)
        if t != raw:
            path.write_bytes(t.encode("utf-8"))
            changed += 1
    print(f"{changed} etkileşim prefab'ının adı Türkçe")


# ---------------------------------------------------------------- 5. yerleştirme

def prefab_info():
    """prefab adı -> (guid, kök Transform, kök GameObject)"""
    info = {}
    for path in INTERACTABLE.rglob("*.prefab"):
        t = path.read_text(encoding="utf-8")
        g = re.search(r"^guid: (\w+)", Path(str(path) + ".meta").read_text(), re.M).group(1)
        for m in re.finditer(r"^--- !u!4 &(-?\d+)\n(.*?)(?=^--- )", t, re.M | re.S):
            if "m_Father: {fileID: 0}" in m.group(2):
                go = re.search(r"m_GameObject: \{fileID: (-?\d+)\}", m.group(2)).group(1)
                info[path.stem] = (g, m.group(1), go)
    return info


MINING_FIRE = ["MiningCopperNode", "MiningTinNode", "MiningIronNode", "MiningCoalNode", "MiningRedCrystalNode",
               "MiningGoldNode", "MiningYellowCrystalNode"]
MINING_ICE = ["MiningSilverNode", "MiningGoldNode", "MiningBlueCrystalNode", "MiningCyanCrystalNode",
              "MiningVioletCrystalNode", "MiningIronNode"]
HERBS = ["HerbalismRedFlowerNode", "HerbalismBlueFlowerNode", "HerbalismYellowFlowerNode", "HerbalismWhiteFlowerNode",
         "HerbalismGreenFlowerNode", "HerbalismVioletFlowerNode", "HerbalismOrangeFlowerNode", "HerbalismCyanFlowerNode",
         "HerbalismGrayFlowerNode", "HerbalismBrownFlowerNode", "HerbalismFlaxNode", "HerbalismHempNode"]
TREES = ["LoggingAshTreeNode", "LoggingElmTreeNode", "LoggingSpruceTreeNode"]
FOOD = ["BreadPickupNode", "CheesePickupNode"]

# köy duvarlarının içi: buraya bir şey koyma
VILLAGE = (-80.0, 265.0, -25.0, 70.0)

ZONE = dict(
    sahne=GAME / "Scenes/Content/FeaturesDemoZone/FeaturesDemoZone.unity",
    navmesh=GAME / "Scenes/Content/FeaturesDemoZone/FeaturesDemoZone/NavMesh-Navigation.asset",
    baslangic=(13.9, -33.8),
    sabit=[
        # (prefab, x, z, arama yarıçapı)  -- kampların ortası ve dağ doruklarındaki düzlükler
        ("HazineSandigi", 55, -72, 6), ("HazineSandigi", -25, -75, 6),
        ("HazineSandigi", 5, -160, 6), ("HazineSandigi", 320, 30, 6), ("HazineSandigi", 200, -110, 6),
        ("HazineSandigi", 220, 170, 6), ("HazineSandigi", -140, -170, 6),
        ("BuyukHazineSandigi", -320, -300, 6), ("BuyukHazineSandigi", 330, -300, 6),
        ("BuyukHazineSandigi", 330, 330, 6), ("BuyukHazineSandigi", -330, 330, 6),
        ("BuyukHazineSandigi", 18.5, 211.1, 4), ("BuyukHazineSandigi", -69.3, 151.9, 4),
        ("BuyukHazineSandigi", -220.2, 70.6, 4), ("BuyukHazineSandigi", -280.2, -51.7, 4),
        ("HazineSandigi", -217.6, 58.8, 4), ("HazineSandigi", 51.5, 155.0, 4),
        # yiyecek: kampların kenarında
        ("BreadPickupNode", 20, -48, 5), ("CheesePickupNode", 68, -60, 5), ("BreadPickupNode", -38, -62, 5),
        ("CheesePickupNode", 14, -150, 5), ("BreadPickupNode", 310, 40, 5), ("CheesePickupNode", 192, -100, 5),
        ("BreadPickupNode", 230, 160, 5), ("CheesePickupNode", -130, -160, 5),
    ],
    # (prefab listesi, adet, bölge x0, x1, z0, z1, en düşük y, en yüksek y, aralık)
    dagitim=[
        (MINING_FIRE, 9, -95, 95, 78, 235, 6, 80, 14),
        (MINING_ICE, 8, -345, -150, -115, 135, 8, 80, 14),
        (HERBS, 8, -260, 300, -290, -40, -1, 2, 25),
        (HERBS, 4, 270, 460, -120, 260, -1, 2, 25),
        (HERBS, 4, 90, 320, 90, 320, -1, 2, 25),
        (HERBS, 4, -460, -120, 140, 420, -1, 2, 25),
        (TREES, 6, -200, -95, -10, 140, -1, 4, 12),
        (TREES, 3, 280, 420, 120, 300, -1, 2, 12),
    ],
)

DUNGEON = dict(
    sahne=GAME / "Scenes/Content/FeaturesDemoDungeon/FeaturesDemoDungeon.unity",
    navmesh=GAME / "Scenes/Content/FeaturesDemoDungeon/FeaturesDemoDungeon/NavMesh-Navigation.asset",
    baslangic=(86.0, 90.0),
    sabit=[("BuyukHazineSandigi", 15, 30, 4), ("HazineSandigi", 110, 15, 4)],
    dagitim=[],
)


def remove_generated(text):
    parts = re.split(r"(?=^--- !u!)", text, flags=re.M)
    removed = set()
    for p in parts:
        m = re.match(r"--- !u!1001 &(\d+)\n", p)
        if m and re.search(r"propertyPath: m_Name\n      value: " + PREFIX, p):
            removed.add(m.group(1))
    gone_transforms = set()
    keep = []
    for p in parts:
        m = re.match(r"--- !u!1001 &(\d+)\n", p)
        if m and m.group(1) in removed:
            continue
        m = re.match(r"--- !u!4 &(\d+) stripped\n", p)
        if m:
            pi = re.search(r"m_PrefabInstance: \{fileID: (\d+)\}", p)
            if pi and pi.group(1) in removed:
                gone_transforms.add(m.group(1))
                continue
        keep.append(p)
    text = "".join(keep)
    for t in gone_transforms:
        text = text.replace(f"  - {{fileID: {t}}}\n", "")
    return text, len(removed)


def instance_doc(name, prefab, x, y, z, yaw, info, used):
    pguid, root_tr, root_go = info[prefab]
    iid, tid = yaratiklar.file_id(name, "li"), None
    if str(iid) in used:
        raise ValueError("fileID çakışması " + name)
    used.add(str(iid))
    tid = (iid ^ int(root_tr)) & MASK
    if str(tid) in used:
        raise ValueError("fileID çakışması " + name)
    used.add(str(tid))

    def mod(target, path, value):
        return (f"    - target: {{fileID: {target}, guid: {pguid},\n        type: 3}}\n"
                f"      propertyPath: {path}\n      value: {value}\n      objectReference: {{fileID: 0}}\n")
    r = math.radians(yaw)
    mods = "".join([
        mod(root_tr, "m_LocalPosition.x", round(x, 3)), mod(root_tr, "m_LocalPosition.y", round(y, 3)),
        mod(root_tr, "m_LocalPosition.z", round(z, 3)),
        mod(root_tr, "m_LocalRotation.w", round(math.cos(r / 2), 5)), mod(root_tr, "m_LocalRotation.x", 0),
        mod(root_tr, "m_LocalRotation.y", round(math.sin(r / 2), 5)), mod(root_tr, "m_LocalRotation.z", 0),
        mod(root_tr, "m_LocalEulerAnglesHint.y", round(yaw, 1)),
        mod(root_go, "m_Name", name),
    ])
    doc = (f"--- !u!1001 &{iid}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n"
           f"    serializedVersion: 3\n    m_TransformParent: {{fileID: 0}}\n    m_Modifications:\n{mods}"
           "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
           f"  m_SourcePrefab: {{fileID: 100100000, guid: {pguid}, type: 3}}\n"
           f"--- !u!4 &{tid} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {root_tr}, guid: {pguid},\n"
           f"    type: 3}}\n  m_PrefabInstance: {{fileID: {iid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return doc, tid


def enemy_points(text):
    pts = []
    for m in re.finditer(r"--- !u!1001 &\d+\n(.*?)(?=^--- )", text, re.M | re.S):
        body = m.group(1)
        if "unitProfileNames" not in body:
            continue
        x = re.search(r"propertyPath: m_LocalPosition.x\n      value: (.*)", body)
        z = re.search(r"propertyPath: m_LocalPosition.z\n      value: (.*)", body)
        if x and z and "m_TransformParent: {fileID: 0}" in body:
            pts.append((float(x.group(1)), float(z.group(1))))
    return pts


def place(scene, info):
    nm = NavMesh(str(scene["navmesh"]), scene["baslangic"])
    path = scene["sahne"]
    text = path.read_bytes().decode("utf-8")
    assert "\r\n" not in text
    text, removed = remove_generated(text)
    used = set(re.findall(r"&(-?\d+)", text))
    taken = [(p[0], p[1], 6.0) for p in enemy_points(text)]  # düşman noktalarının dibine koyma
    docs, roots, problems, counts = [], [], [], {}

    def add(prefab, x, y, z):
        counts[prefab] = counts.get(prefab, 0) + 1
        name = f"{PREFIX}{prefab}_{counts[prefab]}"
        yaw = int(hashlib.md5(name.encode()).hexdigest()[:4], 16) % 360
        doc, tid = instance_doc(name, prefab, x, y, z, yaw, info, used)
        docs.append(doc)
        roots.append(tid)
        taken.append((x, z, 3.0))

    for prefab, x, z, search in scene["sabit"]:
        spot = yaratiklar.find_spot(nm, x, z, search)
        if spot is None:
            problems.append(f"  YER YOK: {prefab} ({x}, {z})")
            continue
        add(prefab, *spot)

    def free(x, z, spacing):
        if VILLAGE[0] < x < VILLAGE[1] and VILLAGE[2] < z < VILLAGE[3]:
            return False
        return all(math.hypot(x - tx, z - tz) >= max(spacing, r) for tx, tz, r in taken)

    for k, (prefabs, count, x0, x1, z0, z1, y0, y1, spacing) in enumerate(scene["dagitim"]):
        candidates = []
        for gx in range(int(x0), int(x1) + 1, 4):
            for gz in range(int(z0), int(z1) + 1, 4):
                h = nm.height(gx, gz)
                if h is None or not (y0 <= h <= y1):
                    continue
                key = hashlib.md5(f"{k}:{gx}:{gz}".encode()).hexdigest()
                candidates.append((key, gx, gz))
        candidates.sort()
        placed = 0
        for _, gx, gz in candidates:
            if placed >= count:
                break
            if not free(gx, gz, spacing) or not nm.slope_ok(gx, gz, radius=1.2, max_step=1.0):
                continue
            add(prefabs[(placed + 5 * k) % len(prefabs)], gx, nm.height(gx, gz), gz)
            placed += 1
        if placed < count:
            problems.append(f"  AZ YER: dağıtım {k} {placed}/{count}")

    i = text.index("--- !u!1660057539 &9223372036854775807\nSceneRoots:")
    text = text[:i] + "".join(docs) + text[i:]
    text += "".join(f"  - {{fileID: {t}}}\n" for t in roots)
    path.write_bytes(text.encode("utf-8"))
    print(f"{path.name}: {removed} eski düğüm silindi, {len(docs)} ganimet noktası eklendi: "
          + ", ".join(f"{k} {v}" for k, v in sorted(counts.items())))
    for p in problems:
        print(p)
    return len(problems)


def main():
    write_purses()
    write_loot_tables(item_names())
    write_chest_prefabs()
    translate_interactables()
    info = prefab_info()
    problems = place(ZONE, info) + place(DUNGEON, info)
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
