#!/usr/bin/env python3
"""Quaternius "Universal Animation Library" (Standard, CC0) animasyonlarını oyuna hazırlar.

Kütüphane Unreal mankeni iskeleti üzerinde yapılmış 40'tan fazla animasyon içerir. Unity onu "Humanoid"
olarak içe aktarınca bu animasyonlar her insan iskeletli modelde oynar. Oyunda canavarlar (Körmös, Cin)
için kendi animasyon profilleri yapılır: kılıç/gürz vuruşu, yumruk, büyü, ölüm, sıçrama...

Lisans CC0 olduğu için FBX depoya açık konur. 23 MB'lık dosyadan yalnız kullanılan animasyonlar tutulur
(fbx_kirp.py), geri kalanı atılır.

Kullanım (depo kök klasöründen):
    python3 "Tools~/dunya/animasyonlar.py" yol/UAL1_Standard.fbx
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import fbxmeta as fm  # noqa: E402

K = fm.Kimlik("3e9f2a71-5c84-4d6b-b1e0-7a2c9d4f6e15")
OUT = fm.ROOT / "Assets/Otuken/Animasyonlar/UAL"
FBX = OUT / "UAL1_Standard.fbx"
PROFILE_DIR = fm.ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame/AnimationProfile/Canavar"

# klip adı -> (son kare (30 fps), döngü mü, vuruş anı 0-1)
#   son kareler FBX'teki AnimationStack LocalStop değerleridir; vuruş anı, Blender'da elin en hızlı olduğu kare
KLIPLER = {
    "Idle_Loop": (75, True, None),
    "Walk_Loop": (40, True, None),
    "Jog_Fwd_Loop": (28, True, None),
    "Sprint_Loop": (20, True, None),
    "Sword_Idle": (50, True, None),
    "Sword_Attack": (46, False, 12 / 46),
    "Punch_Jab": (26, False, 5.5 / 26),
    "Punch_Cross": (30, False, 7.5 / 30),
    "Spell_Simple_Idle_Loop": (63, True, None),
    "Spell_Simple_Shoot": (15, False, None),
    "Hit_Chest": (10, False, None),
    "Hit_Head": (13, False, None),
    "Death01": (72, False, None),
    "Jump_Start": (40, False, None),
    "Jump_Loop": (75, True, None),
    "Jump_Land": (38, False, None),
}


def klip(ad):
    return (K.guid("model:UAL1_Standard"), K.internal_id(ad))


def ortak():
    """Körmös ve Cin'in paylaştığı hareketler; boş bırakılanlar (yana kayma, geri yürüme, yüzme) Human profilinden gelir"""
    return {
        "idleClip": klip("Idle_Loop"),
        "walkClip": klip("Walk_Loop"),
        "runClip": klip("Jog_Fwd_Loop"),
        "jumpClip": klip("Jump_Start"),
        "fallClip": klip("Jump_Loop"),
        "landClip": klip("Jump_Land"),
        "combatIdleClip": klip("Sword_Idle"),
        "combatWalkClip": klip("Walk_Loop"),
        "combatRunClip": klip("Jog_Fwd_Loop"),
        "combatJumpClip": klip("Jump_Start"),
        "combatFallClip": klip("Jump_Loop"),
        "combatLandClip": klip("Jump_Land"),
        "deathClip": klip("Death01"),
        "castClips": [klip("Spell_Simple_Idle_Loop")],
        "takeDamageClips": [klip("Hit_Chest"), klip("Hit_Head")],
    }


PROFILLER = {
    # Körmös: zincirli gürzünü savurur, arada yumruk atar
    "Kormos": dict(ortak(), attackClips=[klip("Sword_Attack"), klip("Punch_Cross")]),
    # Cin: küçük ve çevik; koşarken depar atar, sopası ve yumruğuyla saldırır
    "Cin": dict(ortak(), runClip=klip("Sprint_Loop"), combatRunClip=klip("Sprint_Loop"),
                attackClips=[klip("Sword_Attack"), klip("Punch_Jab"), klip("Punch_Cross")]),
}


def profil_referanslari():
    """yaratiklar.py için: profil adı listesi"""
    return list(PROFILLER)


def main():
    if len(sys.argv) < 2 and not FBX.exists():
        print(__doc__)
        sys.exit(1)
    for folder in (fm.ROOT / "Assets/Otuken", fm.ROOT / "Assets/Otuken/Animasyonlar", OUT, PROFILE_DIR):
        fm.folder_meta(folder, K)

    if len(sys.argv) >= 2:
        from fbx_kirp import kirp
        kalan = kirp(Path(sys.argv[1]), FBX, KLIPLER)
        assert sorted(kalan) == sorted(KLIPLER), kalan
        print(f"{fm.rel(FBX)}: {len(kalan)} animasyon, {FBX.stat().st_size / 1e6:.1f} MB")
        lisans = Path(sys.argv[1]).resolve().parents[1] / "License.txt"
        if lisans.exists():
            hedef = OUT / "License.txt"
            fm.write_bytes(hedef, lisans.read_bytes())
            fm.write_text(Path(str(hedef) + ".meta"),
                          f"fileFormatVersion: 2\nguid: {K.guid('lisans')}\nTextScriptImporter:\n  externalObjects: {{}}\n"
                          "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

    clips = [fm.clip_yaml(ad, f"Armature|{ad}", K.internal_id(ad), son, loop=dongu, bake=True, hit=vurus)
             for ad, (son, dongu, vurus) in KLIPLER.items()]
    fm.fbx_meta(FBX, K.guid("model:UAL1_Standard"), animation_type=3, clips=clips, bake_axis=True, materials=False)

    for ad, props in PROFILLER.items():
        fm.animation_profile(PROFILE_DIR / f"{ad}AnimationProfile.asset", f"{ad}AnimationProfile", ad,
                             K.guid("profil:" + ad), props)
    print(f"{len(KLIPLER)} klip, {len(PROFILLER)} animasyon profili hazır")


if __name__ == "__main__":
    main()
