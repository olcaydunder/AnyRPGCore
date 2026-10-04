# Ötüken Destanı · Hata panosu

Oyunun kendiliğinden gönderdiği hata, çökme, donma, yavaşlık ve arayüz raporları ile oyuncuların "Sorun Bildir" notları. Son güncelleme: 04.10.2026 12:26.

**1** açık sorun · **4** düzeltilen · toplam **11** rapor

## Son APK derlemesi

Derleme [26](https://github.com/olcaydunder/AnyRPGCore/actions/runs/37190818819) · ✅ başarılı · commit `19982b5a` · 04.10.2026 12:24

Tanı raporunda uyarı yok.

### Otomatik oyun testi

Bot yeni oyun başlatıp haritaları gezdi: **tamamlandı**, 0 hata/istisna.

- Binek denemesi: binildi, 2,5 sn'de 13 m gidildi, inildi
- Günlük görevler (botun bir günü): günlük görev yok

| | Harita | Sonuç | Yükleme | Hata | Not |
|---|---|---|---|---|---|
| ✅ | Ötüken Yaylası | tamam | 2 sn | 0 |  |
| ✅ | Umay Tarlaları | tamam | 1 sn | 0 |  |
| ✅ | Börü Tepesi | tamam | 0 sn | 0 |  |
| ✅ | Ak Deniz Kıyısı | tamam | 0 sn | 0 |  |
| ❌ | Ordubalık Çarşısı | sorunlu | 1 sn | 0 | yürüyerek ulaşılamayan: Kapi_KafDagiYolu_1 (123, 12, 35) |
| ✅ | Ordubalık Kenti | tamam | 0 sn | 0 |  |
| ✅ | Ulukayın Ormanı | tamam | 0 sn | 0 |  |
| ✅ | Koncolos İni | tamam | 0 sn | 0 | oyuncu öldü (1. seviye), yeniden doğdu |
| ✅ | Kağan Ordası | tamam | 1 sn | 0 |  |
| ❌ | Kaf Dağı Yolu | sorunlu | 0 sn | 0 | yürüyerek ulaşılamayan: Kapi_OrdubalikCarsisi_1 (135, 13, 42) |
| ✅ | Ergenekon Mağarası | tamam | 0 sn | 0 |  |
| ✅ | Kurgan Mezarlığı | tamam | 0 sn | 0 |  |
| ✅ | Ay Dede Koyu | tamam | 1 sn | 0 |  |
| ✅ | Erlik'in Mağarası | tamam | 0 sn | 0 |  |
| ✅ | Tamu Zindanı | tamam | 0 sn | 0 | oyuncu öldü (1. seviye), yeniden doğdu |

## Açık sorunlar

| Durum | Sorun | Tür | Tekrar | Cihaz | Sürüm | Son görülme |
|---|---|---|---|---|---|---|
| 🔴 açık | [Oyuncu bildirimi: merhaba](kayitlar/oyuncu-b9efbe7a2a.md) | Oyuncu bildirimi | 1 | 1 | 0.1.25 | 04.10.2026 11:42 |

## Düzeltilenler

| Durum | Sorun | Tür | Tekrar | Cihaz | Sürüm | Son görülme |
|---|---|---|---|---|---|---|
| ✅ düzeltildi (0.1.19) | [Arayüz yerleşimi, 3120x1335 ekran](kayitlar/arayuz-3120x1335.md) | Arayüz | 2 | 1 | 0.1.17, 0.1.25 | 04.10.2026 11:41 |
| ✅ düzeltildi (0.1.19) | [İstisna: NullReferenceException: Object reference not set to an instance of an object.](kayitlar/istisna-d581f62b.md) | İstisna | 4 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [İstisna: UnityException: Failed to create texture because of invalid parameters.](kayitlar/istisna-9879748b.md) | İstisna | 2 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [Hata: Texture has out of range width (got 46070 max supported 16384)](kayitlar/hata-16e86c78.md) | Hata | 2 | 1 | 0.1.17 | 03.10.2026 23:44 |

---
Bu sayfa her 15 dakikada bir "Hata raporları" iş akışıyla güncellenir (Tools~/hata/topla.py, Assets/AnyRPG/Core/System/Scripts/Mobile/HataBildirici.cs).
