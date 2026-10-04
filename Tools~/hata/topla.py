#!/usr/bin/env python3
"""
Ötüken Destanı hata raporlarını toplar (GitHub Actions, "Hata raporları" iş akışı).

Oyun (HataBildirici.cs) her raporu ntfy.sh'deki gizli kanala gönderir. Bu betik kanalı okur ve
"hata-kayitlari" dalındaki hata panosunu günceller:
  README.md                 bütün sorunların tablosu (tür, tekrar, cihaz, sürüm, son görülme, durum)
  kayitlar/<anahtar>.md     her farklı sorunun ayrıntısı: ilk rapor, görülmeler, ekran görüntüleri
  ekler/<kimlik>.jpg        ekran görüntüleri
  durum.json                okunan son mesaj ve sayaçlar
Aynı hata (imza) her cihazda tek kayıttır, tekrarları sayılır. Düzeltildi diye işaretlenen bir hata
düzeltmenin sürümünde ya da sonrasında yeniden görülürse "yeniden görüldü" olur.
Depoda Issues açıksa her sorun ayrıca bir kayıt (issue) olarak açılır; GitHub sahibine bildirim gönderir.

Ortam: HATA_KANALI (ntfy kanalı), GH_TOKEN, DEPO (sahip/depo), KAYIT_KLASORU (dalın açıldığı klasör).
İsteğe bağlı: DENEME=1 önce kanala bir deneme raporu gönderir (sistemi uçtan uca sınamak için).
Elle: topla.py duzeltildi <anahtar> <sürüm>   bir sorunu düzeltildi diye işaretler.
APK iş akışı: topla.py derleme <basarili|basarisiz> <numara> <commit> <bağlantı> [rapor.txt]  son derlemeyi yazar.
Yalnız Python'un standart kitaplığını kullanır.
"""
import datetime
import hashlib
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

NTFY = "https://ntfy.sh"
API = "https://api.github.com"
DAL = "hata-kayitlari"
TZ = datetime.timezone(datetime.timedelta(hours=3))  # İstanbul

TUR_ADI = {"istisna": "İstisna", "hata": "Hata", "cokme": "Çökme", "anr": "Donma", "bellek": "Bellek",
           "performans": "Yavaşlık", "arayuz": "Arayüz", "oyuncu": "Oyuncu bildirimi"}
TUR_ETIKETI = {"istisna": "hata", "hata": "hata", "cokme": "çökme", "anr": "çökme", "bellek": "performans",
               "performans": "performans", "arayuz": "arayüz", "oyuncu": "oyuncu-bildirimi"}
ETIKET_RENGI = {"hata-raporu": "5319e7", "hata": "d73a4a", "çökme": "b60205", "performans": "fbca04",
                "arayüz": "0e8a16", "oyuncu-bildirimi": "1d76db"}
TUR_SIRASI = ["cokme", "anr", "istisna", "hata", "bellek", "performans", "arayuz", "oyuncu"]

kanal = os.environ.get("HATA_KANALI", "").strip()
token = os.environ.get("GH_TOKEN", "").strip()
depo = os.environ.get("DEPO", "").strip()
klasor = os.environ.get("KAYIT_KLASORU", "kayit-dali")


def gunluk(*a):
    print(*a, flush=True)


def yol(*parcalar):
    return os.path.join(klasor, *parcalar)


# ---------------------------------------------------------------- GitHub (yalnız Issues açıksa)

def gh(yontem, yol_, govde=None):
    url = yol_ if yol_.startswith("http") else API + yol_
    veri = json.dumps(govde).encode() if govde is not None else None
    istek = urllib.request.Request(url, data=veri, method=yontem)
    istek.add_header("Authorization", "Bearer " + token)
    istek.add_header("Accept", "application/vnd.github+json")
    istek.add_header("X-GitHub-Api-Version", "2022-11-28")
    if veri is not None:
        istek.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(istek, timeout=60) as yanit:
            icerik = yanit.read()
            return json.loads(icerik) if icerik else {}
    except urllib.error.HTTPError as e:
        if e.code in (404, 410, 422):
            return None
        gunluk("GitHub hatası", yontem, yol_, e.code, e.read()[:300])
        raise


def issues_acik():
    if not token or not depo:
        return False
    try:
        bilgi = gh("GET", f"/repos/{depo}")
        return bool(bilgi and bilgi.get("has_issues"))
    except Exception:
        return False


def etiketleri_hazirla():
    for ad, renk in ETIKET_RENGI.items():
        if gh("GET", f"/repos/{depo}/labels/{urllib.parse.quote(ad)}") is None:
            gh("POST", f"/repos/{depo}/labels", {"name": ad, "color": renk})


# ---------------------------------------------------------------- ntfy

def ntfy_oku(since):
    url = f"{NTFY}/{urllib.parse.quote(kanal)}/json?poll=1&since={since}"
    with urllib.request.urlopen(url, timeout=60) as yanit:
        satirlar = yanit.read().decode("utf-8", "replace").splitlines()
    mesajlar = []
    for s in satirlar:
        s = s.strip()
        if not s:
            continue
        try:
            m = json.loads(s)
        except ValueError:
            continue
        if m.get("event") == "message":
            mesajlar.append(m)
    return mesajlar


def ntfy_deneme():
    """sistemi sınamak için kanala oyunun gönderdiği biçimde bir rapor yollar"""
    simdi = datetime.datetime.now(TZ).strftime("%d.%m.%Y %H:%M")
    metin = ("tur: hata\nimza: 00000000\nkimlik: deneme" + str(int(time.time())) + "\nsurum: deneme\ncihaz: GitHub Actions\n"
             "sistem: ubuntu\nekran: -\nsahne: -\noyuncu: -\nsure: 0 sn\n--- mesaj\nDeneme raporu (" + simdi + "): hata bildirim "
             "sistemi çalışıyor.\n")
    govde = json.dumps({"topic": kanal, "title": "Hata: Deneme raporu (sistem sınaması)", "message": metin,
                        "tags": ["white_check_mark"]}).encode()
    istek = urllib.request.Request(NTFY, data=govde, method="POST")
    istek.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(istek, timeout=30) as yanit:
        gunluk("deneme raporu gönderildi", yanit.status)


def ek_indir(url):
    with urllib.request.urlopen(url, timeout=60) as yanit:
        return yanit.read()


# ---------------------------------------------------------------- biçim

def ayristir(metin):
    alanlar, bolumler, ad, satirlar = {}, {}, None, []
    for satir in metin.split("\n"):
        if satir.startswith("--- "):
            if ad:
                bolumler[ad] = "\n".join(satirlar).strip()
            ad, satirlar = satir[4:].strip(), []
        elif ad is None:
            k, _, v = satir.partition(":")
            if k.strip():
                alanlar[k.strip()] = v.strip()
        else:
            satirlar.append(satir)
    if ad:
        bolumler[ad] = "\n".join(satirlar).strip()
    return alanlar, bolumler


def zaman_yaz(saniye):
    return datetime.datetime.fromtimestamp(saniye, TZ).strftime("%d.%m.%Y %H:%M")


def kod(metin):
    metin = (metin or "").replace("```", "ˋˋˋ")
    return f"```text\n{metin}\n```"


def katla(baslik, metin):
    if not metin:
        return ""
    return f"<details><summary>{baslik}</summary>\n\n{kod(metin)}\n\n</details>\n"


def surum_sayisi(s):
    try:
        return tuple(int(x) for x in s.split("."))
    except (ValueError, AttributeError):
        return ()


def dosya_adi(anahtar):
    return re.sub(r"[^a-zA-Z0-9_-]+", "-", anahtar).strip("-")


def tablo_hucresi(metin):
    return (metin or "").replace("|", "\\|").replace("\n", " ")


def ilk_rapor(alanlar, bolumler):
    tur = alanlar.get("tur", "?")
    parcalar = [f"**{TUR_ADI.get(tur, tur)}** · sürüm **{alanlar.get('surum', '?')}** · {alanlar.get('cihaz', '?')} · "
                f"{alanlar.get('sistem', '?')} · ekran {alanlar.get('ekran', '?')} · {alanlar.get('sahne', '?')}", ""]
    if tur == "oyuncu":
        parcalar += ["### Oyuncunun notu", "", "> " + (bolumler.get("mesaj") or "").replace("\n", "\n> "), ""]
    else:
        parcalar += ["### Mesaj", kod(bolumler.get("mesaj")), ""]
    if bolumler.get("yigin"):
        parcalar += ["### Yığın izi", kod(bolumler.get("yigin")), ""]
    if bolumler.get("ayrinti"):
        parcalar += ["### Ayrıntı", kod(bolumler.get("ayrinti")), ""]
    ortam = "\n".join(f"{k}: {v}" for k, v in alanlar.items() if k not in ("imza", "kimlik", "ek"))
    parcalar += [katla("Ortam", ortam), katla("Oyunun durumu", bolumler.get("durum")),
                 katla("Son kayıt satırları", bolumler.get("kayit"))]
    return "\n".join(parcalar)


def sayac_tablosu(k):
    return (f"| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |\n|---|---|---|---|---|\n"
            f"| {k['adet']} | {len(k['cihazlar'])} | {', '.join(k['surumler'][-8:])} | {zaman_yaz(k['ilk'])} | {zaman_yaz(k['son'])} |\n")


def durum_yazisi(k):
    if k.get("duzeltildi"):
        return f"✅ düzeltildi ({k['duzeltildi']})"
    if k.get("yeniden"):
        return "🔁 yeniden görüldü"
    return "🔴 açık"


# ---------------------------------------------------------------- işleme

def rapor_isle(m, durum, issue):
    alanlar, bolumler = ayristir(m["message"])
    tur = alanlar.get("tur", "hata")
    imza = alanlar.get("imza", "00000000")
    kimlik = alanlar.get("kimlik", m["id"])
    surum = alanlar.get("surum", "?")
    cihaz = alanlar.get("cihaz", "?")
    zaman = m.get("time", int(time.time()))
    if tur == "oyuncu":
        anahtar = "oyuncu-" + kimlik
    elif tur == "arayuz":
        anahtar = "arayuz-" + alanlar.get("ekran", "x")
    else:
        anahtar = tur + "-" + imza
    anahtar = dosya_adi(anahtar)

    kayitlar = durum["kayitlar"]
    k = kayitlar.get(anahtar)
    yeni = k is None
    if yeni:
        baslik = (m.get("title") or TUR_ADI.get(tur, tur)).strip()
        if tur == "arayuz":
            baslik = "Arayüz yerleşimi, " + alanlar.get("ekran", "?") + " ekran"
        k = {"tur": tur, "baslik": baslik[:200], "adet": 0, "cihazlar": [], "surumler": [], "ilk": zaman, "son": zaman,
             "ilk_rapor": ilk_rapor(alanlar, bolumler), "gorulmeler": [], "ekler": [], "duzeltildi": None,
             "yeniden": False, "issue": None, "notlar": []}
        kayitlar[anahtar] = k
    yeni_surum = surum not in k["surumler"]
    yeni_cihaz = cihaz not in k["cihazlar"]
    k["adet"] += 1
    k["son"] = max(k["son"], zaman)
    if yeni_surum:
        k["surumler"].append(surum)
    if yeni_cihaz:
        k["cihazlar"].append(cihaz)
    k["gorulmeler"] = (k["gorulmeler"] + [f"{zaman_yaz(zaman)} · {surum} · {cihaz} · {alanlar.get('sahne', '?')} · "
                                          f"{alanlar.get('oyuncu', '?')} · {alanlar.get('ekran', '?')}"])[-60:]
    if tur == "arayuz" and not yeni:
        k["notlar"] = (k["notlar"] + [f"{zaman_yaz(zaman)} · {surum} · {cihaz} — {m.get('title', '')}\n\n"
                                      + kod(bolumler.get("ayrinti"))])[-10:]
    # arayüz raporu sorun değil, bilgidir: çakışma yoksa "yeniden görüldü" sayılmaz
    bilgi = tur == "arayuz" and "çakışma yok" in (bolumler.get("mesaj") or "")
    if k.get("duzeltildi") and not bilgi and surum_sayisi(surum) >= surum_sayisi(k["duzeltildi"]):
        k["yeniden"] = True
        k["notlar"] = (k["notlar"] + [f"{zaman_yaz(zaman)}: {k['duzeltildi']} sürümünde düzeltildi denmişti ama "
                                      f"{surum} sürümünde yeniden görüldü ({cihaz})."])[-10:]
        k["duzeltildi"] = None
    durum["kimlikler"][kimlik] = anahtar

    if issue:
        try:
            issue_guncelle(anahtar, k, yeni, yeni_surum or yeni_cihaz, surum, cihaz, zaman, bolumler)
        except Exception as e:
            gunluk("issue güncellenemedi", anahtar, e)
    gunluk(("yeni" if yeni else "tekrar"), anahtar, k["adet"])


def issue_guncelle(anahtar, k, yeni, haber, surum, cihaz, zaman, bolumler):
    govde = (f"{sayac_tablosu(k)}\n{k['ilk_rapor']}\n\nAyrıntılar ve ekran görüntüleri: "
             f"https://github.com/{depo}/blob/{DAL}/kayitlar/{anahtar}.md\n\n<!-- hata-anahtar: {anahtar} -->")
    numara = k.get("issue")
    kayit = gh("GET", f"/repos/{depo}/issues/{numara}") if numara else None
    if not kayit:
        yeni_kayit = gh("POST", f"/repos/{depo}/issues", {"title": k["baslik"], "body": govde,
                                                         "labels": ["hata-raporu", TUR_ETIKETI.get(k["tur"], "hata")]})
        if yeni_kayit:
            k["issue"] = yeni_kayit["number"]
        return
    gh("PATCH", f"/repos/{depo}/issues/{numara}", {"body": govde})
    if kayit.get("state") == "closed":
        gh("PATCH", f"/repos/{depo}/issues/{numara}", {"state": "open"})
        gh("POST", f"/repos/{depo}/issues/{numara}/comments",
           {"body": f"Kapatılmıştı ama yeniden görüldü: sürüm **{surum}**, {cihaz}, {zaman_yaz(zaman)}."})
    elif haber and not yeni:
        gh("POST", f"/repos/{depo}/issues/{numara}/comments",
           {"body": f"Yeni sürümde ya da cihazda görüldü: sürüm **{surum}**, {cihaz}, {zaman_yaz(zaman)}."})


def ek_isle(m, durum, issue):
    ek = m["attachment"]
    ad = os.path.basename(ek.get("name", ""))
    if not re.fullmatch(r"[a-zA-Z0-9_-]+\.jpg", ad):
        return
    kimlik = ad[:-4]
    try:
        veri = ek_indir(ek["url"])
    except Exception as e:
        gunluk("ek indirilemedi (süresi dolmuş olabilir)", ad, e)
        return
    os.makedirs(yol("ekler"), exist_ok=True)
    with open(yol("ekler", ad), "wb") as f:
        f.write(veri)
    anahtar = durum["kimlikler"].get(kimlik)
    k = durum["kayitlar"].get(anahtar) if anahtar else None
    if not k:
        gunluk("ekin raporu bulunamadı", ad)
        return
    if ad not in k["ekler"]:
        k["ekler"] = (k["ekler"] + [ad])[-12:]
    if issue and k.get("issue"):
        url = f"https://raw.githubusercontent.com/{depo}/{DAL}/ekler/{ad}"
        try:
            gh("POST", f"/repos/{depo}/issues/{k['issue']}/comments", {"body": f"Ekran görüntüsü:\n\n![ekran görüntüsü]({url})"})
        except Exception as e:
            gunluk("issue'ya ekran görüntüsü eklenemedi", e)
    gunluk("ekran görüntüsü", anahtar, ad)


# ---------------------------------------------------------------- pano

def kayit_dosyasi(anahtar, k):
    parcalar = [f"# {k['baslik']}", "", f"Durum: {durum_yazisi(k)}" + (f" · [issue #{k['issue']}](https://github.com/{depo}/issues/{k['issue']})" if k.get("issue") else ""),
                "", sayac_tablosu(k), "", "## İlk rapor", "", k["ilk_rapor"], ""]
    if k["ekler"]:
        parcalar += ["## Ekran görüntüleri", ""]
        for ad in reversed(k["ekler"]):
            parcalar += [f"![{ad}](../ekler/{ad})", ""]
    if k["notlar"]:
        parcalar += ["## Notlar", ""]
        for n in reversed(k["notlar"]):
            parcalar += [n, ""]
    parcalar += ["## Görülmeler (yeniden eskiye)", "", "| Zaman · sürüm · cihaz · harita · oyuncu · ekran |", "|---|"]
    for g in reversed(k["gorulmeler"]):
        parcalar.append(f"| {tablo_hucresi(g)} |")
    parcalar += ["", f"Anahtar: `{anahtar}`"]
    return "\n".join(parcalar) + "\n"


def pano(durum):
    kayitlar = durum["kayitlar"]
    acik = [(a, k) for a, k in kayitlar.items() if not k.get("duzeltildi")]
    kapali = [(a, k) for a, k in kayitlar.items() if k.get("duzeltildi")]

    def sira(ak):
        a, k = ak
        tur_sira = TUR_SIRASI.index(k["tur"]) if k["tur"] in TUR_SIRASI else 99
        return (not k.get("yeniden"), tur_sira, -k["son"])

    def satir(a, k):
        return (f"| {durum_yazisi(k)} | [{tablo_hucresi(k['baslik'][:110])}](kayitlar/{a}.md) | {TUR_ADI.get(k['tur'], k['tur'])} | "
                f"{k['adet']} | {len(k['cihazlar'])} | {', '.join(k['surumler'][-3:])} | {zaman_yaz(k['son'])} |")

    toplam = sum(k["adet"] for k in kayitlar.values())
    parcalar = ["# Ötüken Destanı · Hata panosu", "",
                f"Oyunun kendiliğinden gönderdiği hata, çökme, donma, yavaşlık ve arayüz raporları ile oyuncuların "
                f"\"Sorun Bildir\" notları. Son güncelleme: {zaman_yaz(time.time())}.", "",
                f"**{len(acik)}** açık sorun · **{len(kapali)}** düzeltilen · toplam **{toplam}** rapor", ""]
    d = durum.get("derleme")
    if d:
        isaret = "✅ başarılı" if d["sonuc"] == "basarili" else "❌ başarısız"
        parcalar += ["## Son APK derlemesi", "",
                     f"Derleme [{d['numara']}]({d['baglanti']}) · {isaret} · commit `{d['commit']}` · {zaman_yaz(d['zaman'])}", ""]
        if d.get("uyarilar"):
            parcalar += [f"Tanı raporunda {len(d['uyarilar'])} uyarı "
                         f"([tam rapor](https://github.com/{depo}/releases/download/tani/tani.zip)):", "", kod("\n".join(d["uyarilar"])), ""]
        else:
            parcalar += ["Tanı raporunda uyarı yok.", ""]
        bot = d.get("bot")
        if bot:
            parcalar += ["### Otomatik oyun testi", "",
                         f"Bot yeni oyun başlatıp haritaları gezdi: **{bot['durum']}**, {bot['toplamHata']} hata/istisna.", ""]
            if bot.get("binek"):
                parcalar += [f"- Binek denemesi: {bot['binek']}"]
            if bot.get("gunluk"):
                parcalar += [f"- Günlük görevler (botun bir günü): {bot['gunluk']}"]
            if bot.get("av"):
                parcalar += [f"- İlk haritada av: {bot['av']}"]
            if bot.get("dunya"):
                parcalar += [f"- Dünya haritası: {bot['dunya']}"]
            if bot.get("binek") or bot.get("gunluk") or bot.get("av") or bot.get("dunya"):
                parcalar += [""]
            parcalar += [
                         "| | Harita | Sonuç | Yükleme | Hata | Not |", "|---|---|---|---|---|---|"]
            for h in bot["haritalar"]:
                isaret = "✅" if h.get("sonuc") == "tamam" and not h.get("hata") else ("⚠️" if h.get("sonuc") == "tamam" else "❌")
                parcalar.append(f"| {isaret} | {tablo_hucresi(h.get('ad') or h.get('sahne'))} | {tablo_hucresi(h.get('sonuc'))} | "
                                f"{int(h.get('yuklemeSuresi') or 0)} sn | {h.get('hata') or 0} | {tablo_hucresi('; '.join(x for x in (h.get('not'), h.get('bilgi'), ('ok: ' + h['okHedefi']) if h.get('okHedefi') else None) if x))} |")
            parcalar.append("")
    baslik = "| Durum | Sorun | Tür | Tekrar | Cihaz | Sürüm | Son görülme |\n|---|---|---|---|---|---|---|"
    parcalar += ["## Açık sorunlar", ""]
    if acik:
        parcalar += [baslik] + [satir(a, k) for a, k in sorted(acik, key=sira)]
    else:
        parcalar.append("Açık sorun yok.")
    if kapali:
        parcalar += ["", "## Düzeltilenler", "", baslik] + [satir(a, k) for a, k in sorted(kapali, key=lambda ak: -ak[1]["son"])]
    parcalar += ["", "---", "Bu sayfa her 15 dakikada bir \"Hata raporları\" iş akışıyla güncellenir "
                 "(Tools~/hata/topla.py, Assets/AnyRPG/Core/System/Scripts/Mobile/HataBildirici.cs)."]
    return "\n".join(parcalar) + "\n"


def yaz(dosya, metin):
    os.makedirs(os.path.dirname(dosya), exist_ok=True)
    with open(dosya, "w", encoding="utf-8") as f:
        f.write(metin)


def durum_oku():
    try:
        with open(yol("durum.json"), encoding="utf-8") as f:
            durum = json.load(f)
    except (OSError, ValueError):
        durum = {}
    durum.setdefault("son_zaman", 0)
    durum.setdefault("islenen", [])
    durum.setdefault("kayitlar", {})
    durum.setdefault("kimlikler", {})
    return durum


def durum_yaz(durum):
    if len(durum["kimlikler"]) > 1000:
        durum["kimlikler"] = dict(list(durum["kimlikler"].items())[-1000:])
    yaz(yol("durum.json"), json.dumps(durum, ensure_ascii=False, indent=1))
    for anahtar, k in durum["kayitlar"].items():
        yaz(yol("kayitlar", anahtar + ".md"), kayit_dosyasi(anahtar, k))
    yaz(yol("README.md"), pano(durum))


def derleme_yaz(argumanlar):
    """APK iş akışından: topla.py derleme <başarılı|başarısız> <numara> <commit> <bağlantı> [rapor.txt]"""
    sonuc, numara, commit, baglanti = argumanlar[:4]
    uyarilar = []
    if len(argumanlar) > 4 and os.path.exists(argumanlar[4]):
        with open(argumanlar[4], encoding="utf-8", errors="replace") as f:
            uyarilar = [satir.rstrip()[:300] for satir in f if satir.startswith("!!")][:60]
    durum = durum_oku()
    durum["derleme"] = {"sonuc": sonuc, "numara": numara, "commit": commit[:8], "baglanti": baglanti,
                        "zaman": int(time.time()), "uyarilar": uyarilar}
    bot_isle(durum, numara, os.path.join(os.path.dirname(argumanlar[4]) if len(argumanlar) > 4 else "tani", "oyun_testi.json"))
    durum_yaz(durum)
    gunluk("derleme durumu yazıldı:", sonuc, numara, len(uyarilar), "uyarı")
    return 0


def bot_isle(durum, numara, yol_):
    """otomatik oyun testinin (OyunTesti.cs) sonucu: harita tablosu panoya, bulduğu hatalar kayıtlara"""
    if not os.path.exists(yol_):
        durum["derleme"]["bot"] = None
        return
    try:
        with open(yol_, encoding="utf-8") as f:
            b = json.load(f)
    except (OSError, ValueError) as e:
        gunluk("oyun testi okunamadı", e)
        return
    durum["derleme"]["bot"] = {"durum": b.get("durum", "?"), "toplamHata": b.get("toplamHata", 0),
                               "binek": b.get("binekTesti"), "gunluk": b.get("gunlukGorevler"),
                               "av": b.get("ilkHaritaAvi"), "dunya": b.get("dunyaHaritasi"),
                               "haritalar": [{k: h.get(k) for k in ("sahne", "ad", "sonuc", "yuklemeSuresi", "hata", "not", "bilgi", "okHedefi")}
                                             for h in b.get("haritalar", [])]}
    zaman = int(time.time())
    surum = f"0.1.{numara}"
    for h in b.get("hatalar", []):
        mesaj = (h.get("mesaj") or "").strip()
        ilk = mesaj.split("\n")[0][:200]
        yigin = h.get("yigin") or ""
        imza = hashlib.sha1((h.get("tur", "") + "|" + ilk + "|" + yigin.split("\n")[0]).encode("utf-8")).hexdigest()[:8]
        anahtar = dosya_adi("bot-" + imza)
        k = durum["kayitlar"].get(anahtar)
        if k is None:
            alanlar = {"tur": h.get("tur", "hata"), "surum": surum, "cihaz": "Otomatik oyun testi (CI)", "sistem": "Unity editör",
                       "ekran": "-", "sahne": h.get("sahne", "?")}
            k = {"tur": h.get("tur", "hata"), "baslik": ("Oyun testi: " + ilk)[:200], "adet": 0, "cihazlar": [], "surumler": [],
                 "ilk": zaman, "son": zaman, "ilk_rapor": ilk_rapor(alanlar, {"mesaj": mesaj, "yigin": yigin}),
                 "gorulmeler": [], "ekler": [], "duzeltildi": None, "yeniden": False, "issue": None, "notlar": []}
            durum["kayitlar"][anahtar] = k
        k["adet"] += int(h.get("adet", 1))
        k["son"] = zaman
        if surum not in k["surumler"]:
            k["surumler"].append(surum)
        if "Otomatik oyun testi (CI)" not in k["cihazlar"]:
            k["cihazlar"].append("Otomatik oyun testi (CI)")
        k["gorulmeler"] = (k["gorulmeler"] + [f"{zaman_yaz(zaman)} · derleme {numara} · {h.get('adet', 1)} kez · {h.get('sahne', '?')}"])[-60:]
        if k.get("duzeltildi") and surum_sayisi(surum) >= surum_sayisi(k["duzeltildi"]):
            k["yeniden"] = True
            k["duzeltildi"] = None


def main():
    if len(sys.argv) >= 6 and sys.argv[1] == "derleme":
        return derleme_yaz(sys.argv[2:])
    if len(sys.argv) >= 4 and sys.argv[1] == "duzeltildi":
        durum = durum_oku()
        k = durum["kayitlar"].get(sys.argv[2])
        if not k:
            gunluk("böyle bir kayıt yok:", sys.argv[2])
            return 1
        k["duzeltildi"], k["yeniden"] = sys.argv[3], False
        durum_yaz(durum)
        gunluk("düzeltildi:", sys.argv[2], sys.argv[3])
        return 0

    if not kanal:
        gunluk("HATA_KANALI gerekli")
        return 1
    if os.environ.get("DENEME") == "1":
        ntfy_deneme()
        time.sleep(3)

    durum = durum_oku()
    since = max(int(durum["son_zaman"]) - 120, int(time.time()) - 12 * 3600)
    mesajlar = ntfy_oku(since)
    islenen = set(durum["islenen"])
    yeni = sorted([m for m in mesajlar if m.get("id") not in islenen], key=lambda m: m.get("time", 0))
    gunluk(f"kanalda {len(mesajlar)} mesaj, {len(yeni)} yeni")
    if not yeni and os.path.exists(yol("README.md")):
        return 0

    issue = issues_acik()
    gunluk("Issues", "açık: sorunlar ayrıca issue olarak açılır" if issue else "kapalı: yalnız hata panosu güncellenir")
    if issue and yeni:
        try:
            etiketleri_hazirla()
        except Exception as e:
            gunluk("etiketler hazırlanamadı", e)

    for m in yeni:
        if m.get("attachment") or not (m.get("message") or "").startswith("tur:"):
            continue
        try:
            rapor_isle(m, durum, issue)
        except Exception as e:  # bir rapor bozuksa ötekiler işlensin
            gunluk("rapor işlenemedi", m.get("id"), e)
    for m in yeni:
        if not m.get("attachment"):
            continue
        try:
            ek_isle(m, durum, issue)
        except Exception as e:
            gunluk("ek işlenemedi", m.get("id"), e)

    durum["islenen"] = (durum["islenen"] + [m["id"] for m in yeni])[-1000:]
    for m in yeni:
        durum["son_zaman"] = max(durum["son_zaman"], m.get("time", 0))
    durum_yaz(durum)
    return 0


if __name__ == "__main__":
    sys.exit(main())
