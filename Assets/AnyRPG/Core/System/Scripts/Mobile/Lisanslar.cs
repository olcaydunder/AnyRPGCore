using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>Seçenekler > Hesap > "Açık kaynak lisansları": Resources/Lisanslar.txt (bölümler "=====" ile ayrılır)</summary>
    public class Lisanslar : OtukenPencere {

        private static Lisanslar ornek = null;

        public static void Goster() {
            if (ornek == null) {
                ornek = Kur<Lisanslar>("LisanslarCanvas", 41);
            }
            ornek.kok.SetActive(true);
        }

        protected override void Kur() {
            PencereKur("Lisanslar", 1180f, 700f);
            Baslik("AÇIK KAYNAK LİSANSLARI");
            GameObject cerceve = Kutu(panel.transform, "Liste", Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -76f));
            cerceve.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);
            cerceve.AddComponent<RectMask2D>();
            ScrollRect kaydirma = cerceve.AddComponent<ScrollRect>();
            GameObject icerik = Kutu(cerceve.transform, "Icerik", new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            RectTransform rt = icerik.GetComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup dizi = icerik.AddComponent<VerticalLayoutGroup>();
            dizi.padding = new RectOffset(16, 16, 12, 12);
            dizi.spacing = 14f;
            dizi.childControlHeight = true;
            dizi.childControlWidth = true;
            dizi.childForceExpandHeight = false;
            ContentSizeFitter boy = icerik.AddComponent<ContentSizeFitter>();
            boy.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            kaydirma.content = rt;
            kaydirma.horizontal = false;
            kaydirma.vertical = true;
            kaydirma.movementType = ScrollRect.MovementType.Clamped;
            kaydirma.scrollSensitivity = 40f;

            TextAsset dosya = Resources.Load<TextAsset>("Lisanslar");
            string metin = dosya != null ? dosya.text : "Lisans metni bulunamadı.";
            // bir Text en çok ~16 bin harf çizebilir: parçalara bölünür
            foreach (string parca in Parcala(metin, 2500)) {
                GameObject go = new GameObject("Bolum", typeof(RectTransform));
                go.transform.SetParent(icerik.transform, false);
                Text t = Yazi(go, parca, 17, TextAnchor.UpperLeft, YaziRengi);
                t.supportRichText = false;
            }
        }

        private static List<string> Parcala(string metin, int sinir) {
            List<string> parcalar = new List<string>();
            string[] satirlar = metin.Replace("\r", string.Empty).Split('\n');
            System.Text.StringBuilder s = new System.Text.StringBuilder();
            foreach (string satir in satirlar) {
                if (s.Length + satir.Length > sinir && s.Length > 0) {
                    parcalar.Add(s.ToString().TrimEnd());
                    s.Length = 0;
                }
                s.Append(satir).Append('\n');
            }
            if (s.Length > 0) {
                parcalar.Add(s.ToString().TrimEnd());
            }
            return parcalar;
        }
    }
}
