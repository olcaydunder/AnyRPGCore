#!/usr/bin/env python3
"""Ötüken Yaylası ve Erlik'in Mağarası'na düşmanlar yerleştirir.

1. DUSMANLAR: her düşman türü için bir UnitProfile dosyası yazar (Bozok birliklerinden kopyalanır,
   Erlik'in Ordusu'na geçirilir, kara zırhlı model, Türkçe ad, ganimet tablosu).
2. KAMPLAR: her sahneye noktasal UnitSpawnNode örnekleri ekler. Her nokta sahnenin NavMesh'i
   üzerinde, oyuncunun yürüyerek ulaşabildiği yüzeyde ve düz bir yerde mi diye denetlenir.

Betik tekrar tekrar çalıştırılabilir: önceki çalıştırmanın eklediği "TR_" ile başlayan düğümleri
silip yeniden yazar, profil dosyalarının GUID'leri addan türetildiği için değişmez.

Kullanım (depo kök klasöründen):  python3 "Tools~/dunya/yaratiklar.py"
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
from canavarlar import model_references  # noqa: E402
import iskeletler  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame"
PROFILES = GAME / "Resources/FeaturesDemoGame/UnitProfile"
OUT = PROFILES / "Dusman"
NS = uuid.UUID("5b1c9a2e-7f4d-4c1e-9a77-0e7d2a6b9c31")

# kara zirhli modeller: (prefab guid, kok GameObject fileID)
MODEL = {
    "erkek_hafif": ("523713b97e3438f47b3da4d7a43d12a6", 2786600217745055141),
    "erkek_orta": ("385458e9db12c0d4ba25148d285f3ac1", 7270982331284104345),
    "erkek_agir": ("d270c0661886a1246b4578ea1772d3c6", 8136326391836591863),
    "kadin_orta": ("51ca60a801a611b48ab9c6fa7b1d37d1", 6954704190252621995),
    "kadin_agir": ("c6ec7af6705e52949a16b9e2d25d6c63", 6954704190252621995),
}
# Quaternius canavarları (canavarlar.py): renk çeşidi prefab'ı -> (guid, kök GameObject fileID)
MODEL.update({f"canavar:{ad}": ref for ad, ref in model_references().items()})
# KayKit iskeletleri (iskeletler.py): tür prefab'ı (silahları takılı) -> (guid, kök GameObject fileID)
MODEL.update({f"iskelet:{ad}": ref for ad, ref in iskeletler.model_references().items()})

DUSMANLAR = {
    "Sulmus": dict(
        ad="Şulmus", sablon="BlueThiefUnit", model="kadin_orta", ses="Female Low", saldirganlik=14,
        aciklama="Erlik'in kurnaz cinleri. Gölgeden fırlayıp iki hançerle saldırır, sonra yeniden kaybolur.",
        ganimet=["Potions", "Gray Leather Armor", "Basic Copper Weapons", "Necklaces"]),
    "Yagmaci Okcu": dict(
        ad="Yağmacı Okçu", sablon="BlueArcherUnit", model="erkek_hafif", ses="Male Townsperson", saldirganlik=20,
        aciklama="Obaları basıp sürüleri kaçıran yağmacıların keskin gözlü okçusu.",
        ganimet=["Potions", "Brown Leather Armor", "Basic Bronze Weapons", "Bags"]),
    "Yagmaci Basi": dict(
        ad="Yağmacı Başı", sablon="BlueMarksmanUnit", model="erkek_orta", ses="Male Guard", saldirganlik=20,
        dayaniklilik="Solo Dungeon Minion",
        aciklama="Yağmacıların acımasız başı. Yayı yerine ağır bir arbalet taşır.",
        ganimet=["Potions", "Green Leather Armor", "Random Medieval Weapons", "Bags", "Necklaces"]),
    "Kara Kam": dict(
        ad="Kara Kam", sablon="BlueFireMageUnit", model="erkek_hafif", ses="Male Guard", saldirganlik=22,
        aciklama="Ruhunu Erlik'e satmış kam. Ateş çağırır, alevlerle yakar.",
        ganimet=["Potions", "Scrolls", "Red Cloth Armor", "Necklaces"]),
    "Kara Otaci": dict(
        ad="Kara Otacı", sablon="BluePriestUnit", model="kadin_orta", ses="Female Low", saldirganlik=18,
        aciklama="Yaralı kara ruhları iyileştiren karanlık şifacı. Önce onu düşür!",
        ganimet=["Potions", "Scrolls", "Violet Cloth Armor"]),
    "Abasi": dict(
        ad="Abası", sablon="BlueWarriorUnit", model="erkek_agir", ses="Male Knight", saldirganlik=15,
        dayaniklilik="Solo Dungeon Minion",
        aciklama="Yeraltından çıkan ağır zırhlı kötü ruh. Koca baltasıyla tek vuruşta yere serer.",
        ganimet=["Potions", "Iron Plate Armor", "Basic Iron Weapons"]),
    "Kan Suvarisi": dict(
        ad="Kan Süvarisi", sablon="BlueBloodKnightUnit", model="erkek_agir", ses="Male Knight", saldirganlik=18,
        dayaniklilik="Solo Dungeon Minion",
        aciklama="Erlik'in kan içen süvarisi. Yaralandıkça daha da azgınlaşır.",
        ganimet=["Potions", "Red Plate Armor", "Basic Steel Weapons", "Random Medieval Weapons"]),
    "Kiragi Cadisi": dict(
        ad="Kırağı Cadısı", sablon="BlueIceMageUnit", model="kadin_agir", ses="Female Medium Ice Mage", saldirganlik=22,
        aciklama="Buz Dağı'nın ayazını kana bulayan cadı. Buz okları ve tipiyle saldırır.",
        ganimet=["Potions", "Scrolls", "Blue Cloth Armor", "Necklaces"]),
    "Buz Bekcisi": dict(
        ad="Buz Bekçisi", sablon="BlueKnightUnit", model="erkek_agir", ses="Male Pikeman", saldirganlik=15,
        dayaniklilik="Solo Dungeon Minion",
        aciklama="Buz Dağı'nın geçitlerini kalkanıyla tutan bekçi.",
        ganimet=["Potions", "Silver Plate Armor", "Basic Silver Weapons"]),
    "Yelbegen": dict(
        ad="Yelbegen", sablon="BlueBossUnit", model="erkek_agir", ses="Male Hero", saldirganlik=20,
        dayaniklilik="2 Man",
        aciklama="Yedi başlı dev. Buz Dağı'nın doruğunda yolunu kaybedenleri bekler; yere vurduğunda dağ titrer.",
        ganimet=["Potions", "Blue Plate Armor", "Epic Medieval Weapons", "Necklaces", "Bags"]),
    # ---- Quaternius canavarları: insan iskeletli, kendi silahları modelin parçası; sınıf eşyası giymezler
    "Kormos": dict(
        ad="Körmös", sablon="BlueFighterUnit", model="canavar:KormosKizil", ses="Male Guard", saldirganlik=16,
        canavar=dict(animasyon="Kormos"),
        aciklama="Erlik Han'ın yeraltındaki kölesi. Zincirli gürzüyle saldırır; gözleri karanlıkta kor gibi yanar.",
        ganimet=["Potions", "Scrolls", "Necklaces"]),
    "Agulu Kormos": dict(
        ad="Ağulu Körmös", sablon="BlueFighterUnit", model="canavar:KormosYesil", ses="Male Guard", saldirganlik=16,
        canavar=dict(animasyon="Kormos"),
        aciklama="Bataklıklarda dolaşan körmös. Gürzünü ağuya bulamıştır; vuruşu acıtır.",
        ganimet=["Potions", "Bags"]),
    "Ayaz Kormosu": dict(
        ad="Ayaz Körmösü", sablon="BlueFighterUnit", model="canavar:KormosMavi", ses="Male Pikeman", saldirganlik=18,
        dayaniklilik="Solo Dungeon Minion", canavar=dict(olcek=1.1, animasyon="Kormos"),
        aciklama="Buz Dağı'nın ayazında katılaşmış körmös. Sıradan körmöslerden iri ve dayanıklıdır.",
        ganimet=["Potions", "Scrolls", "Random Medieval Weapons"]),
    "Cali Cini": dict(
        ad="Çalı Cini", sablon="BlueFighterUnit", model="canavar:CinYesil", ses="Female High", saldirganlik=12,
        canavar=dict(kucuk=True, animasyon="Cin"),
        aciklama="Çalıların arasında yaşayan cüce cin. Tek başına zayıftır ama hep sürüyle gezer.",
        ganimet=["Potions"]),
    "Kizil Cin": dict(
        ad="Kızıl Cin", sablon="BlueFighterUnit", model="canavar:CinKizil", ses="Female High", saldirganlik=16,
        canavar=dict(kucuk=True, animasyon="Cin"),
        aciklama="Ateş Dağı'nın sıcak kayalıklarında kaynaşan kızıl cinler. Sopalarını közde sertleştirirler.",
        ganimet=["Potions", "Bags"]),
    "Buz Cini": dict(
        ad="Buz Cini", sablon="BlueFighterUnit", model="canavar:CinMavi", ses="Female High", saldirganlik=16,
        canavar=dict(kucuk=True, animasyon="Cin"),
        aciklama="Karların altında yuva yapan mavi cin. Yolcuların azığını çalar, peşine düşeni dövmeye kalkar.",
        ganimet=["Potions", "Scrolls"]),
    # ---- KayKit iskeletleri: kendi (Generic) animasyonları ve ayrı silah modelleri var; sınıfsızdırlar
    #      (sınıf yetenekleri insan animasyonu oynatır, iskelette oynamaz), yalnız silahlarıyla vururlar
    "Kemik Er": dict(
        ad="Kemik Er", sablon="BlueFighterUnit", model="iskelet:KemikEr", ses="Male Guard", saldirganlik=16,
        iskelet=dict(olcek=0.85),
        aciklama="Erlik Han'ın eski kurganlardan kaldırdığı ölü er. Paslı kılıcı ve küçük kalkanıyla yolu keser; "
                 "yere serilince kemikleri dağılır ama bir gün yine dirilir.",
        ganimet=["Potions", "Basic Copper Weapons"]),
    "Kemik Alp": dict(
        ad="Kemik Alp", sablon="BlueFighterUnit", model="iskelet:KemikAlp", ses="Male Knight", saldirganlik=18,
        dayaniklilik="Solo Dungeon Minion", iskelet=dict(olcek=0.95),
        aciklama="Adı bir zamanlar destanlarda geçen bir alpın kemikleri. Boynuzlu miğferi, iri kalkanı ve "
                 "baltasıyla ölü erleri yönetir.",
        ganimet=["Potions", "Basic Iron Weapons", "Necklaces"]),
    "Kemik Akinci": dict(
        ad="Kemik Akıncı", sablon="BlueFighterUnit", model="iskelet:KemikAkinci", ses="Male Pikeman", saldirganlik=18,
        iskelet=dict(olcek=0.85),
        aciklama="Kızıl kukuletalı ölü akıncı. İki kılıcıyla hızlı ve art arda vurur.",
        ganimet=["Potions", "Random Medieval Weapons", "Bags"]),
    "Kemik Kam": dict(
        ad="Kemik Kam", sablon="BlueFighterUnit", model="iskelet:KemikKam", ses="Male Guard", saldirganlik=16,
        iskelet=dict(olcek=0.85),
        aciklama="Erlik'e kul olmuş ölü kam. Kafatası başlıklı asasıyla vurur; kurganlardaki ölüleri onun "
                 "kaldırdığı söylenir.",
        ganimet=["Potions", "Scrolls", "Necklaces"]),
    "Ulu Evren": dict(
        ad="Ulu Evren", sablon="DragonUnit", model=None, ses=None, saldirganlik=25,
        dayaniklilik="Solo Dungeon Boss",
        aciklama="Ateş Dağı'nın doruğunda uyuyan kadim ejderha. Nefesi lavdan sıcaktır.",
        ganimet=["Potions", "Gold Plate Armor", "Epic Medieval Weapons", "Epic Medieval Weapon Recipes", "Necklaces"]),
}

ORTAK_GANIMET = ["Bozkir Ganimeti"]
ULU_DAYANIKLILIK = {"Solo Dungeon Boss", "2 Man"}

# kamp: (ad, merkez x, merkez z, [(düşman, adet)], ek seviye)   -- düz arazide çember üzerine dizilir
# nokta listesi: (ad, [(düşman, x, z)], ek seviye)                -- dağlarda tek tek seçilmiş düzlükler
ZONE = dict(
    sahne=GAME / "Scenes/Content/FeaturesDemoZone/FeaturesDemoZone.unity",
    navmesh=GAME / "Scenes/Content/FeaturesDemoZone/FeaturesDemoZone/NavMesh-Navigation.asset",
    baslangic=(13.9, -33.8),
    kamplar=[
        ("YagmaciObasi", 5, -160, [("Yagmaci Okcu", 3), ("Yagmaci Basi", 1)], 1),
        ("SulmusKorusuDogu", 320, 30, [("Sulmus", 3)], 1),
        ("SulmusKorusuGuney", 200, -110, [("Sulmus", 2), ("Kara Otaci", 1)], 1),
        ("KuzeyOrmani", 220, 170, [("Yagmaci Okcu", 2), ("Kara Yek", 2)], 1),
        ("BatiCayiri", -140, -170, [("Kara Yek", 3)], 1),
        ("UzakGuneybati", -320, -300, [("Sulmus", 3)], 2),
        ("UzakGuneydogu", 330, -300, [("Yagmaci Okcu", 2), ("Yagmaci Basi", 1)], 2),
        ("UzakKuzeydogu", 330, 330, [("Kara Kam", 2), ("Abasi", 1)], 2),
        ("UzakKuzeybati", -330, 330, [("Kara Yek", 2), ("Kara Otaci", 1)], 2),
        # Quaternius canavarları
        ("CaliCiniCayiriDogu", 140, -150, [("Cali Cini", 3)], 0),
        ("CaliCiniCayiriBati", -80, -130, [("Cali Cini", 3)], 0),
        ("AguluKormosBatakligi", 260, -210, [("Agulu Kormos", 2), ("Cali Cini", 1)], 1),
        ("AtesDagiKormosIni", -95, 150, [("Kormos", 2), ("Kizil Cin", 1)], 2),
        ("AtesDagiKizilCinler", 100, 175, [("Kizil Cin", 2), ("Kormos", 1)], 2),
        ("BuzDagiCinleri", -190, -40, [("Buz Cini", 2), ("Ayaz Kormosu", 1)], 2),
        ("BuzDagiAyazKormosleri", -175, 70, [("Ayaz Kormosu", 2), ("Buz Cini", 1)], 3),
        # KayKit iskeletleri: eski kurganlar
        ("KemikKurganiKuzey", 150, 300, [("Kemik Er", 2), ("Kemik Akinci", 1), ("Kemik Kam", 1)], 2),
        ("KemikKurganiGuney", -150, -260, [("Kemik Er", 2), ("Kemik Alp", 1)], 1),
    ],
    noktalar=[
        # Ateş Dağı (köyün kuzeyi)
        ("AtesDagiEtek", [("Kara Kam", -38.0, 91.7), ("Kara Kam", -72.8, 106.0), ("Abasi", 30.27, 97.17)], 2),
        ("AtesDagiYamac", [("Abasi", 12.25, 117.8), ("Kan Suvarisi", -11.1, 133.2), ("Kara Otaci", -13.6, 126.7)], 2),
        ("AtesDagiSirt", [("Kara Kam", 10.3, 162.2), ("Kara Kam", 13.67, 155.67), ("Kan Suvarisi", 39.3, 175.8),
                          ("Abasi", 51.5, 150.6)], 3),
        ("AtesDagiDoruk", [("Ulu Evren", 4.1, 224.2)], 3),
        # Buz Dağı (batı)
        ("BuzDagiEtek", [("Kiragi Cadisi", -300.6, -59.7), ("Kiragi Cadisi", -278.7, -63.7),
                         ("Buz Bekcisi", -264.6, -50.1)], 2),
        ("BuzDagiKuzey", [("Kiragi Cadisi", -303.3, 73.9), ("Kiragi Cadisi", -309.4, 66.0),
                          ("Buz Bekcisi", -317.4, -19.1)], 2),
        ("BuzDagiGecit", [("Buz Bekcisi", -242.2, 22.4), ("Buz Bekcisi", -224.3, 28.6),
                          ("Kiragi Cadisi", -258.1, 24.0)], 3),
        ("BuzDagiDoruk", [("Yelbegen", -217.6, 66.8), ("Kiragi Cadisi", -218.8, 76.6)], 3),
    ],
)

DUNGEON = dict(
    sahne=GAME / "Scenes/Content/FeaturesDemoDungeon/FeaturesDemoDungeon.unity",
    navmesh=GAME / "Scenes/Content/FeaturesDemoDungeon/FeaturesDemoDungeon/NavMesh-Navigation.asset",
    baslangic=(86.0, 90.0),
    kamplar=[],
    noktalar=[
        ("MagaraGiris", [("Abasi", 80, 52), ("Sulmus", 92, 52)], 1),
        ("MagaraOrta", [("Kara Kam", 86, 10), ("Kara Otaci", 86, -15)], 1),
        ("MagaraSagOda", [("Sulmus", 110, 45), ("Sulmus", 110, -15)], 1),
        ("MagaraKormosleri", [("Kormos", 70, 0), ("Kormos", 100, 20), ("Kormos", 60, 40)], 1),
        ("MagaraKemikSalonu", [("Kemik Er", 8, 35), ("Kemik Er", 8, -15), ("Kemik Kam", 0, 10),
                               ("Kemik Alp", 12, 10)], 1),
        ("MagaraDoguKemikleri", [("Kemik Akinci", 150, 15), ("Kemik Akinci", 165, 20)], 1),
    ],
)

SPAWN_PREFAB = "7ff64ca283808244380f334a006b354f"
SP_TRANSFORM = "2775317982054893304"
SP_GAMEOBJECT = "6390993854296471015"
SP_SCRIPT = "3481425691641389535"
PREFIX = "TR_"

q = lambda s: json.dumps(s, ensure_ascii=True)


def stable_guid(name):
    return uuid.uuid5(NS, "profil:" + name).hex


def write_profiles():
    OUT.mkdir(exist_ok=True)
    folder_meta = OUT.parent / (OUT.name + ".meta")
    if not folder_meta.exists():
        folder_meta.write_text(
            f"fileFormatVersion: 2\nguid: {stable_guid('klasor:Dusman')}\nfolderAsset: yes\nDefaultImporter:\n"
            "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    loot_props = (PROFILES / "EnemyMinionUnit.asset").read_text(encoding="utf-8")
    loot_block = loot_props[loot_props.index("    - rid: 0\n      type: {class: LootableCharacterProps"):]
    for key, d in DUSMANLAR.items():
        src = PROFILES / (d["sablon"] + ".asset")
        t = src.read_text(encoding="utf-8")
        assert "\r\n" not in t

        def sub(pattern, repl, text):
            n = len(re.findall(pattern, text, flags=re.M))
            if n != 1:
                raise ValueError(f"{key}: {pattern} {n} kez bulundu")
            return re.sub(pattern, lambda m: repl, text, flags=re.M)

        file_name = re.sub(r"[^A-Za-z]", "", key) + "Unit"
        t = sub(r"^  m_Name: .*$", f"  m_Name: {file_name}", t)
        t = sub(r"^  resourceName: .*$", f"  resourceName: {key}", t)
        t = sub(r"^  displayName: .*$", f"  displayName: {q(d['ad'])}", t)
        t = sub(r"^  description: .*$", f"  description: {q(d['aciklama'])}", t)
        t = sub(r"^  characterName: .*$", f"  characterName: {q(d['ad'])}", t)
        t = sub(r"^  factionName: .*$", "  factionName: Enemy", t)
        t = sub(r"^  characterRaceName: .*$", "  characterRaceName: ", t)
        t = sub(r"^  defaultToughness: .*$", f"  defaultToughness: {d.get('dayaniklilik', '')}", t)
        t = sub(r"^  isAggressive: .*$", "  isAggressive: 1", t)
        t = sub(r"^  aggroRadius: .*$", f"  aggroRadius: {d['saldirganlik']}", t)
        t = sub(r"^  m_UUID: .*$", f"  m_UUID: {uuid.uuid5(NS, 'uuid:' + key)}", t)
        if d["model"]:
            guid, fid = MODEL[d["model"]]
            t = sub(r"^    modelPrefab: \{fileID: -?\d+, guid: \w+,\n      type: 3\}",
                    f"    modelPrefab: {{fileID: {fid}, guid: {guid},\n      type: 3}}", t)
        if d["ses"]:
            t = sub(r"^  voiceProfile: .*$", f"  voiceProfile: {d['ses']}", t)
        if "canavar" in d:
            c = d["canavar"]
            # sınıf eşyası (zırh parçaları, pençe) bu modellere uymaz: giydirme
            t = sub(r"^  useProviderEquipment: 1$", "  useProviderEquipment: 0", t)
            # Unreal mankeni kemik adları
            t, n = re.subn(r"^(      unitFrameTarget: )head$", lambda m: m.group(1) + "Head", t, flags=re.M)
            assert n == 2, key
            t = sub(r"^    floatTransform: Spine1$", "    floatTransform: spine_02", t)
            if c.get("animasyon"):
                # Universal Animation Library profilleri (animasyonlar.py)
                t = sub(r"^    animationProfileName: $", f"    animationProfileName: {c['animasyon']}", t)
            if c.get("olcek"):
                s = c["olcek"]
                t = sub(r"^    scale: \{x: 1, y: 1, z: 1\}$", f"    scale: {{x: {s}, y: {s}, z: {s}}}", t)
            if c.get("kucuk"):
                # boyu bir metre kadar: ad levhası ve portre kamerası aşağıda
                t = sub(r"^      overrideNameplatePosition: 0$", "      overrideNameplatePosition: 1", t)
                t = sub(r"^      namePlatePosition: \{x: 0, y: 0, z: 0\}$", "      namePlatePosition: {x: 0, y: 1.4, z: 0}", t)
                t, n = re.subn(r"^(      unitPreviewCameraLookOffset: )\{x: 0, y: 1, z: 0\}$",
                               lambda m: m.group(1) + "{x: 0, y: 0.55, z: 0}", t, flags=re.M)
                assert n == 2, key
                t, n = re.subn(r"^(      unitPreviewCameraPositionOffset: )\{x: 0, y: 1, z: 2.5\}$",
                               lambda m: m.group(1) + "{x: 0, y: 0.6, z: 1.6}", t, flags=re.M)
                assert n == 2, key
        if "iskelet" in d:
            c = d["iskelet"]
            t = sub(r"^  useProviderEquipment: 1$", "  useProviderEquipment: 0", t)
            # sınıf yok: Fighter'ın "Punch Combo" yeteneği insan animasyonu oynatır, Generic iskelette oynamaz
            t = sub(r"^  characterClassName: .*$", "  characterClassName: ", t)
            t = sub(r"^    floatTransform: Spine1$", "    floatTransform: chest", t)
            profil = iskeletler.animation_profiles()[d["model"].split(":", 1)[1]]
            t = sub(r"^    animationProfileName: $", f"    animationProfileName: {profil}", t)
            s = c["olcek"]
            t = sub(r"^    scale: \{x: 1, y: 1, z: 1\}$", f"    scale: {{x: {s}, y: {s}, z: {s}}}", t)
        # ölünce üstünden ganimet alınabilsin
        t = sub(r"^  inlineInteractableOptions: \[\]$", "  inlineInteractableOptions:\n  - rid: 0", t)
        # her düşmana bozkır ganimeti (ganimet.py), boss'lara ayrıca ulu ganimet
        tablolar = d["ganimet"] + ORTAK_GANIMET + (["Ulu Ganimet"] if d.get("dayaniklilik") in ULU_DAYANIKLILIK else [])
        lb = re.sub(r"(        lootTableNames:\n)(?:        - .*\n)+",
                    lambda m: m.group(1) + "".join(f"        - {name}\n" for name in tablolar), loot_block)
        assert t.endswith("      type: {class: , ns: , asm: }\n"), key
        t = t + lb
        (OUT / (file_name + ".asset")).write_text(t, encoding="utf-8")
        meta = (OUT / (file_name + ".asset.meta"))
        meta.write_text(
            f"fileFormatVersion: 2\nguid: {stable_guid(key)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    print(f"{len(DUSMANLAR)} düşman profili yazıldı")


def file_id(name, salt):
    h = int(hashlib.md5((salt + name).encode()).hexdigest()[:12], 16)
    return 1000000000 + h % 1100000000


def spawn_doc(name, x, y, z, yaw, profile, extra_levels, respawn, used):
    # a stripped object inside a prefab instance has the fileID (instance fileID XOR source fileID), as Unity computes it
    iid = file_id(name, "i")
    tid = (iid ^ int(SP_TRANSFORM)) & 0x7FFFFFFFFFFFFFFF
    for v in (iid, tid):
        if str(v) in used:
            raise ValueError(f"fileID çakışması: {name}")
        used.add(str(v))

    def mod(target, path, value):
        return (f"    - target: {{fileID: {target}, guid: {SPAWN_PREFAB},\n        type: 3}}\n"
                f"      propertyPath: {path}\n      value: {value}\n      objectReference: {{fileID: 0}}\n")

    r = math.radians(yaw)
    mods = "".join([
        mod(SP_TRANSFORM, "m_LocalPosition.x", round(x, 3)),
        mod(SP_TRANSFORM, "m_LocalPosition.y", round(y, 3)),
        mod(SP_TRANSFORM, "m_LocalPosition.z", round(z, 3)),
        mod(SP_TRANSFORM, "m_LocalRotation.w", round(math.cos(r / 2), 5)),
        mod(SP_TRANSFORM, "m_LocalRotation.x", 0),
        mod(SP_TRANSFORM, "m_LocalRotation.y", round(math.sin(r / 2), 5)),
        mod(SP_TRANSFORM, "m_LocalRotation.z", 0),
        mod(SP_TRANSFORM, "m_LocalEulerAnglesHint.y", round(yaw, 1)),
        mod(SP_SCRIPT, "unitProfileNames.Array.size", 1),
        mod(SP_SCRIPT, "'unitProfileNames.Array.data[0]'", profile),
        mod(SP_SCRIPT, "extraLevels", extra_levels),
        mod(SP_SCRIPT, "respawnTimer", respawn),
        mod(SP_GAMEOBJECT, "m_Name", name),
    ])
    doc = (f"--- !u!1001 &{iid}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n"
           f"    serializedVersion: 3\n    m_TransformParent: {{fileID: 0}}\n    m_Modifications:\n{mods}"
           "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
           f"  m_SourcePrefab: {{fileID: 100100000, guid: {SPAWN_PREFAB}, type: 3}}\n"
           f"--- !u!4 &{tid} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {SP_TRANSFORM}, guid: {SPAWN_PREFAB},\n"
           f"    type: 3}}\n  m_PrefabInstance: {{fileID: {iid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return doc, tid


def find_spot(nm, x, z, max_search=6.0):
    """(x, z) yakınında ulaşılabilir, düz bir nokta"""
    for radius in [0.0, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0]:
        if radius > max_search:
            break
        steps = 1 if radius == 0 else 12
        for k in range(steps):
            a = 2 * math.pi * k / steps
            px, pz = x + radius * math.cos(a), z + radius * math.sin(a)
            if nm.slope_ok(px, pz, radius=1.2, max_step=1.0):
                return px, nm.height(px, pz), pz
    return None


def remove_generated(text):
    """önceki çalıştırmanın TR_ düğümlerini (örnek + stripped transform + kök kaydı) sil"""
    parts = re.split(r"(?=^--- !u!)", text, flags=re.M)
    removed_instances = set()
    for p in parts:
        m = re.match(r"--- !u!1001 &(\d+)\n", p)
        if m and re.search(r"propertyPath: m_Name\n      value: " + PREFIX, p):
            removed_instances.add(m.group(1))
    removed_transforms = set()
    keep = []
    for p in parts:
        m = re.match(r"--- !u!1001 &(\d+)\n", p)
        if m and m.group(1) in removed_instances:
            continue
        m = re.match(r"--- !u!4 &(\d+) stripped\n", p)
        if m:
            pi = re.search(r"m_PrefabInstance: \{fileID: (\d+)\}", p)
            if pi and pi.group(1) in removed_instances:
                removed_transforms.add(m.group(1))
                continue
        keep.append(p)
    text = "".join(keep)
    for t in removed_transforms:
        text = text.replace(f"  - {{fileID: {t}}}\n", "")
    return text, len(removed_instances)


def place(scene, profiles_known):
    nm = NavMesh(str(scene["navmesh"]), scene["baslangic"])
    path = scene["sahne"]
    text = path.read_bytes().decode("utf-8")
    assert "\r\n" not in text
    text, removed = remove_generated(text)
    used = set(re.findall(r"&(-?\d+)", text))
    docs, roots, report = [], [], []
    rnd_yaw = lambda name: int(hashlib.md5(name.encode()).hexdigest()[:4], 16) % 360

    def add(name, profile, x, z, levels, search):
        if profile not in profiles_known:
            raise ValueError(f"bilinmeyen düşman: {profile}")
        spot = find_spot(nm, x, z, search)
        if spot is None:
            report.append(f"  YER YOK: {name} ({x:.1f}, {z:.1f})")
            return
        px, py, pz = spot
        boss = profile in ("Ulu Evren", "Yelbegen")
        doc, tid = spawn_doc(name, px, py, pz, rnd_yaw(name), profile, levels, 600 if boss else 90, used)
        docs.append(doc)
        roots.append(tid)

    for camp, cx, cz, members, levels in scene["kamplar"]:
        units = [m for m, n in members for _ in range(n)]
        for k, profile in enumerate(units):
            a = 2 * math.pi * k / len(units) + 0.4
            r = 5.0 if len(units) > 1 else 0.0
            add(f"{PREFIX}{camp}_{k + 1}", profile, cx + r * math.cos(a), cz + r * math.sin(a), levels, 6.0)
    for camp, members, levels in scene["noktalar"]:
        for k, (profile, x, z) in enumerate(members):
            add(f"{PREFIX}{camp}_{k + 1}", profile, x, z, levels, 4.0)

    i = text.index("--- !u!1660057539 &9223372036854775807\nSceneRoots:")
    text = text[:i] + "".join(docs) + text[i:]
    if not text.endswith("\n"):
        text += "\n"
    text += "".join(f"  - {{fileID: {t}}}\n" for t in roots)
    path.write_bytes(text.encode("utf-8"))
    print(f"{path.name}: {removed} eski düğüm silindi, {len(docs)} düşman noktası eklendi")
    for line in report:
        print(line)
    return len(report)


def main():
    write_profiles()
    known = set(DUSMANLAR) | {"Kara Yek", "Enemy Minion", "Enemy Boss"}
    problems = place(ZONE, known) + place(DUNGEON, known)
    sys.exit(1 if problems else 0)


if __name__ == "__main__":
    main()
