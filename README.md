# Ötüken Destanı · Hata panosu

Oyunun kendiliğinden gönderdiği hata, çökme, donma, yavaşlık ve arayüz raporları ile oyuncuların "Sorun Bildir" notları. Son güncelleme: 04.10.2026 14:43.

**1** açık sorun · **5** düzeltilen · toplam **12** rapor

## Son APK derlemesi

Derleme [30](https://github.com/olcaydunder/AnyRPGCore/actions/runs/37198581902) · ✅ başarılı · commit `d3fc003e` · 04.10.2026 14:43

Tanı raporunda uyarı yok.

### Otomatik oyun testi

Bot yeni oyun başlatıp haritaları gezdi: **tamamlandı**, 0 hata/istisna.

- Binek denemesi: binildi, 2,5 sn'de 13 m gidildi, inildi
- Günlük görevler (botun bir günü): 12 düşman yen 0/12, 1 hazine sandığını boşalt 0/1, 2 farklı diyara ayak bas 2/2 (tamam)
- İlk haritada av: 0 düşman yenildi, seviye 1 → 1, tecrübe 0 → 0
- Dünya haritası: 15/15 diyar görüntülü; harita üzerinden ışınlanma: oldu

| | Harita | Sonuç | Yükleme | Hata | Not |
|---|---|---|---|---|---|
| ✅ | Ötüken Yaylası | tamam | 3 sn | 0 | ok: Yeni görev: Olcayto Han |
| ✅ | Umay Tarlaları | tamam | 1 sn | 0 | oyuncu öldü (1. seviye), yeniden doğdu; ok: Hazine sandığı (günlük görev) |
| ✅ | Börü Tepesi | tamam | 0 sn | 0 | ok: Hazine sandığı (günlük görev) |
| ✅ | Ak Deniz Kıyısı | tamam | 0 sn | 0 | ok: Hazine sandığı (günlük görev) |
| ❌ | Ordubalık Çarşısı | sorunlu | 1 sn | 0 | yürüyerek ulaşılamayan: Kapi_KafDagiYolu_1 (123, 12, 35); ok: Hazine sandığı (günlük görev) |
| ✅ | Ordubalık Kenti | tamam | 0 sn | 0 | ok: Hazine sandığı (günlük görev) |
| ✅ | Ulukayın Ormanı | tamam | 0 sn | 0 | ok: Hazine sandığı (günlük görev) |
| ✅ | Koncolos İni | tamam | 0 sn | 0 | oyuncu öldü (1. seviye), yeniden doğdu; ok: Hazine sandığı (günlük görev) |
| ✅ | Kağan Ordası | tamam | 1 sn | 0 | ok: Hazine sandığı (günlük görev) |
| ❌ | Kaf Dağı Yolu | sorunlu | 0 sn | 0 | yürüyerek ulaşılamayan: Kapi_OrdubalikCarsisi_1 (135, 13, 42); ok: Hazine sandığı (günlük görev) |
| ✅ | Ergenekon Mağarası | tamam | 0 sn | 0 | ok: Hazine sandığı (günlük görev) |
| ✅ | Kurgan Mezarlığı | tamam | 0 sn | 0 | oyuncu öldü (1. seviye), yeniden doğdu; ok: Hazine sandığı (günlük görev) |
| ✅ | Ay Dede Koyu | tamam | 1 sn | 0 | ok: Hazine sandığı (günlük görev) |
| ✅ | Erlik'in Mağarası | tamam | 0 sn | 0 | ok: Hazine sandığı (günlük görev) |
| ✅ | Tamu Zindanı | tamam | 0 sn | 0 | ok: Hazine sandığı (günlük görev) |

## Açık sorunlar

| Durum | Sorun | Tür | Tekrar | Cihaz | Sürüm | Son görülme |
|---|---|---|---|---|---|---|
| 🔴 açık | [Arayüz yerleşimi, 2664x1073 ekran](kayitlar/arayuz-2664x1073.md) | Arayüz | 1 | 1 | 0.1.27 | 04.10.2026 14:15 |

## Düzeltilenler

| Durum | Sorun | Tür | Tekrar | Cihaz | Sürüm | Son görülme |
|---|---|---|---|---|---|---|
| ✅ düzeltildi (0.1.27) | [Oyuncu bildirimi: merhaba](kayitlar/oyuncu-b9efbe7a2a.md) | Oyuncu bildirimi | 1 | 1 | 0.1.25 | 04.10.2026 11:42 |
| ✅ düzeltildi (0.1.19) | [Arayüz yerleşimi, 3120x1335 ekran](kayitlar/arayuz-3120x1335.md) | Arayüz | 2 | 1 | 0.1.17, 0.1.25 | 04.10.2026 11:41 |
| ✅ düzeltildi (0.1.19) | [İstisna: NullReferenceException: Object reference not set to an instance of an object.](kayitlar/istisna-d581f62b.md) | İstisna | 4 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [İstisna: UnityException: Failed to create texture because of invalid parameters.](kayitlar/istisna-9879748b.md) | İstisna | 2 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [Hata: Texture has out of range width (got 46070 max supported 16384)](kayitlar/hata-16e86c78.md) | Hata | 2 | 1 | 0.1.17 | 03.10.2026 23:44 |

---
Bu sayfa her 15 dakikada bir "Hata raporları" iş akışıyla güncellenir (Tools~/hata/topla.py, Assets/AnyRPG/Core/System/Scripts/Mobile/HataBildirici.cs).
