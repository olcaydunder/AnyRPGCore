"""Chop Chop'tan çevrilen haritalara düşman kampları ve sandıklar yerleştirir (haritalar.py çağırır).

Konumlar derlemede pişen yürüme ağının yerel kopyasından seçilir (Tools~/varliklar/yurume_aglari/<harita>.asset;
her derlemenin tani.zip'inden güncellenir):
 - yalnız giriş noktasından yürüyerek ulaşılan, düz ve çevresi de yürünebilir yerler,
 - girişten, Geçit Taşı'ndan ve kapılardan uzak,
 - kamplar birbirinden olabildiğince uzak (en uzak nokta örneklemesi),
 - her kampta 2-3 düşman; bazı kampların ortasında korunan bir sandık.
Aynı yürüme ağı ve aynı ayarla hep aynı sonucu verir (rastgelelik haritanın adıyla tohumlanır).
"""
import collections
import json
import math
import random
from pathlib import Path

import navmesh as N
from navmesh import NavMesh

# harita: (düşman listesi, ek seviye, kamp sayısı)
KADRO = {
    "UmayTarlalari": (["Cali Cini", "Yagmaci Okcu", "Cali Cini", "Kara Yek"], 0, 7),
    "BoruTepesi": (["Cali Cini", "Agulu Kormos", "Yagmaci Okcu", "Kara Yek"], 0, 7),
    "AkDenizKiyisi": (["Yagmaci Okcu", "Sulmus", "Yagmaci Basi", "Yagmaci Okcu"], 1, 6),
    "OrdubalikCarsisi": (["Sulmus", "Kara Otaci", "Yagmaci Basi", "Sulmus"], 1, 6),
    "OrdubalikKenti": (["Sulmus", "Kara Kam", "Abasi", "Kara Otaci"], 1, 6),
    "UlukayinOrmani": (["Sulmus", "Agulu Kormos", "Kara Otaci", "Cali Cini"], 2, 6),
    "KaganOrdasi": (["Abasi", "Kara Kam", "Kan Suvarisi", "Kara Otaci"], 2, 7),
    "KafDagiYolu": (["Kizil Cin", "Kormos", "Kizil Cin", "Abasi"], 2, 6),
    "ErgenekonMagarasi": (["Kormos", "Kizil Cin", "Kan Suvarisi", "Kormos"], 3, 5),
    "AyDedeKoyu": (["Kemik Er", "Kemik Akinci", "Kemik Kam", "Kemik Alp"], 3, 7),
}


def _alan(p):
    a = 0.0
    for i in range(1, len(p) - 1):
        (x0, _, z0), (x1, _, z1), (x2, _, z2) = p[0], p[i], p[i + 1]
        a += abs((x1 - x0) * (z2 - z0) - (x2 - x0) * (z1 - z0)) / 2
    return a


def yukseklik(nm, x, z, yakin=None):
    """(x, z)'deki yürünebilir katmanlardan yakin'a en yakını (köprü altı, tünel gibi çok katlı yerler için)"""
    hs = [N._plane_height(nm.polys[i], x, z) for i in nm.grid.get((int(math.floor(x / 10)), int(math.floor(z / 10))), [])
          if N._inside(nm.polys[i], x, z)]
    if not hs:
        return None
    if yakin is None:
        return max(hs)
    return min(hs, key=lambda h: abs(h - yakin))


def duz(nm, x, z, h, radius=1.5, max_step=0.7):
    """h yüksekliğindeki noktanın çevresi de aynı katmanda yürünebilir ve dik değil mi"""
    for k in range(8):
        a = k * math.pi / 4
        h2 = yukseklik(nm, x + radius * math.cos(a), z + radius * math.sin(a), h)
        if h2 is None or abs(h2 - h) > max_step:
            return False
    return True


def adaylar(nm, rng, adet=4000):
    polys = [nm.polys[i] for i in nm.main]
    alanlar = [_alan(p) for p in polys]
    toplam = sum(alanlar)
    sonuc = []
    for p, a in zip(polys, alanlar):
        n = int(adet * a / toplam + rng.random())
        for _ in range(n):
            # çokgenin içinde rastgele nokta: yelpaze üçgenlerinden biri
            k = rng.randrange(1, len(p) - 1)
            r1, r2 = rng.random(), rng.random()
            if r1 + r2 > 1:
                r1, r2 = 1 - r1, 1 - r2
            x = p[0][0] + r1 * (p[k][0] - p[0][0]) + r2 * (p[k + 1][0] - p[0][0])
            y = p[0][1] + r1 * (p[k][1] - p[0][1]) + r2 * (p[k + 1][1] - p[0][1])
            z = p[0][2] + r1 * (p[k][2] - p[0][2]) + r2 * (p[k + 1][2] - p[0][2])
            sonuc.append((x, y, z))
    return sonuc, toplam


class YurumeAgi:
    """bir haritanın yürüme ağı: bileşenler ve alanları; noktayı en yakın büyük bileşene oturtur"""

    def __init__(self, yol):
        self.polys = N.load_polys(str(yol))
        self.comp = N.components(self.polys)
        self.alan = collections.Counter()
        for i, p in enumerate(self.polys):
            self.alan[self.comp[i]] += _alan(p)
        self.merkez = [(sum(v[0] for v in p) / len(p), sum(v[1] for v in p) / len(p), sum(v[2] for v in p) / len(p))
                       for p in self.polys]
        self._nm = {}

    def bilesen(self, c):
        """bileşen c için navmesh.NavMesh (height, slope_ok)"""
        if c not in self._nm:
            nm = object.__new__(NavMesh)
            nm.polys = self.polys
            nm.main = [i for i in range(len(self.polys)) if self.comp[i] == c]
            nm.grid = collections.defaultdict(list)
            for i in nm.main:
                xs = [v[0] for v in self.polys[i]]
                zs = [v[2] for v in self.polys[i]]
                for gx in range(int(math.floor(min(xs) / 10)), int(math.floor(max(xs) / 10)) + 1):
                    for gz in range(int(math.floor(min(zs) / 10)), int(math.floor(max(zs) / 10)) + 1):
                        nm.grid[(gx, gz)].append(i)
            nm.alan = self.alan[c]
            self._nm[c] = nm
        return self._nm[c]

    def birlesik(self, en_az_alan=300.0):
        """en_az_alan'dan büyük bütün bileşenler tek NavMesh gibi (bileşen hesabı Unity'ninkinden parçalı
        olabiliyor: Unity'nin derleme raporu ulaşılabilirliği ayrıca denetler)"""
        anahtar = ("birlesik", en_az_alan)
        if anahtar not in self._nm:
            nm = object.__new__(NavMesh)
            nm.polys = self.polys
            nm.main = [i for i in range(len(self.polys)) if self.alan[self.comp[i]] >= en_az_alan]
            nm.grid = collections.defaultdict(list)
            for i in nm.main:
                xs = [v[0] for v in self.polys[i]]
                zs = [v[2] for v in self.polys[i]]
                for gx in range(int(math.floor(min(xs) / 10)), int(math.floor(max(xs) / 10)) + 1):
                    for gz in range(int(math.floor(min(zs) / 10)), int(math.floor(max(zs) / 10)) + 1):
                        nm.grid[(gx, gz)].append(i)
            nm.alan = sum(self.alan[c] for c in self.alan if self.alan[c] >= en_az_alan)
            self._nm[anahtar] = nm
        return self._nm[anahtar]

    def oturt(self, p, en_az_alan=2500.0):
        """p'ye en yakın, yeterince büyük bileşende düz bir nokta: ((x, y, z), NavMesh)"""
        en_buyuk = max(self.alan.values())
        sinir = min(en_az_alan, en_buyuk * 0.6)
        x0, y0, z0 = p
        bilesenler = [c for c in self.alan if self.alan[c] >= sinir]
        # 1. girişin hemen altında (ya da birkaç metre yakınında) yürünebilir zemin varsa orası
        for r in (0.0, 1.5, 3.0, 4.5):
            for k in range(1 if r == 0 else 12):
                a = 2 * math.pi * k / 12
                x, z = x0 + r * math.cos(a), z0 + r * math.sin(a)
                for c in bilesenler:
                    nm = self.bilesen(c)
                    h = yukseklik(nm, x, z, y0 - 1.0)
                    if h is not None and y0 - 3.5 <= h <= y0 + 1.0 and duz(nm, x, z, h, 1.0, 0.6):
                        return (x, h, z), nm

        def puan(i):
            # yatay uzaklık + yükseklik farkı; girişin üstündeki yerler pahalı (Chop Chop'ta oyuncu
            # bazen yukarıdan düşerek girer: ör. Ergenekon'un bacası), altındakiler ucuz
            dy = self.merkez[i][1] - y0
            return math.hypot(self.merkez[i][0] - x0, self.merkez[i][2] - z0) + (3.0 * dy if dy > 0 else -0.5 * dy)
        sirali = sorted((i for i in range(len(self.polys)) if self.alan[self.comp[i]] >= sinir), key=puan)
        for i in sirali[:400]:
            nm = self.bilesen(self.comp[i])
            mx, _, mz = self.merkez[i]
            # önce p'nin kendisi, sonra p'den çokgenin ortasına doğru noktalar
            for t in (0.0, 0.3, 0.6, 1.0):
                x, z = x0 + (mx - x0) * t, z0 + (mz - z0) * t
                h = yukseklik(nm, x, z, self.merkez[i][1])
                if h is not None and h - y0 < 6 and duz(nm, x, z, h, 1.2, 0.6):
                    return (x, h, z), nm
        i = sirali[0]
        nm = self.bilesen(self.comp[i])
        return self.merkez[i], nm


def ulasilan_noktalar(dosya):
    """derleme raporundaki (tani/ulasilabilir) girişten yürüyerek ulaşılan noktalar, yoksa None"""
    yol = Path(__file__).resolve().parents[1] / "varliklar/ulasilabilir" / f"{dosya}.json"
    if not yol.exists():
        return None
    return [tuple(p) for p in json.loads(yol.read_text(encoding="utf-8"))["noktalar"]]


def kamplari_sec(dosya, nm, giris, yasaklar, bolge, gy=0.0):
    """nm: girişin bileşeni (NavMesh); giris: (x, z); yasaklar: [(x, z, yarıçap)];
    bolge: (x, z, yarıçap) haritanın tasarlanmış oyun alanı -> kamplar, sandıklar, rapor, ek seviye"""
    rng = random.Random("kamp:" + dosya)
    noktalar, alan = adaylar(nm, rng)
    ulasilan = ulasilan_noktalar(dosya)
    if ulasilan:
        # Unity'nin doğruladığı noktalar: girişten tam yolu olanlar (3 m ızgara)
        noktalar, alan = ulasilan, len(ulasilan) * 9.0
    liste, ek, kamp_sayisi = KADRO[dosya]
    bx, bz, br = bolge
    uygun = []
    for x, y, z in noktalar:
        if math.hypot(x - bx, z - bz) > br:
            continue
        # girişle aynı kat: uçurumun dibindeki ya da tepedeki ayrı bölgeler değil
        if abs(y - gy) > 16:
            continue
        if any(math.hypot(x - a, z - b) < r for a, b, r in yasaklar):
            continue
        if not duz(nm, x, z, y, 2.0, 0.7):
            continue
        uygun.append((x, y, z))
    oyun_alani = alan * len(uygun) / max(1, len(noktalar))
    kamp_sayisi = max(3, min(kamp_sayisi, int(oyun_alani / 900) + 2))
    secilen = []
    if not uygun:
        return [], [], f"uygun yer yok (alan {alan:.0f} m²)", ek
    # en uzak nokta örneklemesi: kamplar birbirinden ve girişten olabildiğince uzak
    referans = [(giris[0], 0.0, giris[1])]
    for _ in range(kamp_sayisi):
        en_iyi, en_iyi_d = None, -1
        for x, y, z in uygun:
            d = min(math.hypot(x - a, z - b) for a, _, b in referans + secilen)
            if d > en_iyi_d:
                en_iyi, en_iyi_d = (x, y, z), d
        if en_iyi is None or en_iyi_d < 14:
            break
        secilen.append(en_iyi)
    kamplar = []
    for k, (cx, cy, cz) in enumerate(secilen):
        uyeler = []
        adet = 2 + (k % 2)
        for j in range(adet):
            for deneme in range(12):
                a = 2 * math.pi * (j / adet) + 0.5 * deneme + k
                r = 3.2 + 0.4 * deneme
                x, z = cx + r * math.cos(a), cz + r * math.sin(a)
                h = yukseklik(nm, x, z, cy)
                if h is not None and abs(h - cy) < 2.0 and duz(nm, x, z, h, 1.0, 0.8):
                    uyeler.append((liste[(k + j) % len(liste)], x, h, z))
                    break
        kamplar.append(((cx, cy, cz), uyeler))
    # sandıklar: her iki kamptan birinin ortasında
    sandiklar = [(("BuyukHazineSandigi" if i == 0 else "HazineSandigi"), m) for i, (m, _) in enumerate(kamplar[1::2][:3])]
    rapor = (f"oyun alanı ~{oyun_alani:.0f} m², {len(kamplar)} kamp, "
             f"{sum(len(u) for _, u in kamplar)} düşman, {len(sandiklar)} sandık")
    return kamplar, sandiklar, rapor, ek


def tas_yeri(nm, giris, ileri, gy=None):
    """Geçit Taşı için girişin önünde/yanında düz bir yer: (x, y, z) ya da None"""
    gx, gz = giris
    en_iyi = None
    # önce girişle aynı katta yakın bir yer; yoksa (giriş dar bir rampa ya da çıkıntıdaysa) biraz ötede
    for tolerans, yaricaplar in ((2.0, (5.0, 6.0, 4.5, 7.0, 8.0, 9.0)), (6.0, (8.0, 10.0, 12.0, 14.0))):
        for r in yaricaplar:
            for k in range(16):
                a = math.atan2(ileri[0], ileri[1]) + math.radians(35 + 22.5 * k)
                x, z = gx + r * math.sin(a), gz + r * math.cos(a)
                h = yukseklik(nm, x, z, gy)
                if h is None or (gy is not None and abs(h - gy) > tolerans):
                    continue
                if duz(nm, x, z, h, 1.8, 0.35):
                    return (x, h, z)
                if en_iyi is None and duz(nm, x, z, h, 1.4, 0.6):
                    en_iyi = (x, h, z)
        if en_iyi is not None:
            return en_iyi
    return en_iyi
