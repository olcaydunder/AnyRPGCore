#!/usr/bin/env python3
"""
Demirci cevherleri (Ötüken): +5'ten sonra her yükseltme cevher yuvasına bir cevher ister.
  +6 Demir Cevheri · +7 Gümüş Cevheri · +8 Altın Cevheri · +9 Gök Demiri
Canavarlardan bölgeye göre düşer (YerdekiGanimet.CevherAt), boss'lar bir tane bırakır; takas ve pazarda satılabilir.
Yazar: simgeler Assets/Otuken/Simge/Cevher/*.png, eşyalar Resources/FeaturesDemoGame/Item/Cevher/*.asset (+ meta).
Çalıştırma: python3 "Tools~/dunya/cevherler.py"
"""
import re
import uuid
from pathlib import Path

import cairosvg

KOK = Path(__file__).resolve().parents[2]
SIMGE = KOK / "Assets/Otuken/Simge/Cevher"
ESYA = KOK / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame/Item/Cevher"
ORNEK_ESYA = KOK / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame/Item/Ganimet/AltinTamgaYuzuguItem.asset"
ORNEK_ESYA_META = Path(str(ORNEK_ESYA) + ".meta")
ORNEK_SIMGE_META = KOK / "Assets/Otuken/Simge/kut.png.meta"

# kaynak adı, görünen ad, açıklama, nitelik, fiyat (bakır), renkler (açık, koyu, parıltı)
CEVHERLER = [
    ("Demir Cevheri", "Demir Cevheri", "Demirci'de +6 yükseltmenin cevher yuvasına konur. Ötüken dağlarından.",
     "UnCommon", 900, ("#B9C3CC", "#4A5560", "#E8F0F8")),
    ("Gumus Cevheri", "Gümüş Cevheri", "Demirci'de +7 yükseltmenin cevher yuvasına konur. Ay ışığında parlar.",
     "Rare", 2400, ("#E4E9F2", "#7C8696", "#FFFFFF")),
    ("Altin Cevheri", "Altın Cevheri", "Demirci'de +8 yükseltmenin cevher yuvasına konur. Kağanların hazinesinden.",
     "Epic", 6000, ("#F6CF5A", "#8A5A12", "#FFF2B0")),
    ("Gok Demiri", "Gök Demiri", "Demirci'de +9 yükseltmenin cevher yuvasına konur. Gökten düşmüş, Tengri'nin demiri.",
     "Legendary", 15000, ("#7FC7FF", "#1E3F86", "#E2F4FF")),
]


def turkce_kacis(s):
    return "".join(c if ord(c) < 128 else "\\u%04x" % ord(c) for c in s)


def simge_svg(acik, koyu, parilti):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 128 128">
<defs><radialGradient id="z" cx="50%" cy="45%" r="60%"><stop offset="0" stop-color="#3A2E22"/><stop offset="1" stop-color="#15100B"/></radialGradient>
<linearGradient id="t" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{acik}"/><stop offset="1" stop-color="{koyu}"/></linearGradient></defs>
<rect x="2" y="2" width="124" height="124" rx="18" fill="url(#z)" stroke="#C9A35A" stroke-width="3"/>
<path d="M30 86 L22 62 L40 34 L70 26 L98 40 L106 70 L90 96 L56 102 Z" fill="#4B3D2E" stroke="#211810" stroke-width="3"/>
<path d="M44 58 L56 40 L74 44 L80 62 L66 76 L48 72 Z" fill="url(#t)" stroke="{koyu}" stroke-width="2"/>
<path d="M76 74 L90 64 L98 76 L88 88 L76 86 Z" fill="url(#t)" stroke="{koyu}" stroke-width="2"/>
<path d="M34 76 L42 70 L48 80 L40 88 Z" fill="url(#t)" stroke="{koyu}" stroke-width="2"/>
<path d="M56 46 L62 44 L60 52 Z" fill="{parilti}"/><path d="M84 68 L88 66 L86 72 Z" fill="{parilti}"/>
<circle cx="70" cy="50" r="3" fill="{parilti}"/></svg>'''


def guid(ad):
    return uuid.uuid5(uuid.NAMESPACE_URL, "otuken-cevher:" + ad).hex


def main():
    SIMGE.mkdir(parents=True, exist_ok=True)
    ESYA.mkdir(parents=True, exist_ok=True)
    for klasor in (SIMGE, ESYA):
        m = Path(str(klasor) + ".meta")
        if not m.exists():
            m.write_text(f"fileFormatVersion: 2\nguid: {guid('klasor-' + klasor.name)}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    ornek = ORNEK_ESYA.read_text(encoding="utf-8")
    for kaynak, ad, aciklama, nitelik, fiyat, renk in CEVHERLER:
        dosya = kaynak.replace(" ", "")
        png = SIMGE / f"{dosya}.png"
        cairosvg.svg2png(bytestring=simge_svg(*renk).encode(), write_to=str(png), output_width=128, output_height=128)
        simge_guid = guid("simge-" + dosya)
        meta = Path(str(png) + ".meta")
        meta.write_text(re.sub(r"^guid: \w+", "guid: " + simge_guid, ORNEK_SIMGE_META.read_text(encoding="utf-8"), flags=re.M), encoding="utf-8")
        s = ornek
        s = re.sub(r"^  m_Name: .*$", lambda m: f"  m_Name: {dosya}Item", s, flags=re.M)
        s = re.sub(r"^  resourceName: .*$", lambda m: f"  resourceName: {kaynak}", s, flags=re.M)
        s = re.sub(r"^  displayName: .*$", lambda m: f'  displayName: "{turkce_kacis(ad)}"', s, flags=re.M)
        s = re.sub(r"^  icon: .*$", lambda m: f"  icon: {{fileID: 21300000, guid: {simge_guid}, type: 3}}", s, flags=re.M)
        s = re.sub(r"^  description: .*$", lambda m: f'  description: "{turkce_kacis(aciklama)}"', s, flags=re.M)
        s = re.sub(r"^  itemQuality: .*$", lambda m: f"  itemQuality: {nitelik}", s, flags=re.M)
        s = re.sub(r"^  basePrice: .*$", lambda m: f"  basePrice: {fiyat}", s, flags=re.M)
        s = re.sub(r"^  weight: .*$", lambda m: "  weight: 0.5", s, flags=re.M)
        (ESYA / f"{dosya}Item.asset").write_text(s, encoding="utf-8")
        Path(str(ESYA / f"{dosya}Item.asset") + ".meta").write_text(
            re.sub(r"^guid: \w+", "guid: " + guid("esya-" + dosya), ORNEK_ESYA_META.read_text(encoding="utf-8"), flags=re.M), encoding="utf-8")
        print("yazıldı", kaynak)


if __name__ == "__main__":
    main()
