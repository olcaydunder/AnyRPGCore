#!/usr/bin/env python3
"""Ötüken Taşı: haritalara saçılan, vurup kırınca içinden ödül çıkan turkuaz kristalli kaya.

Oyunda bir "birim"dir (düşman gibi canı ve seviyesi vardır) ama yerinden kımıldamaz, saldırmaz:
  - MODEL     Assets/Otuken/Modeller/OtukenTasi/OtukenTasi.fbx (Blender'da koddan kurulur, bpy)
                "Kaya"   : yontulmuş iri kaya; ön yüzünde Kök Taş'ın ışıyan Göktürk yazısından bir parça
                "Kristal": kayadan fışkıran gök turkuazı kristaller (kırılınca parçalanan kısım)
              OtukenTasi.prefab: modelin oyundaki hâli + AnyRPG.OtukenTasi bileşeni (sarsılma, parçalanma)
  - MALZEME   kaya ve yazı Kök Taş'ınkiler (M_KokTas, M_KokTasYazi); kristal: M_OtukenKristal (ışır)
  - DAYANIKLILIK "Otuken Tasi": canı aynı seviyedeki bir düşmanın %7'si (birkaç vuruşta kırılır), tecrübesi yarısı,
              akçesi 1,5 katı
  - GANİMET   "Otuken Tasi": her zaman akçe kesesi; sık sık Gök Taşı Parçası (demirci), iksir; bazen
              değerli taş, silah, gerdanlık, çanta; çok nadir Ergenekon Demiri
  - BİRİM     "Otuken Tasi" (UnitProfile): hareketsiz, saldırmaz, sınıfsız; Erlik'in yandaşlarıyla aynı
              taraf (Enemy) sayılır ki vurulabilsin. Seviyesi haritaya göre (HaritaSeviyeleri).
Haritalara derlemede yerleştirilir (Assets/Otuken/Editor/HaritaDoldur.cs).

Kullanım (depo kök klasöründen; pip install bpy):
    python3 "Tools~/dunya/otuken_tasi.py"            # dosyaları yazar
    python3 "Tools~/dunya/otuken_tasi.py" --onizleme # ayrıca önizleme görüntüsü (Tools~/dunya/otuken_tasi.png)
Tekrar çalıştırılabilir: kimlikler adlardan türetilir.
"""
import re
import subprocess
import sys
import tempfile
import uuid
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import fbxmeta as fm  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Otuken/Modeller/OtukenTasi"
GAME = ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame"
RES = GAME / "Resources/FeaturesDemoGame"
K = fm.Kimlik("7c2e41a9-5b3d-4f60-8e1a-2d9c6b4f0a73")

KOKTAS_MALZEME = "6df3ccebc66a5afc83ad56fe40b8d283"       # M_KokTas
KOKTAS_YAZI_MALZEME = "2dea9baed68556e7a76e5b4a087fb62c"  # M_KokTasYazi
TURKUAZ = (0.25, 0.92, 0.86)                               # Kök Taş'ın ışığıyla aynı gök turkuazı
SCRIPT_GUID = uuid.uuid5(uuid.NAMESPACE_URL, "otuken:OtukenTasi.cs").hex   # AnyRPG.OtukenTasi bileşeni

BIRIM_ADI = "Otuken Tasi"
GORUNEN_AD = "Ötüken Taşı"
ACIKLAMA = ("Ötüken'in toprağından fışkıran gök taşı. İçinde atalardan kalma akçe ve Gök Taşı Parçası saklıdır; "
            "kır, ödülünü al. Bir süre sonra yerinde yenisi biter.")


# ---------------------------------------------------------------- model (Blender)

BLENDER = r'''
import bpy, bmesh, math, random, sys
from mathutils import Vector, noise
cikti = {cikti!r}
onizleme = {onizleme!r}
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
rng = random.Random(1405)

m_kaya = bpy.data.materials.new("OtukenKaya")
m_yazi = bpy.data.materials.new("OtukenYazi")
m_kristal = bpy.data.materials.new("OtukenKristal")

def kutu_uv(bm, uv, olcek=1.0):
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            co = l.vert.co
            if ax == 0: u, v = co.y, co.z
            elif ax == 1: u, v = co.x, co.z
            else: u, v = co.x, co.y
            l[uv].uv = (u * olcek, v * olcek)

# --- kaya: gürültüyle yontulmuş ikosfer, altı düz, biraz toprağa gömülü
bm = bmesh.new()
bmesh.ops.create_icosphere(bm, subdivisions=3, radius=1.0)
for v in bm.verts:
    p = v.co.copy()
    d = 1.0 + 0.22 * noise.noise(p * 1.3 + Vector((3.1, 7.7, 1.9))) + 0.08 * noise.noise(p * 3.7)
    p *= d
    v.co = Vector((p.x * 0.62, p.y * 0.52, p.z * 0.58))
    if v.co.z < -0.16:
        v.co.z = -0.16 - 0.02 * (v.co.z + 0.16)
for v in bm.verts:
    v.co.z += 0.16 - 0.04          # alt yüz zeminin 4 cm altında
bm.normal_update()
uv = bm.loops.layers.uv.verify()
kutu_uv(bm, uv, 0.9)
# ön yüz (-Y): Kök Taş yazısından iki sütunluk bir parça, ışıyan harfler
for f in bm.faces:
    c = f.calc_center_median()
    if f.normal.y < -0.5 and 0.22 < c.z < 0.80 and abs(c.x) < 0.30:
        f.material_index = 1
        for l in f.loops:
            x, z = l.vert.co.x, l.vert.co.z
            l[uv].uv = (0.34 + (x + 0.34) / 0.68 * 0.32, 0.40 + (z - 0.16) / 0.72 * 0.30)
me = bpy.data.meshes.new("Kaya")
bm.to_mesh(me)
bm.free()
me.materials.append(m_kaya)
me.materials.append(m_yazi)
kaya = bpy.data.objects.new("Kaya", me)
sc.collection.objects.link(kaya)

# --- kristaller: altıgen, sivri uçlu; kayanın tepesinden ve yanlarından fışkırır
def kristal(bm, uv, taban, yon, r, boy, don=0.0):
    yon = yon.normalized()
    yan1 = yon.cross(Vector((0, 0, 1)) if abs(yon.z) < 0.9 else Vector((1, 0, 0))).normalized()
    yan2 = yon.cross(yan1).normalized()
    alt, ust = [], []
    for k in range(6):
        a = don + k * math.pi / 3
        o = yan1 * math.cos(a) * r + yan2 * math.sin(a) * r
        alt.append(bm.verts.new(taban + o - yon * 0.25))
        ust.append(bm.verts.new(taban + o * 0.92 + yon * boy * 0.72))
    uc = bm.verts.new(taban + yon * boy)
    for k in range(6):
        j = (k + 1) % 6
        bm.faces.new([alt[k], alt[j], ust[j], ust[k]])
        bm.faces.new([ust[k], ust[j], uc])

bm = bmesh.new()
uv = bm.loops.layers.uv.verify()
kristal(bm, uv, Vector((0.03, 0.08, 0.86)), Vector((0.08, -0.05, 1.0)), 0.14, 0.95, 0.3)
kristal(bm, uv, Vector((0.24, 0.14, 0.76)), Vector((0.55, 0.20, 1.0)), 0.10, 0.62, 0.9)
kristal(bm, uv, Vector((-0.22, 0.16, 0.74)), Vector((-0.60, 0.30, 1.0)), 0.105, 0.70, 0.1)
kristal(bm, uv, Vector((-0.04, 0.30, 0.70)), Vector((-0.10, 0.80, 1.0)), 0.085, 0.50, 0.5)
kristal(bm, uv, Vector((0.40, -0.14, 0.50)), Vector((1.0, -0.40, 0.60)), 0.07, 0.38, 0.2)
kristal(bm, uv, Vector((-0.38, -0.18, 0.46)), Vector((-1.0, -0.30, 0.70)), 0.065, 0.34, 0.7)
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
for f in bm.faces:
    for l in f.loops:
        l[uv].uv = (0.5, 0.25 + 0.5 * min(1.0, max(0.0, (l.vert.co.z - 0.3) / 1.4)))
me = bpy.data.meshes.new("Kristal")
bm.to_mesh(me)
bm.free()
me.materials.append(m_kristal)
kris = bpy.data.objects.new("Kristal", me)
sc.collection.objects.link(kris)

ucgen = sum(len(p.vertices) - 2 for o in (kaya, kris) for p in o.data.polygons)
print("ucgen", ucgen)

bpy.ops.export_scene.fbx(filepath=cikti, use_selection=False, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
                         add_leaf_bones=False, bake_anim=False, path_mode='STRIP')

if onizleme:
    m_kaya.diffuse_color = (0.33, 0.35, 0.36, 1)
    m_yazi.diffuse_color = (0.25, 0.85, 0.8, 1)
    m_kristal.diffuse_color = (0.3, 0.95, 0.88, 1)
    import os
    doku = os.path.join({kok!r}, "Assets/Otuken/Modeller/KokTas/Doku")
    def resim(ad):
        return bpy.data.images.load(os.path.join(doku, ad))
    for m, renk, isik in ((m_kaya, (0.33, 0.35, 0.36, 1), 0.0), (m_yazi, (0.2, 0.25, 0.25, 1), 2.5),
                          (m_kristal, (0.08, 0.62, 0.58, 1), 0.6)):
        m.use_nodes = True
        nt = m.node_tree
        b = nt.nodes["Principled BSDF"]
        b.inputs["Base Color"].default_value = renk
        b.inputs["Roughness"].default_value = 0.85 if m is not m_kristal else 0.15
        b.inputs["Emission Color"].default_value = (0.25, 0.92, 0.86, 1)
        b.inputs["Emission Strength"].default_value = isik
        if m is m_kaya:
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = resim("KokTas_Tas.png")
            nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
        if m is m_yazi:
            t = nt.nodes.new("ShaderNodeTexImage"); t.image = resim("KokTas_Yazi.png")
            nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
            e = nt.nodes.new("ShaderNodeTexImage"); e.image = resim("KokTas_YaziIsik.png")
            mix = nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 1.0
            mix.inputs[2].default_value = (0.25, 0.92, 0.86, 1)
            nt.links.new(e.outputs["Color"], mix.inputs[1])
            nt.links.new(mix.outputs[0], b.inputs["Emission Color"])
    bpy.ops.mesh.primitive_plane_add(size=8)
    zemin = bpy.context.active_object
    mz = bpy.data.materials.new("zemin"); mz.use_nodes = True
    mz.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.32, 0.45, 0.22, 1)
    zemin.data.materials.append(mz)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    sc.collection.objects.link(cam)
    cam.location = (1.9, -3.1, 1.7)
    hedef = bpy.data.objects.new("hedef", None); sc.collection.objects.link(hedef); hedef.location = (0, 0, 0.62)
    iz = cam.constraints.new("TRACK_TO"); iz.target = hedef; iz.track_axis = "TRACK_NEGATIVE_Z"; iz.up_axis = "UP_Y"
    sc.camera = cam
    gun = bpy.data.objects.new("gun", bpy.data.lights.new("gun", "SUN"))
    gun.data.energy = 3.0
    gun.rotation_euler = (math.radians(50), math.radians(10), math.radians(30))
    sc.collection.objects.link(gun)
    sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.7, 0.85, 1)
    sc.world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 32
    sc.cycles.device = "CPU"
    sc.render.resolution_x, sc.render.resolution_y = 640, 480
    sc.render.filepath = onizleme
    bpy.ops.render.render(write_still=True)
'''


def model_kur(cikti, onizleme=None):
    betik = BLENDER.format(cikti=str(cikti), onizleme=str(onizleme) if onizleme else "", kok=str(ROOT))
    with tempfile.NamedTemporaryFile("w", suffix=".py", delete=False) as h:
        h.write(betik)
    sonuc = subprocess.run([sys.executable, h.name], capture_output=True, text=True)
    if sonuc.returncode != 0 or not Path(cikti).exists():
        print(sonuc.stdout[-3000:], sonuc.stderr[-3000:])
        raise SystemExit("model kurulamadı")
    for satir in sonuc.stdout.splitlines():
        if satir.startswith("ucgen"):
            print("Ötüken Taşı:", satir)


def kristal_dokusu():
    """dikey turkuaz geçiş: dipte koyu, uçta açık (kristalin UV'si boyuna yayılır)"""
    v = np.linspace(0, 1, 128)[:, None]
    koyu = np.array([0.05, 0.42, 0.42])
    acik = np.array([0.75, 1.0, 0.97])
    renk = koyu + (acik - koyu) * (v ** 1.4)[..., None]
    renk = np.repeat(renk, 32, axis=1)[::-1]
    return Image.fromarray((np.clip(renk, 0, 1) * 255).astype(np.uint8))


# ---------------------------------------------------------------- prefab (FBX'in oyundaki hâli + bileşen)

def prefab_yaz(fbx_guid):
    inst = K.file_id("prefab:ornek")
    go = K.file_id("prefab:kok")
    bil = K.file_id("prefab:bilesen")
    root = fm.FBX_ROOT_GAMEOBJECT
    metin = (
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
        f"--- !u!1001 &{inst}\nPrefabInstance:\n  m_ObjectHideFlags: 0\n  serializedVersion: 2\n  m_Modification:\n"
        "    serializedVersion: 3\n    m_TransformParent: {fileID: 0}\n    m_Modifications:\n"
        f"    - target: {{fileID: {root}, guid: {fbx_guid}, type: 3}}\n      propertyPath: m_Name\n"
        "      value: OtukenTasi\n      objectReference: {fileID: 0}\n"
        "    m_RemovedComponents: []\n    m_RemovedGameObjects: []\n    m_AddedGameObjects: []\n    m_AddedComponents:\n"
        f"    - targetCorrespondingSourceObject: {{fileID: {root}, guid: {fbx_guid}, type: 3}}\n      insertIndex: -1\n"
        f"      addedObject: {{fileID: {bil}}}\n"
        f"  m_SourcePrefab: {{fileID: 100100000, guid: {fbx_guid}, type: 3}}\n"
        f"--- !u!1 &{go} stripped\nGameObject:\n  m_CorrespondingSourceObject: {{fileID: {root}, guid: {fbx_guid}, type: 3}}\n"
        f"  m_PrefabInstance: {{fileID: {inst}}}\n  m_PrefabAsset: {{fileID: 0}}\n"
        f"--- !u!114 &{bil}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n"
        "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
        f"  m_GameObject: {{fileID: {go}}}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
        f"  m_Script: {{fileID: 11500000, guid: {SCRIPT_GUID}, type: 3}}\n  m_Name:\n  m_EditorClassIdentifier:\n"
    )
    yol = OUT / "OtukenTasi.prefab"
    fm.write_text(yol, metin)
    g = K.guid("prefab:OtukenTasi")
    fm.write_text(Path(str(yol) + ".meta"),
                  f"fileFormatVersion: 2\nguid: {g}\nPrefabImporter:\n  externalObjects: {{}}\n"
                  "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    return g, go


# ---------------------------------------------------------------- dayanıklılık, ganimet, birim

def native(path, text, guid):
    fm.write_text(path, text)
    fm.native_meta(path, guid, 11400000)


def dayaniklilik_yaz():
    src = (RES / "UnitToughness/SoloDungeonMinionToughness.asset").read_text(encoding="utf-8")
    t = src.replace("  m_Name: SoloDungeonMinionToughness\n", "  m_Name: OtukenTasiToughness\n")
    t = t.replace("  resourceName: Solo Dungeon Minion\n", f"  resourceName: {BIRIM_ADI}\n")
    for alan, deger in (("currencyMultiplier", 1.5), ("experienceMultiplier", 0.5), ("defaultResourceMultiplier", 0.07)):
        t, n = re.subn(rf"^  {alan}: .*$", f"  {alan}: {deger}", t, count=1, flags=re.M)
        assert n == 1, alan
    native(RES / "UnitToughness/OtukenTasiToughness.asset", t, K.guid("dayaniklilik"))


GANIMET = [
    # (garantili, grup şansı, en çok, aynısından en çok, sınıfa uygun olsun, [(eşya, ağırlık)])
    (True, 100, 1, 0, False, [("Akce Kesesi", 70), ("Dolu Akce Kesesi", 26), ("Altin Kese", 4)]),
    (True, 60, 1, 0, False, [("Gok Tasi Parcasi", 100)]),
    (True, 35, 1, 0, False, [("Health Potion", 60), ("Mana Potion", 40)]),
    (True, 15, 1, 0, False, [(f"{c} Gem", 10) for c in ("Red", "Blue", "Green", "Yellow", "Violet", "Cyan")]),
    (True, 7, 1, 0, True, [(f"Random Medieval {w}", 10) for w in (
        "Bow", "Crossbow", "Dagger", "Grimoire", "One Hand Axe", "One Hand Mace", "One Hand Sword", "Shield", "Staff",
        "Two Hand Axe", "Two Hand Mace", "Two Hand Sword", "Wand")]),
    (True, 5, 1, 0, False, [(f"{c} Necklace", 10) for c in ("Red", "Blue", "Green", "Yellow", "Violet", "Cyan")]),
    (True, 3, 1, 0, False, [("8 Slot Bag", 70), ("10 Slot Bag", 30)]),
    (True, 1, 1, 0, False, [("Ergenekon Demiri", 100)]),
]


def ganimet_yaz():
    bilinen = set()
    for f in (RES / "Item").rglob("*.asset"):
        m = re.search(r"^  resourceName: (.*)$", f.read_text(encoding="utf-8"), re.M)
        if m:
            bilinen.add(m.group(1).strip())
    header = (RES / "LootTable/PotionsLoot.asset").read_text(encoding="utf-8")
    header = header[:header.index("  resourceName:")]
    satirlar = [header.replace("  m_Name: PotionsLoot\n", "  m_Name: OtukenTasiLoot\n"),
                f"  resourceName: {BIRIM_ADI}\n", "  displayName: \n", "  icon: {fileID: 0}\n",
                "  iconBackgroundImage: {fileID: 0}\n", "  description: \n", "  useRegionalDescription: 0\n",
                "  resourceDescriptionProfile: \n", "  optionalOverride: 0\n", "  ignoreGlobalDropLimit: 1\n",
                "  dropLimit: 0\n", "  lootGroups:\n"]
    for garantili, sans, sinir, tekil, sinif, liste in GANIMET:
        satirlar += [f"  - guaranteedDrop: {1 if garantili else 0}\n", f"    groupChance: {sans}\n",
                     f"    dropLimit: {sinir}\n", f"    uniqueLimit: {tekil}\n", "    ignoreGlobalDropLimit: 1\n", "    loot:\n"]
        for esya, agirlik in liste:
            if esya not in bilinen:
                raise SystemExit(f"bilinmeyen eşya: {esya}")
            satirlar += [f"    - itemName: {esya}\n", f"      dropChance: {agirlik}\n", "      minDrops: 1\n",
                         "      maxDrops: 1\n", f"      matchItemRestrictions: {1 if sinif else 0}\n",
                         "      prerequisiteConditions: []\n"]
    klasor = RES / "LootTable/Hazine"
    native(klasor / "OtukenTasiLoot.asset", "".join(satirlar), K.guid("ganimet"))


def q(s):
    """Unity'nin yazdığı gibi çift tırnaklı, ASCII dışı harfler \\uXXXX"""
    return '"' + "".join(c if 32 <= ord(c) < 127 and c not in '"\\' else (
        "\\" + c if c in '"\\' else "\\u%04x" % ord(c)) for c in s) + '"'


def birim_yaz(prefab_guid, prefab_go):
    t = (RES / "UnitProfile/Dusman/CaliCiniUnit.asset").read_text(encoding="utf-8")

    def sub(desen, yeni, adet=1):
        nonlocal t
        t, n = re.subn(desen, yeni if "\\g<" in yeni else (lambda m: yeni), t, count=adet, flags=re.M)
        assert n == adet, (desen, n)

    sub(r"^  m_Name: .*$", "  m_Name: OtukenTasiUnit")
    sub(r"^  resourceName: .*$", f"  resourceName: {BIRIM_ADI}")
    sub(r"^  displayName: .*$", f"  displayName: {q(GORUNEN_AD)}")
    sub(r"^  description: .*$", f"  description: {q(ACIKLAMA)}")
    sub(r"^    modelPrefab: \{fileID: \d+, guid: \w+,\n      type: 3\}$",
        f"    modelPrefab: {{fileID: {prefab_go}, guid: {prefab_guid},\n      type: 3}}")
    sub(r"^    animationProfileName: .*$", "    animationProfileName: ")
    sub(r"^      namePlatePosition: .*$", "      namePlatePosition: {x: 0, y: 2.1, z: 0}")
    sub(r"^      unitFrameTarget: .*$", "      unitFrameTarget: ", 2)
    sub(r"^      unitFrameCameraPositionOffset: .*$", "      unitFrameCameraPositionOffset: {x: 0, y: 0.9, z: 2.2}", 2)
    sub(r"^    attachmentProfileName: .*$", "    attachmentProfileName: ")
    sub(r"^    floatHeight: .*$", "    floatHeight: 0")
    sub(r"^    floatTransform: .*$", "    floatTransform: ")
    sub(r"^  characterName: .*$", f"  characterName: {q(GORUNEN_AD)}")
    sub(r"^  characterClassName: .*$", "  characterClassName: ")
    sub(r"^  defaultToughness: .*$", f"  defaultToughness: {BIRIM_ADI}")
    sub(r"^  voiceProfile: .*$", "  voiceProfile: ")
    sub(r"^  isAggressive: .*$", "  isAggressive: 0")
    sub(r"^  aggroRadius: .*$", "  aggroRadius: 0")
    sub(r"^  isMobile: .*$", "  isMobile: 0")
    sub(r"^  playOnFootstep: .*$", "  playOnFootstep: 0")
    sub(r"^  movementAudioProfileNames:\n  - .*$", "  movementAudioProfileNames: []")
    sub(r"^  faceInteractionTarget: .*$", "  faceInteractionTarget: 0")
    sub(r"^  m_UUID: .*$", f"  m_UUID: {uuid.uuid5(uuid.NAMESPACE_URL, 'otuken:birim:' + BIRIM_ADI)}")
    sub(r"(        lootTableNames:\n)(?:        - .*\n)+", rf"\g<1>        - {BIRIM_ADI}\n")
    native(RES / "UnitProfile/Dusman/OtukenTasiUnit.asset", t, K.guid("birim"))


def main():
    onizleme = "--onizleme" in sys.argv
    fm.folder_meta(OUT, K)
    fm.folder_meta(OUT / "Malzeme", K)
    doku = OUT / "Malzeme/OtukenKristal.png"
    kristal_dokusu().save(doku, optimize=True)
    fm.texture_meta(doku, K.guid("doku:kristal"), 128, srgb=True)
    kristal_m = fm.urp_material(OUT / "Malzeme/M_OtukenKristal.mat", "M_OtukenKristal", K.guid("malzeme:kristal"),
                                K.guid("doku:kristal"), smoothness=0.85,
                                emission=tuple(round(c * 1.4, 3) for c in TURKUAZ))
    fbx = OUT / "OtukenTasi.fbx"
    model_kur(fbx, Path(__file__).with_suffix(".png") if onizleme else None)
    fbx_guid = fm.fbx_meta(fbx, K.guid("model:OtukenTasi"),
                           remaps={"OtukenKaya": KOKTAS_MALZEME, "OtukenYazi": KOKTAS_YAZI_MALZEME,
                                   "OtukenKristal": kristal_m},
                           animation_type=0, materials=True, bake_axis=True)
    prefab_guid, prefab_go = prefab_yaz(fbx_guid)
    dayaniklilik_yaz()
    ganimet_yaz()
    birim_yaz(prefab_guid, prefab_go)
    print("Ötüken Taşı yazıldı:", fm.rel(OUT))


if __name__ == "__main__":
    main()
