#!/usr/bin/env python3
"""Oyunun müziklerini hazırlar: ses dosyalarını dönüştürür, şifreler, ses profillerine ve sahnelere bağlar.

Yerler:
  ana_menu   ana menü                      -> "Main Menu" profili
  yeni_oyun  karakter oluşturma             -> "New Game" profili (oyun yöneticisinde newGameAudio)
  yayla      Ötüken Yaylası (köy ve bozkır) -> "Otuken Yaylasi" profili; birden çok parça sırayla çalar
  magara     Erlik'in Mağarası              -> "Erlik Magarasi" profili
  boss       Tepegöz ve Yelbegen savaşı     -> "Boss Savasi" profili (Boss savaş düzeni, 1. evre)
  boss_ofke  boss'un canı yarıdan azken     -> "Boss Ofkesi" profili (2. evre; yoksa boss parçası sürer)

Ses işleme (ffmpeg): baştaki/sondaki sessizlik kırpılır, ses yüksekliği eşitlenir (-18 LUFS, efektlerin
üstüne çıkmasın diye), OGG Vorbis'e çevrilir. Unity derlemede telefona göre yeniden sıkıştırır (akışla yükleme).

LİSANS: Pixabay parçaları oyunda serbestçe kullanılabilir ama dosyaların "olduğu gibi" dağıtılması yasaktır.
Depo herkese açık olduğu için ses dosyaları depoya açık konmaz; Tools~/varliklar/muzik.tar.enc içinde
şifreli durur, GitHub derlemesi VARLIK_ANAHTARI ile açar (canavarlar.py ile aynı düzen).

Kullanım (depo kök klasöründen):
    VARLIK_ANAHTARI=... python3 "Tools~/dunya/muzikler.py" ana_menu=a.mp3 yayla=b.mp3 yayla=c.mp3 magara=d.mp3 ...
"""
import io
import json
import os
import re
import subprocess
import sys
import tarfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import fbxmeta as fm  # noqa: E402

K = fm.Kimlik("7b2e4c90-1f3a-4d58-9e6b-2c8a5f0d4e17")
OUT = fm.ROOT / "Assets/Otuken/Muzik"
ENCRYPTED = fm.ROOT / "Tools~/varliklar/muzik.tar.enc"
LISTE = fm.ROOT / "Tools~/varliklar/muzik.json"
GAME = fm.ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame"
RES = GAME / "Resources/FeaturesDemoGame"
AUDIO_TEMPLATE_META = fm.ROOT / "Assets/AnyRPG/Core/Content/Audio/Sounds/Music/WarOnWaterChapter1.wav.meta"
GAME_MANAGER = GAME / "Prefab/GameManager/FeaturesDemoGameManager.prefab"
SYSTEM_CONFIG_TARGET = "{fileID: 8448294747759913886, guid: 940327016906947448ca06fe7bf422fb,\n        type: 3}"

# yer -> (profil dosyası, profil adı)
PROFILLER = {
    "ana_menu": (RES / "AudioProfile/MainMenuAudio.asset", "Main Menu"),
    "yeni_oyun": (RES / "AudioProfile/NewGameAudio.asset", "New Game"),
    "yayla": (RES / "AudioProfile/Muzik/OtukenYaylasiMuzik.asset", "Otuken Yaylasi"),
    "magara": (RES / "AudioProfile/Muzik/ErlikMagarasiMuzik.asset", "Erlik Magarasi"),
    "boss": (RES / "AudioProfile/Muzik/BossSavasiMuzik.asset", "Boss Savasi"),
    "boss_ofke": (RES / "AudioProfile/Muzik/BossOfkesiMuzik.asset", "Boss Ofkesi"),
}
YER_ADI = {
    "ana_menu": "Ana menü", "yeni_oyun": "Karakter oluşturma", "yayla": "Ötüken Yaylası",
    "magara": "Erlik'in Mağarası", "boss": "Boss savaşı", "boss_ofke": "Boss öfkesi",
}
SAHNELER = {
    "yayla": RES / "SceneNode/FeaturesDemoZoneSceneNode.asset",
    "magara": RES / "SceneNode/FeaturesDemoDungeonSceneNode.asset",
}
BOSS_STRATEGY = RES / "CombatStrategy/BossCombatStrategy.asset"
CREDITS = RES / "CreditsCategory/MuzikCredits.asset"
CREDITS_SCRIPT = "acbdbda413a9e75489c46764c8bc894e"
AUDIO_PROFILE_SCRIPT = "3fd73ebc5463ba94cb6ff99164c9641a"

# önerilen Pixabay parçalarının sayfaları (dosya adındaki numaradan bulunur; jenerik ekranı için)
SAYFALAR = {
    "377495": ("Unity Winds", "NverAvetyanMusic",
               "https://pixabay.com/music/main-title-unity-winds-ethnic-trailer-377495/"),
    "363329": ("Anatolian Folk - Baglama With Rhodes", "MeditativeTiger",
               "https://pixabay.com/music/folk-intro-anatolian-folk-music-turkish-baglama-with-rhodes-363329/"),
    "412841": ("Ember Yildiz", "NverAvetyanMusic", "https://pixabay.com/music/folk-ember-yildiz-turkish-folk-412841/"),
    "603134": ("Nutag", "inhaleexhalestudio", "https://pixabay.com/music/orchestral-nutag-603134/"),
    "603263": ("SERVER - Ancient Rituals", "Rockot",
               "https://pixabay.com/music/ambient-server-ancient-cinematic-rituals-603263/"),
    "202317": ("Ethnic Cinematic Trailer", "NverAvetyanMusic",
               "https://pixabay.com/music/main-title-ethnic-cinematic-trailer-royalty-free-music-202317/"),
    "604344": ("Steppe Thunder", "baranova_n", "https://pixabay.com/music/world-steppe-thunder-604344/"),
}

q = lambda s: json.dumps(s, ensure_ascii=True)


def oku(path):
    """metin ve satır sonu biçimi ("\r\n" ya da "\n")"""
    data = Path(path).read_bytes().decode("utf-8")
    return data.replace("\r\n", "\n"), ("\r\n" if "\r\n" in data else "\n")


def yaz(path, text, eol="\n"):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    data = text.replace("\n", eol).encode("utf-8")
    if not Path(path).exists() or Path(path).read_bytes() != data:
        Path(path).write_bytes(data)


def pixabay_bilgisi(path):
    """'nveravetyanmusic-unity-winds-ethnic-trailer-377495.mp3' -> (sanatçı, ad, numara)"""
    stem = Path(path).stem
    m = re.match(r"^(.+?)-(.+)-(\d{5,})$", stem)
    if not m:
        return "", stem.replace("_", " ").replace("-", " "), ""
    sanatci, ad, numara = m.groups()
    return sanatci, ad.replace("-", " ").title(), numara


def donustur(kaynak, hedef):
    """sessizliği kırp, ses yüksekliğini eşitle, OGG Vorbis'e çevir"""
    hedef.parent.mkdir(parents=True, exist_ok=True)
    filtre = ("silenceremove=start_periods=1:start_threshold=-55dB:start_silence=0.05,"
              "areverse,silenceremove=start_periods=1:start_threshold=-55dB:start_silence=0.3,areverse,"
              "loudnorm=I=-18:TP=-1.5:LRA=11")
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(kaynak), "-vn", "-map_metadata", "-1",
                    "-af", filtre, "-ar", "44100", "-ac", "2", "-c:a", "libvorbis", "-q:a", "4",
                    "-fflags", "+bitexact", "-flags:a", "+bitexact", str(hedef)], check=True)
    sure = float(subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0",
                                 str(hedef)], capture_output=True, text=True, check=True).stdout.strip())
    return sure


def audio_meta(path, guid):
    text = AUDIO_TEMPLATE_META.read_text(encoding="utf-8")
    text = re.sub(r"^guid: \w+$", f"guid: {guid}", text, count=1, flags=re.M)
    text = re.sub(r"^    quality: .*$", "    quality: 0.5", text, count=1, flags=re.M)
    text = re.sub(r"^  3D: 1$", "  3D: 0", text, count=1, flags=re.M)
    # Unity'nin tepe değeri eşitlemesi kapalı: ses yüksekliği ffmpeg ile zaten eşitlendi
    text = re.sub(r"^  normalize: 1$", "  normalize: 0", text, count=1, flags=re.M)
    fm.write_text(Path(str(path) + ".meta"), text)


def audio_profile(path, resource_name, clip_guids, artist):
    clips = "".join(f"  - {{fileID: 8300000, guid: {g}, type: 3}}\n" for g in clip_guids) or ""
    clip_block = "  audioClips:\n" + clips if clips else "  audioClips: []\n"
    name = path.stem
    text = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {AUDIO_PROFILE_SCRIPT}, type: 3}}\n  m_Name: {name}\n"
            f"  m_EditorClassIdentifier: \n  resourceName: {resource_name}\n  displayName: \n  icon: {{fileID: 0}}\n"
            "  iconBackgroundImage: {fileID: 0}\n  description: \n  useRegionalDescription: 0\n"
            f"  resourceDescriptionProfile: \n  artistName: {q(artist) if artist else ''}\n" + clip_block)
    eol = oku(path)[1] if path.exists() else "\n"
    yaz(path, text, eol)
    meta = Path(str(path) + ".meta")
    if not meta.exists():
        fm.native_meta(path, K.guid("profil:" + resource_name), 11400000)


def set_line(path, pattern, value):
    text, eol = oku(path)
    text, n = re.subn(pattern, lambda m: m.group(1) + value, text, count=1, flags=re.M)
    assert n == 1, (path.name, pattern)
    yaz(path, text, eol)


def set_new_game_audio(value):
    """oyun yöneticisi prefab'ında SystemConfigurationManager.newGameAudio"""
    text, eol = oku(GAME_MANAGER)
    block = (f"    - target: {SYSTEM_CONFIG_TARGET}\n      propertyPath: newGameAudio\n"
             f"      value: {value}\n      objectReference: {{fileID: 0}}\n")
    if "propertyPath: newGameAudio\n" in text:
        text = re.sub(r"(      propertyPath: newGameAudio\n      value: ).*\n", lambda m: m.group(1) + value + "\n", text)
    else:
        anchor = f"    - target: {SYSTEM_CONFIG_TARGET}\n      propertyPath: vendorAudioProfileName\n"
        assert anchor in text
        text = text.replace(anchor, block + anchor, 1)
    yaz(GAME_MANAGER, text, eol)


def credits(parcalar):
    nodes = "".join(
        f"  - creditName: {q(p['ad'])}\n    creditAttribution: {q(p['sanatci'] + ' (Pixabay) - ' + YER_ADI[p['yer']])}\n"
        f"    email: \n    userUrl: {p['url']}\n    downloadUrl: \n"
        for p in parcalar)
    text = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n"
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n"
            "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
            f"  m_Script: {{fileID: 11500000, guid: {CREDITS_SCRIPT}, type: 3}}\n  m_Name: MuzikCredits\n"
            "  m_EditorClassIdentifier: \n  resourceName: Muzik\n  displayName: \"M\\u00fczik\"\n  icon: {fileID: 0}\n"
            "  iconBackgroundImage: {fileID: 0}\n  description: \n  useRegionalDescription: 0\n"
            "  resourceDescriptionProfile: \n  optionalOverride: 0\n  categoryName: \"M\\u00fczik\"\n"
            "  creditsNodes:\n" + nodes)
    fm.write_text(CREDITS, text)
    if not Path(str(CREDITS) + ".meta").exists():
        fm.native_meta(CREDITS, K.guid("credits:Muzik"), 11400000)


def encrypt(key, files):
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode="w:gz") as tar:
        for path in sorted(files):
            info = tar.gettarinfo(str(path), arcname=fm.rel(path))
            info.uid = info.gid = 0
            info.uname = info.gname = ""
            info.mtime = 0
            with open(path, "rb") as handle:
                tar.addfile(info, handle)
    ENCRYPTED.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run(["openssl", "enc", "-aes-256-cbc", "-pbkdf2", "-iter", "200000", "-salt",
                    "-out", str(ENCRYPTED), "-pass", "env:VARLIK_ANAHTARI"],
                   input=buffer.getvalue(), check=True, env=dict(os.environ, VARLIK_ANAHTARI=key))
    print(f"şifreli arşiv: {fm.rel(ENCRYPTED)} ({ENCRYPTED.stat().st_size // 1024} KB, {len(files)} parça)")


def main():
    args = [a for a in sys.argv[1:] if "=" in a]
    if not args:
        print(__doc__)
        sys.exit(1)
    key = os.environ.get("VARLIK_ANAHTARI", "")
    if len(key) < 20:
        sys.exit("VARLIK_ANAHTARI ortam değişkeni (en az 20 karakter) gerekli")

    for folder in (fm.ROOT / "Assets/Otuken", OUT, RES / "AudioProfile/Muzik"):
        fm.folder_meta(folder, K)

    parcalar = []
    for arg in args:
        yer, kaynak = arg.split("=", 1)
        if yer not in PROFILLER:
            sys.exit(f"bilinmeyen yer: {yer} (yerler: {', '.join(PROFILLER)})")
        sanatci, ad, numara = pixabay_bilgisi(kaynak)
        if numara in SAYFALAR:
            ad, sanatci, _ = SAYFALAR[numara]
        dosya_adi = re.sub(r"[^A-Za-z0-9]+", "_", f"{yer}_{ad}").strip("_")[:60] + ".ogg"
        hedef = OUT / dosya_adi
        sure = donustur(kaynak, hedef)
        guid = K.guid("ses:" + dosya_adi)
        audio_meta(hedef, guid)
        url = SAYFALAR[numara][2] if numara in SAYFALAR else "https://pixabay.com/music/"
        parcalar.append(dict(yer=yer, ad=ad, sanatci=sanatci, numara=numara, dosya=dosya_adi, guid=guid,
                             sure=round(sure, 1), url=url))
        print(f"{YER_ADI[yer]:20s} {ad} - {sanatci} ({int(sure // 60)}:{int(sure % 60):02d})")

    # önceki çalıştırmadan kalan, artık kullanılmayan parçaları sil
    kullanilan = {p["dosya"] for p in parcalar}
    for eski in OUT.glob("*.ogg"):
        if eski.name not in kullanilan:
            eski.unlink()
            Path(str(eski) + ".meta").unlink(missing_ok=True)

    # ses profilleri
    for yer, (path, resource_name) in PROFILLER.items():
        secilen = [p for p in parcalar if p["yer"] == yer]
        if not secilen:
            continue
        artist = ", ".join(sorted({p["sanatci"] for p in secilen if p["sanatci"]}))
        audio_profile(path, resource_name, [p["guid"] for p in secilen], artist)

    # sahneler, boss evreleri, karakter oluşturma
    for yer, scene in SAHNELER.items():
        if any(p["yer"] == yer for p in parcalar):
            set_line(scene, r"^(  backgroundMusicProfile: ).*$", PROFILLER[yer][1])
    yerler = {p["yer"] for p in parcalar}
    if "boss" in yerler:
        text, eol = oku(BOSS_STRATEGY)
        evreler = re.findall(r"^    phaseMusicProfileName: .*$", text, flags=re.M)
        assert len(evreler) == 2, evreler
        ofke = "Boss Ofkesi" if "boss_ofke" in yerler else "Boss Savasi"
        parts = re.split(r"(^    phaseMusicProfileName: .*$)", text, flags=re.M)
        parts[1] = "    phaseMusicProfileName: Boss Savasi"
        parts[3] = f"    phaseMusicProfileName: {ofke}"
        yaz(BOSS_STRATEGY, "".join(parts), eol)
    if "yeni_oyun" in yerler:
        set_new_game_audio("New Game")

    credits(parcalar)
    LISTE.parent.mkdir(parents=True, exist_ok=True)
    LISTE.write_text(json.dumps(parcalar, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    encrypt(key, [OUT / p["dosya"] for p in parcalar])
    print(f"{len(parcalar)} parça hazır")


if __name__ == "__main__":
    main()
