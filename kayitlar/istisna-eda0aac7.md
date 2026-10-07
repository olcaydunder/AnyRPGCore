# İstisna: KeyNotFoundException: The given key '5' was not present in the dictionary.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.70 | 07.10.2026 08:35 | 07.10.2026 08:35 |


## İlk rapor

**İstisna** · sürüm **1.0.70** · samsung SM-S911B · Android OS 16 / API-36 (BP2A.250605.031.A3/S911BXXS8EYK5) · ekran 2340x990 · FeaturesDemoZone

### Mesaj
```text
KeyNotFoundException: The given key '5' was not present in the dictionary.
```

### Yığın izi
```text
System.Collections.Generic.Dictionary`2[TKey,TValue].get_Item (TKey key) (at <00000000000000000000000000000000>:0)
AnyRPG.PlayerManagerServer.RequestSpawnPlayerUnit (System.Int32 accountId, System.String sceneName) (at <00000000000000000000000000000000>:0)
AnyRPG.PlayerManagerClient.RequestSpawnPlayerUnit (System.Int32 accountId) (at <00000000000000000000000000000000>:0)
AnyRPG.PlayerManagerClient.HandleLevelLoad () (at <00000000000000000000000000000000>:0)
AnyRPG.LevelManagerClient.PerformLevelLoadActivities (UnityEngine.SceneManagement.Scene newScene) (at <00000000000000000000000000000000>:0)
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 1.0.70
cihaz: samsung SM-S911B
sistem: Android OS 16 / API-36 (BP2A.250605.031.A3/S911BXXS8EYK5)
grafik: Vulkan / Adreno (TM) 740 / bellek 7073 MB
ekran: 2340x990
ayarlar: kalite 3, çözünürlük 0, gölge 0, fps sınırı 1
sahne: FeaturesDemoZone
oyuncu: oyunda değil
sure: 94 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.70 | samsung SM-S911B | Android OS 16 / API-36 (BP2A.250605.031.A3/S911BXXS8EYK5)
Vulkan Adreno (TM) 740 | Bellek 7073 MB | 2340x990 | FPS 114 | Süre 91 sn
Kare süresi: işlemci 9.4 ms | ekran kartı 8.0 ms | ekran 120 Hz | hedef 500 | kalite Ultra | çizim 3840x1625 (ekran 2340x990, ölçek 1.64) | pil 30%
Sahneler: FeaturesDemoZone
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 1371,338 -> Right/ConfirmNewGameBtn_Yes [SystemMenuUICanvas 19]
Oyuncu karakteri: yok | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
08:35:20 [Exception] KeyNotFoundException: The given key '5' was not present in the dictionary.
08:33:48 [Log] OyunAyarlari: 4K açıldı (Adreno (TM) 740, bellek 7073 MB)
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 07.10.2026 08:35 · 1.0.70 · samsung SM-S911B · FeaturesDemoZone · oyunda değil · 2340x990 |

Anahtar: `istisna-eda0aac7`
