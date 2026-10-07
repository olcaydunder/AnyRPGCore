#!/usr/bin/env python3
"""Ana menünün arka planı (Ötüken Destanı): şafakta Ötüken dağları, doğan Kün (güneş), Ay Dede, otağlar, Kök Taş ve
Asena (bozkurt). 2560x1280 (2:1) çizilir; ana menü görüntüyü ekranı kaplayacak biçimde ortalar (MobileSupport,
AspectRatioFitter EnvelopeParent): 20:9 telefonlarda üstten-alttan, 16:9'da yanlardan biraz kırpılır.

Yazar: Assets/AnyRPG/Core/Games/FeaturesDemoGame/Images/FeaturesDemoMainMenu.png (guid aynı kalır, sahne değişmez).
Kullanım (depo kökünden): python3 "Tools~/logo/ana_menu.py" [çıktı.png]
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

KOK = Path(__file__).resolve().parents[2]
CIKTI = KOK / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Images/FeaturesDemoMainMenu.png"
W, H = 2560, 1280
K = 2                       # üst örnekleme
SW, SH = W * K, H * K
rng = np.random.default_rng(1071)


def renk(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


def karis(a, b, t):
    return a + (b - a) * t


def dikey_degrade(duraklar, yuk=SH):
    """duraklar: [(0..1, '#hex')] -> (yuk, 3)"""
    y = np.linspace(0, 1, yuk)
    sonuc = np.zeros((yuk, 3), np.float32)
    for k in range(3):
        sonuc[:, k] = np.interp(y, [d[0] for d in duraklar], [renk(d[1])[k] for d in duraklar])
    return sonuc


def gurultu1d(n, olcekler, seed):
    r = np.random.default_rng(seed)
    x = np.arange(n)
    toplam = np.zeros(n, np.float32)
    for dalga, genlik in olcekler:
        noktalar = r.uniform(-1, 1, n // dalga + 3)
        xs = x / dalga
        i = np.floor(xs).astype(int)
        f = xs - i
        f = f * f * (3 - 2 * f)
        toplam += genlik * (noktalar[i] * (1 - f) + noktalar[i + 1] * f)
    return toplam


def sirt(taban, olcekler, seed, tepeler=()):
    """sütun başına sırt yüksekliği (piksel, üst örneklemeli). tepeler: (x 0..1, yükseklik px, genişlik 0..1)"""
    y = np.full(SW, taban * SH, np.float32) - gurultu1d(SW, [(d * K, g * K) for d, g in olcekler], seed)
    x = np.linspace(0, 1, SW)
    for tx, ty, tg in tepeler:
        y -= ty * K * np.exp(-((x - tx) / tg) ** 2)
    return y


def sirt_maskesi(y):
    satir = np.arange(SH, dtype=np.float32)[:, None]
    return np.clip(satir - y[None, :] + 0.5, 0, 1)  # yumuşak kenar


def bindir(tuval, maske, renkler):
    """renkler: (SH,3) dikey, (SH,SW,3) ya da (3,)"""
    m = maske[..., None]
    tuval[:] = tuval * (1 - m) + renkler * m


def bulanik(goruntu_f, yaricap):
    im = Image.fromarray(np.clip(goruntu_f, 0, 255).astype(np.uint8))
    return np.asarray(im.filter(ImageFilter.GaussianBlur(yaricap)), dtype=np.float32)


def maske_ciz(cizici, mod="L"):
    im = Image.new(mod, (SW, SH), 0)
    cizici(ImageDraw.Draw(im))
    return np.asarray(im, dtype=np.float32) / 255.0


def catmull(noktalar, adim=12, kapali=True):
    p = [np.array(n, np.float64) for n in noktalar]
    n = len(p)
    sonuc = []
    aralik = range(n) if kapali else range(n - 1)
    for i in aralik:
        p0, p1, p2, p3 = p[(i - 1) % n], p[i], p[(i + 1) % n], p[(i + 2) % n]
        for t in np.linspace(0, 1, adim, endpoint=False):
            t2, t3 = t * t, t * t * t
            sonuc.append(tuple(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                                      + (-p0 + 3 * p1 - 3 * p2 + p3) * t3)))
    return sonuc


def main(cikti):
    tuval = np.zeros((SH, SW, 3), np.float32)

    # ---- gök: gece laciverdinden şafak altınına
    gok = dikey_degrade([(0.0, "#120F2E"), (0.22, "#2A1D52"), (0.40, "#5B2E6E"), (0.52, "#B5506A"),
                         (0.60, "#EE8A4E"), (0.66, "#FBC46A"), (1.0, "#FBC46A")])
    tuval[:] = gok[:, None, :]
    gx, gy = 0.505 * SW, 0.555 * SH          # güneşin merkezi (dağın ardında)
    yy, xx = np.mgrid[0:SH, 0:SW].astype(np.float32)
    uzak = np.sqrt(((xx - gx) / (SW * 0.55)) ** 2 + ((yy - gy) / (SH * 0.62)) ** 2)
    parilti = np.clip(1 - uzak, 0, 1) ** 2.2
    tuval += parilti[..., None] * renk("#FFB45A") * 0.55

    # ---- yıldızlar (üst yarı, ufka doğru söner)
    yildiz = Image.new("L", (SW, SH), 0)
    d = ImageDraw.Draw(yildiz)
    for _ in range(520):
        x, y = rng.uniform(0, SW), rng.uniform(0, SH * 0.48) ** 1.0
        r = rng.choice([1.2, 1.6, 2.2, 3.0], p=[0.5, 0.3, 0.15, 0.05]) * K
        a = int(255 * rng.uniform(0.35, 1.0) * (1 - y / (SH * 0.5)) ** 1.5)
        d.ellipse([x - r, y - r, x + r, y + r], fill=a)
    for _ in range(14):    # parlak, haçlı yıldızlar
        x, y = rng.uniform(0.05, 0.95) * SW, rng.uniform(0.03, 0.3) * SH
        L = rng.uniform(10, 22) * K
        d.line([x - L, y, x + L, y], fill=200, width=K)
        d.line([x, y - L, x, y + L], fill=200, width=K)
        d.ellipse([x - 3 * K, y - 3 * K, x + 3 * K, y + 3 * K], fill=255)
    ym = np.asarray(yildiz.filter(ImageFilter.GaussianBlur(0.8 * K)), np.float32) / 255
    tuval = tuval * (1 - ym[..., None]) + renk("#FFF6E2") * ym[..., None]

    # ---- Ay Dede: sağ üstte hilal
    ax, ay, ar = 0.81 * SW, 0.17 * SH, 46 * K
    ay_m = maske_ciz(lambda c: c.ellipse([ax - ar, ay - ar, ax + ar, ay + ar], fill=255))
    golge = maske_ciz(lambda c: c.ellipse([ax - ar + 22 * K, ay - ar - 12 * K, ax + ar + 22 * K, ay + ar - 12 * K], fill=255))
    hilal = np.clip(ay_m - golge, 0, 1)
    hilal = bulanik(hilal[..., None].repeat(3, 2) * 255, 1.2 * K)[..., 0] / 255
    ay_isik = bulanik(hilal[..., None].repeat(3, 2) * 255, 40 * K)[..., 0] / 255
    tuval += ay_isik[..., None] * renk("#9FB8FF") * 0.6
    bindir(tuval, hilal, renk("#FFF3D6"))

    # ---- Kün (güneş): dağın ardından doğuyor, ışınlar
    isin = Image.new("L", (SW, SH), 0)
    d = ImageDraw.Draw(isin)
    for i in range(36):
        a = math.radians(i * 10 + 5)
        uzun = (1.0 if i % 2 == 0 else 0.62) * SH * 0.75
        gen = math.radians(1.6 if i % 2 == 0 else 1.0)
        p = [(gx, gy), (gx + uzun * math.cos(a - gen), gy - uzun * math.sin(a - gen)),
             (gx + uzun * math.cos(a + gen), gy - uzun * math.sin(a + gen))]
        d.polygon(p, fill=60 if i % 2 == 0 else 38)
    im = np.asarray(isin.filter(ImageFilter.GaussianBlur(10 * K)), np.float32) / 255
    sonum = np.clip(1 - np.sqrt(((xx - gx) / (SW * 0.5)) ** 2 + ((yy - gy) / (SH * 0.8)) ** 2), 0, 1)
    tuval += (im * sonum)[..., None] * renk("#FFE0A0") * 0.9
    gr = 120 * K
    gunes = maske_ciz(lambda c: c.ellipse([gx - gr, gy - gr, gx + gr, gy + gr], fill=255))
    tuval += bulanik(gunes[..., None].repeat(3, 2) * 255, 60 * K) / 255 * renk("#FFC870") * 0.9
    gunes_renk = np.zeros((SH, SW, 3), np.float32) + renk("#FFF1C4")
    gunes_renk += (np.clip((yy - (gy - gr)) / (2 * gr), 0, 1)[..., None]) * (renk("#FFC45C") - renk("#FFF1C4"))
    bindir(tuval, bulanik(gunes[..., None].repeat(3, 2) * 255, 1.5 * K)[..., 0] / 255, gunes_renk)

    # ---- bulut şeritleri
    for i, (by, kal, rk, op) in enumerate([(0.36, 10, "#E58B7C", 0.35), (0.43, 14, "#F2A06A", 0.45),
                                           (0.30, 8, "#B9688A", 0.30), (0.49, 12, "#FFC27A", 0.35)]):
        bm = Image.new("L", (SW, SH), 0)
        d = ImageDraw.Draw(bm)
        for _ in range(9):
            x = rng.uniform(-0.1, 1.0) * SW
            L = rng.uniform(0.12, 0.32) * SW
            y = (by + rng.uniform(-0.03, 0.03)) * SH
            t = rng.uniform(0.6, 1.4) * kal * K
            d.ellipse([x, y - t, x + L, y + t], fill=255)
        bmf = np.asarray(bm.filter(ImageFilter.GaussianBlur(9 * K)), np.float32) / 255 * op
        bindir(tuval, bmf, renk(rk))

    # ---- kartallar
    km = Image.new("L", (SW, SH), 0)
    d = ImageDraw.Draw(km)
    for (x, y, s) in [(0.33, 0.25, 1.0), (0.38, 0.21, 0.7), (0.62, 0.29, 0.8), (0.29, 0.31, 0.55)]:
        x, y, s = x * SW, y * SH, s * 34 * K
        kanat = catmull([(x - 2.2 * s, y + 0.2 * s), (x - 1.2 * s, y - 0.55 * s), (x - 0.2 * s, y - 0.05 * s), (x, y + 0.25 * s),
                         (x + 0.2 * s, y - 0.05 * s), (x + 1.2 * s, y - 0.55 * s), (x + 2.2 * s, y + 0.2 * s),
                         (x + 1.2 * s, y - 0.25 * s), (x + 0.15 * s, y + 0.35 * s), (x, y + 0.6 * s), (x - 0.15 * s, y + 0.35 * s),
                         (x - 1.2 * s, y - 0.25 * s)], 8)
        d.polygon(kanat, fill=255)
    bindir(tuval, np.asarray(km.filter(ImageFilter.GaussianBlur(0.8 * K)), np.float32) / 255, renk("#2A1A3A"))

    # ---- dağlar: arkadan öne (uzak olan sisli, açık; yakın olan koyu)
    katmanlar = [
        # taban, gürültü, tohum, tepeler, üst renk, alt renk, karlı mı
        (0.60, [(260, 60), (90, 26), (30, 8)], 11, [(0.435, 300, 0.045), (0.575, 250, 0.05), (0.26, 160, 0.07), (0.76, 190, 0.07), (0.92, 110, 0.05)],
         "#7A5A8E", "#B07A88", True),
        (0.66, [(300, 55), (110, 22), (35, 7)], 23, [(0.16, 150, 0.08), (0.64, 120, 0.09), (0.43, 70, 0.05)],
         "#5E4478", "#8A5F80", True),
        (0.73, [(340, 45), (120, 20), (40, 6)], 37, [(0.08, 120, 0.09), (0.88, 140, 0.08)], "#3B2B5C", "#56406E", False),
        (0.80, [(400, 34), (140, 14), (40, 5)], 41, [(0.95, 60, 0.07)], "#261C40", "#33264E", False),
    ]
    sx = np.linspace(0, 1, SW)
    for taban, olcek, seed, tepeler, ust, alt, karli in katmanlar:
        y = sirt(taban, olcek, seed, tepeler)
        m = sirt_maskesi(y)
        tepe = y.min()
        rr = dikey_degrade([(0, ust), (taban, ust), (1.0, alt)])
        renkler = np.broadcast_to(rr[:, None, :], (SH, SW, 3)).copy()
        # güneşe yakın yamaçlar ışık alır
        yakinlik = np.exp(-((sx - 0.5) / 0.22) ** 2)[None, :, None]
        renkler += yakinlik * renk("#FF9E6A") * 0.18
        # kaya dokusu: yamaçtan aşağı inen damarlar, sırta yakın belirgin
        satir = np.arange(SH, dtype=np.float32)[:, None]
        damar = gurultu1d(SW, [(14 * K, 1.0), (5 * K, 0.5), (40 * K, 0.5)], seed + 9)
        derinlik = np.clip(satir - y[None, :], 0, None)
        cekirdek = np.exp(-0.5 * (np.arange(-240, 241) / 80.0) ** 2)
        duz = np.convolve(np.pad(y, 240, mode="edge"), cekirdek / cekirdek.sum(), mode="valid")
        egim = np.clip(np.gradient(duz) * 0.9, -0.7, 0.7)       # yamaç yönü: damarlar aşağı doğru iner
        kayma = (np.arange(SW)[None, :] + derinlik * egim[None, :]).astype(np.int64) % SW
        doku = damar[kayma] * np.exp(-derinlik / (140 * K))
        renkler *= (1 + 0.06 * doku)[..., None]
        bindir(tuval, m, renkler)
        # sırt boyunca ışık çizgisi
        satir = np.arange(SH, dtype=np.float32)[:, None]
        cizgi = np.clip(1 - np.abs(satir - (y[None, :] + 2.5 * K)) / (2.5 * K), 0, 1) * m
        tuval += (cizgi * (0.25 + 0.75 * yakinlik[..., 0]))[..., None] * renk("#FFB070") * 0.55
        if karli:
            derin = (14 + 22 * (gurultu1d(SW, [(40 * K, 1), (12 * K, 0.4)], seed + 5) * 0.5 + 0.5)) * K
            yukseklik = np.clip((taban * SH - y) / (taban * SH - tepe + 1) - 0.45, 0, 1) * 1.8
            kar = np.clip(1 - (satir - y[None, :]) / (derin * yukseklik + 0.01)[None, :], 0, 1) * m
            kar = (kar > 0.08).astype(np.float32) * m
            kar_renk = renk("#F4E6F0") * 0.85 + yakinlik[..., 0][..., None] * renk("#FFD2A0") * 0.2
            bindir(tuval, kar * 0.85, kar_renk)
        # katmanlar arası pus
        pus = np.clip(1 - np.abs(satir - (taban * SH + 30 * K)) / (90 * K), 0, 1)
        tuval[:] = tuval * (1 - pus[..., None] * 0.18) + renk("#E9A48A") * pus[..., None] * 0.18

    # ---- otağlar (orta uzak, sağ) ve tüten dumanları
    otag = Image.new("L", (SW, SH), 0)
    kapi = Image.new("L", (SW, SH), 0)
    duman = Image.new("L", (SW, SH), 0)
    do, dk, dd = ImageDraw.Draw(otag), ImageDraw.Draw(kapi), ImageDraw.Draw(duman)
    zemin_y = 0.805 * SH
    for (x, s) in [(0.60, 1.0), (0.665, 0.8), (0.70, 0.62), (0.555, 0.7), (0.735, 0.5)]:
        x, s = x * SW, s * 52 * K
        yb = zemin_y + (1 - s / (52 * K)) * -10 * K
        duvar = [(x - s, yb), (x - s * 0.98, yb - 0.75 * s), (x + s * 0.98, yb - 0.75 * s), (x + s, yb)]
        cati = catmull([(x - s * 1.06, yb - 0.70 * s), (x - 0.78 * s, yb - 1.02 * s), (x - 0.4 * s, yb - 1.26 * s),
                        (x - 0.12 * s, yb - 1.34 * s), (x - 0.12 * s, yb - 1.45 * s), (x + 0.12 * s, yb - 1.45 * s),
                        (x + 0.12 * s, yb - 1.34 * s), (x + 0.4 * s, yb - 1.26 * s), (x + 0.78 * s, yb - 1.02 * s),
                        (x + s * 1.06, yb - 0.70 * s), (x, yb - 0.64 * s)], 6)
        do.polygon(duvar, fill=255)
        do.polygon(cati, fill=255)
        dk.rectangle([x - 0.22 * s, yb - 0.55 * s, x + 0.22 * s, yb], fill=255)
        for k in range(10):
            dy = k * 26 * K
            dx = math.sin(k * 0.7) * 10 * K + k * 6 * K
            rr = (6 + k * 2.2) * K
            dd.ellipse([x + dx - rr, yb - 1.45 * s - dy - rr, x + dx + rr, yb - 1.45 * s - dy + rr], fill=int(120 * (1 - k / 10)))
    dm = np.asarray(duman.filter(ImageFilter.GaussianBlur(8 * K)), np.float32) / 255 * 0.5
    bindir(tuval, dm, renk("#C9A0A8"))
    om = np.asarray(otag.filter(ImageFilter.GaussianBlur(0.7 * K)), np.float32) / 255
    bindir(tuval, om, renk("#1E1530"))
    kmk = np.asarray(kapi.filter(ImageFilter.GaussianBlur(0.7 * K)), np.float32) / 255
    tuval += bulanik(kmk[..., None].repeat(3, 2) * 255, 14 * K) / 255 * renk("#FFB050") * 0.7
    bindir(tuval, kmk, renk("#FFC870"))

    # ---- bozkır (ön plan): iki sırt, otlar
    for taban, olcek, seed, ust, alt in [(0.84, [(500, 22), (160, 9), (50, 3)], 51, "#1B1430", "#140F24"),
                                         (0.90, [(600, 26), (200, 10), (60, 3)], 57, "#120D20", "#0B0814")]:
        y = sirt(taban, olcek, seed)
        m = sirt_maskesi(y)
        bindir(tuval, m, dikey_degrade([(0, ust), (taban, ust), (1, alt)])[:, None, :])
        satir = np.arange(SH, dtype=np.float32)[:, None]
        cizgi = np.clip(1 - np.abs(satir - (y[None, :] + 2 * K)) / (2 * K), 0, 1) * m
        tuval += cizgi[..., None] * renk("#E08A5A") * 0.35
        ot = Image.new("L", (SW, SH), 0)
        d = ImageDraw.Draw(ot)
        for _ in range(1400):
            x = rng.uniform(0, SW)
            yb = y[int(min(SW - 1, x))] + 4 * K
            h = rng.uniform(10, 34) * K
            e = rng.uniform(-10, 10) * K
            d.polygon([(x - 1.6 * K, yb), (x + e, yb - h), (x + 1.6 * K, yb)], fill=255)
        bindir(tuval, np.asarray(ot.filter(ImageFilter.GaussianBlur(0.6 * K)), np.float32) / 255, renk(ust))

    # ---- Kök Taş (sol ön): kaplumbağa kaideli yazıt, turkuaz parlayan Göktürk harfleri
    tx, tyb = 0.135 * SW, 0.93 * SH
    tg, th = 118 * K, 560 * K
    tas = maske_ciz(lambda c: c.polygon(catmull([(tx - tg, tyb - 40 * K), (tx - tg * 1.02, tyb - th * 0.82), (tx - tg * 0.75, tyb - th * 0.97),
                                                  (tx, tyb - th), (tx + tg * 0.75, tyb - th * 0.97), (tx + tg * 1.02, tyb - th * 0.82),
                                                  (tx + tg, tyb - 40 * K), (tx, tyb - 30 * K)], 10), fill=255))
    kaide = maske_ciz(lambda c: (c.ellipse([tx - tg * 1.9, tyb - 70 * K, tx + tg * 1.9, tyb + 40 * K], fill=255),
                                 c.ellipse([tx + tg * 1.6, tyb - 66 * K, tx + tg * 2.35, tyb - 6 * K], fill=255)))
    tas_renk = np.zeros((SH, SW, 3), np.float32) + renk("#2B2240")
    tas_renk += np.clip((xx - (tx - tg)) / (2 * tg), 0, 1)[..., None] * (renk("#4A3A5C") - renk("#2B2240"))
    bindir(tuval, np.clip(kaide, 0, 1), renk("#1A1428"))
    bindir(tuval, tas, tas_renk)
    kenar = np.clip(tas - bulanik(tas[..., None].repeat(3, 2) * 255, 3 * K)[..., 0] / 255, 0, 1)
    tuval += kenar[..., None] * renk("#FFB070") * 0.8
    yazi = Image.new("L", (SW, SH), 0)
    d = ImageDraw.Draw(yazi)
    for sutun in range(3):
        cx = tx + (sutun - 1) * tg * 0.55
        y = tyb - th * 0.86
        while y < tyb - 90 * K:
            s = rng.uniform(16, 24) * K
            tur = rng.integers(0, 7)
            w = 4 * K
            if tur == 0:
                d.line([(cx, y), (cx, y + s)], fill=255, width=w)
                d.line([(cx, y + s * 0.4), (cx + s * 0.45, y)], fill=255, width=w)
            elif tur == 1:
                d.line([(cx - s * 0.35, y), (cx, y + s * 0.5), (cx + s * 0.35, y)], fill=255, width=w)
                d.line([(cx, y + s * 0.5), (cx, y + s)], fill=255, width=w)
            elif tur == 2:
                d.line([(cx - s * 0.3, y), (cx + s * 0.3, y + s * 0.5), (cx - s * 0.3, y + s)], fill=255, width=w)
            elif tur == 3:
                d.ellipse([cx - s * 0.3, y + s * 0.2, cx + s * 0.3, y + s * 0.8], outline=255, width=w)
            elif tur == 4:
                d.line([(cx, y), (cx, y + s)], fill=255, width=w)
                d.line([(cx - s * 0.35, y + s * 0.3), (cx + s * 0.35, y + s * 0.3)], fill=255, width=w)
            elif tur == 5:
                d.line([(cx - s * 0.3, y + s), (cx, y), (cx + s * 0.3, y + s)], fill=255, width=w)
            else:
                d.line([(cx - s * 0.3, y), (cx + s * 0.3, y)], fill=255, width=w)
                d.line([(cx + s * 0.3, y), (cx - s * 0.3, y + s)], fill=255, width=w)
                d.line([(cx - s * 0.3, y + s), (cx + s * 0.3, y + s)], fill=255, width=w)
            y += s + 12 * K
    ya = np.asarray(yazi, np.float32) / 255 * tas
    tuval += bulanik(ya[..., None].repeat(3, 2) * 255, 12 * K) / 255 * renk("#40E0D0") * 0.9
    bindir(tuval, bulanik(ya[..., None].repeat(3, 2) * 255, 0.8 * K)[..., 0] / 255, renk("#B8FFF4"))

    # ---- Asena (sağ ön): kayanın üstünde aya uluyan bozkurt
    kx, ky = 0.865 * SW, 0.885 * SH
    kaya = maske_ciz(lambda c: c.polygon(catmull([(kx - 230 * K, SH), (kx - 200 * K, ky - 20 * K), (kx - 120 * K, ky - 70 * K),
                                                  (kx - 10 * K, ky - 92 * K), (kx + 120 * K, ky - 80 * K), (kx + 230 * K, ky - 30 * K),
                                                  (kx + 300 * K, SH)], 10), fill=255))
    bindir(tuval, kaya, renk("#0E0A18"))
    s = 3.1 * K
    ox, oy = kx - 140 * K, ky - 92 * K - 132 * s     # kurdun yerel (0,0)'ı
    kurt = [(3, -2), (11, 1), (19, 5), (27, 9), (32, 8), (35, 7), (39, -2), (43, 4), (45, 9), (49, 13),
            (52, 18), (55, 22), (54, 25), (58, 28), (57, 32), (61, 35), (60, 39), (65, 45), (72, 55), (82, 66),
            (91, 78), (98, 91), (102, 104), (102, 116), (99, 123), (110, 124), (122, 126), (132, 131), (134, 136),
            (122, 138), (106, 138), (90, 137), (72, 137), (71, 133), (77, 129), (72, 121), (64, 118), (57, 122),
            (56, 137), (36, 137), (37, 132), (41, 128), (41, 108), (39, 93), (34, 85), (35, 80), (30, 74), (32, 70),
            (27, 64), (29, 60), (24, 50), (20, 38), (15, 28), (10, 20), (6, 14), (15, 11), (5, 4)]
    kurt_p = catmull([(ox + x * s, oy + y * s) for x, y in kurt], 6)
    km = maske_ciz(lambda c: c.polygon(kurt_p, fill=255))
    km = bulanik(km[..., None].repeat(3, 2) * 255, 0.7 * K)[..., 0] / 255
    bindir(tuval, km, renk("#0E0A18"))
    kk = np.clip(km - bulanik(km[..., None].repeat(3, 2) * 255, 3 * K)[..., 0] / 255, 0, 1)
    tuval += kk[..., None] * renk("#9FB8FF") * 0.9          # ay ışığı kenarı

    # ---- alt karartma (menü düğmeleri okunsun), kenar kararması
    alt = np.clip((yy / SH - 0.70) / 0.30, 0, 1) ** 1.4
    tuval *= (1 - alt * 0.45)[..., None]
    v = np.sqrt(((xx - SW / 2) / (SW * 0.62)) ** 2 + ((yy - SH * 0.48) / (SH * 0.78)) ** 2)
    tuval *= np.clip(1.15 - v * 0.55, 0.55, 1)[..., None]

    # ---- parıltı (bloom) ve ince gren
    parlak = np.clip(tuval - 200, 0, 255)
    tuval += bulanik(parlak, 18 * K) * 0.35
    im = Image.fromarray(np.clip(tuval, 0, 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
    a = np.asarray(im, np.float32)
    a += rng.normal(0, 2.2, a.shape[:2])[..., None]
    Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).save(cikti, optimize=True)
    print("yazıldı", cikti, W, H)


if __name__ == "__main__":
    main(Path(sys.argv[1]) if len(sys.argv) > 1 else CIKTI)
