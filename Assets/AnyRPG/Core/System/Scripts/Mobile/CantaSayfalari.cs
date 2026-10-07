using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Çanta sayfaları (Ötüken): çanta büyüyünce pencere ekrana sığmıyordu. Artık her sayfada 40 göz (8 x 5) görünür,
    /// altta ‹ Sayfa 2/5 › ve sayfa numaraları (en çok 20 sayfa). Çantadaki donanımın köşesinde işaret:
    ///   ▲ altın: kuşanılandan güçlü   ● yeşil: sınıfına uygun   × kırmızı: başka sınıfın
    /// Karakter penceresi Menü'den açılınca çanta da yanında açılır (KarakterleAc).
    /// </summary>
    public class CantaSayfalari : MonoBehaviour {

        public const int SayfaGozu = 40;
        public const int EnCokSayfa = 20;

        private BagPanel panel = null;
        private int sayfa = 0;
        private int sonSayi = -1;
        private int sonSayfa = -1;
        private Text etiket = null;
        private Transform numaralar = null;
        private Font yaziTipi = null;
        private float sonrakiIsaret = 0f;
        private readonly List<GameObject> numaraDugmeleri = new List<GameObject>();

        private static readonly Color altin = new Color(0.91f, 0.77f, 0.48f, 1f);
        private static readonly Color dugmeRengi = new Color(0.22f, 0.16f, 0.1f, 0.95f);
        private static readonly Color seciliRengi = new Color(0.55f, 0.38f, 0.14f, 1f);

        public static void Ekle(BagPanel panel) {
            if (panel == null || panel.GetComponent<CantaSayfalari>() != null) {
                return;
            }
            CantaSayfalari c = panel.gameObject.AddComponent<CantaSayfalari>();
            c.panel = panel;
            c.Kur();
        }

        private void Kur() {
            yaziTipi = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Transform icerik = panel.ContentArea;
            Transform ust = transform;
            Transform sonra = icerik;
            while (sonra != null && sonra.parent != ust) {
                sonra = sonra.parent;
            }
            GameObject satir = new GameObject("CantaSayfalari", typeof(RectTransform));
            satir.transform.SetParent(ust, false);
            if (sonra != null) {
                satir.transform.SetSiblingIndex(sonra.GetSiblingIndex() + 1);
            }
            LayoutElement yer = satir.AddComponent<LayoutElement>();
            yer.minHeight = 36f;
            yer.preferredHeight = 36f;
            yer.flexibleHeight = 0f;
            HorizontalLayoutGroup dizi = satir.AddComponent<HorizontalLayoutGroup>();
            dizi.padding = new RectOffset(4, 4, 3, 3);
            dizi.spacing = 4f;
            dizi.childControlWidth = true;
            dizi.childControlHeight = true;
            dizi.childForceExpandWidth = false;
            dizi.childForceExpandHeight = true;
            dizi.childAlignment = TextAnchor.MiddleCenter;
            Dugme(satir.transform, "‹", 34f, () => Git(sayfa - 1));
            GameObject e = new GameObject("Etiket", typeof(RectTransform));
            e.transform.SetParent(satir.transform, false);
            e.AddComponent<LayoutElement>().preferredWidth = 96f;
            etiket = e.AddComponent<Text>();
            etiket.font = yaziTipi;
            etiket.fontSize = 15;
            etiket.alignment = TextAnchor.MiddleCenter;
            etiket.color = altin;
            etiket.raycastTarget = false;
            Dugme(satir.transform, "›", 34f, () => Git(sayfa + 1));
            GameObject n = new GameObject("Numaralar", typeof(RectTransform));
            n.transform.SetParent(satir.transform, false);
            HorizontalLayoutGroup nd = n.AddComponent<HorizontalLayoutGroup>();
            nd.spacing = 3f;
            nd.childControlWidth = true;
            nd.childControlHeight = true;
            nd.childForceExpandWidth = false;
            nd.childForceExpandHeight = true;
            numaralar = n.transform;
        }

        private GameObject Dugme(Transform ust, string yazi, float en, UnityEngine.Events.UnityAction tik) {
            GameObject go = new GameObject(yazi, typeof(RectTransform));
            go.transform.SetParent(ust, false);
            go.AddComponent<LayoutElement>().preferredWidth = en;
            Image im = go.AddComponent<Image>();
            im.color = dugmeRengi;
            Button b = go.AddComponent<Button>();
            b.targetGraphic = im;
            b.onClick.AddListener(() => {
                MobileFeedback.Tap();
                tik();
            });
            GameObject t = new GameObject("Yazi", typeof(RectTransform));
            t.transform.SetParent(go.transform, false);
            RectTransform rt = t.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Text tx = t.AddComponent<Text>();
            tx.font = yaziTipi;
            tx.text = yazi;
            tx.fontSize = 16;
            tx.fontStyle = FontStyle.Bold;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = new Color(0.97f, 0.92f, 0.82f, 1f);
            tx.raycastTarget = false;
            return go;
        }

        private int SayfaSayisi {
            get { return Mathf.Clamp(Mathf.CeilToInt(panel.Slots.Count / (float)SayfaGozu), 1, EnCokSayfa); }
        }

        private void Git(int yeni) {
            sayfa = Mathf.Clamp(yeni, 0, SayfaSayisi - 1);
            sonSayfa = -1;
            sonrakiIsaret = 0f;
        }

        private void LateUpdate() {
            if (panel == null) {
                return;
            }
            int n = panel.Slots.Count;
            int sayfalar = SayfaSayisi;
            sayfa = Mathf.Clamp(sayfa, 0, sayfalar - 1);
            bool degisti = n != sonSayi || sayfa != sonSayfa;
            if (degisti) {
                for (int i = 0; i < n; i++) {
                    SlotScript s = panel.Slots[i];
                    if (s == null) {
                        continue;
                    }
                    bool gorunur = i / SayfaGozu == sayfa && i < SayfaGozu * EnCokSayfa;
                    if (s.gameObject.activeSelf != gorunur) {
                        s.gameObject.SetActive(gorunur);
                    }
                }
                etiket.text = "Sayfa " + (sayfa + 1) + "/" + sayfalar;
                Numaralar(sayfalar);
                sonSayi = n;
                sonSayfa = sayfa;
                sonrakiIsaret = 0f;
            }
            if (Time.unscaledTime >= sonrakiIsaret) {
                sonrakiIsaret = Time.unscaledTime + 1f;
                Isaretle();
            }
        }

        private void Numaralar(int sayfalar) {
            // en çok 7 numara: geçerli sayfanın çevresi
            int bas = Mathf.Clamp(sayfa - 3, 0, Mathf.Max(0, sayfalar - 7));
            int son = Mathf.Min(sayfalar, bas + 7);
            int gerekli = son - bas;
            while (numaraDugmeleri.Count < gerekli) {
                int k = numaraDugmeleri.Count;
                GameObject d = Dugme(numaralar, string.Empty, 30f, null);
                Button b = d.GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                numaraDugmeleri.Add(d);
            }
            for (int i = 0; i < numaraDugmeleri.Count; i++) {
                GameObject d = numaraDugmeleri[i];
                bool var = i < gerekli;
                d.SetActive(var);
                if (var == false) {
                    continue;
                }
                int hedef = bas + i;
                d.GetComponentInChildren<Text>().text = (hedef + 1).ToString();
                d.GetComponent<Image>().color = hedef == sayfa ? seciliRengi : dugmeRengi;
                Button b = d.GetComponent<Button>();
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => {
                    MobileFeedback.Tap();
                    Git(hedef);
                });
            }
            numaralar.gameObject.SetActive(sayfalar > 1);
        }

        /// <summary>görünen sayfadaki donanımların köşesine uygunluk işareti</summary>
        private void Isaretle() {
            UnitController oyuncu = OtukenAg.Oyun != null && OtukenAg.Oyun.PlayerManagerClient != null ? OtukenAg.Oyun.PlayerManagerClient.UnitController : null;
            int bas = sayfa * SayfaGozu;
            int son = Mathf.Min(panel.Slots.Count, bas + SayfaGozu);
            for (int i = bas; i < son; i++) {
                SlotScript s = panel.Slots[i];
                if (s == null) {
                    continue;
                }
                InventorySlot yuva = s.InventorySlot;
                InstantiatedEquipment esya = yuva != null && yuva.IsEmpty == false ? yuva.InstantiatedItem as InstantiatedEquipment : null;
                string isaret = string.Empty;
                Color renk = Color.white;
                if (esya != null && oyuncu != null && esya.Equipment != null && esya.Equipment.EquipmentSlotType != null) {
                    if (SinifUygunlugu.Kisitli(esya.Equipment) && SinifUygunlugu.Uygun(esya.Equipment, oyuncu) == false) {
                        isaret = "×";
                        renk = new Color(1f, 0.42f, 0.36f, 1f);
                    } else if (Gelisim.DahaIyiMi(esya, oyuncu) > 0f) {
                        isaret = "▲";
                        renk = new Color(1f, 0.82f, 0.3f, 1f);
                    } else if (esya.GetItemLevel(oyuncu.CharacterStats.Level) <= oyuncu.CharacterStats.Level) {
                        isaret = "●";
                        renk = new Color(0.5f, 0.9f, 0.45f, 1f);
                    }
                }
                Transform t = s.transform.Find("OtukenIsaret");
                if (isaret.Length == 0) {
                    if (t != null && t.gameObject.activeSelf) {
                        t.gameObject.SetActive(false);
                    }
                    continue;
                }
                Text yazi;
                if (t == null) {
                    GameObject go = new GameObject("OtukenIsaret", typeof(RectTransform));
                    go.transform.SetParent(s.transform, false);
                    RectTransform rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(1f, 1f);
                    rt.sizeDelta = new Vector2(16f, 16f);
                    yazi = go.AddComponent<Text>();
                    yazi.font = yaziTipi;
                    yazi.fontSize = 13;
                    yazi.fontStyle = FontStyle.Bold;
                    yazi.alignment = TextAnchor.UpperLeft;
                    yazi.raycastTarget = false;
                    Outline o = go.AddComponent<Outline>();
                    o.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    o.effectDistance = new Vector2(1f, -1f);
                    t = go.transform;
                } else {
                    yazi = t.GetComponent<Text>();
                    t.gameObject.SetActive(true);
                }
                t.SetAsLastSibling();
                yazi.text = isaret;
                yazi.color = renk;
            }
        }

        // ---------------------------------------------------------------- karakter ve çanta yan yana

        /// <summary>Menü > Karakter: karakter penceresi açılır, çanta da yanında açılır (kuşanılabilenler işaretli)</summary>
        public static void KarakterleAc(MonoBehaviour calistiran) {
            MobileInput.PressVirtualKey("CHARACTERPANEL");
            if (calistiran != null) {
                calistiran.StartCoroutine(CantayiYaninaAc());
            }
        }

        private static IEnumerator CantayiYaninaAc() {
            // tuşu oyunun girdisi işler: bir iki kare bekle
            for (int i = 0; i < 3; i++) {
                yield return null;
            }
            SystemGameManager oyun = OtukenAg.Oyun;
            UIManager ui = oyun != null ? oyun.UIManager : null;
            if (ui == null || ui.characterPanelWindow == null || ui.characterPanelWindow.IsOpen == false || ui.inventoryWindow == null) {
                yield break;
            }
            if (ui.inventoryWindow.IsOpen == false) {
                ui.inventoryWindow.OpenWindow();
            }
            yield return null;
            Yerlestir(ui.characterPanelWindow, 0.27f);
            Yerlestir(ui.inventoryWindow, 0.73f);
        }

        private static readonly Vector3[] koseler = new Vector3[4];

        private static void Yerlestir(CloseableWindow pencere, float x) {
            RectTransform rt = pencere != null ? pencere.transform as RectTransform : null;
            Canvas tuval = pencere != null ? pencere.GetComponentInParent<Canvas>() : null;
            if (rt == null || tuval == null || tuval.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay) {
                return;
            }
            rt.GetWorldCorners(koseler);
            Vector3 merkez = (koseler[0] + koseler[2]) * 0.5f;
            Vector3 hedef = new Vector3(Screen.width * x, Screen.height * 0.5f, merkez.z);
            rt.position += hedef - merkez;
        }
    }
}
