#!/usr/bin/env python3
"""Quaternius "Bestiary - Dungeon Monsters Kit" (ücretsiz sürüm) canavarlarını oyuna hazırlar.

Paket iki model içerir: Imp ve Puglin. Animasyonları yoktur ama insan (Unreal mankeni adlı) iskeletleri vardır.
Unity onları "Humanoid" olarak içe aktarınca oyundaki insan animasyonları bu modellerde de oynar
(avatar eşleştirmesi içe aktarma sırasında kendiliğinden yapılır: autoGenerateAvatarMappingIfUnspecified).

LİSANS: Quaternius Asset License, model dosyalarının kendisinin dağıtılmasını yasaklar. Depo herkese açık
olduğu için model ve doku dosyaları depoya AÇIK konmaz:
  - Tools~/varliklar/bestiary.tar.enc  : FBX ve dokular, AES-256 ile şifreli (anahtar: VARLIK_ANAHTARI)
  - .gitignore                         : açık hâlleri hiçbir zaman depoya girmez
  - .meta, malzeme ve prefab dosyaları  : bizim yazdığımız ayarlar, açık durur
GitHub derlemesi şifreyi VARLIK_ANAHTARI gizli bilgisiyle çözer (.github/workflows/android-apk.yml).

Kullanım (depo kök klasöründen):
    VARLIK_ANAHTARI=... python3 "Tools~/dunya/canavarlar.py" yol/Bestiary_-_Dungeon_Monsters_Kit_Standard_.zip
"""
import io
import os
import re
import subprocess
import sys
import tarfile
import uuid
import zipfile
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
NS = uuid.UUID("8a3d5e21-6b7c-4f90-a1d2-3c4b5e6f7a81")
OUT = ROOT / "Assets/Otuken/Modeller/Bestiary"
ENCRYPTED = ROOT / "Tools~/varliklar/bestiary.tar.enc"
SCRIPT_META = ROOT / "Assets/AnyRPG/Core/System/Scripts/Mobile/ModelMalzemesi.cs.meta"
TEMPLATE_FBX_META = ROOT / "Assets/AnyRPG/Core/Content/Models/Character/ModularRPGCharacters/FBX/human_male.fbx.meta"
TEMPLATE_COLOR_META = ROOT / "Assets/AnyRPG/Core/Content/Models/Equipment/Necklace/Texture/necklace_1_001_d.png.meta"
TEMPLATE_NORMAL_META = ROOT / "Assets/AnyRPG/Core/Content/Models/Equipment/Necklace/Texture/necklace_1_001_n.png.meta"
TEMPLATE_MATERIAL = ROOT / "Assets/AnyRPG/Core/Content/Models/Equipment/Necklace/Material/Necklace.mat"

FBX_ROOT_GAMEOBJECT = 919132149155446097   # Unity's fixed file id of an imported model's root GameObject
MASK = 0x7FFFFFFFFFFFFFFF

# model -> FBX malzeme adları (hepsi tek malzemeye bağlanır)
MODELLER = {
    "Imp": ["MI_Imp", "Material"],
    "Puglin": ["MI_Puglin"],
}

# renk çeşitleri: (prefab/malzeme adı, model, doku numarası, ışıma gücü)
CESITLER = [
    ("KormosKizil", "Imp", 1, 1.5),
    ("KormosYesil", "Imp", 2, 1.5),
    ("KormosMavi", "Imp", 3, 1.5),
    ("CinYesil", "Puglin", 1, 1.2),
    ("CinKizil", "Puglin", 2, 1.2),
    ("CinMavi", "Puglin", 3, 1.2),
]

DOKU_BOYUTU = 1024
ISIMA_BOYUTU = 512


def guid(name):
    return uuid.uuid5(NS, name).hex


def rel(path):
    return path.relative_to(ROOT).as_posix()


def write_text(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_text(encoding="utf-8") != text:
        path.write_text(text, encoding="utf-8")


def folder_meta(path):
    write_text(Path(str(path) + ".meta"),
               f"fileFormatVersion: 2\nguid: {guid('klasor:' + rel(path))}\nfolderAsset: yes\nDefaultImporter:\n"
               "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# ---------------------------------------------------------------- dokular

def prepare_textures(zip_path):
    """unpack, shrink and convert the textures; returns {name: path}"""
    textures = {}
    tex_dir = OUT / "Textures"
    tex_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(zip_path) as archive:
        def load(name):
            member = next(n for n in archive.namelist() if n.endswith("/Textures/" + name))
            return Image.open(io.BytesIO(archive.read(member)))

        for model in MODELLER:
            for index in (1, 2, 3):
                image = load(f"T_{model}_BaseColor_{index}.png").convert("RGB").resize((DOKU_BOYUTU, DOKU_BOYUTU), Image.LANCZOS)
                path = tex_dir / f"T_{model}_BaseColor_{index}.png"
                image.save(path, optimize=True)
                textures[path.stem] = path
            normal = load(f"T_{model}_Normal.png").convert("RGB").resize((DOKU_BOYUTU, DOKU_BOYUTU), Image.LANCZOS)
            path = tex_dir / f"T_{model}_Normal.png"
            normal.save(path, optimize=True)
            textures[path.stem] = path
            # ORM (R: ortam kapatma, G: pürüzlülük, B: metallik) -> URP: R metallik, A pürüzsüzlük
            orm = load(f"T_{model}_ORM.png").convert("RGB").resize((DOKU_BOYUTU, DOKU_BOYUTU), Image.LANCZOS)
            _, rough, metal = orm.split()
            smooth = rough.point(lambda v: 255 - v)
            metallic = Image.merge("RGBA", (metal, metal, metal, smooth))
            path = tex_dir / f"T_{model}_MetallicSmoothness.png"
            metallic.save(path, optimize=True)
            textures[path.stem] = path
            emissive = load(f"T_{model}_Emissive.png").convert("RGB").resize((ISIMA_BOYUTU, ISIMA_BOYUTU), Image.LANCZOS)
            path = tex_dir / f"T_{model}_Emissive.png"
            emissive.save(path, optimize=True)
            textures[path.stem] = path

        for model in MODELLER:
            member = next(n for n in archive.namelist() if n.endswith(f"/FBX (Unity)/{model}.fbx"))
            (OUT / f"{model}.fbx").write_bytes(archive.read(member))
    return textures


def texture_meta(path, kind):
    """kind: color, normal, linear"""
    template = (TEMPLATE_NORMAL_META if kind == "normal" else TEMPLATE_COLOR_META).read_text(encoding="utf-8")
    text = re.sub(r"^guid: \w+$", f"guid: {guid('doku:' + path.stem)}", template, count=1, flags=re.M)
    if kind == "linear":
        text = re.sub(r"^    sRGBTexture: 1$", "    sRGBTexture: 0", text, count=1, flags=re.M)
    size = ISIMA_BOYUTU if "Emissive" in path.stem else DOKU_BOYUTU
    text = re.sub(r"^(\s+maxTextureSize: )\d+$", lambda m: m.group(1) + str(size), text, flags=re.M)
    write_text(Path(str(path) + ".meta"), text)
    return guid("doku:" + path.stem)


# ---------------------------------------------------------------- malzemeler

def write_material(name, base_guid, normal_guid, metallic_guid, emissive_guid, emission):
    text = TEMPLATE_MATERIAL.read_text(encoding="utf-8")
    text = text.replace("  m_Name: Necklace\n", f"  m_Name: {name}\n")
    text = text.replace("  - _METALLICSPECGLOSSMAP\n  - _NORMALMAP\n",
                        "  - _EMISSION\n  - _METALLICSPECGLOSSMAP\n  - _NORMALMAP\n")
    text = text.replace("  m_LightmapFlags: 4\n", "  m_LightmapFlags: 2\n")

    def texture(prop, tex_guid):
        nonlocal text
        pattern = re.compile(r"(    - " + prop + r":\n        m_Texture: )\{[^}]*\}")
        value = f"{{fileID: 2800000, guid: {tex_guid}, type: 3}}" if tex_guid else "{fileID: 0}"
        text, n = pattern.subn(lambda m: m.group(1) + value, text)
        assert n == 1, prop

    texture("_BaseMap", base_guid)
    texture("_MainTex", base_guid)
    texture("_BumpMap", normal_guid)
    texture("_MetallicGlossMap", metallic_guid)
    texture("_EmissionMap", emissive_guid)
    text = re.sub(r"^    - _Smoothness: .*$", "    - _Smoothness: 1", text, count=1, flags=re.M)
    text = re.sub(r"^    - _EmissionColor: .*$",
                  f"    - _EmissionColor: {{r: {emission}, g: {emission}, b: {emission}, a: 1}}", text, count=1, flags=re.M)
    path = OUT / "Materials" / f"M_{name}.mat"
    write_text(path, text)
    write_text(Path(str(path) + ".meta"),
               f"fileFormatVersion: 2\nguid: {guid('malzeme:' + name)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
               "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    return guid("malzeme:" + name)


# ---------------------------------------------------------------- FBX içe aktarma ayarları

def fbx_meta(model, material_guid):
    text = TEMPLATE_FBX_META.read_text(encoding="utf-8")

    def cut(start, end, replacement):
        nonlocal text
        i = text.index(start)
        j = text.index(end, i)
        text = text[:i] + replacement + text[j:]

    text = re.sub(r"^guid: \w+$", f"guid: {guid('model:' + model)}", text, count=1, flags=re.M)
    cut("  internalIDToNameTable:", "  externalObjects:", "  internalIDToNameTable: []\n")
    remaps = "".join(
        "  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n"
        f"      name: {name}\n    second: {{fileID: 2100000, guid: {material_guid}, type: 2}}\n"
        for name in MODELLER[model])
    cut("  externalObjects:", "  materials:", "  externalObjects:\n" + remaps)
    cut("    clipAnimations:", "    isReadable:", "    clipAnimations: []\n")
    human = ("  humanDescription:\n    serializedVersion: 3\n    human: []\n    skeleton: []\n"
             "    armTwist: 0.5\n    foreArmTwist: 0.5\n    upperLegTwist: 0.5\n    legTwist: 0.5\n"
             "    armStretch: 0.05\n    legStretch: 0.05\n    feetSpacing: 0\n    globalScale: 1\n"
             "    rootMotionBoneName: \n    hasTranslationDoF: 0\n    hasExtraRoot: 0\n    skeletonHasParents: 1\n")
    cut("  humanDescription:", "  lastHumanDescriptionAvatarSource:", human)

    def setting(key, value):
        nonlocal text
        text, n = re.subn(r"^(\s+" + key + r": ).*$", lambda m: m.group(1) + value, text, count=1, flags=re.M)
        assert n == 1, key

    setting("importAnimation", "0")
    setting("bakeAxisConversion", "1")
    setting("importCameras", "0")
    setting("importLights", "0")
    setting("importBlendShapes", "0")
    setting("animationType", "3")
    setting("avatarSetup", "1")
    setting("autoGenerateAvatarMappingIfUnspecified", "1")
    setting("optimizeGameObjects", "0")
    setting("materialImportMode", "2")
    write_text(OUT / f"{model}.fbx.meta", text)
    return guid("model:" + model)


# ---------------------------------------------------------------- renk çeşidi prefab'ları

def file_id(name):
    return int(uuid.uuid5(NS, "id:" + name).hex[:15], 16) % 9000000000000000000 + 100000000000000000


def write_variant_prefab(name, model_guid, material_guid, script_guid):
    instance = file_id(name + ":instance")
    component = file_id(name + ":component")
    root_object = (instance ^ FBX_ROOT_GAMEOBJECT) & MASK
    text = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1001 &{instance}
PrefabInstance:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_Modification:
    serializedVersion: 3
    m_TransformParent: {{fileID: 0}}
    m_Modifications:
    - target: {{fileID: {FBX_ROOT_GAMEOBJECT}, guid: {model_guid}, type: 3}}
      propertyPath: m_Name
      value: {name}
      objectReference: {{fileID: 0}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents:
    - targetCorrespondingSourceObject: {{fileID: {FBX_ROOT_GAMEOBJECT}, guid: {model_guid}, type: 3}}
      insertIndex: -1
      addedObject: {{fileID: {component}}}
  m_SourcePrefab: {{fileID: 100100000, guid: {model_guid}, type: 3}}
--- !u!1 &{root_object} stripped
GameObject:
  m_CorrespondingSourceObject: {{fileID: {FBX_ROOT_GAMEOBJECT}, guid: {model_guid}, type: 3}}
  m_PrefabInstance: {{fileID: {instance}}}
  m_PrefabAsset: {{fileID: 0}}
--- !u!114 &{component}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {root_object}}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
  malzeme: {{fileID: 2100000, guid: {material_guid}, type: 2}}
"""
    path = OUT / "Prefabs" / f"{name}.prefab"
    write_text(path, text)
    prefab_guid = guid("prefab:" + name)
    write_text(Path(str(path) + ".meta"),
               f"fileFormatVersion: 2\nguid: {prefab_guid}\nPrefabImporter:\n  externalObjects: {{}}\n"
               "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    return prefab_guid, root_object


def model_references():
    """(prefab guid, root GameObject file id) of every color variant, for yaratiklar.py"""
    refs = {}
    for name, _, _, _ in CESITLER:
        instance = file_id(name + ":instance")
        refs[name] = (guid("prefab:" + name), (instance ^ FBX_ROOT_GAMEOBJECT) & MASK)
    return refs


# ---------------------------------------------------------------- şifreleme

def licensed_files():
    files = [OUT / f"{model}.fbx" for model in MODELLER]
    files += sorted((OUT / "Textures").glob("*.png"))
    return files


def encrypt(key):
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode="w:gz") as tar:
        for path in licensed_files():
            info = tar.gettarinfo(str(path), arcname=rel(path))
            info.uid = info.gid = 0
            info.uname = info.gname = ""
            info.mtime = 0
            with open(path, "rb") as handle:
                tar.addfile(info, handle)
    ENCRYPTED.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["openssl", "enc", "-aes-256-cbc", "-pbkdf2", "-iter", "200000", "-salt",
                    "-out", str(ENCRYPTED), "-pass", "env:VARLIK_ANAHTARI"],
                   input=buffer.getvalue(), check=True, env=dict(os.environ, VARLIK_ANAHTARI=key))
    print(f"şifreli arşiv: {rel(ENCRYPTED)} ({ENCRYPTED.stat().st_size // 1024} KB, {len(licensed_files())} dosya)")


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    key = os.environ.get("VARLIK_ANAHTARI", "")
    if len(key) < 20:
        sys.exit("VARLIK_ANAHTARI ortam değişkeni (en az 20 karakter) gerekli")

    for folder in (ROOT / "Assets/Otuken", ROOT / "Assets/Otuken/Modeller", OUT, OUT / "Textures", OUT / "Materials", OUT / "Prefabs"):
        folder.mkdir(parents=True, exist_ok=True)
        folder_meta(folder)

    textures = prepare_textures(sys.argv[1])
    tex_guids = {}
    for stem, path in textures.items():
        kind = "normal" if stem.endswith("_Normal") else "linear" if stem.endswith("_MetallicSmoothness") else "color"
        tex_guids[stem] = texture_meta(path, kind)

    script_guid = re.search(r"^guid: (\w+)", SCRIPT_META.read_text(encoding="utf-8"), re.M).group(1)
    material_guids = {}
    for name, model, index, emission in CESITLER:
        material_guids[name] = write_material(
            name, tex_guids[f"T_{model}_BaseColor_{index}"], tex_guids[f"T_{model}_Normal"],
            tex_guids[f"T_{model}_MetallicSmoothness"], tex_guids[f"T_{model}_Emissive"], emission)

    model_guids = {}
    for model in MODELLER:
        first_variant = next(name for name, m, _, _ in CESITLER if m == model)
        model_guids[model] = fbx_meta(model, material_guids[first_variant])

    for name, model, _, _ in CESITLER:
        write_variant_prefab(name, model_guids[model], material_guids[name], script_guid)

    encrypt(key)
    print(f"{len(MODELLER)} model, {len(textures)} doku, {len(CESITLER)} renk çeşidi hazır")


if __name__ == "__main__":
    main()
