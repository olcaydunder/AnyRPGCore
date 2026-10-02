#!/usr/bin/env python3
"""İkili FBX dosyasından istenmeyen animasyonları çıkarır (model, iskelet ve deri olduğu gibi kalır).

KayKit iskeletlerinin her FBX'inde 95 animasyon var (22 MB). Oyunda bunların bir kısmı, tek bir dosyadan
kullanılıyor; diğer dosyalarda animasyon hiç gerekmiyor. Dosyayı Blender'a alıp yeniden dışa aktarmak
kemik yönlerini değiştirebileceği için onun yerine FBX ağacındaki animasyon nesneleri ve bağlantıları
doğrudan silinir; geri kalan bütün düğümler aynen yazılır.

Blender'ın FBX okuyucu/yazıcı modüllerini kullanır (pip install bpy ile gelir).

Kullanım:  python3 fbx_kirp.py girdi.fbx cikti.fbx [tutulacak_animasyon ...]
"""
import sys
from pathlib import Path

import bpy

for _ust in Path(bpy.__file__).resolve().parents:
    _aday = next(_ust.glob("**/addons_core/io_scene_fbx"), None) if _ust.name == "bpy" else None
    if _aday:
        sys.path.insert(0, str(_aday.parent))
        break
from io_scene_fbx import encode_bin, parse_fbx  # noqa: E402

ANIM_TIPLERI = {b"AnimationStack", b"AnimationLayer", b"AnimationCurveNode", b"AnimationCurve"}

_EKLE = {
    b"B": "add_bool", b"C": "add_char", b"Z": "add_int8", b"Y": "add_int16", b"I": "add_int32",
    b"L": "add_int64", b"F": "add_float32", b"D": "add_float64", b"R": "add_bytes", b"S": "add_string",
    b"i": "add_int32_array", b"l": "add_int64_array", b"f": "add_float32_array", b"d": "add_float64_array",
    b"b": "add_bool_array", b"c": "add_byte_array",
}


def _kopya(kaynak, alt_suzgec=None):
    e = encode_bin.FBXElem(kaynak.id)
    for veri, tip in zip(kaynak.props, kaynak.props_type):
        getattr(e, _EKLE[bytes([tip])])(veri)
    for alt in kaynak.elems:
        if alt_suzgec is None or alt_suzgec(alt):
            e.elems.append(_kopya(alt))
    return e


def _yigin_adi(elem):
    # b"Armature|Idle\x00\x01AnimStack" -> "Idle"
    return elem.props[1].split(b"\x00\x01")[0].decode("utf-8").split("|")[-1]


def _ozellikler(elem):
    out = {}
    for p in elem.elems:
        if p.id == b"Properties70":
            for q in p.elems:
                out[q.props[0].decode()] = q.props[4:]
    return out


def yigin_kareleri(girdi, fps=30):
    """animasyon adı -> son kare (klibin başı 0. kare kabul edilir)"""
    kok, _ = parse_fbx.parse(str(girdi))
    objects = next(e for e in kok.elems if e.id == b"Objects")
    kt = 46186158000  # FBX zaman birimi / saniye
    sonuc = {}
    for o in objects.elems:
        if o.id == b"AnimationStack":
            p = _ozellikler(o)
            bas = p.get("LocalStart", [0])[0]
            son = p.get("LocalStop", [0])[0]
            sonuc[_yigin_adi(o)] = round((son - bas) / kt * fps, 3)
    return sonuc


def kirp(girdi, cikti, tut=()):
    tut = set(tut)
    kok, surum = parse_fbx.parse(str(girdi))
    objects = next(e for e in kok.elems if e.id == b"Objects")
    connections = next(e for e in kok.elems if e.id == b"Connections")

    tip = {o.props[0]: o.id for o in objects.elems}
    yiginlar = {o.props[0]: _yigin_adi(o) for o in objects.elems if o.id == b"AnimationStack"}
    eksik = tut - set(yiginlar.values())
    if eksik:
        raise SystemExit(f"{Path(girdi).name}: bu animasyonlar yok: {sorted(eksik)}")

    # animasyon nesneleri arası bağlantılar (kaynak -> hedef):
    # eğri -> eğri düğümü -> katman -> yığın
    ust = {}
    for c in connections.elems:
        kaynak, hedef = c.props[1], c.props[2]
        if tip.get(kaynak) in ANIM_TIPLERI and tip.get(hedef) in ANIM_TIPLERI:
            ust.setdefault(kaynak, set()).add(hedef)

    onbellek = {}

    def tutulur(uid):
        if uid not in onbellek:
            if tip[uid] == b"AnimationStack":
                onbellek[uid] = yiginlar[uid] in tut
            else:
                onbellek[uid] = False
                onbellek[uid] = any(tutulur(h) for h in ust.get(uid, ()))
        return onbellek[uid]

    sil = {uid for uid, t in tip.items() if t in ANIM_TIPLERI and not tutulur(uid)}
    kalan_sayi = {}
    for uid, t in tip.items():
        if uid not in sil:
            kalan_sayi[t] = kalan_sayi.get(t, 0) + 1

    yeni = encode_bin.FBXElem(b"")
    for bolum in kok.elems:
        if bolum.id == b"Objects":
            yeni.elems.append(_kopya(bolum, lambda o: not (o.props and o.props[0] in sil)))
        elif bolum.id == b"Connections":
            yeni.elems.append(_kopya(bolum, lambda c: c.props[1] not in sil and c.props[2] not in sil))
        elif bolum.id == b"Takes":
            yeni.elems.append(_kopya(
                bolum, lambda t: t.id != b"Take" or t.props[0].decode("utf-8").split("|")[-1] in tut))
        elif bolum.id == b"Definitions":
            yeni.elems.append(_tanimlar(bolum, kalan_sayi))
        else:
            yeni.elems.append(_kopya(bolum))

    encode_bin.write(str(cikti), yeni, surum)
    return sorted(ad for uid, ad in yiginlar.items() if uid not in sil)


def _tanimlar(bolum, kalan_sayi):
    """Definitions bölümündeki nesne sayılarını kalan nesnelere göre günceller."""
    e = encode_bin.FBXElem(bolum.id)
    toplam_eleman = None
    toplam = 0
    for ot in bolum.elems:
        if ot.id == b"ObjectType":
            ad = ot.props[0]
            if ad in ANIM_TIPLERI:
                sayi = kalan_sayi.get(ad, 0)
                if sayi == 0:
                    continue
            else:
                sayi = next((a.props[0] for a in ot.elems if a.id == b"Count"), 0)
            yeni_ot = encode_bin.FBXElem(ot.id)
            yeni_ot.add_string(ad)
            for alt in ot.elems:
                if alt.id == b"Count":
                    c = encode_bin.FBXElem(b"Count")
                    c.add_int32(sayi)
                    yeni_ot.elems.append(c)
                else:
                    yeni_ot.elems.append(_kopya(alt))
            toplam += sayi
            e.elems.append(yeni_ot)
        elif ot.id == b"Count":
            toplam_eleman = encode_bin.FBXElem(b"Count")
            e.elems.append(toplam_eleman)
        else:
            e.elems.append(_kopya(ot))
    if toplam_eleman is not None:
        toplam_eleman.add_int32(toplam)
    return e


if __name__ == "__main__":
    g, c, *t = sys.argv[1:]
    kalan = kirp(Path(g), Path(c), t)
    print(f"{Path(c).name}: {len(kalan)} animasyon kaldı, {Path(c).stat().st_size / 1e6:.2f} MB")
