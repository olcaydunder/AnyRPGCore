"""Unity sahne/prefab YAML'ı üzerinde yapısal işlemler: sahiplik ağacı, alt ağaç ve bileşen silme,
kök nesneler, dünya konumu. uyaml.py'nin satır tabanlı ayrıştırıcısını kullanır."""
import math
import re

import uyaml

SAHNE_KOKLERI = 1660057539


def qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw * bx + ax * bw + ay * bz - az * by,
            aw * by - ax * bz + ay * bw + az * bx,
            aw * bz + ax * by - ay * bx + az * bw,
            aw * bw - ax * bx - ay * by - az * bz)


def qrot(q, v):
    x, y, z = v
    p = qmul(qmul(q, (x, y, z, 0.0)), (-q[0], -q[1], -q[2], q[3]))
    return p[:3]


def yaw_q(derece):
    r = math.radians(derece) / 2
    return (0.0, math.sin(r), 0.0, math.cos(r))


def q_yaw(q):
    """Y ekseni etrafındaki dönüş (derece): ileri vektörünün yatay açısı"""
    fx, _, fz = qrot(q, (0.0, 0.0, 1.0))
    return math.degrees(math.atan2(fx, fz))


class TRS:
    __slots__ = ("p", "q", "s")

    def __init__(self, p=(0.0, 0.0, 0.0), q=(0.0, 0.0, 0.0, 1.0), s=(1.0, 1.0, 1.0)):
        self.p, self.q, self.s = tuple(p), tuple(q), tuple(s)

    def __mul__(self, yerel):
        """üst * yerel"""
        sp = tuple(yerel.p[i] * self.s[i] for i in range(3))
        r = qrot(self.q, sp)
        return TRS(tuple(self.p[i] + r[i] for i in range(3)), qmul(self.q, yerel.q),
                   tuple(self.s[i] * yerel.s[i] for i in range(3)))

    def ileri(self):
        return qrot(self.q, (0.0, 0.0, 1.0))

    def __repr__(self):
        return f"TRS(p={tuple(round(v, 3) for v in self.p)}, q={tuple(round(v, 4) for v in self.q)}, s={self.s})"


def _vektor(metin, anahtar, varsayilan):
    m = re.search(r"^\s*" + anahtar + r": \{([^}]*)\}", metin, re.M)
    if not m:
        return varsayilan
    d = dict((k.strip(), float(v)) for k, v in (p.split(":") for p in m.group(1).split(",")))
    eksen = "xyzw"[:len(varsayilan)]
    return tuple(d.get(e, varsayilan[i]) for i, e in enumerate(eksen))


def yerel_trs(belge):
    t = belge.metin
    return TRS(_vektor(t, "m_LocalPosition", (0.0, 0.0, 0.0)),
               _vektor(t, "m_LocalRotation", (0.0, 0.0, 0.0, 1.0)),
               _vektor(t, "m_LocalScale", (1.0, 1.0, 1.0)))


def degisiklikler(pi):
    """PrefabInstance belgesinin m_Modifications listesi: [(hedef fileID, hedef guid, yol, değer, nesne guid)]"""
    sonuc = []
    for m in re.finditer(
            r"    - target: \{fileID: (-?\d+), guid: (\w+),\s+type: \d+\}\n"
            r"      propertyPath: (.*)\n"
            r"      value: ?(.*)\n"
            r"      objectReference: \{fileID: (-?\d+)(?:, guid: (\w+))?", pi.metin):
        sonuc.append((m.group(1), m.group(2), m.group(3).strip(), m.group(4).strip(), m.group(6)))
    return sonuc


class Sahne:

    def __init__(self, metin):
        self.on, self.belgeler = uyaml.ayir(metin)
        self.indeksle()

    def metin(self):
        return uyaml.birlestir(self.on, self.belgeler)

    def indeksle(self):
        self.by = {b.fid: b for b in self.belgeler}
        self.go_tr = {}
        for b in self.belgeler:
            if b.cls in (4, 224) and not b.stripped:
                g = b.ref("m_GameObject")
                if g:
                    self.go_tr[g] = b.fid

    def sahip(self, b):
        """b'nin bağlı olduğu belge: o silinirse b de silinir"""
        if b.stripped:
            return b.ref("m_PrefabInstance")
        if b.cls == 1001:
            p = b.ref("m_TransformParent")
            return None if p in (None, "0") else p
        if b.cls in (4, 224):
            f = b.ref("m_Father")
            return None if f in (None, "0") else f
        if b.cls == 1:
            return self.go_tr.get(b.fid)
        g = b.ref("m_GameObject")
        if g and g != "0":
            return g
        return None

    def ad(self, b):
        """nesnenin adı (GameObject, Transform veya PrefabInstance belgesi için)"""
        if b.cls == 1001:
            for hedef, _, yol, deger, _ in degisiklikler(b):
                if yol == "m_Name":
                    return deger
            return None
        if b.cls in (4, 224):
            g = self.by.get(b.ref("m_GameObject"))
            return g.alan("m_Name") if g and not g.stripped else None
        if b.cls == 1:
            return b.alan("m_Name")
        return None

    def kokler(self):
        """[(ad, belge)] sahnenin kök nesneleri: üstü olmayan Transform'lar ve PrefabInstance'lar"""
        sonuc = []
        for b in self.belgeler:
            if b.cls in (4, 224) and not b.stripped and b.ref("m_Father") in (None, "0"):
                sonuc.append((self.ad(b), b))
            elif b.cls == 1001 and b.ref("m_TransformParent") in (None, "0"):
                sonuc.append((self.ad(b), b))
        return sonuc

    def sil(self, kokler):
        """verilen belgeleri ve onlara bağlı her şeyi (alt nesneler, bileşenler, iç prefab'lar) siler"""
        silinen = set(kokler)
        degisti = True
        while degisti:
            degisti = False
            for b in self.belgeler:
                if b.fid in silinen:
                    continue
                s = self.sahip(b)
                if s is not None and s in silinen:
                    silinen.add(b.fid)
                    degisti = True
        self.belgeler = [b for b in self.belgeler if b.fid not in silinen]
        for b in self.belgeler:
            if (b.cls in (4, 224) and not b.stripped) or b.cls == SAHNE_KOKLERI:
                b.metin = re.sub(r"^  - \{fileID: (-?\d+)\}\n",
                                 lambda m: "" if m.group(1) in silinen else m.group(0), b.metin, flags=re.M)
        self.indeksle()
        return silinen

    def bilesen_sil(self, fids):
        fids = set(fids)
        for b in self.belgeler:
            if b.cls == 1 and not b.stripped:
                b.metin = re.sub(r"^  - component: \{fileID: (-?\d+)\}\n",
                                 lambda m: "" if m.group(1) in fids else m.group(0), b.metin, flags=re.M)
        self.belgeler = [b for b in self.belgeler if b.fid not in fids]
        self.indeksle()

    def kullanilmayan_gomulu_sil(self, siniflar=(43,)):
        """hiçbir belgenin başvurmadığı gömülü varlıkları (ör. ProBuilder Mesh'leri) siler"""
        tum = "".join(b.metin for b in self.belgeler)
        refs = set(re.findall(r"\{fileID: (-?\d+)\}", tum))
        sil = [b.fid for b in self.belgeler if b.cls in siniflar and b.fid not in refs]
        self.belgeler = [b for b in self.belgeler if b.fid not in set(sil)]
        self.indeksle()
        return len(sil)

    def dunya(self, b, prefab_kok_trs=None):
        """b (Transform ya da PrefabInstance) için dünya TRS'i"""
        if b.cls == 1001:
            kok = prefab_kok_trs(b.guid("m_SourcePrefab")) if prefab_kok_trs else (TRS(), None)
            varsayilan, kok_fid = kok
            p = list(varsayilan.p)
            q = list(varsayilan.q)
            s = list(varsayilan.s)
            for hedef, _, yol, deger, _ in degisiklikler(b):
                if kok_fid is not None and hedef != kok_fid:
                    continue
                for alan, dizi, eksen in (("m_LocalPosition.", p, "xyz"), ("m_LocalRotation.", q, "xyzw"),
                                          ("m_LocalScale.", s, "xyz")):
                    if yol.startswith(alan) and yol[len(alan):] in eksen:
                        dizi[eksen.index(yol[len(alan):])] = float(deger)
            yerel = TRS(p, q, s)
            ust = b.ref("m_TransformParent")
        elif b.stripped:
            pi = self.by.get(b.ref("m_PrefabInstance"))
            return self.dunya(pi, prefab_kok_trs) if pi else TRS()
        else:
            yerel = yerel_trs(b)
            ust = b.ref("m_Father")
        if ust in (None, "0"):
            return yerel
        ub = self.by.get(ust)
        if ub is None:
            return yerel
        return self.dunya(ub, prefab_kok_trs) * yerel

    def kullanilan_fidler(self):
        return set(b.fid for b in self.belgeler)
