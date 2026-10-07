#!/usr/bin/env python3
"""Ötüken Destanı'nın yan görevleri: her haritada 3 görev veren, her birinin 9 görevlik zinciri (15 × 27 = 405 görev).

Ana hikâyenin (hikaye.py, 45 görev) yanında oyunu on katına çıkarır. Her haritanın girişinin yakınında üç kişi durur
(avcı, otacı, çoban...). Her biri o haritanın canavarlarıyla ilgili bir zincir verir: canavar avı, iki türü birden
avlama, Ötüken Taşı kırma, seçkin (dayanıklı) canavarlar, haritanın boss'u ve büyük sürek avı. Zincirin görevleri
sırayla açılır (bir önceki teslim edilince). Zincirin son görevi 2. haritadan sonra bir cevher de verir
(Demirci'de +6 ve üstü yükseltme için; harita ilerledikçe Demir, Gümüş, Altın, Gök Demiri).

Yazdıkları:
  - Quest/Yan/*.asset                   görevler ("Yan HH-K-S ...")
  - UnitProfile/YanGorev/*.asset        görev verenler (Olcayto Han'ın kopyası, başka model)
  - Assets/Otuken/Editor/YanGorevVerisi.cs   HaritaDoldur'un her haritaya koyacağı görev verenler

Betik tekrar çalıştırılabilir; GUID'ler addan türetilir.
Kullanım (depo kökünden): python3 "Tools~/dunya/yan_gorevler.py"
"""
import re
import sys
import uuid
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import hikaye as H  # noqa: E402

ROOT = H.ROOT
RES = H.RES
QUEST_DIR = RES / "Quest" / "Yan"
NPC_DIR = RES / "UnitProfile" / "YanGorev"
NS = uuid.UUID("3c7f1e52-9b0d-4a8e-b6d4-71e2a95c0f18")
q = H.q

ERKEK = ["BankNPCUnit", "DanceNPCUnit", "LoopPatrolNPCUnit", "DialogNPCUnit", "VendorNPCUnit"]
KADIN = ["ChangeAppearanceNPCUnit", "ChangeFactionBlueNPCUnit", "ChangeFactionRedNPCUnit", "PlayMusicNPCUnit",
         "CutsceneNPCUnit", "RandomPatrolNPCUnit"]

# Ötüken Yaylası'nın canavarları (HaritaDoldur'un kadrosunda yok; sahnede zaten çok kamp var)
YAYLA = ["Cali Cini", "Kara Yek", "Yagmaci Okcu", "Sulmus", "Kara Kam", "Kiragi Cadisi", "Buz Bekcisi"]
# boss'u olmayan haritada (Ötüken Yaylası) zincirin sonu seçkinlere gider
CEVHER = [None, None, "Demir Cevheri", "Demir Cevheri", "Demir Cevheri", "Gumus Cevheri", "Gumus Cevheri",
          "Gumus Cevheri", "Altin Cevheri", "Altin Cevheri", "Altin Cevheri", "Gok Demiri", "Gok Demiri",
          "Gok Demiri", "Gok Demiri"]

# her haritanın üç görev vereni: (profil, görünen ad, cinsiyet, tanım, korudukları (belirtme hâlinde), derdi)
KISILER = {
    "FeaturesDemoZone": [
        ("Yan Avci Batur", "Avcı Batur", "e", "Yaylanın avcısı. Her sürünün izini bilir.", "sürülerimizi",
         "Yaylada av kalmadı; Erlik'in yaratıkları her şeyi kaçırdı."),
        ("Yan Otaci Ay Hatun", "Otacı Ay Hatun", "k", "Yaylanın otacısı. Yaralı alpları otlarıyla iyileştirir.",
         "ot toplayanlarımı", "Şifalı otları toplamaya giden kızlar korkudan çadırdan çıkamıyor."),
        ("Yan Yilkici Erdem", "Yılkıcı Erdem", "e", "At sürülerine bakan yılkıcı. Atların dilinden anlar.",
         "yılkıyı", "Taylarım her gece ürküyor; karanlıkta bir şeyler dolaşıyor."),
    ],
    "UmayTarlalari": [
        ("Yan Ekinci Bayindir", "Ekinci Bayındır", "e", "Umay Tarlaları'nın en eski ekincisi.", "tarlaları",
         "Ekinler bu yıl erken sarardı; tarlalarda kara ruhlar dolaşıyor."),
        ("Yan Ebe Tolun", "Ebe Tolun", "k", "Umay Ana'nın yolundan giden ebe. Obanın çocuklarını kollar.",
         "çocukları", "Umay Ana'nın koruduğu çocuklar gece kâbuslarla uyanıyor."),
        ("Yan Degirmenci Tarhan", "Değirmenci Tarhan", "e", "Tarlaların değirmencisi.", "değirmeni",
         "Değirmenin taşı durdu; un taşıyan arabalar yolda soyuluyor."),
    ],
    "BoruTepesi": [
        ("Yan Kurtcu Asena", "Kurtçu Asena", "k", "Tepenin kurtlarına bakan kız. Bozkurdun soyundan geldiğine inanır.",
         "kurtlarımı", "Kurtlarım uluyor ama ses vermiyorlar; tepede kötü bir şey var."),
        ("Yan Gozcu Tutuk", "Gözcü Tutuk", "e", "Börü Tepesi'nin gözcüsü. Bozkırı bir uçtan öbür uca görür.",
         "gözcü kulesini", "Kuleden bakınca her gece daha çok kara gölge görüyorum."),
        ("Yan Ok Ustasi Kinik", "Ok Ustası Kınık", "e", "Tepenin ok ustası. Yaptığı oklar hedefini şaşmaz.",
         "ok atölyemi", "Ok yapacak ağaç kesmeye gidenlerim geri dönmüyor."),
    ],
    "AkDenizKiyisi": [
        ("Yan Balikci Yalcin", "Balıkçı Yalçın", "e", "Kıyının balıkçısı. Ak Deniz'in her kayasını tanır.",
         "teknelerimizi", "Ağlarım boş dönüyor; kıyıda yağmacılar kol geziyor."),
        ("Yan Tuzcu Ece", "Tuzcu Ece", "k", "Kıyının tuz yataklarını işleyen kadın.", "tuz yataklarını",
         "Tuz yataklarına gidemiyoruz; kumsal kara ruhlarla dolu."),
        ("Yan Kayikci Oguz", "Kayıkçı Oğuz", "e", "Kıyıdan kıyıya yolcu taşıyan kayıkçı.", "kayıkları",
         "Kayıklarımı yakıyorlar; yolcular karşı kıyıya geçemiyor."),
    ],
    "OrdubalikCarsisi": [
        ("Yan Kervanci Tekin", "Kervancı Tekin", "e", "Ordubalık'a ipek ve tuz getiren kervancı.", "kervanları",
         "Kervanım çarşıya girerken soyuldu; yükümün yarısı gitti."),
        ("Yan Kumasci Gokce", "Kumaşçı Gökçe", "k", "Çarşının en iyi kumaşçısı. İpeğin iyisini kokusundan tanır.",
         "dükkânları", "Dükkânımın kapısına gece kara bir tamga çizdiler."),
        ("Yan Bekci Turgut", "Çarşı Bekçisi Turgut", "e", "Çarşının gece bekçisi.", "çarşıyı",
         "Tek başıma bütün çarşıyı bekleyemem; yardımın gerek."),
    ],
    "OrdubalikKenti": [
        ("Yan Yazici Bengu", "Yazıcı Bengü", "k", "Kentin yazıcısı. Bengü taşlarına atalarımızın sözünü kazır.",
         "yazıtları", "Bengü taşlarımı kırmaya gelen kara kamlar var."),
        ("Yan Subasi Alp Er", "Subaşı Alp Er", "e", "Kentin subaşısı. Sokakların düzeni ondan sorulur.",
         "kenti", "Askerlerimin yarısı yaralı; sokaklar kara ruhlara kaldı."),
        ("Yan Ascibasi Kutlu", "Aşçıbaşı Kutlu", "e", "Kağan'ın sofrasına yemek yetiştiren aşçıbaşı.",
         "mutfağı", "Ambarlarıma giden yolda kimse güvende değil; kazanlar boş kalacak."),
    ],
    "UlukayinOrmani": [
        ("Yan Oduncu Cagatay", "Oduncu Çağatay", "e", "Ulukayın'ın oduncusu. Ulu Kayın'a balta vurmaz, yalnız kurumuş dalı keser.",
         "oduncuları", "Ormanın içi ağuyla doldu; baltam bile kararıyor."),
        ("Yan Otaci Borte", "Otacı Börte", "k", "Ormanın otacısı. Kökleri ve yaprakları okur.",
         "şifa bahçemi", "Şifa bahçemin otları kurudu; ağu toprağa sızıyor."),
        ("Yan Tuzakci Temur", "Tuzakçı Temür", "e", "Ormanın tuzakçısı. Ayak izinden hayvanın yaşını bilir.",
         "tuzaklarımı", "Tuzaklarıma tavşan değil, Erlik'in yaratıkları düşüyor."),
    ],
    "KoncolosIni": [
        ("Yan Madenci Demir", "Madenci Demir", "e", "İnin ağzında demir arayan madenci.", "madencileri",
         "Ocakta kazma sesi bitti; madencilerim içeri girmekten korkuyor."),
        ("Yan Iz Surucu Alaca", "İz Sürücü Alaca", "k", "Kara Koncolos'un izini süren kadın.", "izcileri",
         "İzleri inin derinine iniyor; yalnız başıma inemem."),
        ("Yan Atesci Oktay", "Ateşçi Oktay", "e", "İnin ağzındaki ateşleri yakan bekçi. Koncolos ateşten korkar.",
         "ateşleri", "Ateşlerim her gece söndürülüyor; inin yaratıkları cesaretlendi."),
    ],
    "KaganOrdasi": [
        ("Yan Tug Tasiyan Bumin", "Tuğ Taşıyan Bumin", "e", "Kağan'ın tuğunu taşıyan alp.", "tuğu",
         "Tuğ yere düşerse ordu dağılır; kuşatma her gün daralıyor."),
        ("Yan Atci Saruca", "Atçı Saruca", "k", "Ordanın atlarını yetiştiren kadın.", "atları",
         "Atlarım ürküyor; kan süvarileri ahırların çevresinde dolaşıyor."),
        ("Yan Okcubasi Inal", "Okçubaşı İnal", "e", "Kağan'ın okçularının başı.", "okçularımı",
         "Okçularımın oku tükendi; düşman bitmiyor."),
    ],
    "KafDagiYolu": [
        ("Yan Dagci Kartal", "Dağcı Kartal", "e", "Kaf Dağı'nın patikalarını bilen dağcı.", "patikaları",
         "Patikaları köz kapladı; dağa çıkan kimse geri gelmiyor."),
        ("Yan Yol Bekcisi Tomris", "Yol Bekçisi Tomris", "k", "Kaf Dağı yolunun bekçisi. Kılıcı kadar sözü de keskindir.",
         "yolu", "Yolu tek başıma tutuyorum; gücüm tükeniyor."),
        ("Yan Kervan Basi Ilgaz", "Kervan Başı Ilgaz", "e", "Kaf Dağı'nı aşan kervanların başı.", "yolcuları",
         "Kervanım dağın eteğinde bekliyor; yol açılmadan çıkamayız."),
    ],
    "ErgenekonMagarasi": [
        ("Yan Korukcu Celik", "Körükçü Çelik", "e", "Ergenekon ocağının körükçüsü.", "ocakları",
         "Ocaklar söndükçe demir dağ yeniden kapanıyor."),
        ("Yan Demir Ustasi Arslan", "Demir Ustası Arslan", "e", "Ergenekon'un demir ustası. Kılıçları Kağan'a gider.",
         "örsümü", "Örsüm soğudu; mağaranın derinliğinden kara bir ses geliyor."),
        ("Yan Kazmaci Aybike", "Kazmacı Aybike", "k", "Demir damarlarını kazan kadın. Kazmasını kimseye vermez.",
         "kazmacıları", "Demir damarına ulaşmak üzereydik; körmösler yolu kesti."),
    ],
    "KurganMezarligi": [
        ("Yan Yugcu Ayaz Ana", "Yuğcu Ayaz Ana", "k", "Ölülerin yuğ törenini yöneten yaşlı kadın.", "kurganları",
         "Atalarımızın kurganları yağmalanıyor; ölüler rahat uyuyamıyor."),
        ("Yan Balbal Ustasi Tonguc", "Balbal Ustası Tonguç", "e", "Kurganlara balbal diken taş ustası.", "balbalları",
         "Diktiğim balballar devriliyor; kemik erler gece kalkıyor."),
        ("Yan Kam Kizi Altun", "Kam Kızı Altun", "k", "Kurganların ruhlarıyla konuşan genç kam.", "ruhları",
         "Atalarımın ruhları bana yardım diye fısıldıyor."),
    ],
    "AyDedeKoyu": [
        ("Yan Gece Bekcisi Yildiz", "Gece Bekçisi Yıldız", "k", "Ay Dede'nin ışığı altında köyü bekleyen kız.", "köyü",
         "Ay ışığı her gece biraz daha sönük; karanlıkta ölüler yürüyor."),
        ("Yan Koy Agasi Sungur", "Köy Ağası Sungur", "e", "Ay Dede Köyü'nün ağası.", "evleri",
         "Köylüler evlerini bırakıp kaçmak istiyor; onları durdurmam gerek."),
        ("Yan Ozan Bayat", "Ozan Bayat", "e", "Kopuzuyla destan söyleyen ozan.", "kopuzumu",
         "Söyleyecek yeni bir destan arıyorum; senin yiğitliğin olabilir."),
    ],
    "FeaturesDemoDungeon": [
        ("Yan Alp Kilic", "Alp Kılıç", "e", "Basat'ın yoldaşı. Tepegöz'ü ilk gören alp.", "yoldaşlarımı",
         "Yoldaşlarım mağaranın derinliğinde kayboldu."),
        ("Yan Kam Ana Ulduz", "Kam Ana Ulduz", "k", "Yeraltının yollarını bilen yaşlı kam.", "kamları",
         "Erlik'in mağarasından yükselen kara duman göğü karartıyor."),
        ("Yan Er Ulug", "Er Uluğ", "e", "Erlik'in esirliğinden kaçan er.", "esirleri",
         "Kardeşlerim hâlâ mağarada esir; onları bırakıp gidemem."),
    ],
    "TamuZindani": [
        ("Yan Ak Kiz Aycin", "Ak Kız Ayçin", "k", "Ülgen'in ışığını taşıyan kız. Karanlıkta yolu o aydınlatır.",
         "ışığı", "Ülgen'in ışığı zindanın karanlığında titriyor."),
        ("Yan Gok Alp Tengiz", "Gök Alp Tengiz", "e", "Gökten inen alp. Tamu'ya gönüllü indi.", "gök kapısını",
         "Tamu'nun kapısı açık kaldıkça kara ruhlar yeryüzüne taşıyor."),
        ("Yan Kut Bekcisi Ertug", "Kut Bekçisi Ertuğ", "e", "Ötüken'in kutunu bekleyen yaşlı alp.", "kutu",
         "Erlik Ötüken'in kutunu zindanın taşlarına hapsetti."),
    ],
}

# ---- metinler: {H} haritada, {C}/{C2} canavar, {N}/{N2} sayı, {T} taş sayısı, {Y} korunan, {B}/{Bi} boss
TEK = [
    "{H} {C} sayısı her gün artıyor. {N} tanesini yen ki {Y} rahat bıraksınlar.",
    "Dün gece bir {C} sürüsü {Y} bastı. Peşlerine düş ve {N} tanesini yen.",
    "Gözcülerim {C} izlerini gördü; yuvaları buraya yakın. {N} tanesini yen, gerisi korkup kaçar.",
    "Yolcular {C} korkusundan yola çıkamıyor. {N} tanesini yen, yollar açılsın.",
    "{C} sürüleri {Y} gözüne kestirmiş. Onlar gelmeden sen git: {N} tanesini yen.",
    "Kam dedi ki: ne kadar çok {C} düşerse Erlik'in gözü buradan o kadar çekilir. {N} tane yen, sonra yanıma dön.",
    "Sen gelmeden önce iki yiğit {C} avına çıktı, geri dönmediler. Onların öcünü al: {N} {C} yen.",
    "{H} akşam olunca {C} sesleri yükseliyor. Uyuyabilmemiz için {N} tanesini yen.",
    "Ötüken'in kutu zayıfladıkça {C} çoğalıyor. {N} tanesini yen, kut yeniden güçlensin.",
    "{C} yüzünden ocaklar söndü. {N} tanesini yen; ocağımızı yeniden yakalım.",
    "Erlik'in en sadık kölelerinden biri {C}. {N} tanesini yen, sürüleri büsbütün dağılsın.",
    "{C} bu sabah {Y} yine yokladı. Bu kez sen karşıla: {N} tanesini yen.",
]
CIFT = [
    "{C} ile {C2} birlik olmuş, {Y} kuşatıyorlar. {N} {C} ve {N2} {C2} yen; kuşatma kırılsın.",
    "İki yandan sıkıştık: bir yanda {C}, öbür yanda {C2}. İkisinden de payını al.",
    "Gözcü haber getirdi: {C} sürüsünün ardında {C2} yürüyor. Önce birini, sonra ötekini yen.",
    "{H} kimse rahat yürüyemiyor. {C} ve {C2} avına çık; dönüşte seni sofram bekler.",
    "Erlik'in iki kölesi aynı yolu tutmuş: {C} ve {C2}. Yolu açmak sana düşer.",
]
TAS = [
    "{H} Ötüken Taşları kara ruhların gölgesinde kaldı. {T} taş kır, içlerindeki kutu topla.",
    "Kam der ki her kırılan Ötüken Taşı Erlik'in zincirini gevşetir. {T} taş kır, kut yine bize aksın.",
    "Taşların içinde atalarımızın kutu uyur. {T} Ötüken Taşı kır, kutu uyandır.",
    "Ötüken Taşlarına kara bir pas sinmiş. {T} taşı kırıp kutu özgür bırak; yoksa {Y} de o pas sarar.",
]
TAS_AV = [
    "{C} taşların çevresine üşüşmüş. {N} tanesini yen, sonra {T} Ötüken Taşı kır.",
    "Taşları bekleyen {C} var; kutu onlardan önce sen topla. {N} {C} yen, {T} taş kır.",
    "Kut taşlarından sızan ışık {C} sürülerini çekiyor. {N} tanesini yen ve {T} taşı kır.",
]
SECKIN = [
    "Sürünün başını {C} çekiyor. Başları düşerse ötekiler dağılır: {N} {C} yen.",
    "Sıradan yaratıklar bir şey değil; asıl tehlike {C}. Ağır vururlar, dikkat et. {N} tane yen.",
    "Er meydanında senin gibisi az görüldü. Şimdi gerçek sınav: {N} {C} yen.",
]
BOSS = [
    "Hepsinin başı {B}. Erlik onu yeniden kaldırdı ve yine {Y} tehdit ediyor. Bir kez daha yen; bu kez ben de dua edeceğim.",
    "{B} öldü sanmıştık, ama yine dirildi. {Bi} yen, {H} huzur dönsün.",
    "Son iş en zoru, alp: {Bi} yen. Döndüğünde adın obada destan olsun.",
]
BUYUK = [
    "Artık sana güveniyorum. Büyük sürek avı başlasın: {N} {C}, {N2} {C2} ve {T} Ötüken Taşı. Bitirince {H} adın anılacak.",
    "Bu son görev, alp: {H} ne kadar kara ruh varsa üzerine yürü. {C}, {C2} ve Ötüken Taşları… hepsinden payını al.",
]
SECKIN_SON = [
    "Son sınav: sürülerin başı {C}. {N} tanesini yen, {T} Ötüken Taşı kır; {H} senin adın söylensin.",
    "{C} yeniden toplanıyor. Başları ezilmedikçe huzur yok: {N} {C} yen ve {T} taşı kır.",
]

BASLIK = {
    "tek": ["{C} Avı", "{C} Sürüsü", "{C} Baskını", "{C} Tehdidi", "Yoldaki Gölgeler", "Gece Nöbeti", "Ocak Başında",
            "Öç Avı", "Sürek Avı", "Yolu Açmak", "Sessiz Gece", "{C} Yuvası"],
    "cift": ["Çifte Tehlike", "İki Yandan", "Kuşatmayı Kır", "Karışık Sürü", "Birlik Olmuş Düşman"],
    "tas": ["Kut Kırıntıları", "Taşlardaki Kut", "Uyuyan Kut", "Kara Pas", "Taş Kıran"],
    "tas_av": ["Taşların Bekçileri", "Kut Avcısı", "Işığa Üşüşenler"],
    "seckin": ["Sürünün Başı", "Ağır Vuranlar", "Er Meydanı"],
    "boss": ["Başbuğun Dönüşü", "Yeniden Dirilen", "Son Hesap"],
    "buyuk": ["Büyük Sürek", "Kara Ruhlara Karşı"],
    "seckin_son": ["Başları Ez", "Son Sınav"],
}

# zincirler: (tür, canavarlar (a/b/c ya da e = seçkin), büyük mü)
ZINCIRLER = [
    [("tek", "a", 0), ("tek", "b", 0), ("tas", "", 0), ("cift", "ab", 0), ("tek", "c", 0), ("tas_av", "c", 0),
     ("seckin", "e", 0), ("cift", "ca", 1), ("boss", "", 0)],
    [("tek", "b", 0), ("tas", "", 0), ("tek", "c", 0), ("tek", "a", 0), ("cift", "bc", 0), ("seckin", "e", 0),
     ("tas", "", 1), ("tek", "c", 1), ("buyuk", "ab", 0)],
    [("tas", "", 0), ("tek", "c", 0), ("tek", "a", 0), ("cift", "ac", 0), ("tek", "b", 0), ("tas_av", "a", 0),
     ("tek", "a", 1), ("cift", "bc", 1), ("seckin_son", "e", 0)],
]


def esya_bul(kaynak):
    for f in (RES / "UnitProfile").rglob("*.asset"):
        t = f.read_text(encoding="utf-8", errors="ignore")
        if re.search(r"^  resourceName: " + re.escape(kaynak) + r"$", t, re.M):
            return t
    raise SystemExit(f"profil yok: {kaynak}")


_profil = {}


def profil(kaynak):
    """(görünen ad, seçkin mi)"""
    if kaynak not in _profil:
        t = esya_bul(kaynak)
        ad = re.search(r'^  displayName: "?(.*?)"?$', t, re.M).group(1)
        ad = ad.encode("ascii", "ignore").decode("unicode_escape") if "\\u" in ad else ad
        sertlik = re.search(r"^  defaultToughness: ?(.*)$", t, re.M)
        _profil[kaynak] = (ad, bool(sertlik and sertlik.group(1).strip() == "Solo Dungeon Minion"))
    return _profil[kaynak]


def kadro():
    cs = (ROOT / "Assets/Otuken/Editor/HaritaDoldur.cs").read_text(encoding="utf-8")
    sonuc = {}
    for sahne, liste in re.findall(r'\{ "(\w+)", new\[\] \{ ([^}]*) \} \}', cs):
        sonuc[sahne] = list(dict.fromkeys(re.findall(r'"([^"]+)"', liste)))
    sonuc["FeaturesDemoZone"] = YAYLA
    return sonuc


def bulunma(ad):
    """'nda / 'nde (Umay Tarlaları'nda, Börü Tepesi'nde)"""
    return ad + ("'nda" if H.son_unlu(ad) in "aıou" else "'nde")


def guid(ad):
    return uuid.uuid5(NS, "dosya:" + ad).hex


def rid(ad, k):
    return int(uuid.uuid5(NS, f"rid:{ad}:{k}").int % (2 ** 62)) + 1000


def gorev_yaz(kaynak, ad, metin, hedefler, onkosul, xp, para, odul):
    refs = []
    adimlar = []
    for k, h in enumerate(hedefler):
        r = rid(kaynak, k)
        adimlar.append(f"    - rid: {r}\n")
        refs.append(
            f"    - rid: {r}\n      type: {{class: KillObjective, ns: AnyRPG, asm: Assembly-CSharp}}\n      data:\n"
            f"        amount: {h[1]}\n        deprecatedType: \n        overrideDisplayName: {q(h[2])}\n"
            f"        targetName: {h[0]}\n")
    if onkosul:
        on = ("  prerequisiteConditions:\n  - requireAny: 0\n    reverseMatch: 0\n    questPrerequisites:\n"
              f"    - prerequisiteName: {onkosul}\n      stepIndex: -1\n      requireComplete: 1\n      requireTurnedIn: 1\n")
    else:
        on = "  prerequisiteConditions: []\n"
    odul_satiri = f"  itemRewardNames:\n  - {odul}\n" if odul else "  itemRewardNames: []\n"
    m_name = H.dosya_adi(kaynak)
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
         "  maxItemRewards: 0\n" + odul_satiri + "  maxFactionRewards: 0\n  factionRewards: []\n"
         "  maxAbilityRewards: 0\n  abilityRewardNames: []\n  maxSkillRewards: 0\n  skillRewardNames: []\n"
         "  steps:\n  - questObjectives:\n" + "".join(adimlar) + on +
         "  turnInItems: 0\n  allowRawComplete: 0\n  references:\n    version: 2\n    RefIds:\n" + "".join(refs))
    (QUEST_DIR / (m_name + ".asset")).write_text(t, encoding="utf-8")
    (QUEST_DIR / (m_name + ".asset.meta")).write_text(
        f"fileFormatVersion: 2\nguid: {guid('gorev:' + kaynak)}\nNativeFormatImporter:\n  externalObjects: {{}}\n"
        "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")


def npc_yaz(kaynak_dosya, profil_adi, ad, tanim, gorevler):
    H.NPC_DIR = NPC_DIR
    eski_guid = H.guid
    H.guid = lambda s: guid(s)
    try:
        H.yardimci_yaz(kaynak_dosya, profil_adi, ad, tanim, gorevler)
    finally:
        H.guid = eski_guid


def sec(liste, i):
    return liste[i % len(liste)]


def main():
    QUEST_DIR.mkdir(exist_ok=True)
    NPC_DIR.mkdir(exist_ok=True)
    H.klasor_meta(QUEST_DIR, "Yan")
    H.klasor_meta(NPC_DIR, "YanGorev")
    for eski in list(QUEST_DIR.glob("*.asset*")) + list(NPC_DIR.glob("*.asset*")):
        eski.unlink()

    canavarlar = kadro()
    sayac = 0
    erkek = kadin = 0
    yerlesim = []
    for i, h in enumerate(H.HARITALAR):
        sahne = h["sahne"]
        harita = h["ad"]
        havuz = [c for c in dict.fromkeys([h["av"][0]] + canavarlar[sahne]) if not profil(c)[1]]
        seckinler = [c for c in dict.fromkeys(canavarlar[sahne]) if profil(c)[1]]
        boss = h["boss"]
        kisiler = KISILER[sahne]
        assert len(kisiler) == 3, sahne
        yerlesim.append((sahne, [k[0] for k in kisiler]))
        for j, (pad, gad, cins, tanim, korunan, dert) in enumerate(kisiler):
            a, b, c = sec(havuz, j), sec(havuz, j + 1), sec(havuz, j + 2)
            if len(havuz) >= 3 and c == a:
                c = sec(havuz, j + 3)
            e = sec(seckinler, j) if seckinler else None
            dugumler = []
            onceki = None
            zincir = ZINCIRLER[j]
            for k, (tur, kimler, buyuk) in enumerate(zincir):
                son = k == len(zincir) - 1
                tohum = i * 7 + j * 3 + k
                # boss'suz haritada (Ötüken Yaylası) boss görevi seçkinlere, seçkinsiz haritada seçkin görevi büyük ava döner
                if tur == "boss" and not boss:
                    tur = "seckin_son" if e else "buyuk"
                    kimler = "e" if e else "ab"
                if tur in ("seckin", "seckin_son") and not e:
                    tur, kimler, buyuk = ("tek", "c", 1) if tur == "seckin" else ("buyuk", "ab", 0)
                sozluk = {"a": a, "b": b, "c": c, "e": e}
                adlar = [sozluk[x] for x in kimler]
                n = 4 + k // 2 + (3 if buyuk else 0)
                n2 = max(3, n - 2)
                t_say = 3 + k // 3 + (3 if buyuk else 0)
                if tur in ("seckin", "seckin_son"):
                    n = 2 + k // 4
                d = dict(H=bulunma(harita), Y=korunan, N=n, N2=n2, T=t_say,
                         C=profil(adlar[0])[0] if adlar else "", C2=profil(adlar[1])[0] if len(adlar) > 1 else "",
                         B=boss[1] if boss else "", Bi=H.belirtme(boss[1]) if boss else "")
                if tur == "tek":
                    metin, hedef = sec(TEK, tohum), [(adlar[0], n, d["C"] + " yen")]
                elif tur == "cift":
                    metin = sec(CIFT, tohum)
                    hedef = [(adlar[0], n, d["C"] + " yen"), (adlar[1], n2, d["C2"] + " yen")]
                elif tur == "tas":
                    metin, hedef = sec(TAS, tohum), [("Otuken Tasi", t_say, "Ötüken Taşı kır")]
                elif tur == "tas_av":
                    metin = sec(TAS_AV, tohum)
                    hedef = [(adlar[0], n, d["C"] + " yen"), ("Otuken Tasi", t_say, "Ötüken Taşı kır")]
                elif tur == "seckin":
                    metin, hedef = sec(SECKIN, tohum), [(adlar[0], n, d["C"] + " yen")]
                elif tur == "seckin_son":
                    metin = sec(SECKIN_SON, tohum)
                    hedef = [(adlar[0], n, d["C"] + " yen"), ("Otuken Tasi", t_say, "Ötüken Taşı kır")]
                elif tur == "boss":
                    metin, hedef = sec(BOSS, tohum), [(boss[0], 1, H.belirtme(boss[1]) + " yen")]
                else:  # buyuk
                    d["N"], d["N2"] = n + 3, n
                    metin = sec(BUYUK, tohum)
                    hedef = [(adlar[0], n + 3, d["C"] + " yen"), (adlar[1], n, d["C2"] + " yen"),
                             ("Otuken Tasi", t_say, "Ötüken Taşı kır")]
                metin = metin.format(**d)
                metin = metin[0].upper() + metin[1:]
                if k == 0:
                    metin = f"Ben {gad}. {dert} " + metin
                baslik = sec(BASLIK[tur], tohum).format(**d) + f" ({k + 1}/{len(zincir)})"
                kaynak = f"Yan {i:02d}-{j + 1}-{k + 1} " + H.dosya_adi(
                    sec(BASLIK[tur], tohum).format(**d).translate(str.maketrans("çğıöşüÇĞİÖŞÜ", "cgiosuCGIOSU")))
                agirlik = 0.04 if son else 0.02
                para = (4 + H.seviye(i) + k) * (3 if son else 1)
                odul = CEVHER[i] if son else None
                gorev_yaz(kaynak, baslik, metin, hedef, onceki, H.tecrube(i, agirlik), para, odul)
                dugumler.append(H.dugum(kaynak, True, True))
                onceki = kaynak
                sayac += 1
            if cins == "k":
                model = KADIN[kadin % len(KADIN)]
                kadin += 1
            else:
                model = ERKEK[erkek % len(ERKEK)]
                erkek += 1
            npc_yaz(model, pad, gad, tanim, dugumler)

    satirlar = "".join(
        f'            {{ "{s}", new[] {{ {", ".join(chr(34) + p + chr(34) for p in ps)} }} }},\n' for s, ps in yerlesim)
    cs = ("// yan_gorevler.py yazar; elle değiştirme\nusing System.Collections.Generic;\n\nnamespace Otuken.EditorAraclari {\n\n"
          "    /// <summary>yan görevler: her haritanın üç görev vereni (girişin çevresine, HaritaDoldur koyar)</summary>\n"
          "    public static class YanGorevVerisi {\n"
          "        public static readonly Dictionary<string, string[]> Haritalar = new Dictionary<string, string[]>() {\n"
          + satirlar + "        };\n    }\n}\n")
    (ROOT / "Assets/Otuken/Editor/YanGorevVerisi.cs").write_text(cs, encoding="utf-8")
    meta = ROOT / "Assets/Otuken/Editor/YanGorevVerisi.cs.meta"
    if not meta.exists():
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid('cs:YanGorevVerisi')}\nMonoImporter:\n  externalObjects: {{}}\n"
                        "  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n"
                        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n", encoding="utf-8")
    print(f"{sayac} yan görev, {sum(len(v) for v in KISILER.values())} görev veren yazıldı")


if __name__ == "__main__":
    main()
