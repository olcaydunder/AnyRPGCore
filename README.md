# Ötüken Destanı · Hata panosu

Oyunun kendiliğinden gönderdiği hata, çökme, donma, yavaşlık ve arayüz raporları ile oyuncuların "Sorun Bildir" notları. Son güncelleme: 05.10.2026 02:43.

**5** açık sorun · **5** düzeltilen · toplam **35** rapor

## Son APK derlemesi

Derleme [46](https://github.com/olcaydunder/AnyRPGCore/actions/runs/37242080237) · ✅ başarılı · commit `70d40d06` · 05.10.2026 02:42

Tanı raporunda uyarı yok.

### Otomatik oyun testi

Bot yeni oyun başlatıp haritaları gezdi: **tamamlandı**, 0 hata/istisna.

- Binek denemesi: binildi, 2,5 sn'de 34 m gidildi, inildi
- Günlük görevler (botun bir günü): 12 düşman yen 4/12, 1 hazine sandığını boşalt 0/1, 2 farklı diyara ayak bas 2/2 (tamam)
- İlk haritada av: 4 düşman yenildi, seviye 3 → 3, tecrübe 0 → 420
- Dünya haritası: 15/15 diyar görüntülü; harita üzerinden ışınlanma: oldu
- Demirci: Kahverengi Deri Zırh → Kahverengi Deri Zırh +3 (eşyadaki her temel değer +1); ilk deneme: Başarılı! Kahverengi Deri Zırh +1; Güç Puanı 68 → 70; kayıttaki ad: Kahverengi Deri Zırh +3; seviye 3, ödül 2 (3: 20 Gümüş Akçe); çantaya +9 konunca: Kuşan ile giyildi: Kahverengi Deri Zırh +9, Güç Puanı 212
- Gelişim: Güç Puanı 212, seviye ödülü 2 (son: 3: 20 Gümüş Akçe), daha iyi eşya önerisi 1, oto av becerisi 10 kez
- Çanta: sıralama: 11 eşya → 11, 5 yuva, tür sırası doğru, boşluk yok (ilk: Kahverengi Deri Zırh +3, Şifa İksiri ×5, Şifa İksiri ×3, Kahverengi Deri Zırh); toplu satış: 2 gri kopya eklendi, 2 öneri (2 değersiz), 2 satıldı +74 Bakır Akçe, kalan gri 0, demirci eşyası korundu

| | Harita | Sonuç | Yükleme | Hata | Not |
|---|---|---|---|---|---|
| ✅ | Ötüken Yaylası | tamam | 3 sn | 0 | ok: Yeni görev: Olcayto Han |
| ✅ | Umay Tarlaları | tamam | 2 sn | 0 |  |
| ✅ | Börü Tepesi | tamam | 1 sn | 0 |  |
| ✅ | Ak Deniz Kıyısı | tamam | 1 sn | 0 |  |
| ❌ | Ordubalık Çarşısı | sorunlu | 1 sn | 0 | yürüyerek ulaşılamayan: Kapi_KafDagiYolu_1 (123, 12, 35) |
| ✅ | Ordubalık Kenti | tamam | 0 sn | 0 |  |
| ❌ | Ulukayın Ormanı | sorunlu | 0 sn | 0 | yürüyerek ulaşılamayan: Kapi_OrdubalikKenti_0 (-45, 9, 29) |
| ✅ | Koncolos İni | tamam | 0 sn | 0 |  |
| ✅ | Kağan Ordası | tamam | 1 sn | 0 |  |
| ❌ | Kaf Dağı Yolu | sorunlu | 0 sn | 0 | yürüyerek ulaşılamayan: Kapi_OrdubalikCarsisi_1 (135, 13, 42) |
| ✅ | Ergenekon Mağarası | tamam | 0 sn | 0 |  |
| ✅ | Kurgan Mezarlığı | tamam | 0 sn | 0 |  |
| ✅ | Ay Dede Koyu | tamam | 1 sn | 0 |  |
| ✅ | Erlik'in Mağarası | tamam | 1 sn | 0 |  |
| ✅ | Tamu Zindanı | tamam | 0 sn | 0 |  |

## Açık sorunlar

| Durum | Sorun | Tür | Tekrar | Cihaz | Sürüm | Son görülme |
|---|---|---|---|---|---|---|
| 🔁 yeniden görüldü | [Arayüz yerleşimi, 3120x1335 ekran](kayitlar/arayuz-3120x1335.md) | Arayüz | 4 | 1 | 0.1.25, 0.1.36, 0.1.43 | 05.10.2026 00:08 |
| 🔴 açık | [İstisna: NullReferenceException: Object reference not set to an instance of an object.](kayitlar/istisna-44cafcb8.md) | İstisna | 1 | 1 | 0.1.36 | 04.10.2026 19:08 |
| 🔴 açık | [Hata: Screen position out of view frustum (screen pos inf, -inf) (Camera rect 0 0 640 480)](kayitlar/hata-60de498e.md) | Hata | 11 | 1 | 0.1.44, 0.1.45, 0.1.46 | 05.10.2026 02:39 |
| 🔴 açık | [Hata: InputManager not found in scene.  Is the GameManager in the scene?](kayitlar/hata-8f05154f.md) | Hata | 8 | 2 | 0.1.43 | 05.10.2026 00:12 |
| 🔴 açık | [Bellek: Bellek yetmedi, sistem oyunu kapattı](kayitlar/bellek-d3941b2d.md) | Bellek | 1 | 1 | 0.1.36 | 04.10.2026 19:07 |

## Düzeltilenler

| Durum | Sorun | Tür | Tekrar | Cihaz | Sürüm | Son görülme |
|---|---|---|---|---|---|---|
| ✅ düzeltildi (0.1.32) | [Arayüz yerleşimi, 2664x1073 ekran](kayitlar/arayuz-2664x1073.md) | Arayüz | 1 | 1 | 0.1.27 | 04.10.2026 14:15 |
| ✅ düzeltildi (0.1.27) | [Oyuncu bildirimi: merhaba](kayitlar/oyuncu-b9efbe7a2a.md) | Oyuncu bildirimi | 1 | 1 | 0.1.25 | 04.10.2026 11:42 |
| ✅ düzeltildi (0.1.19) | [İstisna: NullReferenceException: Object reference not set to an instance of an object.](kayitlar/istisna-d581f62b.md) | İstisna | 4 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [İstisna: UnityException: Failed to create texture because of invalid parameters.](kayitlar/istisna-9879748b.md) | İstisna | 2 | 1 | 0.1.17 | 03.10.2026 23:44 |
| ✅ düzeltildi (0.1.19) | [Hata: Texture has out of range width (got 46070 max supported 16384)](kayitlar/hata-16e86c78.md) | Hata | 2 | 1 | 0.1.17 | 03.10.2026 23:44 |

---
Bu sayfa her 15 dakikada bir "Hata raporları" iş akışıyla güncellenir (Tools~/hata/topla.py, Assets/AnyRPG/Core/System/Scripts/Mobile/HataBildirici.cs).
