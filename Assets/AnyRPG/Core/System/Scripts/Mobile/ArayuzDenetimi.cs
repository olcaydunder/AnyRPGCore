using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Ekrandaki arayüz yerleşimini denetler: oyunun kendi göstergelerinden (can çubuğu, yetenek çubuğu, mini harita,
    /// görev listesi, sistem çubuğu...) görünen her parçanın ekrandaki dikdörtgenini toplar ve çakışmaları bulur.
    /// Dokunmatik düğmeleri boş yere kaydıran yerleşim (MobileHud), hata bildirimindeki arayüz raporu (HataBildirici)
    /// ve derlemedeki arayüz önizlemesi (HaritaHazirlik) bunu kullanır.
    /// Dikdörtgenler ekran pikseli cinsindendir, sol alt köşe (0, 0).
    /// </summary>
    public static class ArayuzDenetimi {

        /// <summary>MobileBootstrap.ScaleCanvases pencere ve menüleri 10 yukarı taşır; bunun altı oyun göstergeleridir</summary>
        public const int PencereKatmani = 12;

        public struct Parca {
            /// <summary>parçanın ait olduğu gösterge (pencere, çubuk...) </summary>
            public string grup;
            /// <summary>parçanın tuvaldeki yolu</summary>
            public string yol;
            public Rect ekran;
        }

        /// <summary>bizim kodla kurduğumuz tuvaller: dokunmatik düğmeler ve tam ekran pencereler engel sayılmaz</summary>
        private static readonly HashSet<string> haricTuvaller = new HashSet<string>() {
            MobileHud.CanvasName, SeceneklerPenceresi.CanvasName, IsinlanmaPenceresi.CanvasName, GameGuide.CanvasName,
            GunlukArmagan.CanvasName, HataBildirici.CanvasName, "ErrorOverlayCanvas", GunlukGorevler.CanvasName, GorevOku.CanvasName
        };

        // anlık çıkan, yeri sabit olmayan göstergeler: düğmeler bunlara göre kaydırılmaz
        private static readonly string[] gecici = {
            "MessageFeed", "CombatText", "MouseOver", "ToolTip", "Tooltip", "NamePlate", "Nameplate", "Loading", "Cutscene",
            "CastBar", "HintBar"
        };

        private static readonly List<Graphic> grafikler = new List<Graphic>();
        private static readonly Vector3[] koseler = new Vector3[4];

        // her saniye yüzlerce parça taranır: grup adı ve "geçici mi" sonucu parça başına bir kez hesaplanır
        // (Transform.name her okunuşta yeni yazı üretir; çöp toplayıcıyı yormasın)
        private struct GrupBilgisi {
            public string grup;
            public bool gecici;
        }
        private static readonly Dictionary<int, GrupBilgisi> grupOnbellegi = new Dictionary<int, GrupBilgisi>();

        /// <summary>
        /// görünen oyun göstergelerinin parçaları (pencere katmanının altındaki ekran tuvalleri)
        /// </summary>
        public static void GostergeParcalari(List<Parca> liste) {
            GostergeParcalari(liste, true);
        }

        /// <param name="yollar">parçaların tuvaldeki yolu da yazılsın mı (yalnız raporlar için; her saniyelik denetimde gerekmez)</param>
        public static void GostergeParcalari(List<Parca> liste, bool yollar) {
            liste.Clear();
            if (grupOnbellegi.Count > 5000) {
                grupOnbellegi.Clear();
            }
            Rect ekran = new Rect(0f, 0f, Screen.width, Screen.height);
            foreach (Canvas tuval in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) {
                if (tuval == null || tuval.isRootCanvas == false || tuval.enabled == false || tuval.gameObject.activeInHierarchy == false
                    || tuval.renderMode == RenderMode.WorldSpace || tuval.sortingOrder >= PencereKatmani || haricTuvaller.Contains(tuval.name)
                    || GeciciMi(tuval.name)) {
                    continue;
                }
                Rect tuvalEkrani = TuvalEkrani(tuval, ekran);
                TuvalParcalari(tuval, tuvalEkrani, liste, yollar);
            }
        }

        /// <summary>bir tuvalin görünen parçaları</summary>
        public static void TuvalParcalari(Canvas tuval, Rect ekran, List<Parca> liste, bool yollar) {
            grafikler.Clear();
            tuval.GetComponentsInChildren(false, grafikler);
            float enBuyukAlan = ekran.width * ekran.height * 0.3f;
            foreach (Graphic grafik in grafikler) {
                if (Gorunur(grafik) == false) {
                    continue;
                }
                Rect r = GrafikEkrani(grafik, tuval);
                if (r.width < 3f || r.height < 3f || r.width * r.height > enBuyukAlan) {
                    continue;
                }
                // ekranın dışında kalanlar
                if (r.Overlaps(ekran) == false) {
                    continue;
                }
                GrupBilgisi bilgi;
                int kimlik = grafik.GetInstanceID();
                if (grupOnbellegi.TryGetValue(kimlik, out bilgi) == false) {
                    string ad = GrupAdi(grafik.transform, tuval.transform);
                    bilgi = new GrupBilgisi() { grup = ad, gecici = GeciciMi(ad) };
                    grupOnbellegi[kimlik] = bilgi;
                }
                if (bilgi.gecici) {
                    continue;
                }
                liste.Add(new Parca() { grup = bilgi.grup, yol = yollar ? Yol(grafik.transform, tuval.transform) : null, ekran = r });
            }
        }

        private static bool Gorunur(Graphic grafik) {
            if (grafik == null || grafik.isActiveAndEnabled == false) {
                return false;
            }
            CanvasRenderer cizici = grafik.canvasRenderer;
            if (cizici == null || cizici.cull) {
                return false;
            }
            float alfa = grafik.color.a * cizici.GetAlpha() * cizici.GetInheritedAlpha();
            if (alfa < 0.08f) {
                return false;
            }
            Mask maske = grafik.GetComponent<Mask>();
            if (maske != null && maske.showMaskGraphic == false) {
                return false;
            }
            TMP_Text tmp = grafik as TMP_Text;
            if (tmp != null && string.IsNullOrWhiteSpace(tmp.text)) {
                return false;
            }
            Text yazi = grafik as Text;
            if (yazi != null && string.IsNullOrWhiteSpace(yazi.text)) {
                return false;
            }
            Image resim = grafik as Image;
            if (resim != null && resim.type == Image.Type.Filled && resim.fillAmount <= 0.001f) {
                return false;
            }
            return true;
        }

        /// <summary>grafiğin ekrandaki dikdörtgeni; yazılarda yazının kapladığı alan (kutunun tamamı değil)</summary>
        public static Rect GrafikEkrani(Graphic grafik, Canvas tuval) {
            TMP_Text tmp = grafik as TMP_Text;
            if (tmp != null) {
                Bounds b = tmp.textBounds;
                if (b.size.x > 0.5f && b.size.y > 0.5f) {
                    Transform t = tmp.transform;
                    return EkranDikdortgeni(tuval, t.TransformPoint(b.min), t.TransformPoint(b.max));
                }
            }
            return Dikdortgen(grafik.rectTransform, tuval);
        }

        /// <summary>bir RectTransform'un ekran dikdörtgeni (etkin olmasa da)</summary>
        public static Rect Dikdortgen(RectTransform rt, Canvas tuval) {
            rt.GetWorldCorners(koseler);
            return EkranDikdortgeni(tuval, koseler[0], koseler[2]);
        }

        private static Rect EkranDikdortgeni(Canvas tuval, Vector3 a, Vector3 b) {
            Canvas kok = tuval != null ? tuval.rootCanvas : null;
            Camera kamera = kok != null && kok.renderMode == RenderMode.ScreenSpaceCamera ? kok.worldCamera : null;
            Vector2 p = kamera != null ? (Vector2)kamera.WorldToScreenPoint(a) : (Vector2)a;
            Vector2 q = kamera != null ? (Vector2)kamera.WorldToScreenPoint(b) : (Vector2)b;
            return Rect.MinMaxRect(Mathf.Min(p.x, q.x), Mathf.Min(p.y, q.y), Mathf.Max(p.x, q.x), Mathf.Max(p.y, q.y));
        }

        /// <summary>tuvalin kapladığı ekran alanı (kamera tuvalinde kameranın görüntüsü)</summary>
        public static Rect TuvalEkrani(Canvas tuval, Rect varsayilan) {
            if (tuval.renderMode == RenderMode.ScreenSpaceCamera && tuval.worldCamera != null) {
                return tuval.worldCamera.pixelRect;
            }
            return varsayilan;
        }

        /// <summary>parçanın ait olduğu gösterge: en yakın pencere, yetenek ya da sistem çubuğu; yoksa tuvalin çocuğu</summary>
        public static string GrupAdi(Transform t, Transform kok) {
            Transform enUst = t;
            for (Transform p = t; p != null && p != kok; p = p.parent) {
                if (p.GetComponent<CloseableWindow>() != null || p.GetComponent<ActionBarController>() != null
                    || p.GetComponent<SystemBarController>() != null) {
                    return p.name;
                }
                enUst = p;
            }
            return enUst.name;
        }

        private static string Yol(Transform t, Transform kok) {
            StringBuilder sb = new StringBuilder(t.name);
            int adim = 0;
            for (Transform p = t.parent; p != null && p != kok && adim < 3; p = p.parent, adim++) {
                sb.Insert(0, p.name + "/");
            }
            return sb.ToString();
        }

        public static bool GeciciMi(string ad) {
            if (string.IsNullOrEmpty(ad)) {
                return false;
            }
            foreach (string g in gecici) {
                if (ad.IndexOf(g, System.StringComparison.OrdinalIgnoreCase) >= 0) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// oyunun göstergelerinin birbiriyle çakışmaları (farklı göstergelere ait parçalar üst üste geliyorsa);
        /// her gösterge çifti bir kez yazılır
        /// </summary>
        public static List<string> GostergeCakismalari(List<Parca> parcalar, float enAz) {
            List<string> sonuc = new List<string>();
            HashSet<string> yazilan = new HashSet<string>();
            for (int i = 0; i < parcalar.Count; i++) {
                for (int j = i + 1; j < parcalar.Count; j++) {
                    Parca a = parcalar[i];
                    Parca b = parcalar[j];
                    if (a.grup == b.grup) {
                        continue;
                    }
                    if (Kesisim(a.ekran, b.ekran, enAz) == false) {
                        continue;
                    }
                    string anahtar = string.CompareOrdinal(a.grup, b.grup) < 0 ? a.grup + " | " + b.grup : b.grup + " | " + a.grup;
                    if (yazilan.Add(anahtar)) {
                        sonuc.Add(anahtar + "  (" + a.yol + " ~ " + b.yol + ")");
                    }
                }
            }
            return sonuc;
        }

        /// <summary>iki dikdörtgen her iki yönde en az "enAz" piksel iç içe mi</summary>
        public static bool Kesisim(Rect a, Rect b, float enAz) {
            float x = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
            float y = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return x > enAz && y > enAz;
        }

        /// <summary>daire ile dikdörtgen kesişiyor mu (daire kenarından "pay" kadar içeride sayılır)</summary>
        public static bool DaireDikdortgen(Vector2 merkez, float yaricap, Rect r) {
            float x = Mathf.Clamp(merkez.x, r.xMin, r.xMax);
            float y = Mathf.Clamp(merkez.y, r.yMin, r.yMax);
            float dx = merkez.x - x;
            float dy = merkez.y - y;
            return dx * dx + dy * dy < yaricap * yaricap;
        }

        /// <summary>
        /// yerleşim raporu: dokunmatik düğmelerin göstergelerle ve birbirleriyle, göstergelerin de birbirleriyle
        /// çakışmaları; düğmelerin ve göstergelerin ekrandaki yerleri
        /// </summary>
        public static string YerlesimRaporu(MobileHud hud, Rect ekran, Rect guvenli, out int cakismaSayisi) {
            List<Parca> parcalar = new List<Parca>();
            GostergeParcalari(parcalar);
            List<KeyValuePair<string, Rect>> dugmeler = new List<KeyValuePair<string, Rect>>();
            if (hud != null) {
                hud.DugmeDikdortgenleri(dugmeler);
            }

            List<string> cakismalar = new List<string>();
            foreach (KeyValuePair<string, Rect> d in dugmeler) {
                Vector2 merkez = d.Value.center;
                float yaricap = d.Value.width * 0.5f;
                HashSet<string> yazilan = new HashSet<string>();
                foreach (Parca p in parcalar) {
                    if (DaireDikdortgen(merkez, yaricap - 2f, p.ekran) && yazilan.Add(p.grup)) {
                        cakismalar.Add("düğme " + d.Key + " ~ " + p.grup + " (" + p.yol + ")");
                    }
                }
                if (d.Value.xMin < guvenli.xMin - 1f || d.Value.xMax > guvenli.xMax + 1f || d.Value.yMin < guvenli.yMin - 1f || d.Value.yMax > guvenli.yMax + 1f) {
                    cakismalar.Add("düğme " + d.Key + " ekranın / güvenli alanın dışına taşıyor");
                }
            }
            for (int i = 0; i < dugmeler.Count; i++) {
                for (int j = i + 1; j < dugmeler.Count; j++) {
                    float enAz = (dugmeler[i].Value.width + dugmeler[j].Value.width) * 0.5f - 2f;
                    if ((dugmeler[i].Value.center - dugmeler[j].Value.center).magnitude < enAz) {
                        cakismalar.Add("düğme " + dugmeler[i].Key + " ~ düğme " + dugmeler[j].Key);
                    }
                }
            }
            cakismalar.AddRange(GostergeCakismalari(parcalar, 6f));
            cakismaSayisi = cakismalar.Count;

            StringBuilder sb = new StringBuilder();
            sb.Append("Ekran ").Append(Mathf.RoundToInt(ekran.width)).Append('x').Append(Mathf.RoundToInt(ekran.height))
                .Append(", güvenli alan ").Append(Yaz(guvenli)).Append('\n');
            if (hud != null) {
                sb.Append(hud.YerlesimOzeti());
            }
            sb.Append("Çakışmalar (").Append(cakismalar.Count).Append("):\n");
            foreach (string c in cakismalar) {
                sb.Append("- ").Append(c).Append('\n');
            }
            sb.Append("Düğmeler:\n");
            foreach (KeyValuePair<string, Rect> d in dugmeler) {
                sb.Append(d.Key).Append(' ').Append(Yaz(d.Value)).Append('\n');
            }
            sb.Append("Göstergeler:\n");
            foreach (KeyValuePair<string, Rect> g in Gruplar(parcalar)) {
                sb.Append(g.Key).Append(' ').Append(Yaz(g.Value)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>her göstergenin parçalarını kapsayan dikdörtgen</summary>
        public static List<KeyValuePair<string, Rect>> Gruplar(List<Parca> parcalar) {
            Dictionary<string, Rect> gruplar = new Dictionary<string, Rect>();
            foreach (Parca p in parcalar) {
                Rect r;
                gruplar[p.grup] = gruplar.TryGetValue(p.grup, out r) ? Rect.MinMaxRect(Mathf.Min(r.xMin, p.ekran.xMin), Mathf.Min(r.yMin, p.ekran.yMin),
                    Mathf.Max(r.xMax, p.ekran.xMax), Mathf.Max(r.yMax, p.ekran.yMax)) : p.ekran;
            }
            List<KeyValuePair<string, Rect>> liste = new List<KeyValuePair<string, Rect>>(gruplar);
            liste.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
            return liste;
        }

        public static string Yaz(Rect r) {
            return "(" + Mathf.RoundToInt(r.xMin) + "," + Mathf.RoundToInt(r.yMin) + " " + Mathf.RoundToInt(r.width) + "x" + Mathf.RoundToInt(r.height) + ")";
        }
    }
}
