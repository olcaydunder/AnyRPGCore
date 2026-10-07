using UnityEngine;

namespace AnyRPG {

    /// <summary>
    /// Ötüken savaş hissi (oyuncu karakterleri):
    ///  - Normal saldırı animasyonu %35 hızlı oynar (vuruş aralığı silah türüne göre WeaponSkill attackSpeed: 0,8–1,5 sn)
    ///  - Yakın dövüş menzili açıdan bağımsız: hedef gövdeye ~1,9 m'den yakınsa vuruş geçer (yalnız önündeki kutu değil)
    ///  - Saldırırken karakter hedefe döner (hareket simülasyonunun içinde: telefon ve sunucu aynı sonucu bulur)
    ///  - Hedefe yürürken yakın dövüşte 5 m'de değil vurabileceği mesafede durur
    /// Canavarlar değişmez (yapay zekâları zaten hedefe döner).
    /// </summary>
    public static class SavasAyarlari {

        public const float OyuncuSaldiriAnimasyonHizi = 1.35f;
        // karakterin önündeki vuruş kutusu dışında gövdeden gövdeye en çok bu kadar uzaktaki hedefe vurulur
        public const float YakinErisimPayi = 0.55f;
        // derece/saniye
        private const float DonusHizi = 900f;
        private const float DonmeMenzili = 12f;

        public static bool Oyuncu(UnitController uc) {
            return uc != null && uc.UnitControllerMode == UnitControllerMode.Player;
        }

        /// <summary>animasyon hız çarpanı (yalnız oyuncunun normal saldırısı)</summary>
        public static float AnimasyonCarpani(UnitController uc, AbilityProperties yetenek) {
            if (yetenek == null || yetenek.IsAutoAttack == false || Oyuncu(uc) == false) {
                return 1f;
            }
            return OyuncuSaldiriAnimasyonHizi;
        }

        private static float YatayYaricap(Collider c) {
            if (c == null) {
                return 0.4f;
            }
            Vector3 e = c.bounds.extents;
            return Mathf.Max(e.x, e.z);
        }

        /// <summary>iki gövde arasındaki yatay boşluk (gövde yüzeyleri arası, metre)</summary>
        public static float GovdeArasi(UnitController uc, InteractableBase hedef) {
            if (uc == null || hedef == null || hedef.InteractableGameObject == null) {
                return float.MaxValue;
            }
            Vector3 a = uc.transform.position;
            Vector3 b = hedef.InteractableGameObject.transform.position;
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b) - YatayYaricap(uc.Collider) - YatayYaricap(hedef.Collider);
        }

        /// <summary>yakın dövüş vuruşu için açıdan bağımsız menzil (oyuncu)</summary>
        public static bool YakinMenzilde(UnitController uc, InteractableBase hedef) {
            if (Oyuncu(uc) == false || hedef == null) {
                return false;
            }
            CharacterUnit birim = uc.CharacterUnit;
            float erisim = (birim != null ? birim.HitBoxSize : 1.2f) + YakinErisimPayi;
            return GovdeArasi(uc, hedef) <= erisim;
        }

        /// <summary>yakın dövüşte hedefe yürürken durulacak merkezden merkeze uzaklık</summary>
        public static float TakipMesafesi(UnitController uc, InteractableBase hedef) {
            CharacterUnit birim = uc != null ? uc.CharacterUnit : null;
            float erisim = (birim != null ? birim.HitBoxSize : 1.2f) + YakinErisimPayi;
            // biraz içeride dur ki küçük kaymalarda menzil dışına çıkmasın
            return YatayYaricap(uc != null ? uc.Collider : null) + YatayYaricap(hedef != null ? hedef.Collider : null) + erisim * 0.7f;
        }

        /// <summary>
        /// Duran oyuncu, hedefi canlı bir düşmansa ve yakınsa ona döner (MovementIdleState her tikte çağırır).
        /// Telefonda tahmin, sunucuda asıl simülasyon aynı veriyle çalışır; küçük farkı uzlaştırma düzeltir.
        /// </summary>
        public static void HedefeDon(UnitController uc, double sure) {
            if (Oyuncu(uc) == false || uc.IsMounted || uc.UnitMotor == null || uc.UnitMotor.MovementBody == null) {
                return;
            }
            InteractableBase hedef = uc.Target;
            if (hedef == null || hedef.InteractableGameObject == null) {
                return;
            }
            UnitController hedefBirim = hedef as UnitController;
            if (hedefBirim == null) {
                CharacterUnit cu = CharacterUnit.GetCharacterUnit(hedef);
                hedefBirim = cu != null ? cu.UnitController : null;
            }
            if (hedefBirim == null || hedefBirim == uc || hedefBirim.CharacterStats == null || hedefBirim.CharacterStats.IsAlive == false) {
                return;
            }
            if (uc.BaseCharacter == null || Faction.RelationWith(hedefBirim, uc.BaseCharacter.Faction) > -1) {
                return;
            }
            Vector3 konum = uc.UnitMotor.MovementBody.GetPosition();
            Vector3 yon = hedefBirim.transform.position - konum;
            yon.y = 0f;
            if (yon.sqrMagnitude < 0.04f || yon.sqrMagnitude > DonmeMenzili * DonmeMenzili) {
                return;
            }
            Quaternion simdiki = uc.UnitMotor.MovementBody.GetRotation();
            Quaternion istenen = Quaternion.LookRotation(yon.normalized);
            if (Quaternion.Angle(simdiki, istenen) < 2f) {
                return;
            }
            Quaternion yeni = Quaternion.RotateTowards(simdiki, istenen, DonusHizi * (float)sure);
            uc.UnitMotor.FaceDirection(yeni * Vector3.forward);
        }
    }
}
