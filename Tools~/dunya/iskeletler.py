#!/usr/bin/env python3
"""KayKit "Character Pack: Skeletons" (Kay Lousberg, CC0) iskeletlerini oyuna hazırlar.

Kaynak: https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0
Lisans CC0 olduğu için model dosyaları depoya açık konur.

Paket 4 iskelet (Minion, Warrior, Rogue, Mage), 13 silah/eşya ve her iskelette aynı 95 animasyonu içerir.
Oyunda:
  - iskeletler "Generic" iskelet olarak içe aktarılır ve KENDİ animasyonlarıyla oynar (sendeleyen iskelet
    yürüyüşü, kemik yığınına dönüşen ölüm, yeniden dirilme...). İnsan animasyonları bu modellere uymaz.
  - animasyonlar yalnız Skeleton_Minion.fbx'ten alınır (dört iskeletin kemikleri birebir aynı), diğer üç
    dosyadan animasyonlar silinir; kullanılmayan animasyonlar da atılır (fbx_kirp.py). 88 MB -> ~7 MB.
  - silahlar ayrı modellerdir; ModelSilahlari bileşeni onları elin "handslot" kemiğine takar.
  - her iskelet türü için bir animasyon profili yazılır (saldırı klipleri türüne göre değişir).

Kullanım (depo kök klasöründen):
    git clone --depth 1 https://github.com/KayKit-Game-Assets/KayKit-Character-Pack-Skeletons-1.0 /tmp/kk
    python3 "Tools~/dunya/iskeletler.py" /tmp/kk/addons/kaykit_character_pack_skeletons
"""
import re
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import fbxmeta as fm  # noqa: E402

K = fm.Kimlik("c4d1f6a8-2b37-4e95-8a0c-6f1e3d5b7a92")
OUT = fm.ROOT / "Assets/Otuken/Modeller/KayKitIskelet"
PROFILE_DIR = fm.ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame/AnimationProfile/Canavar"
SCRIPT_META = fm.ROOT / "Assets/AnyRPG/Core/System/Scripts/Mobile/ModelSilahlari.cs.meta"

KARAKTERLER = ["Skeleton_Minion", "Skeleton_Warrior", "Skeleton_Rogue", "Skeleton_Mage"]
ANIMASYON_KAYNAGI = "Skeleton_Minion"
SILAHLAR = ["Skeleton_Blade", "Skeleton_Axe", "Skeleton_Staff", "Skeleton_Shield_Small_A", "Skeleton_Shield_Large_A"]

# klip -> (döngü mü, vuruş karesi (Unity karesi, 30 fps) ya da None)
#   vuruş kareleri: Blender'da silah yuvası kemiğinin en hızlı olduğu kare
KLIPLER = {
    "Idle": (True, None),
    "Idle_Combat": (True, None),
    "Walking_A": (True, None),
    "Walking_D_Skeletons": (True, None),
    "Walking_Backwards": (True, None),
    "Running_A": (True, None),
    "Running_Strafe_Left": (True, None),
    "Running_Strafe_Right": (True, None),
    "Jump_Start": (False, None),
    "Jump_Idle": (True, None),
    "Jump_Land": (False, None),
    "Skeleton_Inactive_Standing_Pose": (True, None),
    "Death_C_Skeletons": (False, None),
    "Death_C_Skeletons_Resurrect": (False, None),
    "Hit_A": (False, None),
    "Hit_B": (False, None),
    "Spellcasting": (True, None),
    "Spellcast_Shoot": (False, 5),
    "1H_Melee_Attack_Chop": (False, 18),
    "1H_Melee_Attack_Slice_Diagonal": (False, 13),
    "1H_Melee_Attack_Slice_Horizontal": (False, 8),
    "1H_Melee_Attack_Stab": (False, 12),
    "Block_Attack": (False, 11),
    "Dualwield_Melee_Attack_Chop": (False, 17),
    "Dualwield_Melee_Attack_Slice": (False, 18),
    "Dualwield_Melee_Attack_Stab": (False, 12),
}

# oyundaki iskelet türleri: prefab adı -> (model, [(silah, kemik)], animasyon profili, saldırı klipleri)
TURLER = {
    "KemikEr": ("Skeleton_Minion", [("Skeleton_Blade", "handslot.r"), ("Skeleton_Shield_Small_A", "handslot.l")],
                "Kemik Er", ["1H_Melee_Attack_Chop", "1H_Melee_Attack_Slice_Diagonal", "1H_Melee_Attack_Stab"]),
    "KemikAlp": ("Skeleton_Warrior", [("Skeleton_Axe", "handslot.r"), ("Skeleton_Shield_Large_A", "handslot.l")],
                 "Kemik Alp", ["1H_Melee_Attack_Slice_Horizontal", "1H_Melee_Attack_Chop", "Block_Attack"]),
    "KemikAkinci": ("Skeleton_Rogue", [("Skeleton_Blade", "handslot.r"), ("Skeleton_Blade", "handslot.l")],
                    "Kemik Akinci", ["Dualwield_Melee_Attack_Chop", "Dualwield_Melee_Attack_Slice",
                                     "Dualwield_Melee_Attack_Stab"]),
    "KemikKam": ("Skeleton_Mage", [("Skeleton_Staff", "handslot.r")],
                 "Kemik Kam", ["Spellcast_Shoot", "1H_Melee_Attack_Chop"]),
}


def model_guid(name):
    return K.guid("model:" + name)


def klip(name):
    return (model_guid(ANIMASYON_KAYNAGI), K.internal_id(name))


def profil(saldirilar):
    """Generic iskelet: her hareketin klibi verilmeli (boş kalan hareket insan klibine düşer ve iskelette oynamaz)"""
    yuru, kos, geri = klip("Walking_D_Skeletons"), klip("Running_A"), klip("Walking_Backwards")
    sol, sag = klip("Running_Strafe_Left"), klip("Running_Strafe_Right")
    p = {
        "attackClips": [klip(s) for s in saldirilar],
        "castClips": [klip("Spellcasting")],
        "takeDamageClips": [klip("Hit_A"), klip("Hit_B")],
        "idleClip": klip("Idle"),
        "combatIdleClip": klip("Idle_Combat"),
        "deathClip": klip("Death_C_Skeletons"),
        "reviveClip": klip("Death_C_Skeletons_Resurrect"),
        "levitatedClip": klip("Jump_Idle"),
        "swimIdleClip": klip("Idle"),
        "swimMoveClip": klip("Walking_A"),
        "flyIdleClip": klip("Jump_Idle"),
        "flyMoveClip": klip("Jump_Idle"),
    }
    for onek in ("", "combat"):
        def ad(alan):
            return (onek + alan[0].upper() + alan[1:]) if onek else alan
        p[ad("jumpClip")] = klip("Jump_Start")
        p[ad("fallClip")] = klip("Jump_Idle")
        p[ad("landClip")] = klip("Jump_Land")
        p[ad("walkClip")] = yuru
        p[ad("runClip")] = kos
        p[ad("walkBackClip")] = geri
        p[ad("runBackClip")] = geri
        p[ad("turnLeftClip")] = yuru
        p[ad("turnRightClip")] = yuru
        p[ad("stunnedClip")] = klip("Skeleton_Inactive_Standing_Pose")
        for yon, k in (("Left", sol), ("Right", sag), ("ForwardLeft", sol), ("ForwardRight", sag),
                       ("BackLeft", geri), ("BackRight", geri)):
            p[ad(f"strafe{yon}Clip")] = k
            p[ad(f"jogStrafe{yon}Clip")] = k
    return p


def write_variant_prefab(name, model, silahlar, script_guid):
    instance = K.file_id(name + ":instance")
    component = K.file_id(name + ":component")
    root_object = (instance ^ fm.FBX_ROOT_GAMEOBJECT) & fm.MASK
    mg = model_guid(model)
    silah_yaml = "".join(
        f"  - silah: {{fileID: {fm.FBX_ROOT_GAMEOBJECT}, guid: {model_guid(s)}, type: 3}}\n    kemik: {kemik}\n"
        for s, kemik in silahlar)
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
    - target: {{fileID: {fm.FBX_ROOT_GAMEOBJECT}, guid: {mg}, type: 3}}
      propertyPath: m_Name
      value: {name}
      objectReference: {{fileID: 0}}
    m_RemovedComponents: []
    m_RemovedGameObjects: []
    m_AddedGameObjects: []
    m_AddedComponents:
    - targetCorrespondingSourceObject: {{fileID: {fm.FBX_ROOT_GAMEOBJECT}, guid: {mg}, type: 3}}
      insertIndex: -1
      addedObject: {{fileID: {component}}}
  m_SourcePrefab: {{fileID: 100100000, guid: {mg}, type: 3}}
--- !u!1 &{root_object} stripped
GameObject:
  m_CorrespondingSourceObject: {{fileID: {fm.FBX_ROOT_GAMEOBJECT}, guid: {mg}, type: 3}}
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
  silahlar:
{silah_yaml}"""
    path = OUT / "Prefab" / f"{name}.prefab"
    fm.write_text(path, text)
    fm.write_text(Path(str(path) + ".meta"),
                  f"fileFormatVersion: 2\nguid: {K.guid('prefab:' + name)}\nPrefabImporter:\n  externalObjects: {{}}\n"
                  "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def model_references():
    """yaratiklar.py için: prefab adı -> (prefab guid, kök GameObject fileID)"""
    return {name: (K.guid("prefab:" + name), (K.file_id(name + ":instance") ^ fm.FBX_ROOT_GAMEOBJECT) & fm.MASK)
            for name in TURLER}


def animation_profiles():
    """yaratiklar.py için: prefab adı -> animasyon profili adı"""
    return {name: t[2] for name, t in TURLER.items()}


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    from fbx_kirp import kirp, yigin_kareleri
    src = Path(sys.argv[1])
    for folder in (fm.ROOT / "Assets/Otuken/Modeller", OUT, OUT / "Silahlar", OUT / "Doku", OUT / "Malzeme",
                   OUT / "Prefab", PROFILE_DIR):
        fm.folder_meta(folder, K)

    # doku ve malzemeler
    doku = OUT / "Doku" / "skeleton_texture.png"
    shutil.copyfile(src / "Textures/skeleton_texture.png", doku)
    fm.texture_meta(doku, K.guid("doku:skeleton_texture"), 512)
    malzeme = fm.urp_material(OUT / "Malzeme" / "M_Iskelet.mat", "M_Iskelet", K.guid("malzeme:Iskelet"),
                              K.guid("doku:skeleton_texture"), smoothness=0.25)
    goz = fm.urp_material(OUT / "Malzeme" / "M_IskeletGoz.mat", "M_IskeletGoz", K.guid("malzeme:IskeletGoz"),
                          K.guid("doku:skeleton_texture"), smoothness=0.4, emission=(2, 1.6, 0.3))
    remaps = {"skeleton": malzeme, "Glow": goz}

    # karakterler: animasyonlar yalnız tek dosyada
    kareler = yigin_kareleri(src / f"Characters/fbx/{ANIMASYON_KAYNAGI}.fbx")
    for model in KARAKTERLER:
        hedef = OUT / f"{model}.fbx"
        tut = list(KLIPLER) if model == ANIMASYON_KAYNAGI else []
        kalan = kirp(src / f"Characters/fbx/{model}.fbx", hedef, tut)
        assert sorted(kalan) == sorted(tut), (model, kalan)
        clips = []
        for ad in tut:
            dongu, vurus = KLIPLER[ad]
            son = kareler[ad]
            assert son == int(son), (ad, son)
            son = int(son)
            clips.append(fm.clip_yaml(ad, ad, K.internal_id(ad), son, loop=dongu,
                                      hit=None if vurus is None else vurus / son))
        fm.fbx_meta(hedef, model_guid(model), remaps=remaps, animation_type=2, clips=clips)
        print(f"{fm.rel(hedef)}: {len(clips)} animasyon, {hedef.stat().st_size / 1e6:.2f} MB")

    # silahlar: kendi dosyalarındaki yerleşimle el yuvasına takılır
    for silah in SILAHLAR:
        hedef = OUT / "Silahlar" / f"{silah}.fbx"
        shutil.copyfile(src / f"Assets/fbx/{silah}.fbx", hedef)
        fm.fbx_meta(hedef, model_guid(silah), remaps={"skeleton": malzeme}, animation_type=0)

    script_guid = re.search(r"^guid: (\w+)", SCRIPT_META.read_text(encoding="utf-8"), re.M).group(1)
    for name, (model, silahlar, profil_adi, saldirilar) in TURLER.items():
        write_variant_prefab(name, model, silahlar, script_guid)
        dosya = profil_adi.replace(" ", "")
        fm.animation_profile(PROFILE_DIR / f"{dosya}AnimationProfile.asset", f"{dosya}AnimationProfile", profil_adi,
                             K.guid("profil:" + profil_adi), profil(saldirilar))
    lisans = OUT / "LICENSE.txt"
    shutil.copyfile(src / "LICENSE.txt", lisans)
    fm.write_text(Path(str(lisans) + ".meta"),
                  f"fileFormatVersion: 2\nguid: {K.guid('lisans')}\nTextScriptImporter:\n  externalObjects: {{}}\n"
                  "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    print(f"{len(KARAKTERLER)} iskelet, {len(SILAHLAR)} silah, {len(TURLER)} tür (prefab + animasyon profili) hazır")


if __name__ == "__main__":
    main()
