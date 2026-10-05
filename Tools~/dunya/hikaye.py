#!/usr/bin/env python3
"""Ötüken Destanı'nın ana hikâyesi: 15 haritayı baştan sona geçen görev zinciri.

Erlik Han Tamu'nun mührünü kırdı, kara ruhları on beş diyara saçıldı. Olcayto Han oyuncuyu yola çıkarır;
her diyarda bir yardımcı (görev veren) vardır: önce o diyarın düşmanlarını dağıtırsın (ve Ötüken Taşı kırarsın),
sonra Erlik'in o diyardaki başbuğunu (boss) yenersin, yardımcı seni bir sonraki diyara gönderir. Sonunda Tamu
Zindanı'nda Erlik Han'ı yenip Ötüken'e, Olcayto Han'a dönersin.

Yazdıkları:
  - Quest/Destan/*.asset             görevler (her harita: varış, av, boss; sonda Erlik Han ve dönüş)
  - UnitProfile/Hikaye/*.asset        her haritanın görev veren yardımcısı (Olcayto Han'ın kopyası, başka model)
  - UnitProfile/QuestNPCUnit.asset    Olcayto Han'a ilk görev, Umay'a yolculuk ve dönüşün teslimi eklenir
  - Assets/Otuken/Editor/HikayeVerisi.cs   HaritaDoldur'un her haritaya koyacağı yardımcı ve boss
Boss'ların profilleri yaratiklar.py'dedir (DUSMANLAR, "ana hikâyenin boss'ları").

Betik tekrar çalıştırılabilir; GUID'ler addan türetilir.
Kullanım (depo kökünden): python3 "Tools~/dunya/hikaye.py"
"""
import json
import re
import uuid
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RES = ROOT / "Assets/AnyRPG/Core/Games/FeaturesDemoGame/Resources/FeaturesDemoGame"
QUEST_DIR = RES / "Quest" / "Destan"
NPC_DIR = RES / "UnitProfile" / "Hikaye"
NS = uuid.UUID("9d3e2c4a-5b61-4f0e-8a1d-2f7c6b5e4d30")
q = lambda s: json.dumps(s, ensure_ascii=True)

# deneyim tablosu (GameManager.prefab experienceChart): seviye -> o seviyeyi bitirmek için gereken
XP = [500, 1000, 1500, 2000, 2500, 3500, 4500, 5500, 6500, 7500, 8500, 10000, 11500, 13000, 14500, 16000, 17500,
      19000, 21000, 23000, 25000, 27000, 29000, 31500, 34000, 36500, 39000, 41500, 44000, 47000, 50000, 54000]

# yardımcıların modelleri: (kaynak profil dosyası) -> modelPrefab guid ve simge oradan alınır
# harita: sahne, SceneNode adı, görünen ad, yardımcı (kaynak, profil adı, görünen ad, tanım),
#         av (hedef profil, görünen, adet, taş), boss (profil, görünen, yeni mi), görev adları ve metinleri
HARITALAR = [
    dict(sahne="FeaturesDemoZone", dugum="Features Demo Zone", ad="Ötüken Yaylası", yardimci=None,
         av=("Cali Cini", "Çalı Cini", 6, 3), boss=None,
         av_ad="Kutun Çağrısı",
         av_metin="Erlik Han, Tamu'nun mührünü kırdı; kara ruhları on beş diyara saçıldı ve Ötüken'in kutu tehlikede. "
                  "Önce kendini göster, alp: yaylaya inen Çalı Cinlerini dağıt, Ötüken Taşlarını kırıp içlerindeki kutu topla."),
    dict(sahne="UmayTarlalari", dugum="UmayTarlalari", ad="Umay Tarlaları",
         yardimci=("DanceNPCUnit", "Destan Ak Ana", "Ak Ana", "Umay Ana'nın kamı. Tarlaları ve obanın çocuklarını kollar."),
         varis_metin="Umay Ana'nın tarlalarından kara haberler geliyor. Geçit Taşı'ndan Umay Tarlaları'na geç ve "
                     "Umay'ın kamı Ak Ana'yı bul.",
         av=("Cali Cini", "Çalı Cini", 8, 2), av_ad="Ekinleri Kurtar",
         av_metin="Çalı Cinleri ekinleri talan ediyor. Onları tarladan kov ve Ötüken Taşı kır ki Umay'ın kutu toprağa dönsün.",
         boss=("Alkarasi", "Alkarası"),
         boss_metin="Bütün bu kötülüğün başı Alkarası. Tarlaların en uzak ucunda, kuruyan ekinlerin arasında bekliyor. "
                    "Onu yen; Umay Ana'nın kutu seninle olsun."),
    dict(sahne="BoruTepesi", dugum="BoruTepesi", ad="Börü Tepesi",
         yardimci=("LoopPatrolNPCUnit", "Destan Tonga", "Börü Alp Tonga", "Bozkurdun soyundan gelen alp. Tepeyi kurtlarıyla korur."),
         varis_metin="Alkarası düşerken Kara Toygar'ın adını andı. Börü Tepesi'ne git, Börü Alp Tonga'yı bul.",
         av=("Agulu Kormos", "Ağulu Körmös", 5, 2), av_ad="Tepenin Ağusu",
         av_metin="Ağulu Körmösler tepenin pınarlarını zehirledi. Kurtlar susuz kaldı; körmösleri temizle.",
         boss=("Kara Toygar", "Kara Toygar"),
         boss_metin="Yağmacıların beyi Kara Toygar tepenin doruğuna kurulmuş. Onu yen, yollar yeniden açılsın."),
    dict(sahne="AkDenizKiyisi", dugum="AkDenizKiyisi", ad="Ak Deniz Kıyısı",
         yardimci=("RandomPatrolNPCUnit", "Destan Aybars", "Kıyı Beyi Aybars", "Ak Deniz kıyısındaki obaların beyi."),
         varis_metin="Kara Toygar'ın yağmacıları kıyıya kaçtı. Ak Deniz Kıyısı'na git, Kıyı Beyi Aybars'ı bul.",
         av=("Yagmaci Okcu", "Yağmacı Okçu", 6, 2), av_ad="Kıyıdaki Yağmacılar",
         av_metin="Yağmacı okçular balıkçı teknelerini yakıyor. Kıyıyı onlardan temizle.",
         boss=("Tengiz Kormosu", "Tengiz Körmösü"),
         boss_metin="Erlik denizin dibinden bir dev uyandırdı: Tengiz Körmösü. Kıyının en uzak kayalıklarında dalgaları döver; onu yen."),
    dict(sahne="OrdubalikCarsisi", dugum="OrdubalikCarsisi", ad="Ordubalık Çarşısı",
         yardimci=("VendorNPCUnit", "Destan Bilge Kul", "Çarşı Ağası Bilge Kül", "Ordubalık çarşısının ağası. Her kervanı tanır."),
         varis_metin="Tengiz Körmösü'nün kabuğunda Ordubalık'ın mührü vardı. Ordubalık Çarşısı'na git, Çarşı Ağası Bilge Kül'ü bul.",
         av=("Sulmus", "Şulmus", 6, 0), av_ad="Çarşının Gölgeleri",
         av_metin="Şulmuslar gölgelerden fırlayıp tüccarları soyuyor. Çarşıyı onlardan temizle.",
         boss=("Kara Kuzgun", "Kara Kuzgun"),
         boss_metin="Şulmusları yöneten hırsızbaşı Kara Kuzgun. Çarşının arka sokaklarında saklanıyor; onu yakala."),
    dict(sahne="OrdubalikKenti", dugum="OrdubalikKenti", ad="Ordubalık Kenti",
         yardimci=("ChangeFactionBlueNPCUnit", "Destan Kutluk Bey", "Kutluk Bey", "Ordubalık kentinin beyi."),
         varis_metin="Kara Kuzgun çaldıklarını kentteki bir kadına taşıyordu. Ordubalık Kenti'ne git, Kutluk Bey'i bul.",
         av=("Kara Kam", "Kara Kam", 5, 0), av_ad="Kara Kamlar",
         av_metin="Ruhunu Erlik'e satmış kara kamlar kentin sokaklarında ateş çağırıyor. Onları sustur.",
         boss=("Kara Albis", "Kara Albıs"),
         boss_metin="Kentin beylerini birbirine düşüren Kara Albıs. Kılık değiştirip kentin en uzak köşesine saklandı; "
                    "onu bul ve yen."),
    dict(sahne="UlukayinOrmani", dugum="UlukayinOrmani", ad="Ulukayın Ormanı",
         yardimci=("ChangeAppearanceNPCUnit", "Destan Kayra", "Orman Kamı Kayra", "Ulu Kayın'ın kamı. Ağaçların dilini bilir."),
         varis_metin="Kara Albıs'ın aynasında Ulu Kayın'ın kurumuş yaprakları göründü. Ulukayın Ormanı'na git, Orman Kamı Kayra'yı bul.",
         av=("Agulu Kormos", "Ağulu Körmös", 4, 3), av_ad="Ulukayın'ın Ağusu",
         av_metin="Ağulu körmösler Ulu Kayın'ın köklerini zehirliyor. Onları yen ve Ötüken Taşlarını kırıp kutu ormana geri ver.",
         boss=("Agu Bey", "Ağu Bey"),
         boss_metin="Ağunun kaynağı Ağu Bey: köklerin arasında yatan dev körmös. Ormanın derinliğinde onu bul ve yen."),
    dict(sahne="KoncolosIni", dugum="KoncolosIni", ad="Koncolos İni",
         yardimci=("ChangeFactionRedNPCUnit", "Destan Cagri", "Avcı Çağrı", "Kara Koncolos'un izini süren avcı."),
         varis_metin="Ağu Bey'in kökleri bir mağaraya uzanıyordu. Koncolos İni'ne git, Avcı Çağrı'yı bul.",
         av=("Kormos", "Körmös", 7, 0), av_ad="İnin Körmösleri",
         av_metin="Körmösler inin ağzını tutuyor. Yolu aç.",
         boss=("Kara Koncolos", "Kara Koncolos"),
         boss_metin="Yılın en uzun gecelerinde obaları basan kara dev Kara Koncolos inin dibinde. Ateşten ve demirden korkar; yen onu."),
    dict(sahne="KaganOrdasi", dugum="KaganOrdasi", ad="Kağan Ordası",
         yardimci=("CutsceneNPCUnit", "Destan Isbara", "Buyrukçu Işbara", "Kağan'ın buyrukçusu. Otağın savunmasını yönetir."),
         varis_metin="Koncolos'un ininde kızıl damgalı kalkanlar vardı. Kağan Ordası'na git, Buyrukçu Işbara'yı bul.",
         av=("Kan Suvarisi", "Kan Süvarisi", 4, 0), av_ad="Otağın Kuşatması",
         av_metin="Erlik'in kan süvarileri Kağan'ın otağını kuşattı. Kuşatmayı kır.",
         boss=("Kizil Tamga", "Kızıl Tamga"),
         boss_metin="Süvarilerin başbuğu Kızıl Tamga ordanın en uzak ucunda. Onu yen, otağ kurtulsun."),
    dict(sahne="KafDagiYolu", dugum="KafDagiYolu", ad="Kaf Dağı Yolu",
         yardimci=("LoopPatrolNPCUnit", "Destan Ak Dogan", "Ak Doğan", "Kaf Dağı'nın yolunu bekleyen alp."),
         varis_metin="Kızıl Tamga'nın süvarileri Kaf Dağı'ndan gelmişti. Kaf Dağı Yolu'na git, Ak Doğan'ı bul.",
         av=("Kizil Cin", "Kızıl Cin", 6, 0), av_ad="Közden Doğanlar",
         av_metin="Kızıl cinler yolu közle kapattı. Onları dağıt.",
         boss=("Alaz Cin", "Alaz Cin"),
         boss_metin="Kızıl cinlerin anası Alaz Cin yolun sonunda. Közden doğdu; onu söndür."),
    dict(sahne="ErgenekonMagarasi", dugum="ErgenekonMagarasi", ad="Ergenekon Mağarası",
         yardimci=("BankNPCUnit", "Destan Bozkurt Ata", "Demirci Bozkurt Ata", "Ergenekon'un demircisi. Demir dağı eriten ocağı o yaktı."),
         varis_metin="Alaz Cin söndüğünde közleri Ergenekon'a doğru savruldu. Ergenekon Mağarası'na git, Demirci Bozkurt Ata'yı bul.",
         av=("Kormos", "Körmös", 5, 2), av_ad="Demir Dağın Kölesi",
         av_metin="Körmösler demir dağın içini kazıyor. Onları durdur ve Ötüken Taşlarını kır.",
         boss=("Demirkiynak", "Demirkıynak"),
         boss_metin="Demir gagalı cadı Demirkıynak ocakları söndürüyor. Mağaranın derinliğinde onu yen."),
    dict(sahne="KurganMezarligi", dugum="KurganMezarligi", ad="Kurgan Mezarlığı",
         yardimci=("DialogNPCUnit", "Destan Kara Bilge", "Mezar Bekçisi Kara Bilge", "Eski kurganların bekçisi."),
         varis_metin="Demirkıynak'ın gagasında kurgan toprağı vardı. Kurgan Mezarlığı'na git, Mezar Bekçisi Kara Bilge'yi bul.",
         av=("Kemik Er", "Kemik Er", 6, 0), av_ad="Kurganın Ölüleri",
         av_metin="Erlik'in kamları kurganlardaki ölüleri kaldırdı. Kemik erleri yeniden toprağa gönder.",
         boss=("Kemik Kagan", "Kemik Kağan"),
         boss_metin="En büyük kurganda yatan eski kağan tahtından kaldırıldı. Kemik Kağan'ı yen, ruhu dinlensin."),
    dict(sahne="AyDedeKoyu", dugum="AyDedeKoyu", ad="Ay Dede Köyü",
         yardimci=("PlayMusicNPCUnit", "Destan Ay Dede", "Ay Dede", "Gece yolcularını ışığıyla koruyan yaşlı bilge."),
         varis_metin="Kemik Kağan'ı kaldıran kam Ay Dede'nin köyüne kaçtı. Ay Dede Köyü'ne git, Ay Dede'yi bul.",
         av=("Kemik Akinci", "Kemik Akıncı", 5, 0), av_ad="Ölü Akıncılar",
         av_metin="Ölü akıncılar köyü basıyor. Onları durdur.",
         boss=("Karakura", "Karakura"),
         boss_metin="Uyuyanların göğsüne çöken ölü kam Karakura, Ay Dede'nin ışığını kararttı. Köyün en uzak ucunda onu yen."),
    dict(sahne="FeaturesDemoDungeon", dugum="Features Demo Dungeon", ad="Erlik'in Mağarası",
         yardimci=("RandomPatrolNPCUnit", "Destan Er Tostuk", "Er Töştük", "Yeraltına inip geri dönen destan kahramanı."),
         varis_metin="Karakura'nın düdüğü Erlik'in Mağarası'ndan duyuluyor. Mağaraya in, Er Töştük'ü bul.",
         av=("Enemy Minion", "Albastı", 3, 0), av_ad="Albastıların İni",
         av_metin="Erlik'in albastıları mağaranın girişini tutuyor. Onları yen.",
         boss=("Enemy Boss", "Tepegöz"),
         boss_metin="Tek gözlü dev Tepegöz mağaranın derinliğinde uyandı. Basat'ın yaptığını yap: Tepegöz'ü yen."),
    dict(sahne="TamuZindani", dugum="TamuZindani", ad="Tamu Zindanı",
         yardimci=("ChangeAppearanceNPCUnit", "Destan Ak Kam", "Ülgen'in Elçisi Ak Kam", "Gök tanrısı Ülgen'in yeryüzüne gönderdiği kam."),
         varis_metin="Tepegöz düşünce yer yarıldı ve Tamu'nun kapısı göründü. Tamu Zindanı'na in, Ülgen'in Elçisi Ak Kam'ı bul.",
         av=("Abasi", "Abası", 5, 0), av_ad="Zindanın Abasıları",
         av_metin="Ağır zırhlı abasılar zindanın koridorlarını tutuyor. Yolu aç.",
         boss=("Tamu Bekcisi", "Tamu Bekçisi"),
         boss_metin="Zindanın kapısını tutan dev zindancı Tamu Bekçisi. O düşmeden Erlik Han'a ulaşamazsın."),
]

SON = dict(
    erlik=("Erlik Han", "Erlik Han"),
    erlik_metin="Erlik Han Tamu'nun en derin yerinde, kırık mührün başında bekliyor. Ötüken'in kutu için onu yen!",
    donus_metin="Erlik Han yenildi; kara ruhları Tamu'ya geri çekiliyor. Ötüken'e dön ve Olcayto Han'a müjdeyi ver.",
)


UNLULER = "aeıioöuü"


def son_unlu(ad):
    for c in reversed(ad.lower()):
        if c in UNLULER:
            return c
    return "a"


def yonelme(ad):
    """'na / 'ne (Umay Tarlaları'na, Börü Tepesi'ne, Ay Dede Köyü'ne)"""
    return ad + ("'na" if son_unlu(ad) in "aıou" else "'ne")


def belirtme(ad):
    """'ı/'i/'u/'ü; ünlüyle bitince iyelikli (-sı) 'nı, ötekiler 'yı (Alkarası'nı, Kızıl Tamga'yı, Tepegöz'ü)"""
    u = son_unlu(ad)
    ek = {"a": "ı", "ı": "ı", "e": "i", "i": "i", "o": "u", "u": "u", "ö": "ü", "ü": "ü"}[u]
    if ad[-1].lower() in UNLULER:
        kaynastirma = "n" if ad[-2:].lower() in ("sı", "si", "su", "sü") else "y"
        return ad + "'" + kaynastirma + ek
    return ad + "'" + ek


def seviye(i):
    return 1 + 2 * i


def tecrube(i, oran):
    """haritanın iki seviyesini bitirmek için gerekenin bir oranı (görevin kendi 100 tecrübesi düşülür)"""
    s = seviye(i)
    toplam = XP[min(s - 1, len(XP) - 1)] + XP[min(s, len(XP) - 1)]
    return max(0, int(round(toplam * oran / 10.0)) * 10 - 100)


def akce(i, taban, kat):
    return taban + kat * seviye(i)


def guid(ad):
    return uuid.uuid5(NS, "dosya:" + ad).hex


def rid(ad, k):
    return int(uuid.uuid5(NS, f"rid:{ad}:{k}").int % (2 ** 62)) + 1000


def dosya_adi(kaynak):
    return re.sub(r"[^A-Za-z0-9]", "", kaynak)


def gorev_yaz(kaynak, ad, metin, hedefler, onkosul, xp, para):
    """hedefler: [("kill", hedef, adet, yazı) | ("zone", sahne düğümü, yazı)]"""
    refs = []
    adimlar = []
    for k, h in enumerate(hedefler):
        r = rid(kaynak, k)
        adimlar.append(f"    - rid: {r}\n")
        if h[0] == "kill":
            refs.append(
                f"    - rid: {r}\n      type: {{class: KillObjective, ns: AnyRPG, asm: Assembly-CSharp}}\n      data:\n"
                f"        amount: {h[2]}\n        deprecatedType: \n        overrideDisplayName: {q(h[3])}\n"
                f"        targetName: {h[1]}\n")
        else:
            refs.append(
                f"    - rid: {r}\n      type: {{class: VisitZoneObjective, ns: AnyRPG, asm: Assembly-CSharp}}\n      data:\n"
                f"        amount: 1\n        type: \n        overrideDisplayName: {q(h[2])}\n        zoneName: {h[1]}\n")
    if onkosul:
        on = ("  prerequisiteConditions:\n  - requireAny: 0\n    reverseMatch: 0\n    questPrerequisites:\n"
              f"    - prerequisiteName: {onkosul}\n      stepIndex: -1\n      requireComplete: 1\n      requireTurnedIn: 1\n")
    else:
        on = "  prerequisiteConditions: []\n"
    m_name = dosya_adi(kaynak)
    t = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n"
         "  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n"
         "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n"
         "  m_Script: {fileID: 11500000, guid: 7b491a306145b8e4aa064bbcf71f8e01, type: 3}\n"
         f"  m_Name: {m_name}\n  m_EditorClassIdentifier: \n  resourceName: {kaynak}\n  displayName: {q(ad)}\n"
         "  icon: {fileID: 0}\n  iconBackgroundImage: {fileID: 0}\n"
         f"  description: {q(metin)}\n  useRegionalDescription: 0\n  resourceDescriptionProfile: \n  optionalOverride: 0\n"
         "  isAchievement: 0\n  repeatableQuest: 0\n  hasOpeningDialog: 0\n  experienceLevel: 1\n  dynamicLevel: 1\n"
         f"  extraLevels: 0\n  baseExperienceReward: {xp}\n  experienceRewardPerLevel: 0\n  automaticCurrencyReward: 0\n"
         f"  rewardCurrencyName: Silver\n  baseCurrencyReward: {para}\n  currencyRewardPerLevel: 0\n"
         "  maxItemRewards: 0\n  itemRewardNames: []\n  maxFactionRewards: 0\n  factionRewards: []\n"
         "  maxAbilityRewards: 0\n  abilityRewardNames: []\n  maxSkillRewards: 0\n  skillRewardNames: []\n"
         "  steps:\n  - questObjectives:\n" + "".join(adimlar) + on +
         "  turnInItems: 0\n  allowRawComplete: 0\n  references:\n    version: 2\n    RefIds:\n" + "".join(refs))
    (QUEST_DIR / (m_name + ".asset")).write_text(t, encoding="utf-8")
    (QUEST_DIR / (m_name + ".asset.meta")).write_text(
        f"fileFormatVersion: 2\nguid: {guid('gorev:' + kaynak)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
        "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


def dugum(gorev, basla, bitir):
    return f"        - startQuest: {1 if basla else 0}\n          endQuest: {1 if bitir else 0}\n          questName: {gorev}\n"


def yardimci_yaz(kaynak_dosya, profil, ad, tanim, gorevler):
    sablon = (RES / "UnitProfile/QuestNPCUnit.asset").read_text(encoding="utf-8")
    kaynak = (RES / "UnitProfile" / (kaynak_dosya + ".asset")).read_text(encoding="utf-8")
    model = re.search(r"^    modelPrefab: \{fileID: (-?\d+), guid: (\w+),$", kaynak, re.M)
    simge = re.search(r"^  icon: .*$", kaynak, re.M).group(0)
    t = sablon

    def sub(desen, yeni):
        nonlocal t
        n = len(re.findall(desen, t, flags=re.M))
        if n != 1:
            raise ValueError(f"{profil}: {desen} {n} kez")
        t = re.sub(desen, lambda m: yeni, t, flags=re.M)

    m_name = dosya_adi(profil) + "Unit"
    sub(r"^  m_Name: .*$", f"  m_Name: {m_name}")
    sub(r"^  resourceName: .*$", f"  resourceName: {profil}")
    sub(r"^  displayName: .*$", f"  displayName: {q(ad)}")
    sub(r"^  icon: .*$", simge)
    sub(r"^  description: .*$", f"  description: {q(tanim)}")
    sub(r"^  characterName: .*$", f"  characterName: {q(ad)}")
    sub(r"^    modelPrefab: \{fileID: -?\d+, guid: \w+,$", f"    modelPrefab: {{fileID: {model.group(1)}, guid: {model.group(2)},")
    sub(r"^  m_UUID: .*$", f"  m_UUID: {uuid.uuid5(NS, 'uuid:' + profil)}")
    sub(r"^        quests:\n(?:        - startQuest: .*\n          endQuest: .*\n          questName: .*\n)+",
        "        quests:\n" + "".join(gorevler))
    NPC_DIR.mkdir(exist_ok=True)
    (NPC_DIR / (m_name + ".asset")).write_text(t, encoding="utf-8")
    (NPC_DIR / (m_name + ".asset.meta")).write_text(
        f"fileFormatVersion: 2\nguid: {guid('yardimci:' + profil)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
        "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


def olcayto_ekle(gorevler):
    yol = RES / "UnitProfile/QuestNPCUnit.asset"
    t = yol.read_text(encoding="utf-8")
    # önceki çalıştırmanın Destan düğümlerini sil, yenilerini listenin sonuna ekle
    t = re.sub(r"        - startQuest: \d\n          endQuest: \d\n          questName: Destan .*\n", "", t)
    i = t.index("        questGiverProfileNames: []\n")
    t = t[:i] + "".join(gorevler) + t[i:]
    yol.write_text(t, encoding="utf-8")


def klasor_meta(klasor, ad):
    meta = klasor.parent / (klasor.name + ".meta")
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid('klasor:' + ad)}\nfolderAsset: yes\nDefaultImporter:\n"
                        "  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


def main():
    QUEST_DIR.mkdir(exist_ok=True)
    NPC_DIR.mkdir(exist_ok=True)
    klasor_meta(QUEST_DIR, "Destan")
    klasor_meta(NPC_DIR, "Hikaye")
    for eski in list(QUEST_DIR.glob("*.asset")) + list(QUEST_DIR.glob("*.asset.meta")):
        eski.unlink()

    no = 0
    def kaynak_adi(ad):
        nonlocal no
        no += 1
        sade = ad.translate(str.maketrans("çğıöşüÇĞİÖŞÜâ'", "cgiosuCGIOSUa ")).title().replace(" ", "")
        return f"Destan {no:02d} {sade}"

    yardimci_gorevleri = {}
    olcayto = []
    onceki = None           # bir önceki görevin kaynak adı (önkoşul)
    yerlesim = []           # HikayeVerisi.cs: (sahne, yardımcı profil, boss profil)
    for i, h in enumerate(HARITALAR):
        sahibi = h["yardimci"][1] if h["yardimci"] else None
        liste = yardimci_gorevleri.setdefault(sahibi, []) if sahibi else olcayto
        # varış (önceki haritanın yardımcısı verir, bu haritanınki teslim alır)
        if i > 0:
            varis = kaynak_adi(h["ad"] + " Yolculuk")
            gorev_yaz(varis, yonelme(h["ad"]) + " Yolculuk", h["varis_metin"], [("zone", h["dugum"], yonelme(h["ad"]) + " git")],
                      onceki, tecrube(i, 0.06), akce(i, 3, 1))
            veren = olcayto if i == 1 else yardimci_gorevleri[HARITALAR[i - 1]["yardimci"][1]]
            veren.append(dugum(varis, True, False))
            liste.append(dugum(varis, False, True))
            onceki = varis
        # av: düşmanlar (+ Ötüken Taşı)
        hedef, hedef_ad, adet, tas = h["av"]
        av = kaynak_adi(h["av_ad"])
        hedefler = [("kill", hedef, adet, hedef_ad + " yen")]
        if tas:
            hedefler.append(("kill", "Otuken Tasi", tas, "Ötüken Taşı kır"))
        gorev_yaz(av, h["av_ad"], h["av_metin"], hedefler, onceki, tecrube(i, 0.22), akce(i, 8, 3))
        liste.append(dugum(av, True, True))
        onceki = av
        # boss
        if h["boss"]:
            bprofil, bad = h["boss"]
            boss = kaynak_adi(bad)
            gorev_yaz(boss, bad, h["boss_metin"], [("kill", bprofil, 1, belirtme(bad) + " yen")],
                      onceki, tecrube(i, 0.32), akce(i, 15, 6))
            liste.append(dugum(boss, True, True))
            onceki = boss
        yerlesim.append((h["sahne"], sahibi or "", h["boss"][0] if h["boss"] and i not in (7, 11, 13, 14) else ""))

    # son: Erlik Han ve Ötüken'e dönüş
    son_yardimci = HARITALAR[-1]["yardimci"][1]
    erlik = kaynak_adi("Erlik Han")
    gorev_yaz(erlik, "Erlik Han", SON["erlik_metin"], [("kill", "Erlik Han", 1, "Erlik Han'ı yen")], onceki,
              tecrube(14, 0.5), akce(14, 60, 10))
    yardimci_gorevleri[son_yardimci].append(dugum(erlik, True, True))
    donus = kaynak_adi("Otukene Donus")
    gorev_yaz(donus, "Ötüken'e Dönüş", SON["donus_metin"], [("zone", "Features Demo Zone", "Ötüken Yaylası'na dön")], erlik,
              tecrube(14, 0.2), akce(14, 100, 10))
    yardimci_gorevleri[son_yardimci].append(dugum(donus, True, False))
    olcayto.append(dugum(donus, False, True))
    # Tamu'ya Erlik Han da konur (Tamu Bekçisi sahnede zaten var)
    yerlesim[-1] = (yerlesim[-1][0], yerlesim[-1][1], "Erlik Han")

    for h in HARITALAR:
        if h["yardimci"]:
            kaynak_dosya, profil, ad, tanim = h["yardimci"]
            yardimci_yaz(kaynak_dosya, profil, ad, tanim, yardimci_gorevleri[profil])
    olcayto_ekle(olcayto)

    # editör verisi
    satirlar = "".join(f'            {{ "{s}", new[] {{ "{y}", "{b}" }} }},\n' for s, y, b in yerlesim)
    cs = ("// hikaye.py yazar; elle değiştirme\nusing System.Collections.Generic;\n\nnamespace Otuken.EditorAraclari {\n\n"
          "    /// <summary>ana hikâye: her haritanın görev veren yardımcısı (girişin yanına) ve yeni boss'u (en uzak açıklığa)</summary>\n"
          "    public static class HikayeVerisi {\n"
          "        // sahne -> { yardımcı profil (boşsa yok), boss profil (boşsa sahnede zaten var) }\n"
          "        public static readonly Dictionary<string, string[]> Haritalar = new Dictionary<string, string[]>() {\n"
          + satirlar + "        };\n    }\n}\n")
    (ROOT / "Assets/Otuken/Editor/HikayeVerisi.cs").write_text(cs, encoding="utf-8")
    meta = ROOT / "Assets/Otuken/Editor/HikayeVerisi.cs.meta"
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid('cs:HikayeVerisi')}\nMonoImporter:\n  externalObjects: {{}}\n"
                        "  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
                        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    print(f"{no} görev, {len(yardimci_gorevleri)} yardımcı yazıldı; Olcayto Han'a {len(olcayto)} düğüm")


if __name__ == "__main__":
    main()
