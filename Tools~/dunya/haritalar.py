#!/usr/bin/env python3
"""Chop Chop'un (Unity Open Project #1, Apache 2.0) on konumunu Ötüken Destanı haritası yapar ve bütün
haritaları birbirine bağlar.

Ne yapar:
 1. SAHNE: her Chop Chop konumundan oyun mantığını (kamera, müzik, ara sahneler, NPC'ler, toplanabilirler,
    Chop Chop betikleri, ses) atar; çevre sanatını (arazi, kaya, ağaç, ev, su, ışık) bırakır. Katmanları
    AnyRPG'ye göre düzeltir (zemin Default), pişmiş ışık verisini atıp ışıkları gerçek zamanlı yapar, dünyayı
    AnyRPG insanlarının boyuna göre OLCEK kadar büyütür.
 2. VARLIK: sahnelerin kullandığı model, doku, malzeme, gölgelendirici ve prefab'lar GUID'leri korunarak
    Assets/Otuken/Haritalar/_ChopChop altına kopyalanır (büyük dokular 1024'e küçülür, prefab'lar temizlenir).
 3. ANYRPG: SceneConfig, giriş noktası (DefaultSpawnLocation), yürüme ağı yüzeyi (NavMeshSurface, derlemede
    pişer), Chop Chop'taki çıkışların yerinde komşu haritaya geçiş kapıları ve yanında yön tabelası,
    giriş noktasının yanında Kök Taş (Geçit Taşı): her haritadan bütün haritalara yolculuk.
 4. KAYITLAR: SceneNode'lar, derleme sahne listesi, Ötüken Yaylası ve Erlik'in Mağarası'na da Geçit Taşı,
    derleme tanı ayarı (Tools~/dunya/tani.json: ekran görüntüsü çekimleri).

Kullanım (depo kök klasöründen):
    git clone https://github.com/UnityTechnologies/open-project-1 /tmp/chopchop   # commit 608eac9
    python3 "Tools~/dunya/koktas.py" <NotoSansOldTurkic-Regular.ttf>                # Kök Taş modeli
    python3 "Tools~/dunya/haritalar.py" /tmp/chopchop
Tekrar çalıştırılabilir: çıktılar baştan yazılır, kimlikler adlardan türetilir.
"""
import hashlib
import json
import math
import os
import re
import shutil
import sys
import uuid
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import uyaml  # noqa: E402
import unitysahne  # noqa: E402
from unitysahne import TRS, Sahne, degisiklikler, yerel_trs  # noqa: E402
import koktas  # noqa: E402
import harita_icerik  # noqa: E402
import hazineler  # noqa: E402
import yaratiklar  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
HEDEF = ROOT / "Assets/Otuken/Haritalar"
ORTAK = HEDEF / "_ChopChop"
GAME = ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame"
SCENENODE = GAME / "Resources/FeaturesDemoGame/SceneNode"
ZONE = GAME / "Scenes/Content/FeaturesDemoZone/FeaturesDemoZone.unity"
DUNGEON = GAME / "Scenes/Content/FeaturesDemoDungeon/FeaturesDemoDungeon.unity"
NS = uuid.UUID("8f3c2b1a-6d5e-4f70-9a8b-7c6d5e4f3a21")
MASK = 0x7FFFFFFFFFFFFFFF
OLCEK = 1.35          # Chop Chop domuzu 1.3 m, AnyRPG insanları 1.8 m
DOKU_SINIRI = 1024
# derlemede pişen yürüme ağlarının yerel kopyası (tani.zip/navmesh): giriş, geçit taşı ve kamp yerleri için
YURUME_AGLARI = ROOT / "Tools~/varliklar/yurume_aglari"

# ---------------------------------------------------------------- haritalar

# kaynak: Chop Chop sahnesi | dosya: bizim sahne adı | gok: gökyüzü ve ortam ışığı | zemin: ayak sesi
# giris: varsayılan giriş noktası olan LocationEntrance (ya da (sahne, ad))
HARITALAR = {
    "Field_Farms": dict(
        dosya="UmayTarlalari", ad="Umay Tarlaları", gok="gunduz", zemin="cayir", muzik="Otuken Yaylasi",
        giris="LocationEntrance_FromTownMarket",
        aciklama="Bereket anası Umay'ın kutsadığı ekinler, limon bahçeleri ve değirmen yolu. "
                 "Yağmacılar ve çalı cinleri hasadı talan ediyor."),
    "Field_Hill": dict(
        dosya="BoruTepesi", ad="Börü Tepesi", gok="gunduz", zemin="cayir", muzik="Otuken Yaylasi",
        giris="LocationEntrance_FromFarms",
        aciklama="Atalarımızı kurtaran Asena'nın uludığı tepe. Meşalelerle aydınlanan patikalar denize iner."),
    "Beach": dict(
        dosya="AkDenizKiyisi", ad="Ak Deniz Kıyısı", gok="gunduz", zemin="kumsal", muzik="Otuken Yaylasi",
        giris="LocationEntrance",
        aciklama="Ak Ana'nın sularından doğduğu söylenen ak köpüklü deniz. Bambu korulukları ve gizli koylar."),
    "Town_Market": dict(
        dosya="OrdubalikCarsisi", ad="Ordubalık Çarşısı", gok="aksam", zemin="kent", muzik="Otuken Yaylasi",
        giris="LocationEntrance_FromFarms",
        aciklama="Kağanlığın başkenti Ordubalık'ın şenlikli çarşısı. Erlik'in casusları kalabalığa karışmış."),
    "Town_Inner": dict(
        dosya="OrdubalikKenti", ad="Ordubalık Kenti", gok="aksam", zemin="kent", muzik="Otuken Yaylasi",
        giris="LocationEntrance_FromMarket_Riverside",
        aciklama="Irmağın çevirdiği değirmenler, taş köprüler ve kentin dar sokakları."),
    "Forest": dict(
        dosya="UlukayinOrmani", ad="Ulukayın Ormanı", gok="aksam", zemin="orman", muzik="Otuken Yaylasi",
        giris="LocationEntrance",
        aciklama="Yer ile göğü bağlayan Ulukayın'ın kökleri bu ormanın altından geçer. Şelalenin ardında "
                 "gölgeler dolaşır."),
    "Town_Upper": dict(
        dosya="KaganOrdasi", ad="Kağan Ordası", gok="aksam", zemin="kent", muzik="Otuken Yaylasi",
        giris="LocationEntrance_FromInnerTown",
        aciklama="Kentin tepesindeki kağan otağı ve beylerin konakları. Erlik'in en güçlü yandaşları burada."),
    "Mountain_Path": dict(
        dosya="KafDagiYolu", ad="Kaf Dağı Yolu", gok="aksam", zemin="kaya", muzik="Erlik Magarasi",
        giris="LocationEntrance_FromTown",
        aciklama="Dünyanın ucundaki Kaf Dağı'na tırmanan sarp yol. Kızıl cinler kayalıklarda pusu kurar."),
    "Mountain_Cave": dict(
        dosya="ErgenekonMagarasi", ad="Ergenekon Mağarası", gok="aksam", zemin="kaya", muzik="Erlik Magarasi",
        giris="LocationEntrance", giris_sabit=(134.9, 31.3, 137.0),
        aciklama="Atalarımızın demir dağı eritip çıktığı Ergenekon. Derinliklerde hâlâ körükler ve kor var."),
    "Beach_Night": dict(
        dosya="AyDedeKoyu", ad="Ay Dede Koyu", gok="gece", zemin="kumsal", muzik="Erlik Magarasi",
        giris=("Beach", "LocationEntrance"),
        aciklama="Ay Dede'nin ışığında gümüşlenen koy. Gece olunca kurganlardan kalkan ölüler kıyıya iner."),
}
SIRA = list(HARITALAR)

# Chop Chop LocationSO (çıkışın götürdüğü yer) -> kaynak sahne
KONUMLAR = {"Beach": "Beach", "Field_Hill": "Field_Hill", "Field_Farms": "Field_Farms", "Town_Market": "Town_Market",
            "Town_Inner": "Town_Inner", "Town_Upper": "Town_Upper", "Forest": "Forest",
            "Mountain_Path": "Mountain_Path", "Mountain_Cave": "Mountain_Cave"}
BILINMEYEN_KONUM = "Forest"   # Forest_Entrance (oyundan çıkarılmış ara bölge)

# var olan haritalar ve KayKit parçalarıyla kurulanlar (zindanlar.py)
ESKI = [("FeaturesDemoZone", "Ötüken Yaylası"), ("FeaturesDemoDungeon", "Erlik'in Mağarası")]
KAYKIT = [("KoncolosIni", "Koncolos İni"), ("KurganMezarligi", "Kurgan Mezarlığı"), ("TamuZindani", "Tamu Zindanı")]

# Geçit Taşı'ndaki sıra: zorluğa göre
YOLCULUK_SIRASI = ["FeaturesDemoZone", "UmayTarlalari", "BoruTepesi", "AkDenizKiyisi", "OrdubalikCarsisi",
                   "OrdubalikKenti", "UlukayinOrmani", "KoncolosIni", "KaganOrdasi", "KafDagiYolu",
                   "ErgenekonMagarasi", "KurganMezarligi", "AyDedeKoyu", "FeaturesDemoDungeon", "TamuZindani"]

GOK = {
    # gökyüzü malzemesi (Chop Chop'un boyalı gökleri) ve üç renkli ortam ışığı (gök, ufuk, yer)
    "gunduz": ("Day", ((0.62, 0.68, 0.76), (0.46, 0.49, 0.51), (0.22, 0.24, 0.22))),
    "aksam": ("Sunset", ((0.4993177, 0.46740067, 0.6226415), (0.4245283, 0.33249098, 0.3146773),
                         (0.10355743, 0.14150941, 0.13586733))),
    "gece": ("Night", ((0.22, 0.28, 0.45), (0.13, 0.16, 0.26), (0.05, 0.06, 0.08))),
}
AYAK_SESI = {"cayir": "Footstep Hits Grass", "kumsal": "Footstep Hits Sand Fast", "kent": "Footstep Hits Cobblestone",
             "orman": "Footstep Hits Leaves", "kaya": "Footstep Hits Gravel Fast"}

# ---------------------------------------------------------------- temizlik kuralları

DUSUR = re.compile(r"^(CameraSystem|LocationExit.*|LocationEntrance.*|MusicPlayer|SpawnSystem|EditorInitializer|"
                   r".*Cutscene.*|Interactables_.*|ReverbZones|DollyTrack|MixingCamera|Main Camera|Entrance_Exits|"
                   r"Exits|FallCatchers|Light Probe Group|-+ .* -+)$")
SAKLANAN_BETIK = {"474bcb49853aa07438625e644c072ee6",   # UniversalAdditionalLightData
                  "a79441f348de89743a2939f4d699eac1"}   # UniversalAdditionalCameraData
ATILAN_BILESEN = {20, 81, 82, 143, 167, 195, 208, 320}   # kamera, ses, karakter, yapay zekâ, zaman çizelgesi
SAKLANAN_KATMAN = {0, 1, 2, 4, 5}                        # Default, TransparentFX, Ignore Raycast, Water, UI
ATLANAN_UZANTI = {".cs", ".wav", ".mp3", ".ogg", ".aif", ".aiff", ".mixer", ".playable", ".signal", ".unity",
                  ".exr", ".lighting", ".dll", ".asmdef"}

# ---------------------------------------------------------------- AnyRPG kimlikleri

SCENECONFIG = ("b0d2b5c75a182bb43a109960c8f6805b", "7975672639374341621", "7883750529680915980")
SPAWN = ("5a2d78ebe26542846be90199022210b5", "8617408416077730537", "204323511576403553")
NAVMESH_BETIK = "7a5ac11cc976e418e8d13136b07e1f52"
INTERACTABLE_BETIK = "df07de734135f834b9ba0098ec4ed914"
LOADSCENE_BETIK = "dfdd7a718358eb84dbd417f51a47a2c9"
TMP_BETIK = "9541d86e2fd84c1d9990edf0852d74ab"
TMP_FONT = "9ff1637cc53af0c4f9d93e4706904ea7"
ZEMIN_BETIK = uuid.uuid5(NS, "betik:ZemineOturt").hex
FBX_KOK_GO = "919132149155446097"
FBX_KOK_TR = "-8679921383154817045"
MAGARA_SESI = "9f9cc9a72a4f7c94f8ec9fc435ad4c77"
SAHNE_KOKLERI_BASLIK = "--- !u!1660057539 &9223372036854775807\nSceneRoots:"

q = lambda s: json.dumps(s, ensure_ascii=True)


def guid(ad):
    return uuid.uuid5(NS, ad).hex


def kimlik(ad, kullanilan):
    """sahnede eşsiz, addan türetilmiş fileID"""
    h = int(hashlib.md5(("harita:" + ad).encode()).hexdigest()[:15], 16) % 8000000000000000000 + 100000000000
    while str(h) in kullanilan:
        h += 7919
    kullanilan.add(str(h))
    return str(h)


def f(v):
    return f"{v:.4f}".rstrip("0").rstrip(".") if v != 0 else "0"


def v3(v):
    return f"{{x: {f(v[0])}, y: {f(v[1])}, z: {f(v[2])}}}"


def q4(v):
    return f"{{x: {f(v[0])}, y: {f(v[1])}, z: {f(v[2])}, w: {f(v[3])}}}"


def yaz(yol, metin):
    yol = Path(yol)
    yol.parent.mkdir(parents=True, exist_ok=True)
    if not yol.exists() or yol.read_text(encoding="utf-8") != metin:
        yol.write_text(metin, encoding="utf-8")


def klasor_meta(yol, kaynak_meta=None):
    yol = Path(yol)
    yol.mkdir(parents=True, exist_ok=True)
    meta = Path(str(yol) + ".meta")
    if kaynak_meta and Path(kaynak_meta).exists():
        metin = Path(kaynak_meta).read_text(encoding="utf-8")
    elif meta.exists():
        return
    else:
        metin = (f"fileFormatVersion: 2\nguid: {guid('klasor:' + yol.relative_to(ROOT).as_posix())}\nfolderAsset: yes\n"
                 "DefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    yaz(meta, metin)


# ---------------------------------------------------------------- Chop Chop deposu

class Kaynak:
    def __init__(self, klon):
        self.kok = Path(klon).resolve() / "UOP1_Project/Assets"
        if not self.kok.is_dir():
            raise SystemExit(f"Chop Chop deposu bulunamadı: {self.kok}")
        self.gmap = {}
        for dp, _, dosyalar in os.walk(self.kok):
            for d in dosyalar:
                if d.endswith(".meta"):
                    p = Path(dp) / d
                    m = re.search(r"^guid: (\w+)", p.read_text(encoding="utf-8", errors="ignore"), re.M)
                    if m:
                        self.gmap[m.group(1)] = Path(str(p)[:-5])
        self.ad_guid = {}
        for g, p in self.gmap.items():
            self.ad_guid.setdefault(p.name, g)
        self.betikler = {g for g, p in self.gmap.items() if p.suffix == ".cs"}
        self.yoldan = {p: g for g, p in self.gmap.items()}
        self._kok_cache = {}

    def yol(self, g):
        return self.gmap.get(g)

    def yol_guid(self, goreli):
        return self.yoldan[self.kok / goreli]

    def goreli(self, p):
        return Path(p).relative_to(self.kok)

    def prefab_kok(self, g):
        """prefab'ın kök Transform'u: (yerel TRS, fileID)"""
        if g in self._kok_cache:
            return self._kok_cache[g]
        sonuc = (TRS(), None)
        p = self.gmap.get(g)
        if p and p.suffix.lower() in (".fbx", ".obj", ".blend", ".dae"):
            meta = Path(str(p) + ".meta").read_text(encoding="utf-8", errors="ignore")
            eski = re.search(r"^\s+(\d+): //RootNode$", meta, re.M)
            # Unity 2019.3'ten beri model nesnelerinin fileID'si addan türetilir; kök Transform hep bu değerdir
            sonuc = (TRS(), eski.group(1) if eski and eski.group(1).startswith("4") else FBX_KOK_TR)
        elif p and p.suffix == ".prefab":
            s = Sahne(p.read_text(encoding="utf-8"))
            for _, b in s.kokler():
                if b.cls in (4, 224):
                    sonuc = (yerel_trs(b), b.fid)
                    break
                if b.cls == 1001:
                    trs = s.dunya(b, self.prefab_kok)
                    taban = self.prefab_kok(b.guid("m_SourcePrefab"))[1]
                    fid = None
                    for x in s.belgeler:
                        if x.stripped and x.cls == 4 and x.ref("m_PrefabInstance") == b.fid \
                                and x.ref("m_CorrespondingSourceObject") == taban:
                            fid = x.fid
                    if fid is None and taban is not None:
                        # varyantın kökü: iç örnekteki nesnenin fileID'si (örnek ^ kaynak)
                        fid = str((int(b.fid) ^ int(taban)) & MASK)
                    sonuc = (trs, fid)
                    break
        self._kok_cache[g] = sonuc
        return sonuc

    def betik_varligi_mi(self, p):
        """Chop Chop betiğine bağlı ScriptableObject (.asset) mi?"""
        if p.suffix != ".asset":
            return False
        m = re.search(r"m_Script: \{fileID: \d+, guid: (\w+)", p.read_text(encoding="utf-8", errors="ignore")[:4000])
        return bool(m and m.group(1) in self.betikler)


# ---------------------------------------------------------------- temizlik

def katman_duzelt(metin):
    metin = re.sub(r"^(  m_Layer: )(\d+)$",
                   lambda m: m.group(1) + (m.group(2) if int(m.group(2)) in SAKLANAN_KATMAN else "0"),
                   metin, flags=re.M)
    # prefab örneği değişikliklerindeki katmanlar
    metin = re.sub(r"(      propertyPath: m_Layer\n      value: )(\d+)",
                   lambda m: m.group(1) + (m.group(2) if int(m.group(2)) in SAKLANAN_KATMAN else "0"), metin)
    return metin


def isik_duzelt(metin):
    """pişmiş ışık verisi olmayacak: ışıklar gerçek zamanlı; dünya büyüdüğü için menzil de büyür"""
    metin = re.sub(r"^(  m_Lightmapping: )\d+$", r"\g<1>4", metin, flags=re.M)
    metin = re.sub(r"(      propertyPath: m_Lightmapping\n      value: )\d+", r"\g<1>4", metin)

    def menzil(m):
        return m.group(1) + f(float(m.group(2)) * OLCEK)
    metin = re.sub(r"^(  m_Range: )([-\d.e]+)$", menzil, metin, flags=re.M)
    metin = re.sub(r"(      propertyPath: m_Range\n      value: )([-\d.e]+)", menzil, metin)
    # parçacıklar dünya ile birlikte büyüsün (Hierarchy)
    metin = re.sub(r"^(  scalingMode: )1$", r"\g<1>0", metin, flags=re.M)
    return metin


def bilesenleri_temizle(s, kaynak):
    """Chop Chop betikleri, ses, kamera, karakter ve yapay zekâ bileşenlerini atar"""
    sil = []
    for b in s.belgeler:
        if b.stripped:
            continue
        if b.cls == 114:
            g = b.guid("m_Script")
            if g not in SAKLANAN_BETIK and b.ref("m_GameObject") not in (None, "0"):
                sil.append(b.fid)
        elif b.cls in ATILAN_BILESEN:
            sil.append(b.fid)
    s.bilesen_sil(sil)
    return len(sil)


def degisiklik_temizle(metin, gecerli):
    """prefab örneklerinde kopyalanmayan Chop Chop varlıklarına başvuran değişiklikleri atar"""
    def degistir(m):
        g = m.group(2)
        return "" if g and g not in gecerli else m.group(0)
    return re.sub(r"    - target: \{fileID: -?\d+, guid: \w+,\s+type: \d+\}\n      propertyPath: .*\n"
                  r"      value: ?.*\n      objectReference: \{fileID: (-?\d+)(?:, guid: (\w+))?(?:,\s+type: \d+)?\}\n",
                  degistir, metin)


# ---------------------------------------------------------------- sahne çevirisi

class Harita:
    def __init__(self, kaynak_ad, ayar):
        self.kaynak = kaynak_ad
        self.__dict__.update(ayar)
        self.sahne_yolu = HEDEF / self.dosya / f"{self.dosya}.unity"
        self.girisler = []   # (ad, dünya TRS, yol guid)
        self.cikislar = []   # (ad, dünya TRS, yol guid, hedef kaynak sahne)
        self.varsayilan = None
        ag = YURUME_AGLARI / f"{self.dosya}.asset"
        self.ag = harita_icerik.YurumeAgi(ag) if ag.exists() else None


def gecis_bilgisi(s, kaynak):
    girisler, cikislar = [], []
    for b in s.belgeler:
        if b.cls != 1001:
            continue
        src = kaynak.yol(b.guid("m_SourcePrefab"))
        if not src or src.name not in ("LocationEntrance.prefab", "LocationExit.prefab"):
            continue
        d = {yol: (deger, g) for _, _, yol, deger, g in degisiklikler(b)}
        w = s.dunya(b, kaynak.prefab_kok)
        if src.name == "LocationEntrance.prefab":
            girisler.append((s.ad(b), w, d.get("_entrancePath", ("", None))[1]))
        else:
            konum = d.get("_locationToLoad", ("", None))[1]
            konum_adi = kaynak.yol(konum).stem if konum and kaynak.yol(konum) else None
            hedef = KONUMLAR.get(konum_adi, BILINMEYEN_KONUM)
            cikislar.append((s.ad(b), w, d.get("_leadsToPath", ("", None))[1], hedef))
    return girisler, cikislar


def sahneyi_temizle(h, kaynak):
    yol = kaynak.kok / f"Scenes/Locations/{h.kaynak}.unity"
    s = Sahne(yol.read_text(encoding="utf-8"))
    h.girisler, h.cikislar = gecis_bilgisi(s, kaynak)

    kokler = s.kokler()
    dusen = [b.fid for ad, b in kokler if DUSUR.match(ad or "")]
    s.sil(dusen)
    atilan = bilesenleri_temizle(s, kaynak)
    gomulu = s.kullanilmayan_gomulu_sil()

    # sahne ayarları: pişmiş ışık / yürüme ağı / görünürlük verisi yok, gök ve ortam ışığı
    gok_adi, ortam = GOK[h.gok]
    for b in s.belgeler:
        if b.cls == 29:
            b.metin = re.sub(r"(  m_OcclusionCullingData: )\{[^}]*\}", r"\1{fileID: 0}", b.metin)
        elif b.cls == 157:
            b.metin = re.sub(r"(  m_LightingDataAsset: )\{[^}]*\}", r"\1{fileID: 0}", b.metin)
            b.metin = re.sub(r"(  m_LightingSettings: )\{[^}]*\}", r"\1{fileID: 0}", b.metin)
            b.metin = re.sub(r"(    m_EnableBakedLightmaps: )1", r"\g<1>0", b.metin)
        elif b.cls == 196:
            b.metin = re.sub(r"(  m_NavMeshData: )\{[^}]*\}", r"\1{fileID: 0}", b.metin)
        elif b.cls == 104:
            sky = kaynak.yol_guid(f"Art/Skybox/Painted/Materials/{gok_adi}.mat")
            b.metin = re.sub(r"(  m_SkyboxMaterial: )\{[^}]*\}", rf"\1{{fileID: 2100000, guid: {sky}, type: 2}}", b.metin)
            for alan, renk in zip(("m_AmbientSkyColor", "m_AmbientEquatorColor", "m_AmbientGroundColor"), ortam):
                b.metin = re.sub(rf"(  {alan}: )\{{[^}}]*\}}",
                                 rf"\1{{r: {renk[0]}, g: {renk[1]}, b: {renk[2]}, a: 1}}", b.metin)
            b.metin = re.sub(r"(  m_AmbientMode: )\d+", r"\g<1>1", b.metin)
            b.metin = re.sub(r"(  m_CustomReflection: )\{[^}]*\}", r"\1{fileID: 0}", b.metin)
    for b in s.belgeler:
        b.metin = isik_duzelt(katman_duzelt(b.metin))
    print(f"  {h.kaynak:14s} -> {h.dosya}: {len(dusen)} kök nesne, {atilan} bileşen, {gomulu} gömülü örgü atıldı; "
          f"{len(h.girisler)} giriş, {len(h.cikislar)} çıkış")
    return s


def bagimliliklar(metinler, kaynak):
    """sahne metinlerinden başlayarak kopyalanacak Chop Chop varlıkları"""
    gorulen, yigin = set(), []
    for m in metinler:
        for g in set(re.findall(r"[0-9a-f]{32}", m)):
            if g in kaynak.gmap:
                yigin.append(g)
    while yigin:
        g = yigin.pop()
        if g in gorulen:
            continue
        p = kaynak.gmap[g]
        if p.is_dir() or p.suffix.lower() in ATLANAN_UZANTI or kaynak.betik_varligi_mi(p):
            continue
        if "TextMeshPro" in p.parts:
            continue
        gorulen.add(g)
        if p.suffix.lower() in (".prefab", ".mat", ".asset", ".controller", ".overridecontroller", ".anim",
                                ".shadergraph", ".shadersubgraph", ".shader", ".hlsl", ".cginc", ".terrainlayer",
                                ".mask", ".physicmaterial", ".spriteatlas", ".rendertexture", ".cubemap"):
            metin = p.read_text(encoding="utf-8", errors="ignore")
            if p.suffix == ".prefab":
                metin = prefab_temizle(metin, kaynak, None)
            for g2 in set(re.findall(r"[0-9a-f]{32}", metin)):
                if g2 in kaynak.gmap and g2 not in gorulen:
                    yigin.append(g2)
    return gorulen


def prefab_temizle(metin, kaynak, gecerli):
    s = Sahne(metin)
    bilesenleri_temizle(s, kaynak)
    s.kullanilmayan_gomulu_sil()
    metin = s.metin()
    metin = isik_duzelt(katman_duzelt(metin))
    if gecerli is not None:
        metin = degisiklik_temizle(metin, gecerli)
    return metin


# ---------------------------------------------------------------- varlık kopyalama

CUSTOM_LIGHTING = r'''// Chop Chop'un Toon gölgelendiricilerinin ışık hesabı, URP 17 (Unity 6) için yeniden yazıldı.
// Asıl dosya: UOP1_Project/Assets/Shaders/CustomHLSL/CustomLighting.hlsl (Apache 2.0).
#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#ifndef SHADERGRAPH_PREVIEW
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
    #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
    #pragma multi_compile_fragment _ _SHADOWS_SOFT
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

void MainLight_float(float3 WorldPos, out float3 Direction, out float3 Color, out float ShadowAtten)
{
#ifdef SHADERGRAPH_PREVIEW
    Direction = float3(0.5, 0.5, 0);
    Color = 1;
    ShadowAtten = 1;
#else
    float4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    Light mainLight = GetMainLight(shadowCoord);
    Direction = mainLight.direction;
    Color = mainLight.color;
    ShadowAtten = mainLight.shadowAttenuation;
#endif
}

void MainLight_half(float3 WorldPos, out half3 Direction, out half3 Color, out half ShadowAtten)
{
    float3 d, c;
    float s;
    MainLight_float(WorldPos, d, c, s);
    Direction = d;
    Color = c;
    ShadowAtten = s;
}

void DirectSpecular_float(float Smoothness, float3 Direction, float3 WorldNormal, float3 WorldView, out float3 Out)
{
#ifdef SHADERGRAPH_PREVIEW
    Out = 0;
#else
    float shininess = exp2(10 * Smoothness + 1);
    float3 n = normalize(WorldNormal);
    float3 v = SafeNormalize(WorldView);
    float3 h = SafeNormalize(Direction + v);
    Out = pow(saturate(dot(n, h)), shininess);
#endif
}

void DirectSpecular_half(half Smoothness, half3 Direction, half3 WorldNormal, half3 WorldView, out half3 Out)
{
    float3 o;
    DirectSpecular_float(Smoothness, Direction, WorldNormal, WorldView, o);
    Out = o;
}

void AdditionalLights_float(float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView,
                            out float3 Diffuse, out float3 Specular)
{
    float3 diffuseColor = 0;
    float3 specularColor = 0;
#ifndef SHADERGRAPH_PREVIEW
    float shininess = exp2(10 * Smoothness + 1);
    float3 n = normalize(WorldNormal);
    float3 v = SafeNormalize(WorldView);
    uint pixelLightCount = GetAdditionalLightsCount();
    for (uint i = 0u; i < pixelLightCount; ++i)
    {
        Light light = GetAdditionalLight(i, WorldPosition);
        float3 c = light.color * (light.distanceAttenuation * light.shadowAttenuation);
        diffuseColor += c * saturate(dot(n, light.direction));
        float3 h = SafeNormalize(light.direction + v);
        specularColor += c * pow(saturate(dot(n, h)), shininess);
    }
#endif
    Diffuse = diffuseColor;
    Specular = specularColor;
}

void AdditionalLights_half(half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView,
                           out half3 Diffuse, out half3 Specular)
{
    float3 d, s;
    AdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, d, s);
    Diffuse = d;
    Specular = s;
}

#endif
'''


def golge_anahtarlarini_cikar(metin):
    """Toon ışık alt grafiğindeki gölge anahtar kelimeleri (shader_feature) atılır: aynı anahtarları
    CustomLighting.hlsl multi_compile ile tanımlıyor; iki kez tanımlanmasınlar."""
    parcalar = re.split(r"\n\n(?=\{)", metin)
    sil = set()
    for p in parcalar:
        o = json.loads(p)
        if "m_KeywordType" in o and o.get("m_Name") in (
                "_MAIN_LIGHT_SHADOWS", "_MAIN_LIGHT_SHADOWS_CASCADE", "_ADDITIONAL_LIGHT_SHADOWS", "_SHADOWS_SOFT"):
            sil.add(o["m_ObjectId"])
    kalan = []
    for p in parcalar:
        o = json.loads(p)
        if o.get("m_ObjectId") in sil:
            continue
        if o.get("m_Type") == "UnityEditor.ShaderGraph.GraphData":
            o["m_Keywords"] = [k for k in o["m_Keywords"] if k["m_Id"] not in sil]
            p = json.dumps(o, indent=4, ensure_ascii=False)
        kalan.append(p)
    assert len(sil) == 4, sil
    return "\n\n".join(kalan).rstrip("\n") + "\n\n"


def doku_kopyala(kaynak_yol, hedef_yol):
    """büyük dokuları DOKU_SINIRI'na küçültür; PSD'ler PNG olur (GUID meta'da kalır)"""
    uz = kaynak_yol.suffix.lower()
    if uz not in (".png", ".jpg", ".jpeg", ".tga", ".tif", ".tiff", ".psd", ".bmp"):
        shutil.copyfile(kaynak_yol, hedef_yol)
        return hedef_yol
    img = Image.open(kaynak_yol)
    buyuk = max(img.size) > DOKU_SINIRI
    if uz == ".psd":
        hedef_yol = hedef_yol.with_suffix(".png")
    if not buyuk and uz != ".psd":
        shutil.copyfile(kaynak_yol, hedef_yol)
        return hedef_yol
    if img.mode not in ("RGB", "RGBA", "L", "LA"):
        img = img.convert("RGBA" if "A" in img.getbands() else "RGB")
    if buyuk:
        oran = DOKU_SINIRI / max(img.size)
        img = img.resize((max(1, round(img.size[0] * oran)), max(1, round(img.size[1] * oran))), Image.LANCZOS)
    if hedef_yol.suffix.lower() in (".jpg", ".jpeg"):
        img.convert("RGB").save(hedef_yol, quality=92)
    elif hedef_yol.suffix.lower() in (".tif", ".tiff"):
        img.save(hedef_yol, compression="tiff_deflate")
    else:
        img.save(hedef_yol, optimize=True)
    return hedef_yol


def varliklari_kopyala(guidler, kaynak):
    if ORTAK.exists():
        shutil.rmtree(ORTAK)
    klasor_meta(HEDEF)
    klasor_meta(ORTAK)
    toplam = 0
    for g in sorted(guidler, key=lambda g: str(kaynak.gmap[g])):
        src = kaynak.gmap[g]
        rel = kaynak.goreli(src)
        dst = ORTAK / rel
        # klasör meta'ları (Chop Chop'taki GUID'leriyle)
        for i in range(1, len(rel.parts)):
            alt = Path(*rel.parts[:i])
            klasor_meta(ORTAK / alt, kaynak.kok / (str(alt) + ".meta"))
        dst.parent.mkdir(parents=True, exist_ok=True)
        uz = src.suffix.lower()
        if uz == ".prefab":
            yaz(dst, prefab_temizle(src.read_text(encoding="utf-8"), kaynak, guidler))
        elif src.name == "CustomLighting.hlsl":
            yaz(dst, CUSTOM_LIGHTING)
        elif src.name == "ToonLightingModel.shadersubgraph":
            yaz(dst, golge_anahtarlarini_cikar(src.read_text(encoding="utf-8")))
        else:
            dst = doku_kopyala(src, dst)
        meta = src.parent / (src.name + ".meta")
        shutil.copyfile(meta, Path(str(dst) + ".meta"))
        toplam += dst.stat().st_size
    yaz(ORTAK / "LISANS.txt", LISANS)
    yaz(ORTAK / "LISANS.txt.meta",
        f"fileFormatVersion: 2\nguid: {guid('lisans:chopchop')}\nTextScriptImporter:\n  externalObjects: {{}}\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    print(f"  {len(guidler)} varlık kopyalandı ({toplam / 1e6:.1f} MB)")


LISANS = """Bu klasördeki çevre sanatı (modeller, dokular, malzemeler, gölgelendiriciler, prefab'lar) Unity
Technologies'in "Chop Chop" oyunundan (Unity Open Project #1) alınmıştır:
    https://github.com/UnityTechnologies/open-project-1  (commit 608eac9)
Lisans: Apache License 2.0 (https://www.apache.org/licenses/LICENSE-2.0)

Değişiklikler (Tools~/dunya/haritalar.py): oyun betikleri, sesler ve ara sahneler çıkarıldı; prefab'lardan
Chop Chop bileşenleri silindi; katmanlar ve ışık ayarları AnyRPG'ye uyarlandı; büyük dokular 1024 piksele
küçültüldü; Shaders/CustomHLSL/CustomLighting.hlsl URP 17 için yeniden yazıldı. Sahneler
Assets/Otuken/Haritalar/<Harita> altındadır ve aynı lisansla dağıtılır.
"""


# ---------------------------------------------------------------- AnyRPG nesneleri

def go_belge(go, ad, bilesenler, katman=0, etiket="Untagged"):
    satirlar = "".join(f"  - component: {{fileID: {c}}}\n" for c in bilesenler)
    return (f"--- !u!1 &{go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n"
            f"  m_Component:\n{satirlar}  m_Layer: {katman}\n  m_Name: {ad}\n  m_TagString: {etiket}\n"
            f"  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n")


def tr_belge(tr, go, p, rot=(0, 0, 0, 1), s=(1, 1, 1), ust="0", cocuklar=()):
    c = "".join(f"  - {{fileID: {x}}}\n" for x in cocuklar)
    return (f"--- !u!4 &{tr}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            f"  serializedVersion: 2\n  m_LocalRotation: {q4(rot)}\n  m_LocalPosition: {v3(p)}\n"
            f"  m_LocalScale: {v3(s)}\n  m_ConstrainProportionsScale: 0\n"
            f"  m_Children:{' []' if not cocuklar else ''}\n{c}  m_Father: {{fileID: {ust}}}\n"
            f"  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n")


def kutu_belge(fid, go, boyut, merkez=(0, 0, 0), tetik=True):
    return (f"--- !u!65 &{fid}\nBoxCollider:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            f"  m_Material: {{fileID: 0}}\n  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
            f"  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n  m_LayerOverridePriority: 0\n"
            f"  m_IsTrigger: {1 if tetik else 0}\n  m_ProvidesContacts: 0\n  m_Enabled: 1\n  serializedVersion: 3\n"
            f"  m_Size: {v3(boyut)}\n  m_Center: {v3(merkez)}\n")


def mb_bas(fid, go, betik):
    return (f"--- !u!114 &{fid}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
            f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
            f"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {{fileID: 11500000, guid: {betik}, type: 3}}\n"
            f"  m_Name: \n  m_EditorClassIdentifier: \n")


def interactable_belge(fid, go, *, ad="", tetik, pencere_yok, menzil=2, levha=None, ipucu=False):
    lv = levha or ""
    return mb_bas(fid, go, INTERACTABLE_BETIK) + (
        f"  showTooltip: {1 if ipucu else 0}\n  interactableName: {q(ad) if ad else ''}\n"
        f"  glowOnMouseOver: {0 if tetik else 1}\n  glowColor: {{r: 1, g: 0.92156863, b: 0.015686275, a: 1}}\n"
        f"  interactionTooltipText: \n  notInteractable: 0\n  interactWithAny: 0\n  interactOnExit: 0\n"
        f"  isTrigger: {1 if tetik else 0}\n  suppressInteractionWindow: {1 if pencere_yok else 0}\n"
        f"  overrideInteractionColliderSize: 0\n  interactionMaxRange: {menzil}\n  interactionPoints: []\n"
        f"  componentController: {{fileID: 0}}\n  persistentObjectComponent:\n    persistObjectPosition: 0\n"
        f"    saveOnLevelUnload: 0\n    saveOnGameSave: 0\n  persistInteractableData: 0\n"
        f"  hasNameplate: {1 if levha else 0}\n  namePlateProps:\n    displayName: {q(lv) if lv else ''}\n"
        f"    overrideNameplatePosition: {1 if levha else 0}\n"
        f"    nameplatePosition: {{x: 0, y: {4.1 if levha else 0}, z: 0}}\n"
        f"  locked: 0\n  keyName: \n  removeKeyOnInteract: 0\n  unlockOnInteract: 0\n")


def loadscene_belge(fid, go, baslik, sahne, varis=None):
    """varis: (konum, ileri) verilirse oyuncu orada belirir, yoksa hedefin DefaultSpawnLocation'ında"""
    p, ileri = varis if varis else ((0, 0, 0), (0, 0, 0))
    return mb_bas(fid, go, LOADSCENE_BETIK) + (
        f"  loadSceneProps:\n    interactionPanelTitle: {q(baslik)}\n    interactionPanelImage: {{fileID: 0}}\n"
        f"    namePlateImage: {{fileID: 0}}\n    hideOnMiniMap: 0\n    prerequisiteConditions: []\n"
        f"    locationTag: \n    overrideSpawnLocation: {1 if varis else 0}\n    spawnLocation: {v3(p)}\n"
        f"    overrideSpawnDirection: {1 if varis else 0}\n    spawnForwardDirection: {v3(ileri)}\n"
        f"    sceneName: {sahne}\n")


def zemin_belge(fid, go):
    return mb_bas(fid, go, ZEMIN_BETIK) + "  yukaridan: 4\n  asagi: 40\n"


def yazi_belgeleri(k, ad, metin, p, rot, boyut=22, renk=(1.0, 0.97, 0.88)):
    """dünyada duran tek satır TextMeshPro yazısı"""
    go, tr, mr, tmp = (kimlik(f"{ad}:{x}", k) for x in ("go", "tr", "mr", "tmp"))
    r, g, b = renk
    rgba = (255 << 24) | (int(b * 255) << 16) | (int(g * 255) << 8) | int(r * 255)
    belgeler = go_belge(go, ad, [tr, mr, tmp])
    belgeler += (f"--- !u!224 &{tr}\nRectTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
                 f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
                 f"  m_LocalRotation: {q4(rot)}\n  m_LocalPosition: {{x: 0, y: 0, z: {f(p[2])}}}\n"
                 f"  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n"
                 f"  m_Father: {{fileID: 0}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n"
                 f"  m_AnchorMin: {{x: 0.5, y: 0.5}}\n  m_AnchorMax: {{x: 0.5, y: 0.5}}\n"
                 f"  m_AnchoredPosition: {{x: {f(p[0])}, y: {f(p[1])}}}\n  m_SizeDelta: {{x: 30, y: 5}}\n"
                 f"  m_Pivot: {{x: 0.5, y: 0.5}}\n")
    belgeler += (f"--- !u!23 &{mr}\nMeshRenderer:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
                 f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
                 "  m_Enabled: 1\n  m_CastShadows: 0\n  m_ReceiveShadows: 0\n  m_DynamicOccludee: 1\n"
                 "  m_StaticShadowCaster: 0\n  m_MotionVectors: 1\n  m_LightProbeUsage: 1\n  m_ReflectionProbeUsage: 1\n"
                 "  m_RayTracingMode: 2\n  m_RayTraceProcedural: 0\n  m_RenderingLayerMask: 1\n  m_RendererPriority: 0\n"
                 f"  m_Materials:\n  - {{fileID: 2180264, guid: {TMP_FONT}, type: 2}}\n"
                 "  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n  m_StaticBatchRoot: {fileID: 0}\n"
                 "  m_ProbeAnchor: {fileID: 0}\n  m_LightProbeVolumeOverride: {fileID: 0}\n  m_ScaleInLightmap: 1\n"
                 "  m_ReceiveGI: 1\n  m_PreserveUVs: 0\n  m_IgnoreNormalsForChartDetection: 0\n  m_ImportantGI: 0\n"
                 "  m_StitchLightmapSeams: 1\n  m_SelectedEditorRenderState: 3\n  m_MinimumChartSize: 4\n"
                 "  m_AutoUVMaxDistance: 0.5\n  m_AutoUVMaxAngle: 89\n  m_LightmapParameters: {fileID: 0}\n"
                 "  m_SortingLayerID: 0\n  m_SortingLayer: 0\n  m_SortingOrder: 0\n  m_AdditionalVertexStreams: {fileID: 0}\n")
    belgeler += mb_bas(tmp, go, TMP_BETIK) + (
        "  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 1\n"
        "  m_RaycastPadding: {x: 0, y: 0, z: 0, w: 0}\n  m_Maskable: 1\n  m_OnCullStateChanged:\n"
        f"    m_PersistentCalls:\n      m_Calls: []\n  m_text: {q(metin)}\n  m_isRightToLeft: 0\n"
        f"  m_fontAsset: {{fileID: 11400000, guid: {TMP_FONT}, type: 2}}\n"
        f"  m_sharedMaterial: {{fileID: 2180264, guid: {TMP_FONT}, type: 2}}\n"
        "  m_fontSharedMaterials: []\n  m_fontMaterial: {fileID: 0}\n  m_fontMaterials: []\n"
        f"  m_fontColor32:\n    serializedVersion: 2\n    rgba: {rgba}\n"
        f"  m_fontColor: {{r: {r}, g: {g}, b: {b}, a: 1}}\n  m_enableVertexGradient: 0\n  m_colorMode: 3\n"
        "  m_fontColorGradient:\n    topLeft: {r: 1, g: 1, b: 1, a: 1}\n    topRight: {r: 1, g: 1, b: 1, a: 1}\n"
        "    bottomLeft: {r: 1, g: 1, b: 1, a: 1}\n    bottomRight: {r: 1, g: 1, b: 1, a: 1}\n"
        "  m_fontColorGradientPreset: {fileID: 0}\n  m_spriteAsset: {fileID: 0}\n  m_tintAllSprites: 0\n"
        "  m_StyleSheet: {fileID: 0}\n  m_TextStyleHashCode: -1183493901\n  m_overrideHtmlColors: 0\n"
        "  m_faceColor:\n    serializedVersion: 2\n    rgba: 4294967295\n"
        f"  m_fontSize: {boyut}\n  m_fontSizeBase: {boyut}\n  m_fontWeight: 400\n  m_enableAutoSizing: 0\n"
        "  m_fontSizeMin: 18\n  m_fontSizeMax: 72\n  m_fontStyle: 1\n  m_HorizontalAlignment: 2\n"
        "  m_VerticalAlignment: 512\n  m_textAlignment: 65535\n  m_characterSpacing: 0\n"
        "  m_characterHorizontalScale: 1\n  m_wordSpacing: 0\n  m_lineSpacing: 0\n  m_lineSpacingMax: 0\n"
        "  m_paragraphSpacing: 0\n  m_charWidthMaxAdj: 0\n  m_TextWrappingMode: 1\n  m_wordWrappingRatios: 0.4\n"
        "  m_overflowMode: 0\n  m_linkedTextComponent: {fileID: 0}\n  parentLinkedComponent: {fileID: 0}\n"
        "  m_enableKerning: 1\n  m_ActiveFontFeatures: 6e72656b\n  m_enableExtraPadding: 0\n"
        "  checkPaddingRequired: 0\n  m_isRichText: 1\n  m_EmojiFallbackSupport: 1\n  m_parseCtrlCharacters: 1\n"
        "  m_isOrthographic: 0\n  m_isCullingEnabled: 0\n  m_horizontalMapping: 0\n  m_verticalMapping: 0\n"
        "  m_uvLineOffset: 0\n  m_geometrySortingOrder: 0\n  m_IsTextObjectScaleStatic: 0\n"
        "  m_VertexBufferAutoSizeReduction: 1\n  m_useMaxVisibleDescender: 1\n  m_pageToDisplay: 1\n"
        "  m_margin: {x: 0, y: 0, z: 0, w: 0}\n  m_isUsingLegacyAnimationComponent: 0\n  m_isVolumetricText: 0\n"
        "  _SortingLayer: 0\n  _SortingLayerID: 0\n  _SortingOrder: 0\n  m_hasFontAssetChanged: 0\n"
        f"  m_renderer: {{fileID: {mr}}}\n  m_maskType: 0\n")
    return belgeler, tr


def prefab_ornegi(k, ad, prefab_guid, kok_tr, kok_go, p, rot=(0, 0, 0, 1), s=None, ust="0", ek=()):
    """PrefabInstance + kök Transform'un stripped belgesi"""
    iid = kimlik(f"{ad}:ornek", k)
    tid = str((int(iid) ^ int(kok_tr)) & MASK)
    k.add(tid)

    def mod(hedef, yol, deger):
        return (f"    - target: {{fileID: {hedef}, guid: {prefab_guid}, type: 3}}\n"
                f"      propertyPath: {yol}\n      value: {deger}\n      objectReference: {{fileID: 0}}\n")

    mods = [mod(kok_go, "m_Name", ad)]
    for i, e in enumerate("xyz"):
        mods.append(mod(kok_tr, f"m_LocalPosition.{e}", f(p[i])))
    for i, e in enumerate("xyzw"):
        mods.append(mod(kok_tr, f"m_LocalRotation.{e}", f(rot[i])))
    if s:
        for i, e in enumerate("xyz"):
            mods.append(mod(kok_tr, f"m_LocalScale.{e}", f(s[i])))
    for hedef, yol, deger in ek:
        mods.append(mod(hedef, yol, deger))
    metin = (f"--- !u!1001 &{iid}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n"
             f"  m_Modification:\n    serializedVersion: 3\n    m_TransformParent: {{fileID: {ust}}}\n"
             f"    m_Modifications:\n{''.join(mods)}    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n"
             f"    m_AddedGameObjects: []\n    m_AddedComponents: []\n"
             f"  m_SourcePrefab: {{fileID: 100100000, guid: {prefab_guid}, type: 3}}\n"
             f"--- !u!4 &{tid} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {kok_tr}, guid: {prefab_guid},\n"
             f"    type: 3}}\n  m_PrefabInstance: {{fileID: {iid}}}\n  m_PrefabAsset: {{fileID: 0}}\n")
    return metin, iid, tid


def bakis(kaynak_p, hedef_p):
    """kaynaktan hedefe yatay bakış dönüşü"""
    dx, dz = hedef_p[0] - kaynak_p[0], hedef_p[2] - kaynak_p[2]
    if abs(dx) + abs(dz) < 1e-6:
        return (0, 0, 0, 1)
    return unitysahne.yaw_q(math.degrees(math.atan2(dx, dz)))


def gecit_tasi(k, ad, p, yaw, secenekler):
    """Kök Taş modelli, tıklanınca yolculuk penceresi açan geçit taşı. secenekler: [(başlık, sahne)]"""
    go, tr, kutu, inter, zemin = (kimlik(f"{ad}:{x}", k) for x in ("go", "tr", "kutu", "inter", "zemin"))
    ls = [kimlik(f"{ad}:ls{i}", k) for i in range(len(secenekler))]
    model_metin, _, model_tr = prefab_ornegi(k, f"{ad}_Model", koktas.model_guid(), FBX_KOK_TR, FBX_KOK_GO,
                                              (0, 0, 0), ust=tr)
    ego, etr, ekutu, ekutu2 = (kimlik(f"{ad}:engel:{x}", k) for x in ("go", "tr", "kutu", "kutu2"))
    belgeler = go_belge(go, ad, [tr, kutu, inter, zemin] + ls, katman=25)
    belgeler += tr_belge(tr, go, p, unitysahne.yaw_q(yaw), cocuklar=[model_tr, etr])
    belgeler += kutu_belge(kutu, go, (2.8, 3.8, 2.8), (0, 1.9, 0), tetik=True)
    belgeler += interactable_belge(inter, go, ad="Geçit Taşı", tetik=False, pencere_yok=False, menzil=4,
                                   levha="Geçit Taşı", ipucu=True)
    belgeler += zemin_belge(zemin, go)
    for fid, (baslik, sahne) in zip(ls, secenekler):
        belgeler += loadscene_belge(fid, go, baslik, sahne)
    belgeler += model_metin
    # yürünemeyen gövde: dikili taş ve kaplumbağa
    # TransparentFX katmanı: yürüme ağına girmez (taşın yeri değişse de ağ bozulmaz), çarpışma yine engeller
    belgeler += go_belge(ego, "Engel", [etr, ekutu, ekutu2], katman=1)
    belgeler += tr_belge(etr, ego, (0, 0, 0), ust=tr)
    belgeler += kutu_belge(ekutu, ego, (1.0, 2.9, 0.4), (0, 1.95, 0), tetik=False)
    belgeler += kutu_belge(ekutu2, ego, (2.0, 0.6, 1.5), (0, 0.3, 0), tetik=False)
    return belgeler, tr


def tum_haritalar():
    """sahne dosyası -> görünen ad (Geçit Taşı'nın gösterdiği bütün haritalar)"""
    adlar = dict(ESKI + KAYKIT)
    adlar.update({h["dosya"]: h["ad"] for h in HARITALAR.values()})
    return adlar


def secenekler_icin(haric):
    adlar = tum_haritalar()
    assert set(adlar) == set(YOLCULUK_SIRASI), set(adlar) ^ set(YOLCULUK_SIRASI)
    return [(adlar[s], s) for s in YOLCULUK_SIRASI if s != haric]


# ---------------------------------------------------------------- harita sahnesi

def harita_yaz(h, s, haritalar, kaynak):
    k = s.kullanilan_fidler()
    ek = []

    # 1. dünya kökü: Chop Chop'taki her şey bunun altında, OLCEK kadar büyük
    dgo, dtr = kimlik("Dunya:go", k), kimlik("Dunya:tr", k)
    cocuklar = []
    for _, b in s.kokler():
        if b.cls in (4, 224):
            b.metin = re.sub(r"^  m_Father: \{fileID: 0\}$", f"  m_Father: {{fileID: {dtr}}}", b.metin, flags=re.M)
            cocuklar.append(b.fid)
        elif b.cls == 1001:
            b.metin = re.sub(r"^    m_TransformParent: \{fileID: 0\}$", f"    m_TransformParent: {{fileID: {dtr}}}",
                             b.metin, count=1, flags=re.M)
            kok_fid = kaynak.prefab_kok(b.guid("m_SourcePrefab"))[1]
            if kok_fid is None:
                continue
            tid = None
            for x in s.belgeler:
                if x.stripped and x.cls in (4, 224) and x.ref("m_PrefabInstance") == b.fid \
                        and x.ref("m_CorrespondingSourceObject") == kok_fid:
                    tid = x.fid
            if tid is None:
                tid = str((int(b.fid) ^ int(kok_fid)) & MASK)
                k.add(tid)
                ek.append(f"--- !u!4 &{tid} stripped\nTransform:\n  m_CorrespondingSourceObject: {{fileID: {kok_fid}, "
                          f"guid: {b.guid('m_SourcePrefab')},\n    type: 3}}\n  m_PrefabInstance: {{fileID: {b.fid}}}\n"
                          f"  m_PrefabAsset: {{fileID: 0}}\n")
            cocuklar.append(tid)
    ek.append(go_belge(dgo, "Dunya", [dtr]) + tr_belge(dtr, dgo, (0, 0, 0), s=(OLCEK, OLCEK, OLCEK), cocuklar=cocuklar))

    def dunya_p(trs):
        return tuple(v * OLCEK for v in trs.p)

    # 2. giriş noktası
    if isinstance(h.giris, tuple):
        diger = haritalar[h.giris[0]]
        giris = next(g for g in diger.girisler if g[0] == h.giris[1])
    else:
        giris = next(g for g in h.girisler if g[0] == h.giris)
    gp = dunya_p(giris[1])
    gyaw = unitysahne.q_yaw(giris[1].q)
    nm = None
    if h.ag is not None:
        # Chop Chop girişleri bazen kayanın içinde ya da suyun üstünde (domuz ara sahneyle girer):
        # yürüme ağının en yakın büyük parçasındaki düz bir yere taşı (ya da derleme raporunda denenmiş sabit yer)
        if getattr(h, "giris_sabit", None):
            gp = h.giris_sabit
        else:
            (x, y, z), _ = h.ag.oturt(gp)
            gp = (x, y + 0.05, z)
        nm = h.ag.birlesik()
    h.varsayilan = (gp, gyaw)
    metin, _, _ = prefab_ornegi(k, "DefaultSpawnLocation", SPAWN[0], SPAWN[1], SPAWN[2], gp, unitysahne.yaw_q(gyaw))
    ek.append(metin)
    metin, _, _ = prefab_ornegi(k, "SceneConfig", SCENECONFIG[0], SCENECONFIG[1], SCENECONFIG[2], (0, 0, 0))
    ek.append(metin)

    # 3. yürüme ağı yüzeyi: her derlemede HaritaHazirlik pişirir
    nav_ref = "{fileID: 0}"
    ngo, ntr, nmb = (kimlik(f"Navigation:{x}", k) for x in ("go", "tr", "mb"))
    ek.append(go_belge(ngo, "Navigation", [ntr, nmb]) + tr_belge(ntr, ngo, (0, 0, 0)) + mb_bas(nmb, ngo, NAVMESH_BETIK) + (
        "  m_SerializedVersion: 0\n  m_AgentTypeID: 0\n  m_CollectObjects: 0\n  m_Size: {x: 10, y: 10, z: 10}\n"
        "  m_Center: {x: 0, y: 2, z: 0}\n  m_LayerMask:\n    serializedVersion: 2\n    m_Bits: 1\n"
        "  m_UseGeometry: 1\n  m_DefaultArea: 0\n  m_GenerateLinks: 0\n  m_IgnoreNavMeshAgent: 1\n"
        "  m_IgnoreNavMeshObstacle: 1\n  m_OverrideTileSize: 0\n  m_TileSize: 256\n  m_OverrideVoxelSize: 0\n"
        f"  m_VoxelSize: 0.16666667\n  m_MinRegionArea: 2\n  m_NavMeshData: {nav_ref}\n  m_BuildHeightMesh: 0\n"))

    # 4. geçit taşı: girişin önünde, sağa doğru (yürüme ağı varsa üzerinde düz bir yer)
    ileri = unitysahne.qrot(unitysahne.yaw_q(gyaw), (0, 0, 1))
    sag = (ileri[2], 0, -ileri[0])
    tp = (gp[0] + ileri[0] * 5 + sag[0] * 4, gp[1], gp[2] + ileri[2] * 5 + sag[2] * 4)
    if nm is not None:
        yer = harita_icerik.tas_yeri(nm, (gp[0], gp[2]), (ileri[0], ileri[2]), gp[1])
        if yer:
            tp = yer
    tas_yaw = math.degrees(math.atan2(gp[0] - tp[0], gp[2] - tp[2]))
    metin, _ = gecit_tasi(k, "GecitTasi", tp, tas_yaw, secenekler_icin(h.dosya))
    ek.append(metin)
    h.tas = tp

    # 5. komşu haritalara kapılar (Chop Chop çıkışlarının yerinde)
    h.kapilar = []
    for i, (ad, trs, yol, hedef) in enumerate(h.cikislar):
        hh = haritalar[hedef]
        varis = next((g for g in hh.girisler if g[2] == yol), None)
        vp = dunya_p(varis[1]) if varis else None
        if vp is not None and hh.ag is not None:
            vp = hh.ag.oturt(vp)[0]
        vileri = varis[1].ileri() if varis else None
        p = dunya_p(trs)
        s_ = tuple(abs(v) * OLCEK for v in trs.s)
        kgo, ktr, kkutu, kint, kls = (kimlik(f"Kapi{i}:{x}", k) for x in ("go", "tr", "kutu", "int", "ls"))
        kad = f"Kapi_{hh.dosya}_{i}"
        ek.append(go_belge(kgo, kad, [ktr, kkutu, kint, kls], katman=11)
                  + tr_belge(ktr, kgo, p, trs.q, s_)
                  + kutu_belge(kkutu, kgo, (1, 1, 1))
                  + interactable_belge(kint, kgo, tetik=True, pencere_yok=True)
                  + loadscene_belge(kls, kgo, hh.ad, hh.dosya,
                                    ((vp[0], vp[1] + 0.3, vp[2]), (vileri[0], 0, vileri[2])) if varis else None))
        dusme = s_[0] > 12 or s_[2] > 12   # uçurumdan düşenleri yakalayan geniş kutular: yazı ve tabela yok
        if not dusme:
            rot = bakis(gp, p)
            yp = (p[0], p[1] + max(2.2, s_[1] * 0.5 + 1.6), p[2])
            metin, _ = yazi_belgeleri(k, f"KapiYazi_{i}", f"{hh.ad}", yp, rot)
            ek.append(metin)
        h.kapilar.append((kad, p, dusme))

    # 6. düşman kampları ve sandıklar (yürüme ağının yerel kopyası varsa)
    h.rapor = "yürüme ağı yok: düşman yerleştirilmedi"
    if nm is not None and h.dosya in harita_icerik.KADRO:
        yasak = [(gp[0], gp[2], 24), (tp[0], tp[2], 12)] + [(p[0], p[2], 12) for _, p, dusme in h.kapilar if not dusme]
        # tasarlanmış oyun alanı: Chop Chop giriş ve çıkışlarının çevresi (uzaktaki manzara adaları değil)
        isaret = [dunya_p(g[1]) for g in h.girisler] + [p for _, p, dusme in h.kapilar if not dusme] + [gp]
        mx = sum(p[0] for p in isaret) / len(isaret)
        mz = sum(p[2] for p in isaret) / len(isaret)
        yaricap = max(90.0, max(math.hypot(p[0] - mx, p[2] - mz) for p in isaret) + 55.0)
        kamplar, sandiklar, h.rapor, ek_seviye = harita_icerik.kamplari_sec(
            h.dosya, nm, (gp[0], gp[2]), yasak, (mx, mz, yaricap), gp[1])
        for i, (merkez, uyeler) in enumerate(kamplar):
            for j, (profil, x, y, z) in enumerate(uyeler):
                yaw = math.degrees(math.atan2(merkez[0] - x, merkez[2] - z)) + 180
                doc, tid = yaratiklar.spawn_doc(f"TR_{h.dosya}_{i + 1}_{j + 1}", x, y + 0.05, z, yaw, profil,
                                                ek_seviye + (1 if i >= len(kamplar) - 2 else 0), 120, k)
                ek.append(doc)
        bilgi = hazineler.prefab_info()
        for i, (prefab, (x, y, z)) in enumerate(sandiklar):
            doc, tid = hazineler.instance_doc(f"TRL_{h.dosya}_{i + 1}", prefab, x, y, z, (i * 97) % 360, bilgi, k)
            ek.append(doc)

    # 7. birleştir
    on = s.on
    metin = s.metin()
    metin = metin.rstrip("\n") + "\n" + "".join(ek)
    yaz(h.sahne_yolu, metin)
    klasor_meta(h.sahne_yolu.parent)
    yaz(Path(str(h.sahne_yolu) + ".meta"),
        f"fileFormatVersion: 2\nguid: {sahne_guid(h.dosya)}\nDefaultImporter:\n  externalObjects: {{}}\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def sahne_guid(dosya):
    return guid("sahne:" + dosya)


# ---------------------------------------------------------------- SceneNode, derleme listesi, tanı

def scene_node(h):
    sablon = (SCENENODE / "FeaturesDemoZoneSceneNode.asset").read_text(encoding="utf-8")
    t = sablon
    ad_dosya = f"{h.dosya}SceneNode"

    def sub(desen, yeni, cok_satir=False):
        nonlocal t
        t2, n = re.subn(desen, lambda m: yeni, t, count=1, flags=re.M | (re.S if cok_satir else 0))
        assert n == 1, desen
        t = t2

    sub(r"^  m_Name: .*$", f"  m_Name: {ad_dosya}")
    sub(r"^  resourceName: .*$", f"  resourceName: {h.dosya}")
    sub(r"^  displayName: .*$", f"  displayName: {q(h.ad)}")
    sub(r"^  description: .*?(?=^  useRegionalDescription:)", f"  description: {q(h.aciklama)}\n", True)
    sub(r"^  sceneFile: .*$", f"  sceneFile: {h.dosya}")
    sub(r"^  backgroundMusicProfile: .*$", f"  backgroundMusicProfile: {h.muzik}")
    if h.zemin == "kaya" and h.kaynak == "Mountain_Cave":
        # mağara uğultusu (Erlik'in Mağarası'nınki), kuş sesi değil
        for alan in ("dayAmbientSoundsAudio", "nightAmbientSoundsAudio"):
            sub(rf"^  {alan}: \{{[^}}]*\}}", f"  {alan}: {{fileID: 8300000, guid: {MAGARA_SESI}, type: 3}}", True)
    sub(r"^  footStepProfiles:\n(?:  - .*\n)+", f"  footStepProfiles:\n  - {AYAK_SESI[h.zemin]}\n")
    sub(r"^  sunRotationMode: .*$", "  sunRotationMode: 0")
    sub(r"^  rotateSunColor: .*$", "  rotateSunColor: 0")
    sub(r"^  blendedSkybox: .*$", "  blendedSkybox: 0")
    sub(r"^  rotateSkybox: .*$", "  rotateSkybox: 0")
    sub(r"^  weatherWeights:\n(?:  - .*\n|    .*\n)*", "  weatherWeights: []\n")
    yol = SCENENODE / f"{ad_dosya}.asset"
    yaz(yol, t)
    yaz(Path(str(yol) + ".meta"),
        f"fileFormatVersion: 2\nguid: {guid('scenenode:' + h.dosya)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
        "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def derleme_listesi(haritalar):
    yol = ROOT / "ProjectSettings/EditorBuildSettings.asset"
    t = yol.read_text(encoding="utf-8")
    t = re.sub(r"  - enabled: 1\n    path: Assets/Otuken/Haritalar/.*\n    guid: \w+\n", "", t)
    yollar = [(h.sahne_yolu.relative_to(ROOT).as_posix(), sahne_guid(h.dosya)) for h in haritalar]
    # zindanlar.py'nin kurduğu KayKit haritaları da listede kalsın
    yollar += [(f"Assets/Otuken/Haritalar/{d}/{d}.unity", sahne_guid(d)) for d, _ in KAYKIT
               if (HEDEF / d / f"{d}.unity").exists()]
    ekler = "".join(f"  - enabled: 1\n    path: {y}\n    guid: {g}\n" for y, g in yollar)
    i = t.index("  m_configObjects:")
    t = t[:i] + ekler + t[i:]
    yaz(yol, t)


def tani_ayari(haritalar, eski_cekimler):
    sahneler = []
    for h in haritalar:
        cekimler = []
        tp = h.tas
        gp = h.varsayilan[0]
        cekimler.append(dict(ad="gecit_tasi", konum=[gp[0], gp[1] + 2.2, gp[2]], hedef=[tp[0], tp[1] + 1.6, tp[2]], fov=55))
        for kad, p, dusme in h.kapilar:
            if dusme:
                continue
            d = math.hypot(p[0] - gp[0], p[2] - gp[2]) or 1
            ux, uz = (p[0] - gp[0]) / d, (p[2] - gp[2]) / d
            cekimler.append(dict(ad=kad.lower(), konum=[p[0] - ux * 9, p[1] + 4, p[2] - uz * 9],
                                 hedef=[p[0], p[1] + 1, p[2]], fov=55))
        sahneler.append(dict(yol=h.sahne_yolu.relative_to(ROOT).as_posix(), kusbakisi=True, cekimler=cekimler))
    sahneler += eski_cekimler
    # zindanlar.py'nin çekimleri korunur
    eski_yol = ROOT / "Tools~/dunya/tani.json"
    if eski_yol.exists():
        kaykit = {f"Assets/Otuken/Haritalar/{d}/{d}.unity" for d, _ in KAYKIT}
        sahneler += [s for s in json.loads(eski_yol.read_text(encoding="utf-8"))["sahneler"] if s["yol"] in kaykit]
    def yuvarla(o):
        if isinstance(o, float):
            return round(o, 2)
        if isinstance(o, list):
            return [yuvarla(x) for x in o]
        if isinstance(o, dict):
            return {a: yuvarla(b) for a, b in o.items()}
        return o
    yaz(ROOT / "Tools~/dunya/tani.json", json.dumps(dict(sahneler=yuvarla(sahneler)), ensure_ascii=False) + "\n")


# ---------------------------------------------------------------- Ötüken Yaylası ve Erlik'in Mağarası

def eski_sahneye_tas(yol, sahne_adi, p, yaw):
    """Unity 6 biçimindeki sahneye Geçit Taşı ekler (önceki eklenen silinir)"""
    t = yol.read_text(encoding="utf-8")
    assert "\r\n" not in t
    s = Sahne(t)
    eski = [b.fid for ad, b in s.kokler() if ad == "GecitTasi"]
    if eski:
        s.sil(eski)
    k = s.kullanilan_fidler()
    metin, kok = gecit_tasi(k, "GecitTasi", p, yaw, secenekler_icin(sahne_adi))
    t = s.metin()
    i = t.index(SAHNE_KOKLERI_BASLIK)
    t = t[:i] + metin + t[i:]
    t = t.rstrip("\n") + "\n" + f"  - {{fileID: {kok}}}\n"
    yol.write_text(t, encoding="utf-8")


# ---------------------------------------------------------------- ana akış

def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    kaynak = Kaynak(sys.argv[1])
    if not (ROOT / "Assets/Otuken/Modeller/KokTas/KokTas.fbx").exists():
        raise SystemExit("önce koktas.py çalıştırılmalı")
    print("Sahneler temizleniyor:")
    haritalar = {ad: Harita(ad, ayar) for ad, ayar in HARITALAR.items()}
    sahneler = {ad: sahneyi_temizle(h, kaynak) for ad, h in haritalar.items()}

    print("Varlıklar:")
    guidler = bagimliliklar([s.metin() for s in sahneler.values()], kaynak)
    for ad, s in sahneler.items():
        for b in s.belgeler:
            if b.cls == 1001:
                b.metin = degisiklik_temizle(b.metin, guidler)
    varliklari_kopyala(guidler, kaynak)

    print("Haritalar yazılıyor:")
    for ad in SIRA:
        h = haritalar[ad]
        harita_yaz(h, sahneler[ad], haritalar, kaynak)
        scene_node(h)
        print(f"  {h.ad}: giriş {tuple(round(v, 1) for v in h.varsayilan[0])}, {len(h.kapilar)} kapı; {h.rapor}")
    sirali = [haritalar[a] for a in SIRA]
    derleme_listesi(sirali)

    # Ötüken Yaylası ve Erlik'in Mağarası'na geçit taşı (konumlar yürüme ağına göre seçildi)
    eski_cekimler = []
    for yol, sahne_adi, p, yaw, bakis_p in ESKI_TASLAR:
        eski_sahneye_tas(yol, sahne_adi, p, yaw)
        eski_cekimler.append(dict(yol=yol.relative_to(ROOT).as_posix(), kusbakisi=False, cekimler=[
            dict(ad="gecit_tasi", konum=list(bakis_p), hedef=[p[0], p[1] + 1.6, p[2]], fov=55)]))
    tani_ayari(sirali, eski_cekimler)
    print("tamam")


# (sahne yolu, sahne adı, konum, yön, bakış noktası) -- yürüme ağında düz ve boş yerler
ESKI_TASLAR = [
    # Ötüken Yaylası: köy kapısına giden yolun doğu kenarı, giriş noktasının sağ önü (ağaçların önünde)
    (ZONE, "FeaturesDemoZone", (17.2, 0.06, -27.5), -152.4, (12.5, 2.2, -36.5)),
    # Erlik'in Mağarası: giriş koridorunun açıldığı salonun batısı
    (DUNGEON, "FeaturesDemoDungeon", (78.0, 0.3, 63.0), 63.4, (86.0, 2.4, 70.0)),
]

if __name__ == "__main__":
    main()
