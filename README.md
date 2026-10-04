# Ötüken Destanı · Hata panosu

Oyunun kendiliğinden gönderdiği hata, çökme, donma, yavaşlık ve arayüz raporları ile oyuncuların "Sorun Bildir" notları. Son güncelleme: 04.10.2026 03:07.

**0** açık sorun · **4** düzeltilen · toplam **9** rapor

## Son APK derlemesi

Derleme [23](https://github.com/olcaydunder/AnyRPGCore/actions/runs/37162892319) · ✅ başarılı · commit `435d0995` · 04.10.2026 03:07

Tanı raporunda 1 uyarı ([tam rapor](https://github.com/olcaydunder/AnyRPGCore/releases/download/tani/tani.zip)):

```text
!! zemine oturtulamayan nesne: 1 (GecitTasi (101.1, 16.0, 30.6))
```

### Otomatik oyun testi

Bot yeni oyun başlatıp haritaları gezdi: **tamamlandı**, 0 hata/istisna.

| | Harita | Sonuç | Yükleme | Hata | Not |
|---|---|---|---|---|---|
| ✅ | Ötüken Yaylası | tamam | 3 sn | 0 |  |
| ✅ | Umay Tarlaları | tamam | 2 sn | 0 |  |
| ✅ | Börü Tepesi | tamam | 1 sn | 0 |  |
| ✅ | Ak Deniz Kıyısı | tamam | 0 sn | 0 |  |
| ✅ | Ordubalık Çarşısı | tamam | 1 sn | 0 |  |
| ✅ | Ordubalık Kenti | tamam | 0 sn | 0 |  |
| ✅ | Ulukayın Ormanı | tamam | 0 sn | 0 |  |
| ❌ | Koncolos İni | sorunlu | 0 sn | 0 | oyuncu öldü |
| ❌ | Kağan Ordası | ışınlanılamadı | 0 sn | 0 | Ölüyken ışınlanamazsın. Önce yeniden doğ. |
| ❌ | Kaf Dağı Yolu | ışınlanılamadı | 0 sn | 0 | Ölüyken ışınlanamazsın. Önce yeniden doğ. |
| ❌ | Ergenekon Mağarası | ışınlanılamadı | 0 sn | 0 | Ölüyken ışınlanamazsın. Önce yeniden doğ. |
| ❌ | Kurgan Mezarlığı | ışınlanılamadı | 0 sn | 0 | Ölüyken ışınlanamazsın. Önce yeniden doğ. |
| ❌ | Ay Dede Koyu | ışınlanılamadı | 0 sn | 0 | Ölüyken ışınlanamazsın. Önce yeniden doğ. |
| ❌ | Erlik'in Mağarası | ışınlanılamadı | 0 sn | 0 | Ölüyken ışınlanamazsın. Önce yeniden doğ. |
| ❌ | Tamu Zindanı | ışınlanılamadı | 0 sn | 0 | Ölüyken ışınlanamazsın. Önce yeniden doğ. |

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
