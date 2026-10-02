"""Unity varlık dosyaları için ortak yazıcılar: FBX içe aktarma ayarları, doku ayarı, URP malzemesi,
animasyon profili. iskeletler.py ve animasyonlar.py kullanır.

Unity'yi açmadan çalışır: dosyalar, projede Unity'nin kendi yazdığı örneklerden (şablon) türetilir.
"""
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEMPLATE_FBX_META = ROOT / "Assets/AnyRPG/Core/Content/Models/Character/ModularRPGCharacters/FBX/human_male.fbx.meta"
TEMPLATE_COLOR_META = ROOT / "Assets/AnyRPG/Core/Content/Models/Equipment/Necklace/Texture/necklace_1_001_d.png.meta"
TEMPLATE_MATERIAL = ROOT / "Assets/AnyRPG/Core/Content/Models/Equipment/Necklace/Material/Necklace.mat"
TEMPLATE_ANIMATION_PROFILE = ROOT / "Assets/AnyRPG/Core/System/Resources/AnimationProfile/HumanAnimationProfile.asset"

FBX_ROOT_GAMEOBJECT = 919132149155446097   # içe aktarılan modelin kök GameObject'inin sabit fileID'si
MASK = 0x7FFFFFFFFFFFFFFF


class Kimlik:
    """Bir araç için sabit GUID ve fileID üretici (aynı ad hep aynı kimliği verir)."""

    def __init__(self, ns):
        self.ns = uuid.UUID(ns)

    def guid(self, name):
        return uuid.uuid5(self.ns, name).hex

    def file_id(self, name):
        return int(uuid.uuid5(self.ns, "id:" + name).hex[:15], 16) % 9000000000000000000 + 100000000000000000

    def internal_id(self, name):
        """FBX alt varlığı (animasyon klibi) için işaretli 64 bit kimlik"""
        value = int(uuid.uuid5(self.ns, "klip:" + name).hex[:16], 16)
        if value >= 1 << 63:
            value -= 1 << 64
        return value or 1


def rel(path):
    return Path(path).relative_to(ROOT).as_posix()


def write_text(path, text):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_text(encoding="utf-8") != text:
        path.write_text(text, encoding="utf-8")


def write_bytes(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != data:
        path.write_bytes(data)


def folder_meta(path, kimlik):
    """klasör ve .meta dosyası; var olan klasörün GUID'si hiç değiştirilmez"""
    Path(path).mkdir(parents=True, exist_ok=True)
    if Path(str(path) + ".meta").exists():
        return
    write_text(Path(str(path) + ".meta"),
               f"fileFormatVersion: 2\nguid: {kimlik.guid('klasor:' + rel(path))}\nfolderAsset: yes\nDefaultImporter:\n"
               "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def native_meta(path, guid, main_id):
    write_text(Path(str(path) + ".meta"),
               f"fileFormatVersion: 2\nguid: {guid}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
               f"  mainObjectFileID: {main_id}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def texture_meta(path, guid, size, srgb=True):
    text = TEMPLATE_COLOR_META.read_text(encoding="utf-8")
    text = re.sub(r"^guid: \w+$", f"guid: {guid}", text, count=1, flags=re.M)
    if not srgb:
        text = re.sub(r"^    sRGBTexture: 1$", "    sRGBTexture: 0", text, count=1, flags=re.M)
    text = re.sub(r"^(\s+maxTextureSize: )\d+$", lambda m: m.group(1) + str(size), text, flags=re.M)
    write_text(Path(str(path) + ".meta"), text)


def urp_material(path, name, guid, base_guid, smoothness=0.2, emission=None):
    """Tek dokulu URP/Lit malzemesi. emission: (r, g, b) verilirse ışır."""
    text = TEMPLATE_MATERIAL.read_text(encoding="utf-8")
    text = text.replace("  m_Name: Necklace\n", f"  m_Name: {name}\n")
    keywords = "  - _EMISSION\n" if emission else ""
    text = text.replace("  m_ValidKeywords:\n  - _METALLICSPECGLOSSMAP\n  - _NORMALMAP\n",
                        "  m_ValidKeywords:\n" + keywords if keywords else "  m_ValidKeywords: []\n")
    if emission:
        text = text.replace("  m_LightmapFlags: 4\n", "  m_LightmapFlags: 2\n")

    def texture(prop, tex_guid):
        nonlocal text
        pattern = re.compile(r"(    - " + prop + r":\n        m_Texture: )\{[^}]*\}")
        value = f"{{fileID: 2800000, guid: {tex_guid}, type: 3}}" if tex_guid else "{fileID: 0}"
        text, n = pattern.subn(lambda m: m.group(1) + value, text)
        assert n == 1, prop

    texture("_BaseMap", base_guid)
    texture("_MainTex", base_guid)
    texture("_BumpMap", None)
    texture("_MetallicGlossMap", None)
    text, n = re.subn(r"^    - _Smoothness: .*$", f"    - _Smoothness: {smoothness}", text, count=1, flags=re.M)
    assert n == 1
    if emission:
        r, g, b = emission
        text, n = re.subn(r"^    - _EmissionColor: .*$", f"    - _EmissionColor: {{r: {r}, g: {g}, b: {b}, a: 1}}",
                          text, count=1, flags=re.M)
        assert n == 1
    write_text(path, text)
    native_meta(path, guid, 2100000)
    return guid


# ---------------------------------------------------------------- FBX içe aktarma ayarları

HIT_EVENT = ("      - time: {time}\n        functionName: Hit\n        data: \n"
             "        objectReferenceParameter: {{instanceID: 0}}\n        floatParameter: 0\n"
             "        intParameter: 0\n        messageOptions: 0\n")


def clip_yaml(name, take, internal_id, last_frame, loop=False, bake=False, hit=None):
    """ModelImporter klip kaydı. bake: kök hareketini (dönüş, yükseklik, yatay) poza göm (insan iskeleti, yerinde
    oynayan klipler). hit: vuruş anı (0-1 arası, klibin süresine oranla) -> "Hit" animasyon olayı."""
    events = "      events: []\n" if hit is None else "      events:\n" + HIT_EVENT.format(time=round(hit, 4))
    b = 1 if bake else 0
    return (
        f"    - serializedVersion: 16\n      name: {name}\n      takeName: {take}\n      internalID: {internal_id}\n"
        f"      firstFrame: 0\n      lastFrame: {last_frame}\n      wrapMode: 0\n      orientationOffsetY: 0\n"
        f"      level: 0\n      cycleOffset: 0\n      loop: 0\n      hasAdditiveReferencePose: 0\n"
        f"      loopTime: {1 if loop else 0}\n      loopBlend: 0\n"
        f"      loopBlendOrientation: {b}\n      loopBlendPositionY: {b}\n      loopBlendPositionXZ: {b}\n"
        f"      keepOriginalOrientation: 0\n      keepOriginalPositionY: 1\n      keepOriginalPositionXZ: 0\n"
        f"      heightFromFeet: 0\n      mirror: 0\n"
        f"      bodyMask: 01000000010000000100000001000000010000000100000001000000010000000100000001000000010000000100000001000000\n"
        f"      curves: []\n{events}      transformMask: []\n      maskType: 3\n      maskSource: {{instanceID: 0}}\n"
        f"      additiveReferencePoseFrame: 0\n")


def fbx_meta(path, guid, *, remaps=None, animation_type=0, clips=(), bake_axis=False, materials=True):
    """animation_type: 0 yok (durağan nesne), 2 Generic, 3 Humanoid. clips: clip_yaml çıktıları.
    remaps: {FBX malzeme adı: malzeme guid}"""
    text = TEMPLATE_FBX_META.read_text(encoding="utf-8")

    def cut(start, end, replacement):
        nonlocal text
        i = text.index(start)
        j = text.index(end, i)
        text = text[:i] + replacement + text[j:]

    text = re.sub(r"^guid: \w+$", f"guid: {guid}", text, count=1, flags=re.M)
    cut("  internalIDToNameTable:", "  externalObjects:", "  internalIDToNameTable: []\n")
    if remaps:
        block = "".join(
            "  - first:\n      type: UnityEngine:Material\n      assembly: UnityEngine.CoreModule\n"
            f"      name: {name}\n    second: {{fileID: 2100000, guid: {mat}, type: 2}}\n"
            for name, mat in remaps.items())
        cut("  externalObjects:", "  materials:", "  externalObjects:\n" + block)
    else:
        cut("  externalObjects:", "  materials:", "  externalObjects: {}\n")
    cut("    clipAnimations:", "    isReadable:",
        "    clipAnimations:\n" + "".join(clips) if clips else "    clipAnimations: []\n")
    human = ("  humanDescription:\n    serializedVersion: 3\n    human: []\n    skeleton: []\n"
             "    armTwist: 0.5\n    foreArmTwist: 0.5\n    upperLegTwist: 0.5\n    legTwist: 0.5\n"
             "    armStretch: 0.05\n    legStretch: 0.05\n    feetSpacing: 0\n    globalScale: 1\n"
             "    rootMotionBoneName: \n    hasTranslationDoF: 0\n    hasExtraRoot: 0\n    skeletonHasParents: 1\n")
    cut("  humanDescription:", "  lastHumanDescriptionAvatarSource:", human)

    def setting(key, value):
        nonlocal text
        text, n = re.subn(r"^(\s+" + key + r": ).*$", lambda m: m.group(1) + str(value), text, count=1, flags=re.M)
        assert n == 1, key

    setting("importAnimation", 1 if clips else 0)
    setting("animationCompression", 3)
    setting("bakeAxisConversion", 1 if bake_axis else 0)
    setting("importCameras", 0)
    setting("importLights", 0)
    setting("importBlendShapes", 0)
    setting("importVisibility", 0)
    setting("animationType", animation_type)
    setting("avatarSetup", 1 if animation_type in (2, 3) else 0)
    setting("autoGenerateAvatarMappingIfUnspecified", 1 if animation_type == 3 else 0)
    setting("optimizeGameObjects", 0)
    setting("materialImportMode", 2 if materials else 0)
    write_text(Path(str(path) + ".meta"), text)
    return guid


# ---------------------------------------------------------------- animasyon profili

PROFILE_SCRIPT_GUID = "621a5f1f29fd4ac41a1db478d5473374"

# AnimationProps alanları, sınıftaki sırasıyla (Mirror alanları her zaman 0)
TEK_KLIPLER = [
    "idleClip", "jumpClip", "fallClip", "landClip", "walkClip", "runClip", "walkBackClip", "runBackClip",
    "turnLeftClip", "turnRightClip", "strafeLeftClip", "jogStrafeLeftClip", "strafeRightClip", "jogStrafeRightClip",
    "strafeForwardLeftClip", "jogStrafeForwardLeftClip", "strafeForwardRightClip", "jogStrafeForwardRightClip",
    "strafeBackLeftClip", "jogStrafeBackLeftClip", "strafeBackRightClip", "jogStrafeBackRightClip", "stunnedClip",
]
SAVAS_KLIPLERI = [
    "combatIdleClip", "combatJumpClip", "combatFallClip", "combatLandClip", "combatWalkClip", "combatRunClip",
    "combatWalkBackClip", "combatRunBackClip", "combatTurnLeftClip", "combatTurnRightClip", "combatStrafeLeftClip",
    "combatJogStrafeLeftClip", "combatStrafeRightClip", "combatJogStrafeRightClip", "combatStrafeForwardLeftClip",
    "combatJogStrafeForwardLeftClip", "combatStrafeForwardRightClip", "combatJogStrafeForwardRightClip",
    "combatStrafeBackLeftClip", "combatJogStrafeBackLeftClip", "combatStrafeBackRightClip",
    "combatJogStrafeBackRightClip", "combatStunnedClip",
]
SON_KLIPLER = ["deathClip", "reviveClip", "levitatedClip", "swimIdleClip", "swimMoveClip", "flyIdleClip", "flyMoveClip"]
LISTELER = ["attackClips", "castClips", "takeDamageClips", "actionClips"]


def clip_ref(ref):
    """ref: None ya da (fbx guid, klip internalID)"""
    return "{fileID: 0}" if ref is None else f"{{fileID: {ref[1]}, guid: {ref[0]}, type: 3}}"


def animation_profile(path, asset_name, resource_name, guid, props):
    """props: alan adı -> klip (guid, id) ya da liste alanları için klip listesi. Verilmeyen alan boş kalır
    (oyun o hareket için varsayılan "Human" profilini kullanır)."""
    bilinen = set(TEK_KLIPLER) | set(SAVAS_KLIPLERI) | set(SON_KLIPLER) | set(LISTELER)
    bilinmeyen = set(props) - bilinen
    assert not bilinmeyen, bilinmeyen
    header = TEMPLATE_ANIMATION_PROFILE.read_text(encoding="utf-8")
    header = header[:header.index("  animationProps:\n")]
    header = header.replace("  m_Name: HumanAnimationProfile\n", f"  m_Name: {asset_name}\n")
    header = header.replace("  resourceName: Human\n", f"  resourceName: {resource_name}\n")
    lines = ["  animationProps:", "    useRootMotion: 0", "    suppressAdjustAnimatorSpeed: 0"]
    for key in LISTELER:
        refs = props.get(key) or []
        if refs:
            lines.append(f"    {key}:")
            lines += [f"    - {clip_ref(r)}" for r in refs]
        else:
            lines.append(f"    {key}: []")
    for key in TEK_KLIPLER:
        lines.append(f"    {key}: {clip_ref(props.get(key))}")
    lines.append("    fullCombatMirror: 0")
    for key in SAVAS_KLIPLERI:
        lines.append(f"    {key[:-4]}Mirror: 0")
        lines.append(f"    {key}: {clip_ref(props.get(key))}")
    for key in SON_KLIPLER:
        lines.append(f"    {key}: {clip_ref(props.get(key))}")
    write_text(path, header + "\n".join(lines) + "\n")
    native_meta(path, guid, 11400000)
    return guid
