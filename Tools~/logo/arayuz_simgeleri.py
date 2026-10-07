#!/usr/bin/env python3
"""
Ekran düğmesi simgeleri (Ötüken HUD): altın çizgili, saydam zeminli 192x192 PNG'ler.
Çıktı: Assets/Otuken/Resources/Arayuz/*.png (Resources.Load<Sprite>("Arayuz/canta") ile yüklenir)
Çalıştırma: python3 "Tools~/logo/arayuz_simgeleri.py"
Meta dosyaları (sprite içe aktarma) yoksa Assets/Otuken/Simge/kut.png.meta örnek alınarak yazılır.
"""
import re
import uuid
from pathlib import Path

import cairosvg

KOK = Path(__file__).resolve().parents[2]
CIKTI = KOK / "Assets/Otuken/Resources/Arayuz"
ORNEK_META = KOK / "Assets/Otuken/Simge/kut.png.meta"
N = 192
ALTIN = "#F2CF7A"
KOYU = "#5A3A10"
CIZGI = 10


def svg(govde):
    return f'''<svg xmlns="http://www.w3.org/2000/svg" width="{N}" height="{N}" viewBox="0 0 192 192">
<defs><filter id="g" x="-20%" y="-20%" width="140%" height="140%"><feDropShadow dx="0" dy="3" stdDeviation="3" flood-color="#000" flood-opacity="0.55"/></filter></defs>
<g filter="url(#g)" fill="none" stroke="{ALTIN}" stroke-width="{CIZGI}" stroke-linecap="round" stroke-linejoin="round">{govde}</g></svg>'''


SIMGELER = {
    # heybe/çanta: gövde, kapak, kayış, toka
    "canta": '''<path d="M44 78 Q44 62 60 62 L132 62 Q148 62 148 78 L152 148 Q152 160 140 160 L52 160 Q40 160 40 148 Z"/>
<path d="M70 62 Q70 32 96 32 Q122 32 122 62"/>
<path d="M44 92 L148 92"/><rect x="84" y="86" width="24" height="22" rx="4" fill="{0}"/>'''.format(ALTIN),
    # çark (ayarlar)
    "ayarlar": "".join(
        f'<rect x="88" y="22" width="16" height="30" rx="4" fill="{ALTIN}" stroke="none" transform="rotate({a} 96 96)"/>'
        for a in range(0, 360, 45)) + '<circle cx="96" cy="96" r="44"/><circle cx="96" cy="96" r="16"/>',
    # ışınlanma: geçit kemeri ve içinde girdap
    "isinlan": '''<path d="M46 166 L46 82 Q46 34 96 34 Q146 34 146 82 L146 166"/>
<path d="M96 132 Q72 128 72 106 Q72 86 94 84 Q114 84 116 102 Q116 116 102 118 Q90 118 90 108 Q92 100 100 102"/>
<path d="M30 166 L162 166"/>''',
    # menü: 3x3 kare ızgara
    "menu": "".join(f'<rect x="{x}" y="{y}" width="34" height="34" rx="7"/>' for x in (36, 79, 122) for y in (36, 79, 122)),
    # hedef: nişangâh
    "hedef": '''<circle cx="96" cy="96" r="52"/><circle cx="96" cy="96" r="18" fill="{0}"/>
<path d="M96 22 L96 54 M96 138 L96 170 M22 96 L54 96 M138 96 L170 96"/>'''.format(ALTIN),
    # oto av: kılıç ve dönen ok
    "otoav": '''<path d="M60 132 L126 66 M118 58 L134 74 M64 120 L76 132 M56 140 L46 150"/>
<path d="M150 104 A56 56 0 1 1 112 42" /><path d="M104 28 L116 42 L100 52" fill="none"/>''',
    # binek: at başı
    "binek": '''<path d="M62 166 L70 118 Q60 92 74 64 L84 40 L94 58 Q122 56 140 80 L152 104 Q156 116 144 120 L126 112 Q116 128 120 166 Z"/>
<circle cx="112" cy="78" r="5" fill="{0}"/>'''.format(ALTIN),
    # etkinlik: bayrak
    "etkinlik": '''<path d="M58 170 L58 26"/><path d="M58 32 Q90 18 112 34 Q134 50 158 36 L158 104 Q134 118 112 102 Q90 86 58 100"/>''',
    # harita: katlanmış harita
    "harita": '''<path d="M28 50 L72 34 L120 50 L164 34 L164 142 L120 158 L72 142 L28 158 Z"/><path d="M72 34 L72 142 M120 50 L120 158"/>''',
    # depo: sandık
    "depo": '''<rect x="32" y="74" width="128" height="84" rx="8"/><path d="M32 74 Q32 40 66 40 L126 40 Q160 40 160 74"/>
<path d="M32 100 L160 100"/><rect x="84" y="92" width="24" height="26" rx="4" fill="{0}"/>'''.format(ALTIN),
}


def meta_yaz(png: Path):
    meta = Path(str(png) + ".meta")
    if meta.exists():
        return
    ornek = ORNEK_META.read_text(encoding="utf-8")
    g = uuid.uuid5(uuid.NAMESPACE_URL, "otuken-arayuz:" + png.name).hex
    meta.write_text(re.sub(r"^guid: \w+", "guid: " + g, ornek, flags=re.M), encoding="utf-8")


def main():
    CIKTI.mkdir(parents=True, exist_ok=True)
    klasor_meta = Path(str(CIKTI) + ".meta")
    if not klasor_meta.exists():
        g = uuid.uuid5(uuid.NAMESPACE_URL, "otuken-arayuz-klasor").hex
        klasor_meta.write_text(f"fileFormatVersion: 2\nguid: {g}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    for ad, govde in SIMGELER.items():
        p = CIKTI / f"{ad}.png"
        cairosvg.svg2png(bytestring=svg(govde).encode(), write_to=str(p), output_width=N, output_height=N)
        meta_yaz(p)
        print("yazıldı", p.relative_to(KOK))


if __name__ == "__main__":
    main()
