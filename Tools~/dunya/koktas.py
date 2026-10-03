#!/usr/bin/env python3
"""Kök Taş: haritalar arası yolculuk yapılan "Geçit Taşı"nın modeli.

Orhun yazıtları gibi kaplumbağa kaideli bir dikili taş; iki geniş yüzünde Göktürk harfleriyle
Kül Tigin yazıtının açılışı ("Üze kök teŋri asra yagız yer kılıntukda...") kazılı, harfler
gök turkuazı ışıkla parlar. Model Blender'da (bpy) koddan kurulur, dokular PIL ile çizilir.

Çıktı: Assets/Otuken/Modeller/KokTas/ (KokTas.fbx, Doku/, Malzeme/)

Kullanım:
    git clone --depth 1 --filter=blob:none --sparse https://github.com/notofonts/notofonts.github.io /tmp/nf
    (cd /tmp/nf && git sparse-checkout set fonts/NotoSansOldTurkic)
    python3 "Tools~/dunya/koktas.py" /tmp/nf/fonts/NotoSansOldTurkic/hinted/ttf/NotoSansOldTurkic-Regular.ttf
"""
import math
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, str(Path(__file__).parent))
import fbxmeta as fm  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/Otuken/Modeller/KokTas"
K = fm.Kimlik("3e9d7c51-0a2b-4c8e-9f61-5d4b3a2c1e07")

# taşın ölçüleri (metre): yazılı gövde ve kaplumbağa kaide
GOVDE_EN, GOVDE_DERIN, GOVDE_BOY, KEMER = 0.95, 0.34, 2.35, 0.38
KAIDE_BOY = 0.55

# Kül Tigin yazıtı, doğu yüzü, 1-3. satırların başı ve güney yüzünden bir cümle (Göktürk harfleriyle).
# Yazı sütunlara akıtılır: sağdan sola, her sütun yukarıdan aşağı.
#   üze kök teŋri asra yagız yer kılıntukda ekin ara kişi oglı kılınmış
#   kişi oglınta üze eçüm apam Bumın kagan İstemi kagan olurmış
#   olurupan Türük bodunıŋ ilin törüsin tuta birmiş
#   Türük bodun Ötüken yış olursar beŋgü il tuta olurtaçı sen
# "Üstte mavi gök, altta yağız yer yaratıldığında, ikisinin arasında insanoğlu yaratılmış. İnsanoğlunun üzerine
#  atalarım Bumın Kağan, İstemi Kağan tahta oturmuş; oturup Türk milletinin ilini, töresini tutmuş...
#  Türk milleti, Ötüken ormanında oturursan ebedî il tutarak oturacaksın."
METIN = (
    "𐰇𐰔𐰀 𐰚𐰇𐰜 𐱅𐰭𐰼𐰃 𐰀𐰽𐰺𐰀 𐰖𐰍𐰔 𐰘𐰼 𐰶𐰞𐰣𐱃𐰸𐰑𐰀 𐰚𐰃𐰤 𐰀𐰺𐰀 𐰚𐰃𐱁𐰃 𐰆𐰍𐰞𐰃 𐰶𐰞𐰣𐰢𐰿 "
    "𐰚𐰃𐱁𐰃 𐰆𐰍𐰞𐰣𐱃𐰀 𐰇𐰔𐰀 𐰀𐰲𐰇𐰢 𐰀𐰯𐰀𐰢 𐰉𐰆𐰢𐰃𐰣 𐰴𐰍𐰣 𐰃𐰾𐱅𐰢𐰃 𐰴𐰍𐰣 𐰆𐰞𐰺𐰢𐰿 "
    "𐰆𐰞𐰺𐰯𐰣 𐱅𐰇𐰼𐰜 𐰉𐰆𐰑𐰣 𐰃𐰠𐰃𐰤 𐱅𐰇𐰼𐰇𐰾𐰃𐰤 𐱃𐰆𐱃𐰀 𐰋𐰃𐰼𐰢𐰃𐱁 "
    "𐱅𐰇𐰼𐰜 𐰉𐰆𐰑𐰣 𐰇𐱅𐰜𐰤 𐰖𐰃𐰿 𐰆𐰞𐰺𐰽𐰺 𐰋𐰭𐰏𐰇 𐰃𐰠 𐱃𐰆𐱃𐰀 𐰆𐰞𐰺𐱃𐰱𐰃 𐰾𐰤"
).split()
SUTUN = 6
ISIK = (0.25, 0.92, 0.86)   # gök turkuazı


# ---------------------------------------------------------------- dokular

def gurultu(boyut, olcekler, tohum):
    """döşenebilir (kenarları birleşen) çok katmanlı gürültü, 0..1"""
    rng = np.random.default_rng(tohum)
    toplam = np.zeros((boyut, boyut))
    for olcek, agirlik in olcekler:
        k = max(2, boyut // olcek)
        kaba = rng.random((k, k))
        # periyodik büyütme: 3x3 döşe, büyüt, ortayı kes
        doseli = np.tile(kaba, (3, 3))
        img = Image.fromarray((doseli * 255).astype(np.uint8)).resize((boyut * 3, boyut * 3), Image.BICUBIC)
        orta = np.asarray(img, dtype=np.float64)[boyut:2 * boyut, boyut:2 * boyut] / 255.0
        toplam += orta * agirlik
    toplam -= toplam.min()
    return toplam / max(toplam.max(), 1e-6)


def tas_dokusu(boyut, tohum):
    g = gurultu(boyut, [(4, 0.45), (16, 0.3), (64, 0.18), (256, 0.07)], tohum)
    benek = gurultu(boyut, [(128, 0.6), (256, 0.4)], tohum + 7)
    renk = np.zeros((boyut, boyut, 3))
    koyu = np.array([0.28, 0.30, 0.31])
    acik = np.array([0.55, 0.56, 0.55])
    for i in range(3):
        renk[..., i] = koyu[i] + (acik[i] - koyu[i]) * g
    # yosun lekeleri (alçak frekanslı yeşilimsi)
    yosun = np.clip((gurultu(boyut, [(3, 0.7), (12, 0.3)], tohum + 3) - 0.62) * 3.0, 0, 1)
    renk = renk * (1 - 0.35 * yosun[..., None]) + np.array([0.30, 0.36, 0.22]) * 0.35 * yosun[..., None]
    renk += (benek[..., None] - 0.5) * 0.06
    return np.clip(renk, 0, 1)


def yazi_dokusu(yazi_tipi):
    """Gövdenin geniş yüzü: 0.95 x 2.73 m alan; 512x1024 dokuya (yatayda gerilmiş) çizilir."""
    en, boy = 380, 1092                   # gerçek oran (0.348)
    tas = tas_dokusu(1152, 11)[:boy, :en]
    oyuk = Image.new("L", (en, boy), 0)   # kazınmış yerler
    d = ImageDraw.Draw(oyuk)

    # çerçeve: kenardan 18 px içerde, üstte kemerli
    kemer_y = int(boy * (KEMER / (GOVDE_BOY + KEMER)))
    d.rectangle([18, kemer_y, en - 19, boy - 22], outline=255, width=5)
    d.arc([18, 18, en - 19, 2 * kemer_y - 18], 180, 360, fill=255, width=5)

    # kemerde güneş (kök) damgası
    cx, cy, r = en // 2, kemer_y - 12, 46
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=255, width=6)
    d.ellipse([cx - 12, cy - 12, cx + 12, cy + 12], fill=255)
    for k in range(8):
        a = k * math.pi / 4
        d.line([cx + math.cos(a) * (r + 8), cy + math.sin(a) * (r + 8),
                cx + math.cos(a) * (r + 26), cy + math.sin(a) * (r + 26)], fill=255, width=6)

    # yazı sütunları: Orhun anıtlarındaki gibi dikey satırlar, sağdan sola; metin sütunlara akar
    ust, alt = kemer_y + 80, boy - 48
    sutun_en = (en - 60) / SUTUN
    uzunluk = alt - ust
    olc = ImageDraw.Draw(Image.new("L", (8, 8)))

    def dagit(boyut):
        font = ImageFont.truetype(yazi_tipi, boyut)
        sutunlar, simdiki, kalan = [], [], uzunluk - 12
        for kelime in METIN:
            w = sum(olc.textlength(h, font=font) for h in kelime) + boyut * 0.55
            if w > kalan and simdiki:
                sutunlar.append(simdiki)
                simdiki, kalan = [], uzunluk - 12
            simdiki.append(kelime)
            kalan -= w
        sutunlar.append(simdiki)
        return font, sutunlar

    # metnin tamamı sığana kadar harfleri küçült
    for boyut in range(46, 20, -2):
        font, sutunlar = dagit(boyut)
        if len(sutunlar) <= SUTUN and boyut <= sutun_en * 0.75:
            break
    print("yazı boyu", boyut, "px,", len(sutunlar), "sütun")
    for s, kelimeler in enumerate(sutunlar[:SUTUN]):
        satir = Image.new("L", (uzunluk, int(sutun_en)), 0)
        sd = ImageDraw.Draw(satir)
        x = satir.width - 6
        for kelime in kelimeler:
            # sağdan sola: harfleri sağdan başlayarak tek tek koy (yazı tipi RTL yerleşimi yapmayabilir)
            for harf in kelime:
                x -= sd.textlength(harf, font=font)
                sd.text((x, satir.height / 2), harf, font=font, fill=255, anchor="lm")
            x -= boyut * 0.22
            n = boyut * 0.09
            for dy in (-boyut * 0.2, boyut * 0.2):   # iki noktalı kelime ayıracı
                sd.ellipse([x - n, satir.height / 2 + dy - n, x + n, satir.height / 2 + dy + n], fill=255)
            x -= boyut * 0.33
        # satırı dik çevir: yazının başı (sağ uç) üste gelsin
        dik = satir.rotate(90, expand=True)
        sx = int(en - 30 - (s + 1) * sutun_en)
        oyuk.paste(dik, (sx, ust), dik)
        if s > 0:
            d.line([sx + sutun_en, ust - 6, sx + sutun_en, alt + 6], fill=150, width=2)
    if len(sutunlar) > SUTUN:
        print("uyarı: metin taştı,", len(sutunlar), "sütun")

    oyuk = oyuk.filter(ImageFilter.GaussianBlur(1.0))
    o = np.asarray(oyuk, dtype=np.float64) / 255.0
    # kabartma etkisi: oyuğun alt-sağı gölge, üst-solu ışık
    golge = np.asarray(ImageChops.offset(oyuk, 2, 3), dtype=np.float64) / 255.0
    renk = tas * (1 - 0.65 * o[..., None]) - 0.10 * np.clip(golge - o, 0, 1)[..., None]
    renk += 0.08 * np.clip(o - golge, 0, 1)[..., None]
    renk = np.clip(renk, 0, 1)
    isik = np.clip(o * 1.15, 0, 1)

    def resim(dizi):
        img = Image.fromarray((dizi * 255).astype(np.uint8))
        return img.resize((512, 1024), Image.LANCZOS)

    albedo = resim(renk)
    emisyon = resim(np.stack([isik] * 3, axis=-1))
    return albedo, emisyon


# ---------------------------------------------------------------- model (Blender)

BLENDER = r'''
import bpy, bmesh, math, sys
GOVDE_EN, GOVDE_DERIN, GOVDE_BOY, KEMER, KAIDE_BOY = {olcu}
cikti = {cikti!r}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.unit_settings.system = 'METRIC'
bpy.context.scene.unit_settings.scale_length = 1.0

def malzeme(ad):
    m = bpy.data.materials.new(ad)
    return m

m_tas = malzeme("KokTas")
m_yazi = malzeme("KokTasYazi")

def kutu_uv(bm, olcek=1.0):
    uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax = max(range(3), key=lambda i: abs(n[i]))
        for l in f.loops:
            co = l.vert.co
            if ax == 0: u, v = co.y, co.z
            elif ax == 1: u, v = co.x, co.z
            else: u, v = co.x, co.y
            l[uv].uv = (u * olcek, v * olcek)

# --- yazılı gövde: kemerli profil, Y boyunca derinlik
bm = bmesh.new()
profil = [(-GOVDE_EN / 2, 0.0), (GOVDE_EN / 2, 0.0)]
for i in range(13):
    t = math.pi * i / 12
    profil.append((GOVDE_EN / 2 * math.cos(t), GOVDE_BOY + KEMER * math.sin(t)))
on = [bm.verts.new((x, -GOVDE_DERIN / 2, z + KAIDE_BOY)) for x, z in profil]
arka = [bm.verts.new((x, GOVDE_DERIN / 2, z + KAIDE_BOY)) for x, z in profil]
f_on = bm.faces.new(list(reversed(on)))
f_arka = bm.faces.new(arka)
n = len(profil)
yanlar = []
for i in range(n):
    j = (i + 1) % n
    yanlar.append(bm.faces.new([on[i], on[j], arka[j], arka[i]]))
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
kutu_uv(bm, 1.0)
uv = bm.loops.layers.uv.verify()
yuk = GOVDE_BOY + KEMER
for f in (f_on, f_arka):
    f.material_index = 1
    for l in f.loops:
        x, z = l.vert.co.x, l.vert.co.z - KAIDE_BOY
        u = (x + GOVDE_EN / 2) / GOVDE_EN
        if f is f_arka:
            u = 1.0 - u
        l[uv].uv = (u, z / yuk)
bmesh.ops.triangulate(bm, faces=[f_on, f_arka])
me = bpy.data.meshes.new("Govde")
bm.to_mesh(me)
bm.free()
govde = bpy.data.objects.new("Govde", me)
bpy.context.scene.collection.objects.link(govde)
me.materials.append(m_tas)
me.materials.append(m_yazi)
bev = govde.modifiers.new("pah", "BEVEL")
bev.width = 0.025
bev.segments = 2
bev.limit_method = 'ANGLE'

# --- kaplumbağa kaide
def kure(ad, konum, olcek, seg=20, hal=10, yarim=False):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=hal, radius=1.0)
    if yarim:
        alt = [v for v in bm.verts if v.co.z < -0.001]
        bmesh.ops.delete(bm, geom=alt, context='VERTS')
        kenar = [e for e in bm.edges if e.is_boundary]
        bmesh.ops.holes_fill(bm, edges=kenar)
    for v in bm.verts:
        v.co.x = v.co.x * olcek[0] + konum[0]
        v.co.y = v.co.y * olcek[1] + konum[1]
        v.co.z = v.co.z * olcek[2] + konum[2]
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    kutu_uv(bm, 0.8)
    me = bpy.data.meshes.new(ad)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(ad, me)
    bpy.context.scene.collection.objects.link(o)
    me.materials.append(m_tas)
    for p in me.polygons:
        p.use_smooth = True
    return o

kabuk = kure("Kabuk", (0, 0.05, 0.0), (1.05, 0.72, KAIDE_BOY + 0.08), yarim=True)
bas = kure("Bas", (0, -0.92, 0.22), (0.24, 0.30, 0.20), seg=14, hal=8)
ayaklar = [kure("Ayak%d" % i, (sx * 0.78, sy, 0.06), (0.22, 0.28, 0.12), seg=10, hal=6)
           for i, (sx, sy) in enumerate([(-1, -0.45), (1, -0.45), (-1, 0.55), (1, 0.55)])]

# hepsini tek nesnede birleştir (modifier'lar uygulanır)
bpy.context.view_layer.objects.active = govde
for o in [govde, kabuk, bas] + ayaklar:
    o.select_set(True)
bpy.ops.object.convert(target='MESH')
bpy.ops.object.join()
tas = bpy.context.view_layer.objects.active
tas.name = "KokTas"
tas.data.name = "KokTas"
for p in tas.data.polygons:
    if p.material_index == 1:
        p.use_smooth = False

bpy.ops.export_scene.fbx(filepath=cikti, use_selection=False, apply_unit_scale=True,
                         apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                         bake_space_transform=True, mesh_smooth_type='FACE', use_mesh_modifiers=True,
                         add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
print("ucgen", sum(len(p.vertices) - 2 for p in tas.data.polygons))
'''


def model_kur(cikti):
    betik = BLENDER.format(olcu=(GOVDE_EN, GOVDE_DERIN, GOVDE_BOY, KEMER, KAIDE_BOY), cikti=str(cikti))
    with tempfile.NamedTemporaryFile("w", suffix=".py", delete=False) as h:
        h.write(betik)
    sonuc = subprocess.run([sys.executable, h.name], capture_output=True, text=True)
    if sonuc.returncode != 0 or not Path(cikti).exists():
        print(sonuc.stdout[-3000:], sonuc.stderr[-3000:])
        raise SystemExit("model kurulamadı")
    for satir in sonuc.stdout.splitlines():
        if satir.startswith("ucgen"):
            print("Kök Taş:", satir)


def main():
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    yazi_tipi = sys.argv[1]
    fm.folder_meta(OUT.parent, K)
    fm.folder_meta(OUT, K)
    for alt in ("Doku", "Malzeme"):
        fm.folder_meta(OUT / alt, K)

    albedo, emisyon = yazi_dokusu(yazi_tipi)
    tas = Image.fromarray((tas_dokusu(512, 5) * 255).astype(np.uint8))
    dosyalar = {"KokTas_Yazi.png": albedo, "KokTas_YaziIsik.png": emisyon, "KokTas_Tas.png": tas}
    for ad, img in dosyalar.items():
        yol = OUT / "Doku" / ad
        img.save(yol, optimize=True)
        fm.texture_meta(yol, K.guid("doku:" + ad), 1024, srgb=True)

    yazi_m = fm.urp_material(OUT / "Malzeme/M_KokTasYazi.mat", "M_KokTasYazi", K.guid("malzeme:yazi"),
                             K.guid("doku:KokTas_Yazi.png"), smoothness=0.15,
                             emission=tuple(round(c * 2.2, 3) for c in ISIK),
                             emission_map=K.guid("doku:KokTas_YaziIsik.png"))
    tas_m = fm.urp_material(OUT / "Malzeme/M_KokTas.mat", "M_KokTas", K.guid("malzeme:tas"),
                            K.guid("doku:KokTas_Tas.png"), smoothness=0.1)

    fbx = OUT / "KokTas.fbx"
    model_kur(fbx)
    fm.fbx_meta(fbx, K.guid("model:KokTas"), remaps={"KokTas": tas_m, "KokTasYazi": yazi_m},
                animation_type=0, materials=True, bake_axis=True)
    (OUT / "LISANS.txt").write_text(
        "Kök Taş modeli ve dokuları bu proje için koddan üretildi (Tools~/dunya/koktas.py).\n"
        "Göktürk harfleri Noto Sans Old Turkic yazı tipiyle çizildi (SIL Open Font License 1.1).\n"
        "Yazı: Kül Tigin yazıtı, doğu yüzü, 1. satırın başı.\n", encoding="utf-8")
    fm.write_text(OUT / "LISANS.txt.meta",
                  f"fileFormatVersion: 2\nguid: {K.guid('lisans')}\nTextScriptImporter:\n  externalObjects: {{}}\n"
                  "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    print("Kök Taş yazıldı:", fm.rel(OUT))


def model_guid():
    return K.guid("model:KokTas")


if __name__ == "__main__":
    main()
