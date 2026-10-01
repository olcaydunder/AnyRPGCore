"""Unity NavMesh-Navigation.asset (ikili) okuyucu.

Her karo (tile) "VAND" ile baslar: 11 int baslik (magic, surum, x, z, katman, poligon sayisi, kose sayisi, ...),
bmin(3f), bmax(3f), 1 float, sonra kose dizisi (x, y, z float) ve 32 baytlik poligonlar
(6 x u16 kose indeksi, 6 x u16 komsu, u32 bayrak, u8 kose sayisi, u8 alan).
Burada yalnizca yurunebilir yuzeyin poligonlari ve baglanti bilesenleri gerekiyor.
"""
import collections
import math
import struct

TILE = 128.0 / 3.0  # 42.667 m


def load_polys(path):
    data = open(path, "rb").read()
    polys = []
    start = 0
    while True:
        i = data.find(b"VAND", start)
        if i < 0:
            break
        start = i + 4
        header = struct.unpack_from("<11i", data, i)
        poly_count, vert_count = header[5], header[6]
        if not (0 < poly_count < 100000 and 0 < vert_count < 100000):
            continue
        voff = i + 72
        verts = [struct.unpack_from("<3f", data, voff + 12 * k) for k in range(vert_count)]
        poff = voff + 12 * vert_count
        for k in range(poly_count):
            o = poff + 32 * k
            idx = struct.unpack_from("<6H", data, o)
            n = data[o + 28]
            if n < 3 or n > 6 or any(v >= vert_count for v in idx[:n]):
                continue
            polys.append([verts[v] for v in idx[:n]])
    return polys


def components(polys):
    """bir kosesini ya da bir karo sinirindaki kenarini paylasan poligonlar ayni bilesende"""
    parent = list(range(len(polys)))

    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a

    def union(a, b):
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[ra] = rb

    shared = collections.defaultdict(list)
    border = collections.defaultdict(list)
    for i, pts in enumerate(polys):
        for (x, y, z) in pts:
            shared[(round(x * 5), round(z * 5), round(y))].append(i)
        for a, b in zip(pts, pts[1:] + pts[:1]):
            for axis, other in ((0, 2), (2, 0)):
                if abs(a[axis] - b[axis]) < 0.05:
                    t = a[axis] / TILE
                    if abs(t - round(t)) < 0.002:
                        lo, hi = sorted((a[other], b[other]))
                        border[(axis, round(a[axis], 1))].append((lo, hi, (a[1] + b[1]) / 2, i))
    for lst in shared.values():
        for j in lst[1:]:
            union(lst[0], j)
    for segs in border.values():
        segs.sort()
        for x in range(len(segs)):
            for y in range(x + 1, len(segs)):
                if segs[y][0] > segs[x][1]:
                    break
                if abs(segs[x][2] - segs[y][2]) < 3 and min(segs[x][1], segs[y][1]) - max(segs[x][0], segs[y][0]) > 0.3:
                    union(segs[x][3], segs[y][3])
    return [find(i) for i in range(len(polys))]


class NavMesh:
    def __init__(self, path, reachable_from):
        self.polys = load_polys(path)
        comp = components(self.polys)
        start = self._containing(reachable_from[0], reachable_from[1], range(len(self.polys)))
        if start is None:
            raise ValueError("baslangic noktasi navmesh uzerinde degil")
        self.main = [i for i in range(len(self.polys)) if comp[i] == comp[start]]
        # kaba izgara: hizli arama
        self.grid = collections.defaultdict(list)
        for i in self.main:
            xs = [p[0] for p in self.polys[i]]
            zs = [p[2] for p in self.polys[i]]
            for gx in range(int(math.floor(min(xs) / 10)), int(math.floor(max(xs) / 10)) + 1):
                for gz in range(int(math.floor(min(zs) / 10)), int(math.floor(max(zs) / 10)) + 1):
                    self.grid[(gx, gz)].append(i)

    def _containing(self, x, z, candidates):
        for i in candidates:
            if _inside(self.polys[i], x, z):
                return i
        return None

    def height(self, x, z):
        """(x, z) ulasilabilir yuzeydeyse yuzey yuksekligi, degilse None"""
        i = self._containing(x, z, self.grid.get((int(math.floor(x / 10)), int(math.floor(z / 10))), []))
        if i is None:
            return None
        return _plane_height(self.polys[i], x, z)

    def slope_ok(self, x, z, radius=1.5, max_step=1.2):
        """etrafi da yurunebilir ve cok dik degil mi (birim sigsin, ucurum kenarinda dogmasin)"""
        h = self.height(x, z)
        if h is None:
            return False
        for k in range(8):
            a = k * math.pi / 4
            h2 = self.height(x + radius * math.cos(a), z + radius * math.sin(a))
            if h2 is None or abs(h2 - h) > max_step:
                return False
        return True


def _inside(pts, x, z):
    inside = False
    n = len(pts)
    for k in range(n):
        x1, _, z1 = pts[k]
        x2, _, z2 = pts[(k + 1) % n]
        if (z1 > z) != (z2 > z):
            xi = x1 + (z - z1) * (x2 - x1) / (z2 - z1)
            if x < xi:
                inside = not inside
    return inside


def _plane_height(pts, x, z):
    # ucgen yelpazesi ile ic icine dusen ucgenin duzleminden yukseklik
    p0 = pts[0]
    for k in range(1, len(pts) - 1):
        a, b, c = p0, pts[k], pts[k + 1]
        d = (b[2] - c[2]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[2] - c[2])
        if abs(d) < 1e-9:
            continue
        l1 = ((b[2] - c[2]) * (x - c[0]) + (c[0] - b[0]) * (z - c[2])) / d
        l2 = ((c[2] - a[2]) * (x - c[0]) + (a[0] - c[0]) * (z - c[2])) / d
        l3 = 1 - l1 - l2
        if min(l1, l2, l3) >= -1e-6:
            return l1 * a[1] + l2 * b[1] + l3 * c[1]
    return sum(p[1] for p in pts) / len(pts)
