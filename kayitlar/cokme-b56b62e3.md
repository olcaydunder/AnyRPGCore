# Çökme: Çökme (yerel kod): crash

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 4 | 1 | 1.0.70, 1.0.78 | 07.10.2026 08:35 | 07.10.2026 22:55 |


## İlk rapor

**Çökme** · sürüm **1.0.70** · samsung SM-S911B · Android OS 16 / API-36 (BP2A.250605.031.A3/S911BXXS8EYK5) · ekran 2340x990 · FeaturesDemoMainMenu

### Mesaj
```text
Çökme (yerel kod): crash
```

### Ayrıntı
```text
Zaman 2026-10-07 08:35:31 | bellek (PSS) 0 MB | önem 100 (100 = ön planda)
Önceki oturumun kaydı:
Oturum başladı 2026-10-07 08:33:48
Oyuna dönüldü 08:33:50
08:35:20 [Exception] KeyNotFoundException: The given key '5' was not present in the dictionary.
   System.Collections.Generic.Dictionary`2[TKey,TValue].get_Item (TKey key) (at <00000000000000000000000000000000>:0)
   AnyRPG.PlayerManagerServer.RequestSpawnPlayerUnit (System.Int32 accountId, System.String sceneName) (at <00000000000000000000000000000000>:0)
   AnyRPG.PlayerManagerClient.RequestSpawnPlayerUnit (System.Int32 accountId) (at <00000000000000000000000000000000>:0)
   AnyRPG.PlayerManagerClient.HandleLevelLoad () (at <00000000000000000000000000000000>:0)
   AnyRPG.LevelManagerClient.PerformLevelLoadActivities (UnityEngine.SceneManagement.Scene newScene) (at <00000000000000000000000000000000>:0)
```

<details><summary>Ortam</summary>

```text
tur: cokme
surum: 1.0.70
cihaz: samsung SM-S911B
sistem: Android OS 16 / API-36 (BP2A.250605.031.A3/S911BXXS8EYK5)
grafik: Vulkan / Adreno (TM) 740 / bellek 7073 MB
ekran: 2340x990
ayarlar: kalite 3, çözünürlük 0, gölge 0, fps sınırı 1
sahne: FeaturesDemoMainMenu
oyuncu: oyunda değil
sure: 12 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.70 | samsung SM-S911B | Android OS 16 / API-36 (BP2A.250605.031.A3/S911BXXS8EYK5)
Vulkan Adreno (TM) 740 | Bellek 7073 MB | 2340x990 | FPS 137 | Süre 10 sn
Kare süresi: işlemci 8.4 ms | ekran kartı 6.8 ms | ekran 120 Hz | hedef 500 | kalite Ultra | çizim 3840x1625 (ekran 2340x990, ölçek 1.64) | pil 29%
Sahneler: FeaturesDemoMainMenu
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 1375,319 -> NetworkLoginPanel/NetworkLoginBtn_Yes [SystemMenuUICanvas 19]
Oyuncu karakteri: yok | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>



## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 07.10.2026 22:55 · 1.0.78 · samsung SM-S911B · FeaturesDemoMainMenu · oyunda değil · 2340x990 |
| 07.10.2026 22:53 · 1.0.78 · samsung SM-S911B · FeaturesDemoMainMenu · oyunda değil · 2340x990 |
| 07.10.2026 22:52 · 1.0.78 · samsung SM-S911B · FeaturesDemoMainMenu · oyunda değil · 2340x990 |
| 07.10.2026 08:35 · 1.0.70 · samsung SM-S911B · FeaturesDemoMainMenu · oyunda değil · 2340x990 |

Anahtar: `cokme-b56b62e3`
