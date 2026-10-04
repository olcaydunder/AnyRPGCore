using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnyRPG {

    /// <summary>
    /// Görev oku: oyuncuya sıradaki görev hedefini gösterir. Hedef ekrandaysa üstünde aşağı bakan sarı bir işaret,
    /// ekran dışındaysa ekranın kenarında ona dönen bir ok; yanında adı ve uzaklığı yazar.
    /// Hedef seçimi (saniyede iki kez, yalnız içinde bulunulan haritada):
    ///  1. teslim edilecek görevi olan görev veren ("?" işaretli kişi)
    ///  2. süren görevin hedefi: avlanacak düşman (KillObjective) ya da kullanılacak nesne (UseInteractableObjective)
    ///  3. yeni görev veren ("!" işaretli kişi)
    ///  4. bugünün "hazine sandığı" günlük görevi sürüyorsa en yakın dolu sandık
    /// Hedefe 4 metreden çok yaklaşınca gizlenir. Seçenekler > Oyun > Görev oku ile kapatılır. Kodla kurulur.
    /// </summary>
    public class GorevOku : MonoBehaviour {

        public const string CanvasName = "GorevOkuCanvas";
        // HUD'un (MobileHud, 5) altında, oyun göstergelerinin üstünde
        private const int SortingOrder = 4;
        private const string AcikKey = "gorev-oku";
        private const float SecimAraligi = 0.5f;
        private const float VarisMesafesi = 4f;
        private const float KenarPayi = 70f;
        private const float UstPay = 120f;
        private const float AltPay = 110f;
        private const float HedefYuksekligi = 2.6f;

        private static readonly Color okRengi = new Color(1f, 0.82f, 0.3f, 0.95f);
        private static readonly Color yaziRengi = new Color(1f, 0.95f, 0.82f, 1f);

        private static GorevOku instance = null;

        public static bool Acik {
            get { return PlayerPrefs.GetInt(AcikKey, 1) == 1; }
            set {
                PlayerPrefs.SetInt(AcikKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>oyun testi için: şu an gösterilen hedefin açıklaması (yoksa boş)</summary>
        public static string SonHedef { get; private set; }

        private SystemGameManager oyun = null;
        private RectTransform tuval = null;
        private RectTransform ok = null;
        private Text yazi = null;
        private RectTransform yaziRt = null;
        private Transform hedef = null;
        private string hedefAdi = string.Empty;
        private float sonrakiSecim = 0f;
        private bool gorunur = false;

        private readonly List<InteractableBase> adaylar = new List<InteractableBase>();

        /// <summary>MobileBootstrap saniyede bir çağırır</summary>
        public static void Tick(SystemGameManager systemGameManager, bool oyunda) {
            if (oyunda == false || Acik == false) {
                if (instance != null) {
                    instance.Gizle();
                    instance.oyun = systemGameManager;
                    instance.enabled = false;
                }
                return;
            }
            if (instance == null) {
                Kur();
            }
            instance.oyun = systemGameManager;
            instance.enabled = true;
        }

        private static void Kur() {
            GameObject canvasObject = new GameObject(CanvasName);
            DontDestroyOnLoad(canvasObject);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            CanvasScaler olcekleyici = canvasObject.AddComponent<CanvasScaler>();
            olcekleyici.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            olcekleyici.referenceResolution = new Vector2(1422f, 800f);
            olcekleyici.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            olcekleyici.matchWidthOrHeight = 1f;
            instance = canvasObject.AddComponent<GorevOku>();
            instance.Build();
        }

        private void Build() {
            tuval = (RectTransform)transform;
            GameObject okNesnesi = new GameObject("Ok");
            okNesnesi.transform.SetParent(transform, false);
            ok = okNesnesi.AddComponent<RectTransform>();
            ok.anchorMin = ok.anchorMax = new Vector2(0.5f, 0.5f);
            ok.pivot = new Vector2(0.5f, 0.5f);
            ok.sizeDelta = new Vector2(46f, 46f);
            Image resim = okNesnesi.AddComponent<Image>();
            resim.sprite = Ucgen();
            resim.color = okRengi;
            resim.raycastTarget = false;
            Outline cizgi = okNesnesi.AddComponent<Outline>();
            cizgi.effectColor = new Color(0.25f, 0.15f, 0.02f, 0.9f);
            cizgi.effectDistance = new Vector2(2f, -2f);

            GameObject yaziNesnesi = new GameObject("Yazi");
            yaziNesnesi.transform.SetParent(transform, false);
            yaziRt = yaziNesnesi.AddComponent<RectTransform>();
            yaziRt.anchorMin = yaziRt.anchorMax = new Vector2(0.5f, 0.5f);
            yaziRt.pivot = new Vector2(0.5f, 0.5f);
            yaziRt.sizeDelta = new Vector2(360f, 56f);
            yazi = yaziNesnesi.AddComponent<Text>();
            yazi.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            yazi.fontSize = 19;
            yazi.alignment = TextAnchor.MiddleCenter;
            yazi.color = yaziRengi;
            yazi.raycastTarget = false;
            yazi.horizontalOverflow = HorizontalWrapMode.Wrap;
            yazi.verticalOverflow = VerticalWrapMode.Overflow;
            Outline yaziCizgisi = yaziNesnesi.AddComponent<Outline>();
            yaziCizgisi.effectColor = new Color(0f, 0f, 0f, 0.85f);
            yaziCizgisi.effectDistance = new Vector2(1.5f, -1.5f);
            Gizle();
        }

        private void Gizle() {
            if (gorunur || ok.gameObject.activeSelf) {
                ok.gameObject.SetActive(false);
                yaziRt.gameObject.SetActive(false);
            }
            gorunur = false;
        }

        private void LateUpdate() {
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu == null || (oyun.LevelManagerClient != null && oyun.LevelManagerClient.IsCutscene())) {
                Gizle();
                return;
            }
            if (Time.unscaledTime >= sonrakiSecim) {
                sonrakiSecim = Time.unscaledTime + SecimAraligi;
                try {
                    HedefSec(oyuncu);
                } catch (System.Exception e) {
                    // görev verileri sahne değişirken yarım olabilir: bir dahaki seçimde yeniden denenir
                    hedef = null;
                    Debug.LogWarning("GorevOku: hedef seçilemedi: " + e.Message);
                }
            }
            if (hedef == null || hedef.gameObject.activeInHierarchy == false) {
                SonHedef = string.Empty;
                Gizle();
                return;
            }
            Vector3 hedefYeri = hedef.position + Vector3.up * HedefYuksekligi;
            float mesafe = Vector3.Distance(oyuncu.transform.position, hedef.position);
            if (mesafe < VarisMesafesi) {
                Gizle();
                return;
            }
            Camera kamera = oyun.CameraManager != null ? oyun.CameraManager.ActiveMainCamera : null;
            if (kamera == null) {
                kamera = Camera.main;
            }
            if (kamera == null) {
                Gizle();
                return;
            }
            Goster(kamera, hedefYeri, mesafe);
        }

        private void Goster(Camera kamera, Vector3 hedefYeri, float mesafe) {
            Vector3 ekran = kamera.WorldToScreenPoint(hedefYeri);
            bool arkada = ekran.z < 0f;
            Vector2 yerel;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(tuval, new Vector2(ekran.x, ekran.y), null, out yerel);
            if (arkada) {
                // kameranın arkasındaki nokta ters düşer: yönü çevir ve kenara it
                yerel = -yerel;
                if (yerel.sqrMagnitude < 1f) {
                    yerel = new Vector2(0f, -1f);
                }
                yerel = yerel.normalized * 100000f;
            }
            Vector2 boyut = tuval.rect.size;
            float xEn = boyut.x * 0.5f - KenarPayi;
            float yUst = boyut.y * 0.5f - UstPay;
            float yAlt = -(boyut.y * 0.5f - AltPay);
            bool icinde = arkada == false && yerel.x > -xEn && yerel.x < xEn && yerel.y > yAlt && yerel.y < yUst;

            string uzaklik = mesafe < 1000f ? Mathf.RoundToInt(mesafe) + " m" : (mesafe / 1000f).ToString("0.0") + " km";
            yazi.text = hedefAdi + " · " + uzaklik;

            if (icinde) {
                // hedefin üstünde aşağı bakan işaret, hafif sallanır
                float salinim = Mathf.Sin(Time.unscaledTime * 4f) * 6f;
                ok.anchoredPosition = yerel + new Vector2(0f, 26f + salinim);
                ok.localEulerAngles = new Vector3(0f, 0f, 180f);
                yaziRt.anchoredPosition = yerel + new Vector2(0f, 72f + salinim);
            } else {
                // ekranın kenarında, hedefe dönen ok
                Vector2 yon = yerel.sqrMagnitude > 0.01f ? yerel.normalized : Vector2.up;
                float olcek = float.MaxValue;
                if (Mathf.Abs(yon.x) > 0.0001f) {
                    olcek = Mathf.Min(olcek, xEn / Mathf.Abs(yon.x));
                }
                if (yon.y > 0.0001f) {
                    olcek = Mathf.Min(olcek, yUst / yon.y);
                } else if (yon.y < -0.0001f) {
                    olcek = Mathf.Min(olcek, -yAlt / -yon.y);
                }
                Vector2 kenar = yon * olcek;
                ok.anchoredPosition = kenar;
                ok.localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(yon.y, yon.x) * Mathf.Rad2Deg - 90f);
                // yazı okun içeri tarafında
                Vector2 icYon = -yon;
                Vector2 yaziYeri = kenar + icYon * 58f;
                float yariGenislik = yaziRt.sizeDelta.x * 0.5f;
                yaziYeri.x = Mathf.Clamp(yaziYeri.x, -boyut.x * 0.5f + yariGenislik + 8f, boyut.x * 0.5f - yariGenislik - 8f);
                yaziRt.anchoredPosition = yaziYeri;
            }
            if (gorunur == false) {
                ok.gameObject.SetActive(true);
                yaziRt.gameObject.SetActive(true);
                gorunur = true;
            }
        }

        // ---------------------------------------------------------------- hedef seçimi

        private void HedefSec(UnitController oyuncu) {
            Vector3 p = oyuncu.transform.position;
            adaylar.Clear();
            adaylar.AddRange(Object.FindObjectsByType<InteractableBase>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));

            InteractableBase teslim = null, yeni = null, amac = null;
            float teslimMesafe = float.MaxValue, yeniMesafe = float.MaxValue, amacMesafe = float.MaxValue;
            string amacAdi = string.Empty;

            // süren görevlerin şu anki adımındaki bitmemiş hedefler
            List<QuestObjective> hedefler = new List<QuestObjective>();
            if (oyuncu.CharacterQuestLog != null && oyuncu.CharacterQuestLog.Quests != null) {
                foreach (Quest gorev in oyuncu.CharacterQuestLog.Quests.Values) {
                    if (gorev == null || gorev.IsComplete(oyuncu) || gorev.Steps == null || gorev.Steps.Count == 0) {
                        continue;
                    }
                    int adim = Mathf.Clamp(gorev.CurrentStep(oyuncu), 0, gorev.Steps.Count - 1);
                    foreach (QuestObjective amacNesnesi in gorev.Steps[adim].QuestObjectives) {
                        if (amacNesnesi != null && amacNesnesi.IsComplete(oyuncu) == false
                            && (amacNesnesi is KillObjective || amacNesnesi is UseInteractableObjective)) {
                            hedefler.Add(amacNesnesi);
                        }
                    }
                }
            }

            foreach (InteractableBase aday in adaylar) {
                if (aday == null || aday == oyuncu) {
                    continue;
                }
                float mesafe = Vector3.Distance(p, aday.transform.position);
                UnitController birim = aday as UnitController;
                bool canli = birim == null || (birim.CharacterStats != null && birim.CharacterStats.IsAlive);

                // görev verenler
                if (aday.Interactables != null && canli) {
                    foreach (InteractableOptionComponent secenek in aday.Interactables.Values) {
                        QuestGiverComponent gorevVeren = secenek as QuestGiverComponent;
                        if (gorevVeren == null) {
                            continue;
                        }
                        string durum = gorevVeren.GetIndicatorType(oyuncu);
                        if (durum == "complete" && mesafe < teslimMesafe) {
                            teslim = aday;
                            teslimMesafe = mesafe;
                        } else if (durum == "available" && mesafe < yeniMesafe) {
                            yeni = aday;
                            yeniMesafe = mesafe;
                        }
                    }
                }

                // görev hedefleri
                if (hedefler.Count == 0 || mesafe >= amacMesafe) {
                    continue;
                }
                foreach (QuestObjective amacNesnesi in hedefler) {
                    bool uyuyor = false;
                    if (amacNesnesi is KillObjective) {
                        uyuyor = birim != null && canli && OldurmeHedefi(birim, amacNesnesi.ObjectiveName);
                    } else if (amacNesnesi is UseInteractableObjective) {
                        uyuyor = SystemDataUtility.MatchResource(aday.DisplayName, amacNesnesi.ObjectiveName);
                    }
                    if (uyuyor) {
                        amac = aday;
                        amacMesafe = mesafe;
                        amacAdi = amacNesnesi.DisplayName + " " + Mathf.Clamp(amacNesnesi.CurrentAmount(oyuncu), 0, amacNesnesi.Amount) + "/" + amacNesnesi.Amount;
                        break;
                    }
                }
            }

            if (teslim != null) {
                Sec(teslim, "Görevi teslim et: " + teslim.DisplayName);
            } else if (amac != null) {
                Sec(amac, amacAdi);
            } else if (yeni != null) {
                Sec(yeni, "Yeni görev: " + yeni.DisplayName);
            } else if (GunlukGorevler.SandikGoreviSuruyor && EnYakinSandik(oyuncu) is InteractableBase sandik) {
                Sec(sandik, "Hazine sandığı (günlük görev)");
            } else {
                hedef = null;
                hedefAdi = string.Empty;
            }
        }

        private void Sec(InteractableBase aday, string ad) {
            hedef = aday.transform;
            hedefAdi = ad;
            SonHedef = ad;
        }

        /// <summary>KillObjective'in kendi eşleştirmesiyle aynı: karakter adı, birim profili ya da topluluk</summary>
        private static bool OldurmeHedefi(UnitController birim, string hedefAdi) {
            if (string.IsNullOrEmpty(hedefAdi) || birim.BaseCharacter == null) {
                return false;
            }
            if (SystemDataUtility.MatchResource(birim.BaseCharacter.CharacterName, hedefAdi)) {
                return true;
            }
            if (birim.UnitProfile != null && SystemDataUtility.MatchResource(birim.UnitProfile.ResourceName, hedefAdi)) {
                return true;
            }
            return birim.BaseCharacter.Faction != null && SystemDataUtility.MatchResource(birim.BaseCharacter.Faction.ResourceName, hedefAdi);
        }

        private InteractableBase EnYakinSandik(UnitController oyuncu) {
            Vector3 p = oyuncu.transform.position;
            InteractableBase enYakin = null;
            float enAz = float.MaxValue;
            foreach (InteractableBase aday in adaylar) {
                if (aday == null || aday.Interactables == null) {
                    continue;
                }
                foreach (InteractableOptionComponent secenek in aday.Interactables.Values) {
                    ItemPickupComponent toplama = secenek as ItemPickupComponent;
                    if (toplama == null || Ganimet.IsTreasureChest(toplama.Props) == false) {
                        continue;
                    }
                    // boşaltılmış sandık yeniden dolana kadar görünmez ve açılamaz
                    if ((toplama.Props.SpawnObject != null && toplama.Props.SpawnObject.activeInHierarchy == false)
                        || toplama.GetValidOptionCount(oyuncu) == 0) {
                        continue;
                    }
                    float mesafe = Vector3.Distance(p, aday.transform.position);
                    if (mesafe < enAz) {
                        enAz = mesafe;
                        enYakin = aday;
                    }
                }
            }
            return enYakin;
        }

        // ---------------------------------------------------------------- ok resmi

        private static Sprite ucgen = null;

        /// <summary>yukarı bakan dolu üçgen (resim dosyası gerektirmez)</summary>
        private static Sprite Ucgen() {
            if (ucgen != null) {
                return ucgen;
            }
            const int boyut = 64;
            Texture2D doku = new Texture2D(boyut, boyut, TextureFormat.RGBA32, false);
            doku.wrapMode = TextureWrapMode.Clamp;
            Color32[] pikseller = new Color32[boyut * boyut];
            for (int y = 0; y < boyut; y++) {
                // üstte sivri, altta geniş
                float yarim = (boyut - 1 - y) * 0.5f * 0.92f;
                for (int x = 0; x < boyut; x++) {
                    float uzaklik = Mathf.Abs(x - (boyut - 1) * 0.5f);
                    float alfa = Mathf.Clamp01(yarim - uzaklik + 0.5f);
                    if (y < 4) {
                        alfa *= y / 4f;
                    }
                    pikseller[y * boyut + x] = new Color32(255, 255, 255, (byte)(alfa * 255f));
                }
            }
            doku.SetPixels32(pikseller);
            doku.Apply(false, true);
            ucgen = Sprite.Create(doku, new Rect(0f, 0f, boyut, boyut), new Vector2(0.5f, 0.5f), 100f);
            return ucgen;
        }
    }
}
