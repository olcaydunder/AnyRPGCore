using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Savaş efektleri (Ötüken): vuruşun, kritiğin, becerinin, kaçınmanın, ölümün ve seviye atlamanın ışıkları.
    ///  - Vuruş: isabet noktasında parlama, kılıç yayı ve saçılan kıvılcımlar. Kritik turuncu, daha büyük ve yerde
    ///    şok halkasıyla; beceri vuruşu mavi-beyaz ve halkalı.
    ///  - Oyuncu vurulunca kısa kızıl parlama; kaçınınca ayak altında toz ve arkasında ışık izi.
    ///  - Düşman ölünce yükselen mor ışıklar; seviye atlayınca altın halka ve ışık sütunu; şifa alınca yeşil ışıklar.
    /// Beş parçacık sistemi kodla bir kez kurulur (prefab yok) ve her efekt onlara tek tek parçacık yayar
    /// (ParticleSystem.Emit): nesne yaratılıp silinmez, telefonu yormaz. Malzemeler Resources/Efekt içindedir
    /// (Tools~/logo/efekt_dokulari.py). Yalnız görüntüdür; oyun mantığına dokunmaz, ekransız sunucuda hiçbir şey yapmaz.
    /// Çağıranlar: PlayerManagerClient.HandleReceiveCombatTextEvent, UnitMovementController.KacinmaGuncelle,
    /// MobileBootstrap (Tick: oyuncunun öldürmelerini dinler).
    /// </summary>
    public static class Efektler {

        private const string AcikKey = "savas-efektleri";

        private static readonly Color vurusRengi = new Color(1f, 0.93f, 0.74f, 1f);
        private static readonly Color kritikRengi = new Color(1f, 0.56f, 0.14f, 1f);
        private static readonly Color beceriRengi = new Color(0.55f, 0.85f, 1f, 1f);
        private static readonly Color yaraRengi = new Color(1f, 0.25f, 0.2f, 1f);
        private static readonly Color izRengi = new Color(0.65f, 0.88f, 1f, 0.75f);
        private static readonly Color tozRengi = new Color(0.82f, 0.76f, 0.66f, 0.5f);
        private static readonly Color olumRengi = new Color(0.78f, 0.62f, 1f, 1f);
        private static readonly Color altinRengi = new Color(1f, 0.82f, 0.32f, 1f);
        private static readonly Color sifaRengi = new Color(0.45f, 1f, 0.5f, 1f);

        private static bool kullanilamaz = false;
        private static ParticleSystem isik = null;
        private static ParticleSystem kivilcim = null;
        private static ParticleSystem yay = null;
        private static ParticleSystem halka = null;
        private static ParticleSystem toz = null;
        private static EfektIzi iz = null;
        private static UnitController izlenen = null;
        private static float sonSifa = -10f;
        private static bool hataYazildi = false;

        /// <summary>bu oturumda yayılan parçacık sayısı (oyun testi okur)</summary>
        public static int YayilanSayisi = 0;

        public static bool Acik {
            get { return PlayerPrefs.GetInt(AcikKey, 1) == 1; }
            set { PlayerPrefs.SetInt(AcikKey, value ? 1 : 0); }
        }

        // ---------------------------------------------------------------- kurulum

        private static bool Hazir() {
            if (kullanilamaz || Acik == false) {
                return false;
            }
            if (isik != null && kivilcim != null && yay != null && halka != null && toz != null) {
                return true;
            }
            // ekransız sunucu
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) {
                kullanilamaz = true;
                return false;
            }
            Material isikMalzemesi = Resources.Load<Material>("Efekt/Isik");
            Material yayMalzemesi = Resources.Load<Material>("Efekt/Yay");
            Material halkaMalzemesi = Resources.Load<Material>("Efekt/Halka");
            Material tozMalzemesi = Resources.Load<Material>("Efekt/Toz");
            if (isikMalzemesi == null || yayMalzemesi == null || halkaMalzemesi == null || tozMalzemesi == null) {
                kullanilamaz = true;
                Debug.LogWarning("Efektler: malzemeler bulunamadı (Resources/Efekt), savaş efektleri kapalı");
                return false;
            }
            GameObject kok = new GameObject("[Efektler]");
            Object.DontDestroyOnLoad(kok);
            iz = kok.AddComponent<EfektIzi>();
            isik = Sistem(kok.transform, "Isik", isikMalzemesi, ParticleSystemRenderMode.Billboard, 0f, 0.65f, 1.35f, 300);
            kivilcim = Sistem(kok.transform, "Kivilcim", isikMalzemesi, ParticleSystemRenderMode.Stretch, 1.6f, 1f, 0.25f, 400);
            yay = Sistem(kok.transform, "Yay", yayMalzemesi, ParticleSystemRenderMode.Billboard, 0f, 0.55f, 1.3f, 60);
            halka = Sistem(kok.transform, "Halka", halkaMalzemesi, ParticleSystemRenderMode.HorizontalBillboard, 0f, 0.15f, 1f, 40);
            toz = Sistem(kok.transform, "Toz", tozMalzemesi, ParticleSystemRenderMode.Billboard, -0.05f, 0.5f, 1.6f, 120);
            return true;
        }

        /// <summary>
        /// kendiliğinden parçacık yaymayan, hep çalışan bir sistem; parçacıklar Emit ile tek tek verilir.
        /// Boyut ömür boyunca ilkBoyut → sonBoyut katına gider, saydamlık sona doğru söner.
        /// </summary>
        private static ParticleSystem Sistem(Transform kok, string ad, Material malzeme, ParticleSystemRenderMode mod, float yercekimi,
            float ilkBoyut, float sonBoyut, int enCok) {
            GameObject go = new GameObject(ad);
            go.transform.SetParent(kok, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule ana = ps.main;
            ana.loop = true;
            ana.playOnAwake = false;
            ana.simulationSpace = ParticleSystemSimulationSpace.World;
            ana.maxParticles = enCok;
            ana.startSpeed = 0f;
            ana.startLifetime = 0.3f;
            ana.startSize = 1f;
            ana.gravityModifier = yercekimi;

            ParticleSystem.EmissionModule yayim = ps.emission;
            yayim.enabled = false;
            ParticleSystem.ShapeModule bicim = ps.shape;
            bicim.enabled = false;

            ParticleSystem.ColorOverLifetimeModule renk = ps.colorOverLifetime;
            renk.enabled = true;
            Gradient gecis = new Gradient();
            gecis.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.35f), new GradientAlphaKey(0f, 1f) });
            renk.color = new ParticleSystem.MinMaxGradient(gecis);

            ParticleSystem.SizeOverLifetimeModule boyut = ps.sizeOverLifetime;
            boyut.enabled = true;
            float enBuyuk = Mathf.Max(ilkBoyut, sonBoyut);
            boyut.size = new ParticleSystem.MinMaxCurve(enBuyuk, AnimationCurve.EaseInOut(0f, ilkBoyut / enBuyuk, 1f, sonBoyut / enBuyuk));

            ParticleSystemRenderer cizici = go.GetComponent<ParticleSystemRenderer>();
            cizici.sharedMaterial = malzeme;
            cizici.renderMode = mod;
            cizici.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cizici.receiveShadows = false;
            if (mod == ParticleSystemRenderMode.Stretch) {
                cizici.velocityScale = 0.1f;
                cizici.lengthScale = 1.2f;
            }
            ps.Play();
            return ps;
        }

        private static void Parcacik(ParticleSystem ps, Vector3 konum, Vector3 hiz, float boyut, float omur, Color renk, float donus = 0f) {
            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            p.position = konum;
            p.velocity = hiz;
            p.startSize = boyut;
            p.startLifetime = omur;
            p.startColor = renk;
            p.rotation = donus;
            ps.Emit(p, 1);
            YayilanSayisi++;
        }

        private static void Hata(System.Exception e) {
            // görüntü oyunu bozmasın: bir kez yazılır
            if (hataYazildi == false) {
                hataYazildi = true;
                Debug.LogWarning("Efektler: " + e.Message);
            }
        }

        private static Vector3 Govde(InteractableBase birim) {
            if (birim.Collider != null) {
                return birim.Collider.bounds.center;
            }
            return birim.transform.position + Vector3.up;
        }

        private static float Yaricap(InteractableBase birim) {
            if (birim.Collider == null) {
                return 0.4f;
            }
            Vector3 e = birim.Collider.bounds.extents;
            return Mathf.Max(e.x, e.z);
        }

        // ---------------------------------------------------------------- vuruş

        /// <summary>oyuncunun bir düşmana vuruşu</summary>
        public static void Vurus(InteractableBase hedef, InteractableBase vuran, bool kritik, bool beceri) {
            if (hedef == null) {
                return;
            }
            try {
                Vector3 merkez = Govde(hedef);
                Vector3 yon = vuran != null ? hedef.transform.position - vuran.transform.position : Vector3.forward;
                yon.y = 0f;
                yon = yon.sqrMagnitude > 0.0001f ? yon.normalized : Vector3.forward;
                // isabet noktası: gövdenin vurana bakan yüzü
                Vector3 nokta = merkez - yon * (Yaricap(hedef) * 0.7f);
                Vector3 zemin = new Vector3(merkez.x, hedef.transform.position.y + 0.08f, merkez.z);
                VurusNoktasi(nokta, yon, zemin, kritik, beceri);
            } catch (System.Exception e) {
                Hata(e);
            }
        }

        /// <param name="yon">vuruşun yönü (vurandan hedefe, yatay)</param>
        public static void VurusNoktasi(Vector3 nokta, Vector3 yon, Vector3 zemin, bool kritik, bool beceri) {
            if (Hazir() == false) {
                return;
            }
            Color renk = kritik ? kritikRengi : (beceri ? beceriRengi : vurusRengi);
            float guc = kritik ? 1.6f : (beceri ? 1.25f : 1f);
            // parlama ve savurma yayı
            Parcacik(isik, nokta, Vector3.zero, 1.7f * guc, 0.16f, renk);
            Parcacik(isik, nokta, Vector3.zero, 0.8f * guc, 0.22f, Color.white);
            Parcacik(yay, nokta, Vector3.zero, 1.9f * guc, 0.2f, renk, Random.Range(0f, 360f));
            if (kritik) {
                Parcacik(yay, nokta, Vector3.zero, 2.5f, 0.24f, Color.white, Random.Range(0f, 360f));
            }
            // kıvılcımlar: vuruş yönüne ve yukarı doğru saçılır, yere düşer
            int adet = kritik ? 24 : (beceri ? 16 : 12);
            for (int i = 0; i < adet; i++) {
                Vector3 sacilma = (Random.onUnitSphere + yon * 0.9f + Vector3.up * 0.6f).normalized;
                Parcacik(kivilcim, nokta, sacilma * Random.Range(3.5f, 8f) * guc, Random.Range(0.18f, 0.34f) * guc,
                    Random.Range(0.25f, 0.5f), i % 3 == 0 ? Color.white : renk);
            }
            if (kritik || beceri) {
                Parcacik(halka, zemin, Vector3.zero, kritik ? 5f : 3.6f, 0.35f, renk);
            }
        }

        /// <summary>oyuncu vuruldu: gövdesinde kısa kızıl parlama</summary>
        public static void OyuncuVuruldu(UnitController oyuncu) {
            if (oyuncu == null || Hazir() == false) {
                return;
            }
            try {
                Vector3 merkez = Govde(oyuncu);
                Parcacik(isik, merkez, Vector3.zero, 1.5f, 0.16f, yaraRengi);
                for (int i = 0; i < 5; i++) {
                    Parcacik(kivilcim, merkez, (Random.onUnitSphere + Vector3.up * 0.5f).normalized * Random.Range(2f, 4.5f),
                        Random.Range(0.08f, 0.14f), Random.Range(0.2f, 0.35f), yaraRengi);
                }
            } catch (System.Exception e) {
                Hata(e);
            }
        }

        // ---------------------------------------------------------------- kaçınma

        /// <summary>kaçınma hamlesi başladı: ayak altında toz, yerde halka, hamle boyunca arkada ışık izi</summary>
        public static void Kacinma(UnitController oyuncu) {
            if (oyuncu == null || Hazir() == false) {
                return;
            }
            try {
                Vector3 ayak = oyuncu.transform.position + Vector3.up * 0.1f;
                for (int i = 0; i < 9; i++) {
                    Vector2 cember = Random.insideUnitCircle.normalized;
                    Vector3 hiz = new Vector3(cember.x, 0.35f, cember.y) * Random.Range(1.2f, 2.8f);
                    Parcacik(toz, ayak, hiz, Random.Range(0.7f, 1.2f), Random.Range(0.4f, 0.65f), tozRengi, Random.Range(0f, 360f));
                }
                Parcacik(halka, ayak, Vector3.zero, 2.6f, 0.3f, izRengi);
                if (iz != null) {
                    iz.Baslat(oyuncu.transform, SavasAyarlari.KacinmaSuresi + 0.08f);
                }
            } catch (System.Exception e) {
                Hata(e);
            }
        }

        /// <summary>EfektIzi her karede çağırır: gövde ve bacak hizasında birer ışık bırakır</summary>
        internal static void IzBirak(Vector3 konum) {
            if (Hazir() == false) {
                return;
            }
            Parcacik(isik, konum + Vector3.up * 1.05f + Random.insideUnitSphere * 0.12f, Vector3.zero, 1f, 0.32f, izRengi);
            Parcacik(isik, konum + Vector3.up * 0.45f + Random.insideUnitSphere * 0.12f, Vector3.zero, 0.75f, 0.26f, izRengi);
        }

        // ---------------------------------------------------------------- ölüm, seviye, şifa

        /// <summary>düşman öldü: gövdesinden yükselen ışıklar ve bir toz bulutu</summary>
        public static void OlumNoktasi(Vector3 merkez) {
            if (Hazir() == false) {
                return;
            }
            Parcacik(isik, merkez, Vector3.zero, 2.4f, 0.3f, olumRengi);
            for (int i = 0; i < 12; i++) {
                Vector3 yer = merkez + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.6f, 0.4f), Random.Range(-0.5f, 0.5f));
                Vector3 hiz = new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(1.4f, 3.2f), Random.Range(-0.4f, 0.4f));
                Parcacik(isik, yer, hiz, Random.Range(0.22f, 0.5f), Random.Range(0.7f, 1.2f), i % 4 == 0 ? Color.white : olumRengi);
            }
            for (int i = 0; i < 6; i++) {
                Vector2 cember = Random.insideUnitCircle;
                Parcacik(toz, merkez + Vector3.down * 0.5f, new Vector3(cember.x, 0.4f, cember.y) * 1.4f, Random.Range(0.9f, 1.5f),
                    Random.Range(0.5f, 0.8f), tozRengi, Random.Range(0f, 360f));
            }
        }

        /// <summary>seviye atlandı: yerde büyüyen altın halkalar, yükselen ışık sütunu ve kıvılcımlar</summary>
        public static void SeviyeAtladi(UnitController oyuncu) {
            if (oyuncu == null || Hazir() == false) {
                return;
            }
            try {
                Vector3 ayak = oyuncu.transform.position + Vector3.up * 0.1f;
                Parcacik(halka, ayak, Vector3.zero, 7f, 0.7f, altinRengi);
                Parcacik(halka, ayak, Vector3.zero, 4.5f, 0.5f, Color.white);
                for (int i = 0; i < 10; i++) {
                    Parcacik(isik, ayak + Vector3.up * (i * 0.28f), Vector3.up * Random.Range(1.5f, 3f), Random.Range(1.2f, 1.9f),
                        Random.Range(0.5f, 0.9f), altinRengi);
                }
                for (int i = 0; i < 36; i++) {
                    float aci = i * (Mathf.PI * 2f / 36f);
                    Vector3 cember = new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci));
                    Parcacik(kivilcim, ayak + cember * 0.7f, cember * Random.Range(0.5f, 2f) + Vector3.up * Random.Range(5f, 10f),
                        Random.Range(0.12f, 0.22f), Random.Range(0.5f, 0.9f), i % 3 == 0 ? Color.white : altinRengi);
                }
            } catch (System.Exception e) {
                Hata(e);
            }
        }

        /// <summary>şifa alındı (iksir, büyü): gövdeden yükselen yeşil ışıklar; saniyede en çok iki kez</summary>
        public static void Sifa(UnitController oyuncu) {
            if (oyuncu == null || Time.unscaledTime - sonSifa < 0.5f || Hazir() == false) {
                return;
            }
            sonSifa = Time.unscaledTime;
            try {
                Vector3 ayak = oyuncu.transform.position;
                for (int i = 0; i < 8; i++) {
                    Vector2 cember = Random.insideUnitCircle * 0.6f;
                    Parcacik(isik, ayak + new Vector3(cember.x, Random.Range(0.2f, 1.2f), cember.y), Vector3.up * Random.Range(1f, 2.2f),
                        Random.Range(0.25f, 0.45f), Random.Range(0.6f, 0.9f), sifaRengi);
                }
                Parcacik(halka, ayak + Vector3.up * 0.1f, Vector3.zero, 2.4f, 0.4f, sifaRengi);
            } catch (System.Exception e) {
                Hata(e);
            }
        }

        // ---------------------------------------------------------------- olaylar

        /// <summary>MobileBootstrap saniyede bir çağırır: oyuncunun öldürmelerini dinler</summary>
        public static void Tick(SystemGameManager oyun, bool oyunda) {
            UnitController oyuncu = oyunda && oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu == izlenen) {
                return;
            }
            if (izlenen != null && izlenen.UnitEventController != null) {
                izlenen.UnitEventController.OnKillEvent -= Oldurdu;
            }
            izlenen = oyuncu;
            if (izlenen != null && izlenen.UnitEventController != null) {
                izlenen.UnitEventController.OnKillEvent += Oldurdu;
            }
        }

        private static void Oldurdu(UnitController olduren, UnitController olen, float pay) {
            if (olen == null) {
                return;
            }
            try {
                OlumNoktasi(Govde(olen));
            } catch (System.Exception e) {
                Hata(e);
            }
        }

        /// <summary>oyun testi: parçacıkları verilen süre kadar ilerletir (görüntü almadan önce) ve canlı parçacıkları sayar</summary>
        public static int TestIlerlet(float sure) {
            if (Hazir() == false) {
                return -1;
            }
            int canli = 0;
            foreach (ParticleSystem ps in new ParticleSystem[] { isik, kivilcim, yay, halka, toz }) {
                ps.Simulate(sure, true, false);
                canli += ps.particleCount;
                ps.Play();
            }
            return canli;
        }
    }

    /// <summary>kaçınma hamlesi boyunca oyuncunun arkasında ışık izi bırakır (Efektler kurar)</summary>
    public class EfektIzi : MonoBehaviour {

        private Transform hedef = null;
        private float bitis = -1f;

        public void Baslat(Transform izlenecek, float sure) {
            hedef = izlenecek;
            bitis = Time.time + sure;
            enabled = true;
        }

        private void Update() {
            if (hedef == null || Time.time >= bitis) {
                hedef = null;
                enabled = false;
                return;
            }
            Efektler.IzBirak(hedef.position);
        }
    }
}
