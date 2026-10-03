# İstisna: NullReferenceException: Object reference not set to an instance of an object.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 4 | 1 | 0.1.17 | 03.10.2026 23:09 | 03.10.2026 23:44 |


## İlk rapor

**İstisna** · sürüm **0.1.17** · samsung SM-S938B · Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3) · ekran 3120x1335 · KoncolosIni

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object.
```

### Yığın izi
```text
AnyRPG.TerrainDetector.ConvertToSplatMapCoordinate (UnityEngine.Vector3 worldPosition) (at <00000000000000000000000000000000>:0)
AnyRPG.TerrainDetector.GetActiveTerrainTextureIdx (UnityEngine.Vector3 position) (at <00000000000000000000000000000000>:0)
AnyRPG.LevelManagerClient.GetTerrainFootStepProfile (UnityEngine.Vector3 transformPosition) (at <00000000000000000000000000000000>:0)
AnyRPG.UnitController.SetEnvironmentFootStepAudioProfile () (at <00000000000000000000000000000000>:0)
AnyRPG.UnitController.PlayFootStep () (at <00000000000000000000000000000000>:0)
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 0.1.17
cihaz: samsung SM-S938B
sistem: Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
grafik: Vulkan / Adreno (TM) 830 / bellek 11113 MB
ekran: 3120x1335
ayarlar: kalite 1, çözünürlük 0, gölge 0, fps sınırı 1
sahne: KoncolosIni
oyuncu: seviye 1
sure: 29 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 0.1.17 | samsung SM-S938B | Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
Vulkan Adreno (TM) 830 | Bellek 11113 MB | 3120x1335 | FPS 30 | Süre 26 sn
Kare süresi: işlemci 52.7 ms | ekran kartı 16.8 ms | ekran 60 Hz | hedef 60 | kalite Medium | pil 32%
Sahneler: KoncolosIni
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 2448,126 -> Panel/Işınlan [IsinlanmaCanvas 31]
Oyuncu karakteri: oluştu (MecanimMale138) | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
23:09:01 [Exception] NullReferenceException: Object reference not set to an instance of an object.
23:09:01 [Exception] NullReferenceException: Object reference not set to an instance of an object.
23:09:01 [Exception] NullReferenceException: Object reference not set to an instance of an object.
23:09:01 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:08:47 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:08:38 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:08:37 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 03.10.2026 23:44 · 0.1.17 · samsung SM-S938B · KaganOrdasi · seviye 1 · 3120x1335 |
| 03.10.2026 23:43 · 0.1.17 · samsung SM-S938B · KurganMezarligi · seviye 1 · 3120x1335 |
| 03.10.2026 23:10 · 0.1.17 · samsung SM-S938B · UlukayinOrmani · seviye 1 · 3120x1335 |
| 03.10.2026 23:09 · 0.1.17 · samsung SM-S938B · KoncolosIni · seviye 1 · 3120x1335 |

Anahtar: `istisna-d581f62b`
