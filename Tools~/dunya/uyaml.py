"""Unity YAML sahne/prefab dosyaları için hafif ayrıştırıcı (tam YAML değil, satır tabanlı)."""
import re

BASLIK = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?\s*$", re.M)

SINIF = {1: "GameObject", 4: "Transform", 224: "RectTransform", 20: "Camera", 23: "MeshRenderer",
         33: "MeshFilter", 54: "Rigidbody", 64: "MeshCollider", 65: "BoxCollider", 135: "SphereCollider",
         136: "CapsuleCollider", 108: "Light", 114: "MonoBehaviour", 82: "AudioSource", 95: "Animator",
         137: "SkinnedMeshRenderer", 198: "ParticleSystem", 199: "ParticleSystemRenderer", 1001: "PrefabInstance",
         218: "Terrain", 154: "TerrainCollider", 29: "OcclusionCullingSettings", 104: "RenderSettings",
         157: "LightmapSettings", 196: "NavMeshSettings", 205: "LODGroup", 212: "SpriteRenderer",
         120: "LineRenderer", 96: "TrailRenderer", 215: "ReflectionProbe", 220: "LightProbeGroup",
         320: "PlayableDirector", 1660057539: "SceneRoots", 195: "NavMeshAgent", 208: "NavMeshObstacle",
         111: "Animation", 61: "BoxCollider2D", 59: "HingeJoint", 252: "VFX?", 2083052967: "VisualEffect",
         73398921: "VFXRenderer", 223: "Canvas", 222: "CanvasRenderer", 226: "CanvasGroup"}


class Belge:
    __slots__ = ("cls", "fid", "stripped", "metin")

    def __init__(self, cls, fid, stripped, metin):
        self.cls, self.fid, self.stripped, self.metin = cls, fid, stripped, metin

    @property
    def ad(self):
        return SINIF.get(self.cls, str(self.cls))

    def alan(self, anahtar):
        m = re.search(r"^\s*" + re.escape(anahtar) + r": ?(.*)$", self.metin, re.M)
        return m.group(1).strip() if m else None

    def ref(self, anahtar):
        v = self.alan(anahtar)
        if not v:
            return None
        m = re.search(r"fileID: (-?\d+)", v)
        return m.group(1) if m else None

    def guid(self, anahtar):
        v = self.alan(anahtar)
        if not v:
            return None
        m = re.search(r"guid: ([0-9a-f]{32})", v)
        return m.group(1) if m else None


def oku(yol):
    with open(yol, encoding="utf-8", errors="ignore", newline="") as h:
        metin = h.read()
    return ayir(metin)


def ayir(metin):
    """(ön_metin, [Belge...]) döndürür; birleştirilince dosya aynen geri oluşur."""
    eslesmeler = list(BASLIK.finditer(metin))
    on = metin[:eslesmeler[0].start()] if eslesmeler else metin
    belgeler = []
    for i, m in enumerate(eslesmeler):
        son = eslesmeler[i + 1].start() if i + 1 < len(eslesmeler) else len(metin)
        belgeler.append(Belge(int(m.group(1)), m.group(2), bool(m.group(3)), metin[m.start():son]))
    return on, belgeler


def birlestir(on, belgeler):
    return on + "".join(b.metin for b in belgeler)


def bilesenler(go):
    """GameObject belgesindeki bileşen fileID listesi."""
    return re.findall(r"- component: \{fileID: (-?\d+)\}", go.metin)


def cocuklar(tr):
    blok = re.search(r"m_Children:(.*?)\n  m_Father", tr.metin, re.S)
    if not blok:
        return []
    return re.findall(r"fileID: (-?\d+)", blok.group(1))
