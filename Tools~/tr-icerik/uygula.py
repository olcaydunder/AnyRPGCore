#!/usr/bin/env python3
"""icerik.json dosyasindaki Turkce adlari ve metinleri oyun verilerine uygular.

Sadece gorunen ad (displayName), aciklama (description), gorev hedefi metni
(overrideDisplayName) ve diyalog satirlarini degistirir. resourceName'lere
dokunmaz; bu yuzden oyun ici baglantilar bozulmaz.

Kullanim:  python3 "Tools~/tr-icerik/uygula.py"   (depo kok klasorunden)
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
GAME = ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame"
SPEC = json.loads((Path(__file__).parent / "icerik.json").read_text(encoding="utf-8"))

# Unity YAML: cift tirnakli, \u kacisli dize her zaman guvenli okunur.
q = lambda s: json.dumps(s, ensure_ascii=True)

TOP_KEY = re.compile(r"^  [A-Za-z_]\w*:", re.M)


def index(folder):
    out = {}
    for f in (GAME / folder).rglob("*.asset"):
        m = re.search(r"^  resourceName: (.*)$", f.read_text(encoding="utf-8"), re.M)
        if m:
            out.setdefault(m.group(1).strip(), []).append(f)
    return out


def set_top(text, key, value):
    """2 bosluk girintili ust duzey alanin (cok satirli olabilir) degerini degistir."""
    m = re.search(rf"^  {key}:.*?(?=^  [A-Za-z_]\w*:)", text, re.M | re.S)
    if not m:
        raise ValueError(f"alan yok: {key}")
    return text[: m.start()] + f"  {key}: {q(value)}\n" + text[m.end():]


def set_nth(text, key, indent, values):
    """Belirli girintideki `key:` alanlarini sirasiyla verilen degerlerle degistir."""
    # deger, girintisi <= indent olan bir sonraki anahtar ya da liste ogesine kadar surer
    pat = re.compile(rf"^{' ' * indent}{key}:.*?(?=^ {{0,{indent}}}(?:- |[A-Za-z_]\w*:)|\Z)", re.M | re.S)
    found = list(pat.finditer(text))
    if len(found) != len(values):
        raise ValueError(f"{key}: dosyada {len(found)} tane var, icerikte {len(values)} tane verildi")
    for m, v in reversed(list(zip(found, values))):
        text = text[: m.start()] + f"{' ' * indent}{key}: {q(v)}\n" + text[m.end():]
    return text


def main():
    changed, errors = 0, []
    for folder, entries in SPEC.items():
        if folder.startswith("_"):
            continue
        idx = index(folder)
        for name, data in entries.items():
            files = idx.get(name)
            if not files:
                errors.append(f"{folder}/{name}: dosya bulunamadi")
                continue
            for f in files:
                raw = f.read_bytes().decode("utf-8")
                crlf = "\r\n" in raw  # orijinal satir sonunu koru
                t = raw.replace("\r\n", "\n")
                try:
                    if folder == "Dialog":
                        t = set_nth(t, "description", 4, data)
                    else:
                        t = set_top(t, "displayName", data["ad"])
                        if "aciklama" in data:
                            t = set_top(t, "description", data["aciklama"])
                        if "hedefler" in data:
                            t = set_nth(t, "overrideDisplayName", 8, data["hedefler"])
                except ValueError as e:
                    errors.append(f"{folder}/{name}: {e}")
                    continue
                if crlf:
                    t = t.replace("\n", "\r\n")
                f.write_bytes(t.encode("utf-8"))
                changed += 1
    print(f"{changed} dosya guncellendi")
    for e in errors:
        print("HATA:", e)
    sys.exit(1 if errors else 0)


if __name__ == "__main__":
    main()
