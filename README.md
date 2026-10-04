# Ötüken Destanı · Hata panosu

Oyunun kendiliğinden gönderdiği hata, çökme, donma, yavaşlık ve arayüz raporları ile oyuncuların "Sorun Bildir" notları. Son güncelleme: 04.10.2026 03:39.

**0** açık sorun · **4** düzeltilen · toplam **9** rapor

## Son APK derlemesi

Derleme [24](https://github.com/olcaydunder/AnyRPGCore/actions/runs/37164612951) · ✅ başarılı · commit `6dbf9e78` · 04.10.2026 03:39

Tanı raporunda 1 uyarı ([tam rapor](https://github.com/olcaydunder/AnyRPGCore/releases/download/tani/tani.zip)):

```text
!! zemine oturtulamayan nesne: 1 (GecitTasi (101.1, 16.0, 30.6))
```

### Otomatik oyun testi

Bot yeni oyun başlatıp haritaları gezdi: **tamamlandı**, 0 hata/istisna.

| | Harita | Sonuç | Yükleme | Hata | Not |
|---|---|---|---|---|---|
| ❌ | Ötüken Yaylası | sorunlu | 3 sn | 0 | 12 nesnede eksik/bozuk malzeme: Billboard (malzeme yok), Billboard (malzeme yok), Billboard (malzeme yok), Billboard (malzeme yok), Billboard (malzeme yok) |
| ✅ | Umay Tarlaları | tamam | 1 sn | 0 |  |
| ✅ | Börü Tepesi | tamam | 0 sn | 0 |  |
| ❌ | Ak Deniz Kıyısı | sorunlu | 0 sn | 0 | 1 nesnede eksik/bozuk malzeme: BoatHideWaterMask (malzeme yok) |
| ❌ | Ordubalık Çarşısı | sorunlu | 1 sn | 0 | yürüyerek ulaşılamayan: Kapi_KafDagiYolu_1 (123, 12, 35) |
| ❌ | Ordubalık Kenti | sorunlu | 0 sn | 0 | yürüyerek ulaşılamayan: Kapi_KaganOrdasi_3 (7, 20, 63), Kapi_UlukayinOrmani_1 (-30, 10, 34) |
| ❌ | Ulukayın Ormanı | sorunlu | 0 sn | 0 | yürüyerek ulaşılamayan: Kapi_OrdubalikKenti_0 (-45, 9, 29) |
| ✅ | Koncolos İni | tamam | 0 sn | 0 |  |
| ✅ | Kağan Ordası | tamam | 1 sn | 0 |  |
| ❌ | Kaf Dağı Yolu | sorunlu | 0 sn | 0 | yürüyerek ulaşılamayan: Kapi_OrdubalikCarsisi_1 (135, 13, 42) |
| ✅ | Ergenekon Mağarası | tamam | 0 sn | 0 |  |
| ✅ | Kurgan Mezarlığı | tamam | 0 sn | 0 |  |
| ✅ | Ay Dede Koyu | tamam | 1 sn | 0 |  |
| ✅ | Erlik'in Mağarası | tamam | 0 sn | 0 |  |
| ✅ | Tamu Zindanı | tamam | 0 sn | 0 |  |

## Açık sorunlar

Açık sorun yok.

## Düzeltilenler

| Durum | Sorun | Tür | Tekrar | Cihaz | Sürüm | Son görülme |
|---|---|---|---|---|---|---|
| ✅ düzeltildi (0.1.19) | [İstisna: NullReferenceException: Object reference not set to an instance of an object.](kayitlar/istisna-d581f62b.md) | İstisna | 4 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [İstisna: UnityException: Failed to create texture because of invalid parameters.](kayitlar/istisna-9879748b.md) | İstisna | 2 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [Hata: Texture has out of range width (got 46070 max supported 16384)](kayitlar/hata-16e86c78.md) | Hata | 2 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [Arayüz yerleşimi, 3120x1335 ekran](kayitlar/arayuz-3120x1335.md) | Arayüz | 1 | 1 | 0.1.17 | 03.10.2026 23:09 |

---
Bu sayfa her 15 dakikada bir "Hata raporları" iş akışıyla güncellenir (Tools~/hata/topla.py, Assets/AnyRPG/Core/System/Scripts/Mobile/HataBildirici.cs).
