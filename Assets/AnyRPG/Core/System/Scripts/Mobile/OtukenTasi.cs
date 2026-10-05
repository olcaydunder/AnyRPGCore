using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Ötüken Taşı modelinin canlılığı (Tools~/dunya/otuken_tasi.py modeli kurar, bu bileşen prefabındadır):
    ///  - vurulunca kaya sarsılır, kristaller parlar, kristal kıymıkları sıçrar,
    ///  - kırılınca (birim ölünce) kristaller parçalanıp dağılır, yazının ışığı söner, geriye kırık kaya kalır,
    ///  - taşı kıran oyuncu (otomatik av açık değilse) ödülü kendiliğinden alır: kırıntıya yürür, ganimeti toplar,
    ///  - taş dönmez (savaşa girince saldırana dönmesin).
    /// Taşın kendisi bir AnyRPG birimidir ("Otuken Tasi"): canı, seviyesi, ganimeti onda; yeniden doğma
    /// haritadaki doğma noktasının işidir. Bu bileşen yalnız görünüşle ilgilenir; başsız sunucuda görüntü kurmaz.
    /// </summary>
    public class OtukenTasi : MonoBehaviour {

        public const string BirimAdi = "Otuken Tasi";

        private static readonly Color isik = new Color(0.25f, 0.92f, 0.86f, 1f);
        private static readonly int emisyon = Shader.PropertyToID("_EmissionColor");
        private static SystemGameManager oyun = null;

        private UnitController birim = null;
        private Transform kaya = null;
        private MeshRenderer kayaCizici = null;
        private MeshRenderer kristalCizici = null;
        private Mesh kristalOrgusu = null;
        private MaterialPropertyBlock blok = null;
        private Vector3 kayaYeri;
        private Quaternion donus;
        private bool donusAlindi = false;
        private int sonCan = -1;
        private bool kirildi = false;
        private float sarsintiBitis = 0f;
        private float parlamaBitis = 0f;
        private bool gorunum = true;
        private bool parliyor = false;
        private float oyuncuVurdu = -100f;   // yerel oyuncunun bu taşı en son hedef aldığı vuruş

        /// <summary>bu birim bir Ötüken Taşı mı (günlük görevler, otomatik av gibi yerler ayırt etmek için)</summary>
        public static bool TasMi(UnitController u) {
            return u != null && u.UnitProfile != null && u.UnitProfile.ResourceName == BirimAdi;
        }

        private void Awake() {
            gorunum = !(Application.isBatchMode && Sunucu.BatchSunucuOlsun);
            kaya = transform.Find("Kaya");
            Transform kristal = transform.Find("Kristal");
            if (kaya != null) {
                kayaCizici = kaya.GetComponent<MeshRenderer>();
                kayaYeri = kaya.localPosition;
            }
            if (kristal != null) {
                kristalCizici = kristal.GetComponent<MeshRenderer>();
                MeshFilter mf = kristal.GetComponent<MeshFilter>();
                kristalOrgusu = mf != null ? mf.sharedMesh : null;
            }
            blok = new MaterialPropertyBlock();
        }

        private bool Yetkili {
            get {
                if (oyun == null) {
                    return true;
                }
                return oyun.GameMode == GameMode.Local || (oyun.NetworkManagerServer != null && oyun.NetworkManagerServer.ServerModeActive);
            }
        }

        private void Update() {
            if (birim == null) {
                birim = GetComponentInParent<UnitController>();
                if (birim == null || birim.CharacterStats == null) {
                    return;
                }
                if (oyun == null) {
                    oyun = FindAnyObjectByType<SystemGameManager>();
                }
                sonCan = birim.CharacterStats.CurrentPrimaryResource;
                kirildi = birim.CharacterStats.IsAlive == false;
                if (kirildi) {
                    KirikGoster(false);
                }
                return;
            }
            CharacterStats st = birim.CharacterStats;
            if (st == null) {
                return;
            }
            int can = st.CurrentPrimaryResource;
            if (kirildi == false && st.IsAlive == false) {
                kirildi = true;
                KirikGoster(true);
                OdulAl();
            } else if (kirildi && st.IsAlive) {
                // dirilen taş (ender): yeniden bütün
                kirildi = false;
                Butun();
            } else if (kirildi == false && sonCan >= 0 && can < sonCan) {
                Vuruldu();
            }
            sonCan = can;

            if (gorunum == false || kaya == null) {
                return;
            }
            // sarsıntı ve parlama
            float simdi = Time.time;
            if (simdi < sarsintiBitis) {
                float g = (sarsintiBitis - simdi) / 0.25f;
                kaya.localPosition = kayaYeri + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * 0.045f * g;
            } else if (kirildi == false) {
                kaya.localPosition = kayaYeri;
            }
            if (kristalCizici != null && kirildi == false) {
                float p = simdi < parlamaBitis ? (parlamaBitis - simdi) / 0.15f : 0f;
                if (p > 0f) {
                    blok.Clear();
                    blok.SetColor(emisyon, isik * (1.4f + 3f * p));
                    kristalCizici.SetPropertyBlock(blok);
                    parliyor = true;
                } else if (parliyor) {
                    blok.Clear();
                    kristalCizici.SetPropertyBlock(blok);
                    parliyor = false;
                }
            }
        }

        private void LateUpdate() {
            // taş dönmez: savaşa girince saldırana dönmesin (yalnız taşı yöneten tarafta: tek oyunculu ya da sunucu)
            if (birim == null || Yetkili == false) {
                return;
            }
            if (donusAlindi == false) {
                donus = birim.transform.rotation;
                donusAlindi = true;
            }
            if (birim.transform.rotation != donus) {
                birim.transform.rotation = donus;
            }
        }

        private void Vuruldu() {
            if (gorunum == false) {
                return;
            }
            UnitController oyuncu = oyun != null && oyun.PlayerManagerClient != null ? oyun.PlayerManagerClient.UnitController : null;
            if (oyuncu != null && oyuncu.Target == (InteractableBase)birim) {
                oyuncuVurdu = Time.time;
            }
            sarsintiBitis = Time.time + 0.25f;
            parlamaBitis = Time.time + 0.15f;
            Kiymiklar(3, 0.07f, 2.2f, 0.7f);
        }

        private void KirikGoster(bool canli) {
            if (gorunum == false) {
                return;
            }
            if (canli) {
                Kiymiklar(12, 0.16f, 3.6f, 1.4f);
                MobileFeedback.Light();
            }
            if (kristalCizici != null) {
                kristalCizici.enabled = false;
            }
            if (kaya != null) {
                // kırık kaya: biraz çöker ve yana yatar, yazının ışığı söner
                kaya.localPosition = kayaYeri + Vector3.down * 0.12f;
                kaya.localScale = new Vector3(1.05f, 0.78f, 1f);
                kaya.localRotation = Quaternion.Euler(6f, 0f, -9f);
            }
            if (kayaCizici != null && kayaCizici.sharedMaterials.Length > 1) {
                MaterialPropertyBlock b = new MaterialPropertyBlock();
                b.SetColor(emisyon, Color.black);
                kayaCizici.SetPropertyBlock(b, 1);
            }
        }

        private void Butun() {
            if (gorunum == false) {
                return;
            }
            if (kristalCizici != null) {
                kristalCizici.enabled = true;
            }
            if (kaya != null) {
                kaya.localPosition = kayaYeri;
                kaya.localScale = Vector3.one;
                kaya.localRotation = Quaternion.identity;
            }
            if (kayaCizici != null) {
                kayaCizici.SetPropertyBlock(new MaterialPropertyBlock(), 1);
            }
        }

        /// <summary>kristal kıymıkları: kristal örgüsünün küçük kopyaları sıçrar, döner, küçülüp kaybolur</summary>
        private void Kiymiklar(int adet, float boy, float hiz, float omur) {
            if (kristalOrgusu == null || kristalCizici == null) {
                return;
            }
            Bounds b = kristalCizici.bounds;
            List<Transform> parcalar = new List<Transform>();
            List<Vector3> hizlar = new List<Vector3>();
            List<Vector3> donmeler = new List<Vector3>();
            for (int i = 0; i < adet; i++) {
                GameObject p = new GameObject("Kiymik");
                p.AddComponent<MeshFilter>().sharedMesh = kristalOrgusu;
                MeshRenderer r = p.AddComponent<MeshRenderer>();
                r.sharedMaterial = kristalCizici.sharedMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Vector3 yer = new Vector3(Random.Range(b.min.x, b.max.x), Random.Range(b.center.y, b.max.y), Random.Range(b.min.z, b.max.z));
                p.transform.position = yer;
                p.transform.rotation = Random.rotation;
                p.transform.localScale = Vector3.one * boy * Random.Range(0.6f, 1.2f);
                Vector3 disari = yer - new Vector3(b.center.x, b.min.y, b.center.z);
                disari.y = Mathf.Abs(disari.y) + 0.6f;
                hizlar.Add(disari.normalized * hiz * Random.Range(0.6f, 1.1f));
                donmeler.Add(Random.insideUnitSphere * 540f);
                parcalar.Add(p.transform);
                Destroy(p, omur + 0.2f);
            }
            StartCoroutine(Ucur(parcalar, hizlar, donmeler, omur));
        }

        private static IEnumerator Ucur(List<Transform> parcalar, List<Vector3> hizlar, List<Vector3> donmeler, float omur) {
            float t = 0f;
            Vector3[] ilkBoy = new Vector3[parcalar.Count];
            for (int i = 0; i < parcalar.Count; i++) {
                ilkBoy[i] = parcalar[i].localScale;
            }
            while (t < omur) {
                float dt = Time.deltaTime;
                t += dt;
                float kalan = 1f - Mathf.Clamp01((t - omur * 0.55f) / (omur * 0.45f));
                for (int i = 0; i < parcalar.Count; i++) {
                    if (parcalar[i] == null) {
                        continue;
                    }
                    hizlar[i] += Vector3.down * 9.8f * dt;
                    parcalar[i].position += hizlar[i] * dt;
                    parcalar[i].Rotate(donmeler[i] * dt, Space.World);
                    parcalar[i].localScale = ilkBoy[i] * kalan;
                }
                yield return null;
            }
        }

        /// <summary>taşı kıran yerel oyuncu ödülü kendiliğinden alsın (otomatik av açıkken av zaten toplar)</summary>
        private void OdulAl() {
            if (gorunum == false || oyun == null || oyun.PlayerManagerClient == null || OtomatikAv.Acik) {
                return;
            }
            UnitController oyuncu = oyun.PlayerManagerClient.UnitController;
            PlayerController kontrol = oyun.PlayerManagerClient.PlayerController;
            if (oyuncu == null || kontrol == null || oyuncu.CharacterStats == null || oyuncu.CharacterStats.IsAlive == false) {
                return;
            }
            bool hedefti = oyuncu.Target == (InteractableBase)birim || Time.time - oyuncuVurdu < 8f;
            // okçu ve büyücüler taşı uzaktan kırar: menzil kadar uzaktan da ödüle yürünür
            if (hedefti == false || Vector3.Distance(oyuncu.transform.position, birim.transform.position) > 45f) {
                return;
            }
            StartCoroutine(OdulAlGec(kontrol, oyuncu));
        }

        private IEnumerator OdulAlGec(PlayerController kontrol, UnitController oyuncu) {
            // ganimet ölümden hemen sonra hazırlanır; kıymıklar uçarken kırıntıya yürünür
            yield return new WaitForSeconds(0.6f);
            if (birim == null || oyuncu == null || oyuncu.CharacterStats == null || oyuncu.CharacterStats.IsAlive == false) {
                yield break;
            }
            if (oyuncu.CharacterCombat != null && oyuncu.CharacterCombat.GetInCombat()) {
                // başka bir düşmanla savaştaysa savaşı bölme
                UnitController hedef = oyuncu.Target as UnitController;
                if (hedef != null && hedef != birim && hedef.CharacterStats != null && hedef.CharacterStats.IsAlive) {
                    yield break;
                }
            }
            kontrol.RightMouseInteraction(birim);
        }
    }
}
