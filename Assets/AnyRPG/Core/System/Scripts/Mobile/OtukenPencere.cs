using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Kodla kurulan pencerelerin (takas, pazar, ticaret, Kut Dükkânı, hesap silme, şartlar, lisanslar) ortak yapı taşları:
    /// kendi tuvali (DontDestroyOnLoad), koyu panel, altın kenarlı düğmeler, kaydırılan liste, yazı kutusu.
    /// Demirci ve Toplu Sat pencereleriyle aynı görünüm (renkler, LegacyRuntime yazı tipi, 1422x800 ölçek).
    /// </summary>
    public abstract class OtukenPencere : MonoBehaviour {

        public static readonly Color Altin = new Color(0.91f, 0.77f, 0.48f, 1f);
        public static readonly Color PanelRengi = new Color(0.09f, 0.07f, 0.05f, 0.97f);
        public static readonly Color SatirRengi = new Color(0.16f, 0.12f, 0.08f, 0.95f);
        public static readonly Color SeciliRengi = new Color(0.45f, 0.32f, 0.14f, 1f);
        public static readonly Color DugmeRengi = new Color(0.2f, 0.15f, 0.1f, 0.95f);
        public static readonly Color OnayRengi = new Color(0.25f, 0.5f, 0.2f, 1f);
        public static readonly Color TehlikeRengi = new Color(0.6f, 0.18f, 0.12f, 1f);
        public static readonly Color VurguRengi = new Color(0.7f, 0.32f, 0.12f, 1f);
        public static readonly Color YaziRengi = new Color(0.96f, 0.92f, 0.84f, 1f);
        public static readonly Color IpucuRengi = new Color(0.75f, 0.7f, 0.62f, 1f);
        public static readonly Color IyiRengi = new Color(0.55f, 0.9f, 0.45f, 1f);
        public static readonly Color HataRengi = new Color(1f, 0.5f, 0.4f, 1f);

        protected Font font = null;
        protected GameObject kok = null;
        protected GameObject panel = null;

        public bool Acik {
            get { return kok != null && kok.activeSelf; }
        }

        /// <summary>pencerenin tuvalini ve bileşenini kurar (bir kez)</summary>
        protected static T Kur<T>(string tuvalAdi, int sira) where T : OtukenPencere {
            GameObject tuval = new GameObject(tuvalAdi);
            DontDestroyOnLoad(tuval);
            Canvas canvas = tuval.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sira;
            CanvasScaler olcek = tuval.AddComponent<CanvasScaler>();
            olcek.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olcek.referenceResolution = new Vector2(1422f, 800f);
            olcek.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            olcek.matchWidthOrHeight = 1f;
            tuval.AddComponent<GraphicRaycaster>();
            T p = tuval.AddComponent<T>();
            p.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            p.Kur();
            if (p.kok != null) {
                p.kok.SetActive(false);
            }
            return p;
        }

        /// <summary>alt sınıf pencereyi burada kurar (Arkaplan + Panel ile başlar)</summary>
        protected abstract void Kur();

        public virtual void Kapat() {
            if (kok != null) {
                kok.SetActive(false);
            }
        }

        /// <summary>ekranı karartan arka plan ve ortada genişlik x yükseklik panel</summary>
        protected void PencereKur(string ad, float genislik, float yukseklik) {
            kok = Kutu(transform, ad, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            kok.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            panel = Kutu(kok.transform, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-genislik * 0.5f, -yukseklik * 0.5f), new Vector2(genislik * 0.5f, yukseklik * 0.5f));
            panel.AddComponent<Image>().color = PanelRengi;
            Outline cizgi = panel.AddComponent<Outline>();
            cizgi.effectColor = Altin;
            cizgi.effectDistance = new Vector2(2f, -2f);
        }

        protected Text Baslik(string metin) {
            Text t = Yazi(Kutu(panel.transform, "Baslik", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -62f), new Vector2(-120f, -8f)),
                metin, 32, TextAnchor.MiddleLeft, Altin);
            t.fontStyle = FontStyle.Bold;
            // sağ üstte kapat
            Dugme(panel.transform, "Kapat", new Vector2(1f, 1f), new Vector2(-64f, -34f), new Vector2(104f, 46f), 20, DugmeRengi, () => {
                MobileFeedback.Tap();
                Kapat();
            });
            return t;
        }

        // ---------------------------------------------------------------- yapı taşları

        public static GameObject Kutu(Transform ust, string ad, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject go = new GameObject(ad);
            go.transform.SetParent(ust, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        protected Text Yazi(GameObject hedef, string icerik, int boyut, TextAnchor hiza, Color renk) {
            Text t = hedef.AddComponent<Text>();
            t.font = font;
            t.text = icerik;
            t.fontSize = boyut;
            t.alignment = hiza;
            t.color = renk;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        protected GameObject Dugme(Transform ust, string etiket, Vector2 anchor, Vector2 konum, Vector2 boyut, int yaziBoyu, Color renk, UnityAction tik) {
            GameObject go = Kutu(ust, etiket, anchor, anchor, Vector2.zero, Vector2.zero);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = konum;
            rt.sizeDelta = boyut;
            Image resim = go.AddComponent<Image>();
            resim.color = renk;
            Outline cizgi = go.AddComponent<Outline>();
            cizgi.effectColor = new Color(Altin.r, Altin.g, Altin.b, 0.6f);
            cizgi.effectDistance = new Vector2(1f, -1f);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = resim;
            ColorBlock renkler = b.colors;
            renkler.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
            b.colors = renkler;
            b.onClick.AddListener(tik);
            Yazi(Kutu(go.transform, "Yazi", Vector2.zero, Vector2.one, new Vector2(4f, 0f), new Vector2(-4f, 0f)), etiket, yaziBoyu, TextAnchor.MiddleCenter, YaziRengi);
            return go;
        }

        /// <summary>düğmenin yazısını değiştirir</summary>
        protected static void DugmeYazisi(GameObject dugme, string metin) {
            Text t = dugme != null ? dugme.GetComponentInChildren<Text>() : null;
            if (t != null) {
                t.text = metin;
            }
        }

        /// <summary>kaydırılan dikey liste; içerik RectTransform'u döner (satırlar Satir ile eklenir)</summary>
        protected RectTransform Liste(Transform ust, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject cerceve = Kutu(ust, "Liste", anchorMin, anchorMax, offsetMin, offsetMax);
            cerceve.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            cerceve.AddComponent<RectMask2D>();
            ScrollRect kaydirma = cerceve.AddComponent<ScrollRect>();
            GameObject icerik = Kutu(cerceve.transform, "Icerik", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            RectTransform rt = icerik.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 1f);
            kaydirma.content = rt;
            kaydirma.horizontal = false;
            kaydirma.vertical = true;
            kaydirma.movementType = ScrollRect.MovementType.Clamped;
            kaydirma.scrollSensitivity = 30f;
            return rt;
        }

        protected static void ListeyiTemizle(RectTransform liste) {
            for (int i = liste.childCount - 1; i >= 0; i--) {
                Destroy(liste.GetChild(i).gameObject);
            }
            liste.sizeDelta = new Vector2(0f, 0f);
        }

        /// <summary>listeye i. satırı ekler (yükseklik boy); dokunulunca tik (null olabilir)</summary>
        protected GameObject Satir(RectTransform liste, int i, float boy, Color renk, UnityAction tik) {
            GameObject satir = Kutu(liste, "Satir" + i, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(6f, -(i + 1) * (boy + 6f)), new Vector2(-6f, -i * (boy + 6f) - 6f));
            Image arka = satir.AddComponent<Image>();
            arka.color = renk;
            if (tik != null) {
                Button b = satir.AddComponent<Button>();
                b.targetGraphic = arka;
                b.onClick.AddListener(tik);
            }
            liste.sizeDelta = new Vector2(0f, Mathf.Max(liste.sizeDelta.y, (i + 1) * (boy + 6f) + 6f));
            return satir;
        }

        protected Image Simge(Transform ust, Sprite sprite, Vector2 offsetMin, Vector2 offsetMax) {
            GameObject kutu = Kutu(ust, "Simge", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), offsetMin, offsetMax);
            Image resim = kutu.AddComponent<Image>();
            resim.sprite = sprite;
            resim.preserveAspect = true;
            resim.raycastTarget = false;
            resim.color = sprite == null ? new Color(0.3f, 0.25f, 0.2f, 1f) : Color.white;
            return resim;
        }

        /// <summary>tek satırlık yazı kutusu (pazar başlığı, şifre, fiyat...)</summary>
        protected InputField Girdi(Transform ust, string ipucu, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            int sinir, InputField.ContentType tur = InputField.ContentType.Standard) {
            GameObject go = Kutu(ust, "Girdi", anchorMin, anchorMax, offsetMin, offsetMax);
            Image arka = go.AddComponent<Image>();
            arka.color = new Color(0f, 0f, 0f, 0.55f);
            Outline cizgi = go.AddComponent<Outline>();
            cizgi.effectColor = new Color(Altin.r, Altin.g, Altin.b, 0.5f);
            cizgi.effectDistance = new Vector2(1f, -1f);
            Text yer = Yazi(Kutu(go.transform, "Ipucu", Vector2.zero, Vector2.one, new Vector2(12f, 2f), new Vector2(-12f, -2f)), ipucu, 22,
                TextAnchor.MiddleLeft, new Color(IpucuRengi.r, IpucuRengi.g, IpucuRengi.b, 0.6f));
            yer.fontStyle = FontStyle.Italic;
            Text metin = Yazi(Kutu(go.transform, "Metin", Vector2.zero, Vector2.one, new Vector2(12f, 2f), new Vector2(-12f, -2f)), string.Empty, 22,
                TextAnchor.MiddleLeft, YaziRengi);
            metin.supportRichText = false;
            InputField alan = go.AddComponent<InputField>();
            alan.targetGraphic = arka;
            alan.textComponent = metin;
            alan.placeholder = yer;
            alan.characterLimit = sinir;
            alan.contentType = tur;
            alan.lineType = InputField.LineType.SingleLine;
            return alan;
        }

        // ---------------------------------------------------------------- oyun

        protected static SystemGameManager Oyun {
            get { return OtukenAg.Oyun; }
        }

        protected static UnitController Oyuncu {
            get {
                SystemGameManager o = OtukenAg.Oyun;
                return o != null && o.PlayerManagerClient != null ? o.PlayerManagerClient.UnitController : null;
            }
        }

        /// <summary>bakır cinsinden tutarı "1 Altın 2 Gümüş Akçe 5 Bakır Akçe" diye yazar</summary>
        public static string ParaYazisi(int bakir) {
            if (bakir <= 0) {
                return "0 Bakır Akçe";
            }
            int altin = bakir / 10000;
            int gumus = (bakir / 100) % 100;
            int kalan = bakir % 100;
            List<string> p = new List<string>();
            if (altin > 0) {
                p.Add(altin + " Altın");
            }
            if (gumus > 0) {
                p.Add(gumus + " Gümüş Akçe");
            }
            if (kalan > 0) {
                p.Add(kalan + " Bakır Akçe");
            }
            return string.Join(" ", p);
        }

        /// <summary>"12a 5g 30b", "1250" gibi yazılan tutarı bakıra çevirir (a: altın, g: gümüş, b: bakır; çıplak sayı gümüş)</summary>
        public static int ParaOku(string metin) {
            if (string.IsNullOrWhiteSpace(metin)) {
                return 0;
            }
            long toplam = 0;
            string sayi = string.Empty;
            bool birimVar = false;
            foreach (char c0 in metin.ToLowerInvariant() + " ") {
                char c = c0;
                if (char.IsDigit(c)) {
                    sayi += c;
                    continue;
                }
                if (sayi.Length > 0) {
                    long n;
                    long.TryParse(sayi, out n);
                    if (c == 'a') {
                        toplam += n * 10000;
                        birimVar = true;
                    } else if (c == 'g') {
                        toplam += n * 100;
                        birimVar = true;
                    } else if (c == 'b') {
                        toplam += n;
                        birimVar = true;
                    } else {
                        toplam += birimVar ? n : n * 100;
                    }
                    sayi = string.Empty;
                }
            }
            return (int)Math.Min(toplam, 2000000000L);
        }
    }
}
