#!/usr/bin/env python3
"""
Savaş efektlerinin dokuları ve malzemeleri (AnyRPG.Efektler kodla kurduğu parçacık sistemlerinde kullanır).
Çıktı: Assets/Otuken/Resources/Efekt/
  isik.png   yumuşak ışık topu (parlama, kıvılcım, iz)
  yay.png    kılıç savurma yayı (hilal)
  halka.png  şok dalgası halkası
  Isik.mat, Yay.mat, Halka.mat   ışığı toplayan (additive) saydam parçacık malzemeleri
  Toz.mat                        olağan saydam (toz, duman)
Malzemeler AnyRPG'nin ReflectDamageMaterial.mat'ı örnek alınarak yazılır (URP Particles/Unlit, saydam): böylece
derlemede gölgelendirici türü atılmaz. Dokular beyazdır; renk parçacığın renginden gelir.
Kenney "Particle Pack" (CC0, kenney.nl) dokularından seçilenler de buradan alınır: 256 piksele küçültülür,
her birine aynı örnekten malzeme yazılır (KENNEY sözlüğü: malzeme adı -> (doku, ışığı toplayan mı)).
Çalıştırma: python3 "Tools~/logo/efekt_dokulari.py" [Kenney paketinin png klasörü]
    git clone https://github.com/Calinou/kenney-particle-pack /tmp/kpp
    python3 "Tools~/logo/efekt_dokulari.py" /tmp/kpp/addons/kenney_particle_pack
"""
import sys
import re
import uuid
from pathlib import Path

import numpy as np
from PIL import Image

KOK = Path(__file__).resolve().parents[2]
CIKTI = KOK / "Assets/Otuken/Resources/Efekt"
ORNEK_DOKU_META = KOK / "Assets/Otuken/Simge/kut.png.meta"
ORNEK_MALZEME = KOK / "Assets/AnyRPG/Core/Content/Particles/Point/ReflectDamageMaterial.mat"
ORNEK_DOKU_GUID = "c01e7fa649a19614eac6a70f9ebedac8"


def kimlik(ad):
    return uuid.uuid5(uuid.NAMESPACE_URL, "otuken-efekt:" + ad).hex


def kaydet(ad, alfa):
    a = np.clip(alfa, 0.0, 1.0)
    n = a.shape[0]
    rgba = np.zeros((n, n, 4), dtype=np.uint8)
    rgba[..., :3] = 255
    rgba[..., 3] = (a * 255).astype(np.uint8)
    p = CIKTI / f"{ad}.png"
    Image.fromarray(rgba, "RGBA").save(p)
    meta = Path(str(p) + ".meta")
    if not meta.exists():
        ornek = ORNEK_DOKU_META.read_text(encoding="utf-8")
        meta.write_text(re.sub(r"^guid: \w+", "guid: " + kimlik(ad + ".png"), ornek, flags=re.M), encoding="utf-8")
    print("yazıldı", p.relative_to(KOK))


def izgara(n):
    y, x = np.mgrid[0:n, 0:n]
    c = (n - 1) / 2.0
    return (x - c) / c, (c - y) / c


def isik():
    x, y = izgara(128)
    r = np.sqrt(x * x + y * y)
    # parlak çekirdek + geniş hale; kenarda tam sıfır
    a = 0.75 * np.exp(-(r / 0.22) ** 2) + 0.45 * np.exp(-(r / 0.5) ** 2)
    return a * np.clip((1.0 - r) / 0.25, 0, 1)


def halka():
    x, y = izgara(256)
    r = np.sqrt(x * x + y * y)
    a = np.exp(-((r - 0.82) / 0.045) ** 2) + 0.35 * np.exp(-((r - 0.74) / 0.12) ** 2)
    return a * np.clip((1.0 - r) / 0.06, 0, 1)


def yay():
    x, y = izgara(256)
    r = np.sqrt(x * x + y * y)
    aci = np.degrees(np.arctan2(y, x))  # -180..180, 0 sağ
    # sağa bakan 150 derecelik hilal: dış kenarı keskin, içe doğru solar; uçlara doğru incelir
    u = np.clip(1.0 - np.abs(aci) / 75.0, 0, 1)
    dis = 0.88
    kalinlik = 0.05 + 0.34 * u ** 0.8
    ic = np.clip((r - (dis - kalinlik)) / np.maximum(kalinlik, 1e-4), 0, 1)
    a = 1.25 * (ic ** 1.5) * np.clip((dis + 0.03 - r) / 0.03, 0, 1) * (u ** 0.5)
    return a


# malzeme adı -> (Kenney dokusu, ışığı toplayan mı)
KENNEY = {
    "Yildiz": ("star_09", True),      # isabet parlaması
    "Pence": ("scratch_01", True),    # kritik vuruşun pençe izi
    "Girdap": ("twirl_02", True),     # beceri vuruşunun dönen girdabı
    "Rune": ("magic_02", True),       # büyü çemberi (yerde)
    "Dalga": ("light_01", True),      # yerde yayılan dalga
    "Simsek": ("spark_02", True),     # beceri vuruşunun şimşeği
    "Kor": ("fire_01", True),         # kritik vuruşun kor patlaması
    "Cizgi": ("trace_01", True),      # yükselen ışık çizgileri
    "Duman": ("smoke_05", False),     # toz ve duman
    "Toprak": ("dirt_02", False),     # yerden sıçrayan toprak
}


def kenney(klasor):
    klasor = Path(klasor)
    for ad, (doku, toplayan) in KENNEY.items():
        kaynak = klasor / f"{doku}.png"
        im = Image.open(kaynak).convert("RGBA").resize((256, 256), Image.LANCZOS)
        # beyaz doku, parlaklık x saydamlık alfa olur (renk parçacıktan gelir)
        d = np.asarray(im).astype(np.float32) / 255.0
        alfa = d[..., 3] * d[..., :3].max(axis=2)
        kaydet("kenney_" + doku, alfa / max(alfa.max(), 1e-4))
        malzeme(ad, "kenney_" + doku, toplayan)
    (CIKTI / "KENNEY_LISANS.txt").write_text(
        "Bu klasördeki kenney_*.png dokuları Kenney'nin \"Particle Pack\" (1.1) paketinden alınmış, 256 piksele\n"
        "küçültülmüştür. Kenney Vleugels (www.kenney.nl). Lisans: Creative Commons Zero (CC0),\n"
        "http://creativecommons.org/publicdomain/zero/1.0/\n", encoding="utf-8")
    meta = CIKTI / "KENNEY_LISANS.txt.meta"
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {kimlik('KENNEY_LISANS.txt')}\nTextScriptImporter:\n  externalObjects: {{}}\n"
                        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


def malzeme(ad, doku, toplayan):
    s = ORNEK_MALZEME.read_text(encoding="utf-8")
    s = s.replace("m_Name: ReflectDamageMaterial", "m_Name: " + ad)
    assert ORNEK_DOKU_GUID in s
    s = s.replace(ORNEK_DOKU_GUID, kimlik(doku + ".png"))
    if not toplayan:
        # olağan saydamlık: kaynak alfa, 1 - kaynak alfa
        for eski, yeni in (("- _Blend: 2", "- _Blend: 0"), ("- _DstBlend: 1\n", "- _DstBlend: 10\n"),
                           ("- _DstBlendAlpha: 1", "- _DstBlendAlpha: 10")):
            assert eski in s, eski
            s = s.replace(eski, yeni)
    p = CIKTI / f"{ad}.mat"
    p.write_text(s, encoding="utf-8")
    meta = Path(str(p) + ".meta")
    if not meta.exists():
        meta.write_text(
            f"fileFormatVersion: 2\nguid: {kimlik(ad + '.mat')}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
            "  mainObjectFileID: 2100000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    print("yazıldı", p.relative_to(KOK))


def main():
    CIKTI.mkdir(parents=True, exist_ok=True)
    klasor_meta = Path(str(CIKTI) + ".meta")
    if not klasor_meta.exists():
        klasor_meta.write_text(
            f"fileFormatVersion: 2\nguid: {kimlik('klasor')}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n"
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    kaydet("isik", isik())
    kaydet("halka", halka())
    kaydet("yay", yay())
    malzeme("Isik", "isik", True)
    malzeme("Halka", "halka", True)
    malzeme("Yay", "yay", True)
    malzeme("Toz", "isik", False)
    if len(sys.argv) > 1:
        kenney(sys.argv[1])


if __name__ == "__main__":
    main()
