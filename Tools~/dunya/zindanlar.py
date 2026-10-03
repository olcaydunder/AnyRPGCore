#!/usr/bin/env python3
"""KayKit (Kay Lousberg, CC0) parçalarıyla üç harita kurar:

  Tepegöz İni       Dede Korkut'un tek gözlü devinin kemiklerle dolu mağara zindanı (Dungeon Remastered)
  Kurgan Mezarlığı  gece, ölülerin kalktığı eski kurganlar ve mezar taşları (Halloween Bits)
  Tamu Zindanı      Erlik Han'ın yeraltı zindanı: dikenli zeminler, ızgaralar, kızıl sancaklar

Haritalar ASCII çizimden kurulur (her karakter 4x4 metrelik bir hücre). Duvarlar (ya da çitler) yürünebilir
hücre ile boşluk arasındaki her kenara kendiliğinden konur, köşelere sütun gelir. Düşmanlar, sandıklar,
giriş noktası ve Geçit Taşı çizimdeki harflerden yerleşir. Yürüme ağı derlemede pişer (HaritaHazirlik).

Kullanım (depo kök klasöründen, haritalar.py'den sonra):
    git clone https://github.com/KayKit-Game-Assets/KayKit-Dungeon-Remastered-1.0 /tmp/kk-zindan
    git clone https://github.com/KayKit-Game-Assets/KayKit-Halloween-Bits-1.0 /tmp/kk-mezar
    python3 "Tools~/dunya/zindanlar.py" /tmp/kk-zindan /tmp/kk-mezar
"""
import hashlib
import json
import math
import random
import re
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import fbxmeta as fm  # noqa: E402
import haritalar as hr  # noqa: E402
import hazineler  # noqa: E402
import unitysahne  # noqa: E402
import yaratiklar  # noqa: E402
from unitysahne import Sahne  # noqa: E402

ROOT = hr.ROOT
K = fm.Kimlik("6c2a9e41-3b5d-4f8a-9e27-1d0c8b7a6f53")
HUCRE = 4.0
DUNGEON = hr.DUNGEON

# ---------------------------------------------------------------- parçalar

ZINDAN_PARCALARI = {
    # ad: katı mı (çarpışma örgüsü)
    "floor_tile_large": True, "floor_tile_large_rocks": True, "floor_dirt_large": True,
    "floor_dirt_large_rocky": True, "floor_tile_big_grate": True, "floor_tile_big_spikes": True,
    "wall": True, "wall_broken": True, "wall_cracked": True, "wall_arched": True, "wall_window_closed": True,
    "wall_shelves": True, "wall_gated": True, "pillar": True, "pillar_decorated": True, "column": True,
    "torch_mounted": False, "torch_lit": True, "barrel_large": True, "barrel_small": True,
    "barrel_small_stack": True, "box_large": True, "box_stacked": True, "crates_stacked": True,
    "table_long_decorated_A": True, "table_medium_broken": True, "chair": True, "stool": True,
    "shelf_large": True, "keg_decorated": True, "candle_triple": False, "rubble_large": True,
    "rubble_half": True, "sword_shield_broken": False, "coin_stack_large": False, "chest_gold": True,
    "banner_red": False, "banner_patternA_red": False, "banner_shield_red": False, "banner_triple_red": False,
    "bed_frame": True, "trunk_large_A": True,
}
MEZAR_PARCALARI = {
    "floor_dirt": True, "floor_dirt_grave": True, "path_A": False, "path_B": False, "path_C": False,
    "path_D": False, "grave_A": True, "grave_A_destroyed": True, "grave_B": True, "gravestone": True,
    "gravemarker_A": True, "gravemarker_B": True, "crypt": True, "coffin": True, "coffin_decorated": True,
    "fence": True, "fence_broken": True, "fence_pillar": True, "arch_gate": True, "tree_dead_large": True,
    "tree_dead_large_decorated": True, "tree_dead_medium": True, "tree_dead_small": True,
    "lantern_standing": True, "post_lantern": True, "post_skull": True, "bone_A": False, "bone_B": False,
    "bone_C": False, "skull": False, "skull_candle": False, "ribcage": True, "shrine_candles": True,
    "bench": True, "candle_triple": False, "pillar": True,
}
ZINDAN_KLASOR = ROOT / "Assets/Otuken/Modeller/KayKitZindan"
MEZAR_KLASOR = ROOT / "Assets/Otuken/Modeller/KayKitMezar"


def parcalari_al(kaynak, klasor, parcalar, doku_adi, malzeme_adi, fbx_alt):
    """FBX'leri kopyalar, tek malzemeli (URP Lit) içe aktarma ayarlarını yazar"""
    fm.folder_meta(klasor, K)
    for alt in ("FBX", "Doku", "Malzeme"):
        fm.folder_meta(klasor / alt, K)
    doku = next(Path(kaynak).rglob(doku_adi))
    hedef_doku = klasor / "Doku" / doku_adi
    fm.write_bytes(hedef_doku, doku.read_bytes())
    fm.texture_meta(hedef_doku, K.guid("doku:" + doku_adi), 512)
    malzeme = fm.urp_material(klasor / "Malzeme" / f"{malzeme_adi}.mat", malzeme_adi, K.guid("malzeme:" + malzeme_adi),
                              K.guid("doku:" + doku_adi), smoothness=0.15)
    fbx_klasor = next(p for p in Path(kaynak).rglob(fbx_alt) if p.is_dir())
    for ad, kati in parcalar.items():
        src = fbx_klasor / f"{ad}.fbx"
        dst = klasor / "FBX" / f"{ad}.fbx"
        fm.write_bytes(dst, src.read_bytes())
        fm.fbx_meta(dst, parca_guid(klasor, ad), remaps={"texture": malzeme}, animation_type=0, materials=True,
                    bake_axis=True, colliders=kati)
    lisans = next(Path(kaynak).rglob("LICENSE.txt"))
    fm.write_text(klasor / "LICENSE.txt", lisans.read_text(encoding="utf-8", errors="ignore"))
    fm.write_text(klasor / "LICENSE.txt.meta",
                  f"fileFormatVersion: 2\nguid: {K.guid('lisans:' + klasor.name)}\nTextScriptImporter:\n"
                  "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def parca_guid(klasor, ad):
    return K.guid(f"parca:{klasor.name}:{ad}")


# ---------------------------------------------------------------- haritalar (ASCII)
# Ortak harfler:  # boşluk   . zemin   , zemin çeşidi   S giriş (kuzeye bakar)   G Geçit Taşı
#                 e düşman   E güçlü düşman   B boss   C büyük sandık   c sandık
# Zindan:         b fıçılar  x sandık yığını  t masa  r moloz  k kemikler  ^ dikenli zemin  g ızgara
#                 P sütun    f ayaklı meşale  K tabut  a mumlar
# Mezarlık:       p patika   + mezar   t kuru ağaç   L fenerli direk   M türbe (kript)   s sunak   b bank
#                 A kemerli kapı (kuzey çitine)

HARITALAR = {
    "TepegozIni": dict(
        ad="Tepegöz İni", tur="zindan", zemin=("floor_dirt_large", "floor_dirt_large_rocky"),
        duvar=("wall", "wall", "wall_cracked", "wall_broken"), muzik="Erlik Magarasi", ayak="Footstep Hits Gravel Fast",
        aciklama="Dede Korkut'un anlattığı tek gözlü Tepegöz'ün ini. Yerler yediği yiğitlerin kemikleriyle dolu; "
                 "Basat'tan beri kimse gözüne mil çekmeye cesaret edemedi.",
        dusmanlar=["Kormos", "Agulu Kormos", "Kizil Cin", "Cali Cini"], guclu=["Kormos", "Agulu Kormos"],
        boss="Tepegoz", ek_seviye=2,
        harita="""
##############
######kr.B.k##
######..,k..r#
######.k..,..#
##.e.##..E.c.#
##k..r.,..k..#
##,.e##.e..k.#
##..,#########
####.#########
#.e..,..#....#
#k.x.b..e.,k.#
#..,..e.#..c.#
#.c.....#E...#
####,########
####..G..####
####...S.####
#############
"""),
    "KurganMezarligi": dict(
        ad="Kurgan Mezarlığı", tur="mezar", muzik="Erlik Magarasi", ayak="Footstep Hits Mud",
        aciklama="Eski beylerin yattığı kurganlar. Ay Dede bulutlara saklandığında Erlik'in kamları ölüleri "
                 "mezarlarından kaldırır.",
        dusmanlar=["Kemik Er", "Kemik Akinci", "Kemik Kam"], guclu=["Kemik Alp"], boss="Kemik Kagan", ek_seviye=3,
        harita="""
################
#t.+.+.L.+.+.t.#
#.+.e.+.+.e.+..#
#..+...M...+.+.#
#t.+.+.....+.e.#
#..E..s..B..+..#
#.+.+.L.....L+.#
#..+..+.p.+..t.#
#.e..+..p..e.+.#
#.+.c..+p+....c#
#t...+..p..+.+.#
#..+.L..pE.+...#
#.+..+..p...+.t#
#..e...bpb..e..#
#t.+..G.p.+..+.#
#.......S......#
################
"""),
    "TamuZindani": dict(
        ad="Tamu Zindanı", tur="zindan", zemin=("floor_tile_large", "floor_tile_large_rocks"),
        duvar=("wall", "wall", "wall_cracked", "wall_window_closed", "wall_gated"), muzik="Erlik Magarasi",
        ayak="Footstep Hits Stone",
        aciklama="Tamu: Erlik Han'ın kötü ruhları zincire vurduğu yeraltı zindanı. Dikenli taşların arasında "
                 "zindancıları nöbet tutar.",
        dusmanlar=["Kara Kam", "Abasi", "Sulmus", "Kara Otaci"], guclu=["Kan Suvarisi", "Abasi"],
        boss="Tamu Bekcisi", ek_seviye=4,
        harita="""
#################
#####a..B..a#####
#####.^...^.#####
#####P.g.g.P#####
#####..E.E..#####
#######.^.#######
#.x.b##.g.##.t.c#
#.e...#...#..e..#
#..^..g.e.g..^..#
#.e.C.#...#.e.c.#
###.###.^.###.###
###.###...###.###
#..e...g.g...e..#
#.k..P.....P..x.#
#.....f.G.f.....#
#######.S.#######
#################
"""),
}


def izgara(metin):
    satirlar = [s for s in metin.strip("\n").split("\n")]
    en = max(len(s) for s in satirlar)
    return [s.ljust(en, "#") for s in satirlar]


def hucre_merkezi(r, c, satir_sayisi):
    return (c * HUCRE, 0.0, (satir_sayisi - 1 - r) * HUCRE)


YONLER = {(0, -1): ("K", (0.0, 1.0)), (0, 1): ("G", (0.0, -1.0)), (1, 0): ("D", (1.0, 0.0)), (-1, 0): ("B", (-1.0, 0.0))}
# (dc, dr): (ad, dünya yönü (x, z))  -- satır aşağı indikçe z azalır


def yurunur(ch):
    return ch != "#"


# ---------------------------------------------------------------- sahne yazımı

class SahneYazici:
    def __init__(self, ad):
        self.ad = ad
        self.k = set()
        self.belgeler = []
        self.kokler = []
        self.isik_sayisi = 0
        self.gruplar = []

    def kimlik(self, ad):
        return hr.kimlik(f"{self.ad}:{ad}", self.k)

    def grup(self, ad):
        go, tr = self.kimlik(f"grup:{ad}:go"), self.kimlik(f"grup:{ad}:tr")
        g = dict(go=go, tr=tr, ad=ad, cocuk=[])
        self.kokler.append(tr)
        self.gruplar.append(g)
        return g

    def metin(self, baslik):
        gruplar = "".join(hr.go_belge(g["go"], g["ad"], [g["tr"]], katman=0)
                          + hr.tr_belge(g["tr"], g["go"], (0, 0, 0), cocuklar=g["cocuk"]) for g in self.gruplar)
        kokler = "".join(f"  - {{fileID: {k}}}\n" for k in self.kokler)
        return (baslik + gruplar + "".join(self.belgeler)
                + f"{hr.SAHNE_KOKLERI_BASLIK}\n  m_ObjectHideFlags: 0\n  m_Roots:\n{kokler}")

    def model(self, grup, klasor, parca, p, yaw=0.0, olcek=None, ad=None):
        n = len(grup["cocuk"])
        metin, _, tid = hr.prefab_ornegi(self.k, ad or f"{grup['ad']}_{parca}_{n}", parca_guid(klasor, parca),
                                         hr.FBX_KOK_TR, hr.FBX_KOK_GO, p, unitysahne.yaw_q(yaw),
                                         s=(olcek, olcek, olcek) if olcek else None, ust=grup["tr"])
        self.belgeler.append(metin)
        grup["cocuk"].append(tid)

    def isik(self, grup, p, renk=(1.0, 0.62, 0.3), siddet=2.2, menzil=9.0, tur=2, golge=0, yaw=0.0, egim=0.0):
        go, tr, li, ek = (self.kimlik(f"isik{self.isik_sayisi}:{x}") for x in ("go", "tr", "li", "ek"))
        self.isik_sayisi += 1
        r, g, b = renk
        q = unitysahne.qmul(unitysahne.yaw_q(yaw), (math.sin(math.radians(egim) / 2), 0, 0, math.cos(math.radians(egim) / 2)))
        metin = hr.go_belge(go, f"Isik_{self.isik_sayisi}", [tr, li, ek]) + hr.tr_belge(tr, go, p, q, ust=grup["tr"])
        metin += (f"--- !u!108 &{li}\nLight:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
                  f"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go}}}\n"
                  f"  m_Enabled: 1\n  serializedVersion: 12\n  m_Type: {tur}\n  m_Color: {{r: {r}, g: {g}, b: {b}, a: 1}}\n"
                  f"  m_Intensity: {siddet}\n  m_Range: {menzil}\n  m_SpotAngle: 30\n  m_InnerSpotAngle: 21.80208\n"
                  f"  m_CookieSize2D: {{x: 10, y: 10}}\n  m_Shadows:\n    m_Type: {golge}\n    m_Resolution: -1\n"
                  "    m_CustomResolution: -1\n    m_Strength: 0.8\n    m_Bias: 0.05\n    m_NormalBias: 0.4\n"
                  "    m_NearPlane: 0.2\n    m_CullingMatrixOverride:\n      e00: 1\n      e01: 0\n      e02: 0\n"
                  "      e03: 0\n      e10: 0\n      e11: 1\n      e12: 0\n      e13: 0\n      e20: 0\n      e21: 0\n"
                  "      e22: 1\n      e23: 0\n      e30: 0\n      e31: 0\n      e32: 0\n      e33: 1\n"
                  "    m_UseCullingMatrixOverride: 0\n  m_Cookie: {fileID: 0}\n  m_DrawHalo: 0\n  m_Flare: {fileID: 0}\n"
                  "  m_RenderMode: 0\n  m_CullingMask:\n    serializedVersion: 2\n    m_Bits: 2204106559\n"
                  "  m_RenderingLayerMask: 1\n  m_Lightmapping: 4\n  m_LightShadowCasterMode: 0\n"
                  "  m_AreaSize: {x: 1, y: 1}\n  m_BounceIntensity: 1\n  m_ColorTemperature: 6570\n"
                  "  m_UseColorTemperature: 0\n  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}\n"
                  "  m_UseBoundingSphereOverride: 0\n  m_UseViewFrustumForShadowCasterCull: 1\n  m_ForceVisible: 0\n"
                  "  m_ShadowRadius: 0\n  m_ShadowAngle: 0\n  m_LightUnit: 1\n  m_LuxAtDistance: 1\n"
                  "  m_EnableSpotReflector: 1\n")
        metin += hr.mb_bas(ek, go, "474bcb49853aa07438625e644c072ee6") + (
            "  m_UsePipelineSettings: 1\n  m_AdditionalLightsShadowResolutionTier: 2\n  m_CustomShadowLayers: 0\n"
            "  m_LightCookieSize: {x: 1, y: 1}\n  m_LightCookieOffset: {x: 0, y: 0}\n  m_SoftShadowQuality: 0\n"
            "  m_RenderingLayersMask:\n    serializedVersion: 0\n    m_Bits: 1\n  m_ShadowRenderingLayersMask:\n"
            "    serializedVersion: 0\n    m_Bits: 1\n  m_Version: 4\n  m_LightLayerMask: 1\n  m_ShadowLayerMask: 1\n"
            "  m_RenderingLayers: 1\n  m_ShadowRenderingLayers: 1\n")
        self.belgeler.append(metin)
        grup["cocuk"].append(tr)
        return li

    def kok_ekle(self, metin, kok):
        self.belgeler.append(metin)
        self.kokler.append(kok)


def sahne_basligi(gok_guid, ortam, sis, gunes_fid):
    """Unity 6 sahne başı: görünürlük, render, ışık haritası, yürüme ağı ayarları (Erlik'in Mağarası'ndan)"""
    on, bl = unitysahne.uyaml.oku(DUNGEON)
    metin = on + "".join(b.metin for b in bl[:4])
    gok, ufuk, yer = ortam
    metin = re.sub(r"  m_SkyboxMaterial: \{[^}]*\}", f"  m_SkyboxMaterial: {{fileID: 2100000, guid: {gok_guid}, type: 2}}", metin)
    metin = re.sub(r"  m_AmbientMode: \d+", "  m_AmbientMode: 1", metin)
    for alan, c in (("m_AmbientSkyColor", gok), ("m_AmbientEquatorColor", ufuk), ("m_AmbientGroundColor", yer)):
        metin = re.sub(rf"  {alan}: \{{[^}}]*\}}", f"  {alan}: {{r: {c[0]}, g: {c[1]}, b: {c[2]}, a: 1}}", metin)
    renk, yogunluk = sis
    metin = re.sub(r"  m_Fog: \d", "  m_Fog: 1", metin)
    metin = re.sub(r"  m_FogColor: \{[^}]*\}", f"  m_FogColor: {{r: {renk[0]}, g: {renk[1]}, b: {renk[2]}, a: 1}}", metin)
    metin = re.sub(r"  m_FogDensity: [\d.]+", f"  m_FogDensity: {yogunluk}", metin)
    metin = re.sub(r"  m_Sun: \{fileID: \d+\}", f"  m_Sun: {{fileID: {gunes_fid}}}", metin)
    return metin


def dusman_docu(s, ad, profil, p, yaw, ek, boss=False):
    doc, tid = yaratiklar.spawn_doc(ad, p[0], p[1], p[2], yaw, profil, ek, 600 if boss else 120, s.k)
    s.kok_ekle(doc, tid)


def sandik_docu(s, ad, prefab, p, yaw, bilgi):
    doc, tid = hazineler.instance_doc(ad, prefab, p[0], p[1], p[2], yaw, bilgi, s.k)
    s.kok_ekle(doc, tid)


def zindan_kur(dosya, h, rng):
    g = izgara(h["harita"])
    R, C = len(g), len(g[0])
    s = SahneYazici(dosya)
    zemin, duvarlar, sutunlar, esyalar, isiklar = (s.grup(x) for x in ("Zemin", "Duvarlar", "Sutunlar", "Esyalar", "Isiklar"))
    kl = ZINDAN_KLASOR
    hucreler = [(r, c) for r in range(R) for c in range(C) if yurunur(g[r][c])]
    noktalar = {}
    for r, c in hucreler:
        ch = g[r][c]
        p = hucre_merkezi(r, c, R)
        if ch == "^":
            s.model(zemin, kl, "floor_tile_big_spikes", p, rng.choice([0, 90, 180, 270]))
        elif ch == "g":
            s.model(zemin, kl, "floor_tile_big_grate", p, rng.choice([0, 90]))
        else:
            parca = h["zemin"][1] if ch == "," or rng.random() < 0.18 else h["zemin"][0]
            s.model(zemin, kl, parca, p, rng.choice([0, 90, 180, 270]))
        noktalar.setdefault(ch, []).append((r, c))

    # duvarlar: yürünür hücre ile boşluk arasındaki her kenar
    kenarlar = []
    for r, c in hucreler:
        for (dc, dr), (yad, (dx, dz)) in YONLER.items():
            r2, c2 = r + dr, c + dc
            if not (0 <= r2 < R and 0 <= c2 < C) or not yurunur(g[r2][c2]):
                kenarlar.append((r, c, dx, dz))
    meşale = 0
    for i, (r, c, dx, dz) in enumerate(kenarlar):
        cx, _, cz = hucre_merkezi(r, c, R)
        p = (cx + dx * HUCRE / 2, 0.0, cz + dz * HUCRE / 2)
        yaw = 0.0 if dx == 0 else 90.0
        parca = rng.choice(h["duvar"])
        s.model(duvarlar, kl, parca, p, yaw)
        # duvar meşalesi: odaya bakan yüzde, seyrek; ışık sayısı sınırlı (mobil)
        if (r * 7 + c * 3 + int(dx * 5 + dz * 11)) % 5 == 0 and s.isik_sayisi < 9:
            ic = (-dx, -dz)
            tp = (cx + dx * (HUCRE / 2 - 0.5), 1.9, cz + dz * (HUCRE / 2 - 0.5))
            tyaw = math.degrees(math.atan2(ic[0], ic[1]))
            s.model(esyalar, kl, "torch_mounted", tp, tyaw)
            s.isik(isiklar, (tp[0] + ic[0] * 0.6, 2.9, tp[2] + ic[1] * 0.6))
            meşale += 1
        elif (r + c) % 6 == 2 and rng.random() < 0.5:
            ic = (-dx, -dz)
            bp = (cx + dx * (HUCRE / 2 - 0.5), 0.6, cz + dz * (HUCRE / 2 - 0.5))
            s.model(esyalar, kl, rng.choice(["banner_red", "banner_patternA_red", "banner_shield_red"]), bp,
                    math.degrees(math.atan2(ic[0], ic[1])))

    # köşe sütunları: duvarların yön değiştirdiği ya da birleştiği köşeler
    kose = {}
    for r, c, dx, dz in kenarlar:
        cx, _, cz = hucre_merkezi(r, c, R)
        ex, ez = cx + dx * HUCRE / 2, cz + dz * HUCRE / 2
        if dx == 0:
            uclar = [(ex - 2, ez), (ex + 2, ez)]
        else:
            uclar = [(ex, ez - 2), (ex, ez + 2)]
        for u in uclar:
            kose.setdefault((round(u[0], 2), round(u[1], 2)), set()).add("y" if dx == 0 else "d")
    for (x, z), yonler in kose.items():
        if len(yonler) > 1:
            s.model(sutunlar, kl, "pillar", (x, 0.0, z))

    # nesneler
    for r, c in hucreler:
        ch = g[r][c]
        cx, cy, cz = hucre_merkezi(r, c, R)
        j = lambda a=1.0: (rng.random() - 0.5) * a
        if ch == "b":
            s.model(esyalar, kl, "barrel_large", (cx - 0.8, 0, cz + 0.6), rng.random() * 360)
            s.model(esyalar, kl, "barrel_small_stack", (cx + 0.9, 0, cz - 0.5), rng.random() * 360)
        elif ch == "x":
            s.model(esyalar, kl, rng.choice(["crates_stacked", "box_stacked"]), (cx + j(), 0, cz + j()), rng.choice([0, 90]))
        elif ch == "t":
            s.model(esyalar, kl, "table_long_decorated_A", (cx, 0, cz), 90)
            s.model(esyalar, kl, "stool", (cx - 1.4, 0, cz + 0.6), 90)
            s.model(esyalar, kl, "stool", (cx + 1.4, 0, cz - 0.6), -90)
        elif ch == "r":
            s.model(esyalar, kl, rng.choice(["rubble_large", "rubble_half"]), (cx + j(), 0, cz + j()), rng.random() * 360)
        elif ch == "k":
            s.model(esyalar, MEZAR_KLASOR, rng.choice(["bone_A", "bone_B", "bone_C"]), (cx + j(2), 0.05, cz + j(2)), rng.random() * 360)
            s.model(esyalar, MEZAR_KLASOR, rng.choice(["skull", "ribcage", "bone_B"]), (cx + j(2), 0.05, cz + j(2)), rng.random() * 360)
        elif ch == "P":
            s.model(sutunlar, kl, "pillar_decorated", (cx, 0, cz))
        elif ch == "f":
            s.model(esyalar, kl, "torch_lit", (cx, 0, cz))
            s.isik(isiklar, (cx, 2.6, cz), siddet=2.6, menzil=11)
        elif ch == "a":
            s.model(esyalar, kl, "candle_triple", (cx + j(), 0.05, cz + j()))
            s.model(esyalar, MEZAR_KLASOR, "skull_candle", (cx + j(2), 0.05, cz + j(2)), rng.random() * 360)
            s.isik(isiklar, (cx, 1.2, cz), renk=(1.0, 0.75, 0.45), siddet=1.5, menzil=6)
        elif ch == "K":
            s.model(esyalar, MEZAR_KLASOR, "coffin_decorated", (cx, 0, cz), rng.choice([0, 90]))
    return s, g, noktalar


def mezar_kur(dosya, h, rng):
    g = izgara(h["harita"])
    R, C = len(g), len(g[0])
    s = SahneYazici(dosya)
    zemin, citler, mezarlar, agaclar, isiklar = (s.grup(x) for x in ("Zemin", "Citler", "Mezarlar", "Agaclar", "Isiklar"))
    kl = MEZAR_KLASOR
    hucreler = [(r, c) for r in range(R) for c in range(C) if yurunur(g[r][c])]
    noktalar = {}
    for r, c in hucreler:
        noktalar.setdefault(g[r][c], []).append((r, c))
    ic = set(hucreler)
    # zemin: içerisi ve çitin dışında iki hücrelik orman şeridi
    dis = set()
    for r in range(-2, R + 2):
        for c in range(-2, C + 2):
            if (r, c) in ic:
                continue
            if any((r + a, c + b) in ic for a in range(-2, 3) for b in range(-2, 3)):
                dis.add((r, c))
    for r, c in sorted(ic | dis):
        p = hucre_merkezi(r, c, R)
        parca = "floor_dirt_grave" if (r, c) in ic and g[r][c] == "+" else "floor_dirt"
        s.model(zemin, kl, parca, p, rng.choice([0, 90, 180, 270]))
    for r, c in sorted(dis):
        cx, _, cz = hucre_merkezi(r, c, R)
        if rng.random() < 0.75:
            s.model(agaclar, kl, rng.choice(["tree_dead_large", "tree_dead_medium", "tree_dead_large_decorated"]),
                    (cx + (rng.random() - 0.5) * 2.5, 0, cz + (rng.random() - 0.5) * 2.5), rng.random() * 360,
                    olcek=round(1.0 + rng.random() * 0.6, 2))
        if rng.random() < 0.35:
            s.model(mezarlar, kl, rng.choice(["grave_A_destroyed", "gravemarker_B", "gravestone"]),
                    (cx + (rng.random() - 0.5) * 2.5, 0, cz + (rng.random() - 0.5) * 2.5), rng.random() * 360)

    # çit: iç hücre ile dış arasındaki kenarlar; arada kırık çit, kuzeyde (A) kemerli kapı
    kose = set()
    duvarlar = []
    for r, c in hucreler:
        for (dc, dr), (yad, (dx, dz)) in YONLER.items():
            if (r + dr, c + dc) in ic:
                continue
            cx, _, cz = hucre_merkezi(r, c, R)
            p = (cx + dx * HUCRE / 2, 0.0, cz + dz * HUCRE / 2)
            yaw = 0.0 if dx == 0 else 90.0
            s.model(citler, kl, "fence_broken" if rng.random() < 0.2 else "fence", p, yaw)
            duvarlar.append((p, yaw))
            for u in ([(p[0] - 2, p[2]), (p[0] + 2, p[2])] if dx == 0 else [(p[0], p[2] - 2), (p[0], p[2] + 2)]):
                kose.add((round(u[0], 2), round(u[1], 2)))
    for x, z in sorted(kose):
        s.model(citler, kl, "fence_pillar", (x, 0, z))
    # görünmez duvar: çitin üstünden atlanmasın
    for i, (p, yaw) in enumerate(duvarlar):
        go, tr, bx = (s.kimlik(f"engel{i}:{x}") for x in ("go", "tr", "bx"))
        citler["cocuk"].append(tr)
        s.belgeler.append(hr.go_belge(go, "GorunmezDuvar", [tr, bx]) + hr.tr_belge(tr, go, p, unitysahne.yaw_q(yaw), ust=citler["tr"])
                          + hr.kutu_belge(bx, go, (4.2, 6.0, 0.4), (0, 3.0, 0), tetik=False))

    for r, c in hucreler:
        ch = g[r][c]
        cx, _, cz = hucre_merkezi(r, c, R)
        j = lambda a=1.0: (rng.random() - 0.5) * a
        if ch == "+":
            s.model(mezarlar, kl, rng.choice(["grave_A", "grave_B", "gravestone", "gravemarker_A", "grave_A_destroyed"]),
                    (cx + j(0.8), 0, cz + j(0.8)), rng.choice([0, 0, 180, 10, -10]))
            if rng.random() < 0.3:
                s.model(mezarlar, kl, rng.choice(["bone_A", "bone_C", "skull"]), (cx + j(2.5), 0.03, cz + j(2.5)), rng.random() * 360)
        elif ch == "t":
            s.model(agaclar, kl, rng.choice(["tree_dead_large", "tree_dead_medium", "tree_dead_small"]),
                    (cx + j(), 0, cz + j()), rng.random() * 360)
        elif ch == "L":
            s.model(isiklar, kl, "lantern_standing", (cx + 0.8, 0, cz + 0.6))
            s.model(isiklar, kl, "post_lantern", (cx, 0, cz), rng.random() * 360)
            s.isik(isiklar, (cx, 3.0, cz), renk=(1.0, 0.7, 0.35), siddet=2.4, menzil=12)
        elif ch == "M":
            s.model(mezarlar, kl, "crypt", (cx, 0, cz), 180)
            s.isik(isiklar, (cx, 2.5, cz - 4.8), renk=(0.45, 0.9, 0.7), siddet=1.8, menzil=8)
        elif ch == "s":
            s.model(mezarlar, kl, "shrine_candles", (cx, 0, cz), 180)
            s.isik(isiklar, (cx, 1.5, cz - 1.0), renk=(1.0, 0.75, 0.45), siddet=1.6, menzil=6)
        elif ch == "b":
            s.model(mezarlar, kl, "bench", (cx, 0, cz), 90)
        elif ch == "p":
            for a, b in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
                s.model(zemin, kl, rng.choice(["path_A", "path_B", "path_C", "path_D"]), (cx + a, 0.02, cz + b), rng.choice([0, 90, 180, 270]))
    # türbenin kapladığı yer: çevresindeki hücrelerde mezar olmasın diye haritada boş bırakıldı
    return s, g, noktalar


def harita_kur(dosya, h, bilgi, mevcut):
    rng = random.Random(dosya)
    if h["tur"] == "mezar":
        s, g, noktalar = mezar_kur(dosya, h, rng)
        gok, ortam, sis = "Night", ((0.20, 0.25, 0.42), (0.11, 0.13, 0.22), (0.04, 0.05, 0.07)), ((0.07, 0.10, 0.17), 0.02)
        gunes = dict(renk=(0.62, 0.72, 1.0), siddet=0.55, egim=48, yaw=-35)
    else:
        s, g, noktalar = zindan_kur(dosya, h, rng)
        gok, ortam, sis = "Night", ((0.16, 0.15, 0.20), (0.10, 0.09, 0.11), (0.04, 0.035, 0.035)), ((0.03, 0.025, 0.03), 0.025)
        gunes = dict(renk=(0.55, 0.55, 0.75), siddet=0.35, egim=60, yaw=20)
    R = len(g)

    # ay ışığı (gölgeli yönlü ışık)
    isiklar = s.grup("Gok")
    gunes_li = s.isik(isiklar, (0, 30, 0), renk=gunes["renk"], siddet=gunes["siddet"], tur=1, golge=2,
                      yaw=gunes["yaw"], egim=gunes["egim"], menzil=10)

    # giriş, Geçit Taşı, yürüme ağı, SceneConfig
    (sr, sc), = noktalar["S"]
    sp = hucre_merkezi(sr, sc, R)
    metin, _, tid = hr.prefab_ornegi(s.k, "DefaultSpawnLocation", *hr.SPAWN, (sp[0], 0.1, sp[2]))
    s.kok_ekle(metin, tid)
    metin, _, tid = hr.prefab_ornegi(s.k, "SceneConfig", *hr.SCENECONFIG, (0, 0, 0))
    s.kok_ekle(metin, tid)
    (gr, gc), = noktalar["G"]
    gp = hucre_merkezi(gr, gc, R)
    metin, kok = hr.gecit_tasi(s.k, "GecitTasi", (gp[0], 0.05, gp[2]), math.degrees(math.atan2(sp[0] - gp[0], sp[2] - gp[2])),
                               hr.secenekler_icin(dosya))
    s.kok_ekle(metin, kok)
    ngo, ntr, nmb = (s.kimlik(f"Navigation:{x}") for x in ("go", "tr", "mb"))
    nav = mevcut.get(dosya)
    s.kok_ekle(hr.go_belge(ngo, "Navigation", [ntr, nmb]) + hr.tr_belge(ntr, ngo, (0, 0, 0)) + hr.mb_bas(nmb, ngo, hr.NAVMESH_BETIK) + (
        "  m_SerializedVersion: 0\n  m_AgentTypeID: 0\n  m_CollectObjects: 0\n  m_Size: {x: 10, y: 10, z: 10}\n"
        "  m_Center: {x: 0, y: 2, z: 0}\n  m_LayerMask:\n    serializedVersion: 2\n    m_Bits: 1\n"
        "  m_UseGeometry: 1\n  m_DefaultArea: 0\n  m_GenerateLinks: 0\n  m_IgnoreNavMeshAgent: 1\n"
        "  m_IgnoreNavMeshObstacle: 1\n  m_OverrideTileSize: 0\n  m_TileSize: 256\n  m_OverrideVoxelSize: 0\n"
        "  m_VoxelSize: 0.16666667\n  m_MinRegionArea: 2\n"
        f"  m_NavMeshData: {nav or '{fileID: 0}'}\n  m_BuildHeightMesh: 0\n"), ntr)

    # düşmanlar ve sandıklar
    def yaw_rast(r, c):
        return (r * 37 + c * 61) % 360
    sayac = 0
    for harf, liste in (("e", h["dusmanlar"]), ("E", h["guclu"]), ("B", [h["boss"]])):
        for i, (r, c) in enumerate(noktalar.get(harf, [])):
            p = hucre_merkezi(r, c, R)
            profil = liste[(i + r) % len(liste)]
            ek = h["ek_seviye"] + (1 if harf == "E" else 2 if harf == "B" else 0)
            dusman_docu(s, f"TR_{dosya}_{harf}{i + 1}", profil, (p[0], 0.1, p[2]), yaw_rast(r, c), ek, boss=harf == "B")
            sayac += 1
            if harf == "e" and h["tur"] == "zindan":
                # her noktada ikişer düşman: biri hücrenin öbür köşesinde
                profil2 = liste[(i + c + 1) % len(liste)]
                dusman_docu(s, f"TR_{dosya}_{harf}{i + 1}b", profil2, (p[0] + 1.2, 0.1, p[2] - 1.0), yaw_rast(c, r), ek)
                sayac += 1
    for harf, prefab in (("C", "BuyukHazineSandigi"), ("c", "HazineSandigi")):
        for i, (r, c) in enumerate(noktalar.get(harf, [])):
            p = hucre_merkezi(r, c, R)
            sandik_docu(s, f"TRL_{dosya}_{prefab}_{i + 1}", prefab, (p[0], 0.05, p[2]), yaw_rast(r, c), bilgi)

    return s, sp, gp, gunes_li, gok, ortam, sis, sayac


class Kayit:
    """haritalar.py'nin SceneNode / derleme listesi / tanı yazıcılarının beklediği alanlar"""

    def __init__(self, dosya, h):
        self.dosya = dosya
        self.kaynak = dosya
        self.ad = h["ad"]
        self.aciklama = h["aciklama"]
        self.muzik = h["muzik"]
        self.zemin = "kaya"
        self.sahne_yolu = hr.HEDEF / dosya / f"{dosya}.unity"
        self.kapilar = []


def scene_node(kayit, ayak, magara):
    hr.scene_node(kayit)
    yol = hr.SCENENODE / f"{kayit.dosya}SceneNode.asset"
    t = yol.read_text(encoding="utf-8")
    t = re.sub(r"^  footStepProfiles:\n(?:  - .*\n)+", f"  footStepProfiles:\n  - {ayak}\n", t, flags=re.M)
    if magara:
        for alan in ("dayAmbientSoundsAudio", "nightAmbientSoundsAudio"):
            t = re.sub(rf"^  {alan}: \{{[^}}]*\}}", f"  {alan}: {{fileID: 8300000, guid: {hr.MAGARA_SESI}, type: 3}}", t, flags=re.M)
    yol.write_text(t, encoding="utf-8")


def mevcut_navmesh(dosya):
    """depoya eklenmiş pişmiş yürüme ağı varsa ona başvuru"""
    meta = hr.HEDEF / dosya / dosya / "NavMesh-Navigation.asset.meta"
    if meta.exists():
        g = re.search(r"^guid: (\w+)", meta.read_text(), re.M).group(1)
        return f"{{fileID: 23800000, guid: {g}, type: 2}}"
    return None


def main():
    if len(sys.argv) < 3:
        raise SystemExit(__doc__)
    parcalari_al(sys.argv[1], ZINDAN_KLASOR, ZINDAN_PARCALARI, "dungeon_texture.png", "M_KayKitZindan", "fbx")
    parcalari_al(sys.argv[2], MEZAR_KLASOR, MEZAR_PARCALARI, "halloweenbits_texture.png", "M_KayKitMezar", "fbx(unity)")
    yaratiklar.write_profiles()
    yaratiklar.canavar_boss_stratejisi()
    bilgi = hazineler.prefab_info()
    gece = re.search(r"^guid: (\w+)", (hr.ORTAK / "Art/Skybox/Painted/Materials/Night.mat.meta").read_text(), re.M).group(1)
    kayitlar = []
    tani = json.loads((ROOT / "Tools~/dunya/tani.json").read_text(encoding="utf-8"))
    for dosya, h in HARITALAR.items():
        mevcut = {dosya: mevcut_navmesh(dosya)}
        s, sp, gp, gunes_li, gok, ortam, sis, sayac = harita_kur(dosya, h, bilgi, mevcut)
        kayit = Kayit(dosya, h)
        metin = s.metin(sahne_basligi(gece, ortam, sis, gunes_li))
        hr.yaz(kayit.sahne_yolu, metin)
        hr.klasor_meta(kayit.sahne_yolu.parent)
        hr.yaz(Path(str(kayit.sahne_yolu) + ".meta"),
               f"fileFormatVersion: 2\nguid: {hr.sahne_guid(dosya)}\nDefaultImporter:\n  externalObjects: {{}}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
        scene_node(kayit, h["ayak"], h["tur"] == "zindan")
        kayitlar.append(kayit)
        yol = kayit.sahne_yolu.relative_to(ROOT).as_posix()
        tani["sahneler"] = [x for x in tani["sahneler"] if x["yol"] != yol]
        tani["sahneler"].append(dict(yol=yol, kusbakisi=True, cekimler=[
            dict(ad="gecit_tasi", konum=[round(sp[0], 2), 2.4, round(sp[2] - 3, 2)], hedef=[gp[0], 1.6, gp[2]], fov=55)]))
        print(f"  {h['ad']}: {len(s.belgeler)} belge, {s.isik_sayisi} ışık, {sayac} düşman")
    # derleme listesi: haritalar.py'nin yazdıklarına ekle
    yol = ROOT / "ProjectSettings/EditorBuildSettings.asset"
    t = yol.read_text(encoding="utf-8")
    for k in kayitlar:
        sahne = k.sahne_yolu.relative_to(ROOT).as_posix()
        if sahne not in t:
            i = t.index("  m_configObjects:")
            t = t[:i] + f"  - enabled: 1\n    path: {sahne}\n    guid: {hr.sahne_guid(k.dosya)}\n" + t[i:]
    yol.write_text(t, encoding="utf-8")
    hr.yaz(ROOT / "Tools~/dunya/tani.json", json.dumps(tani, ensure_ascii=False) + "\n")
    print("tamam")


if __name__ == "__main__":
    main()
