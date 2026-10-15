# İstisna: NullReferenceException: Object reference not set to an instance of an object.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.79 | 16.10.2026 02:10 | 16.10.2026 02:10 |


## İlk rapor

**İstisna** · sürüm **1.0.79** · samsung SM-A346E · Android OS 15 / API-35 (AP3A.240905.015.A2/A346EXXU9DYF4) · ekran 2340x1007 · FeaturesDemoZone

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object.
```

### Yığın izi
```text
AnyRPG.QuestGiverComponent.CanInteract (AnyRPG.UnitController sourceUnitController, System.Boolean processRangeCheck, System.Boolean passedRangeCheck, System.Boolean processNonCombatCheck, System.Boolean viaSwitch) (at <00000000000000000000000000000000>:0)
AnyRPG.InteractableBase.GetCurrentInteractables (AnyRPG.UnitController sourceUnitController) (at <00000000000000000000000000000000>:0)
AnyRPG.InteractableBase.HandlePrerequisiteUpdates () (at <00000000000000000000000000000000>:0)
AnyRPG.InteractableBase.UpdateOnPlayerUnitSpawn (AnyRPG.UnitController sourceUnitController) (at <00000000000000000000000000000000>:0)
AnyRPG.LevelManagerServer.HandlePlayerUnitSpawn (AnyRPG.UnitController sourceUnitController) (at <00000000000000000000000000000000>:0)
AnyRPG.PlayerManagerClient.HandlePlayerUnitSpawn () (at <00000000000000000000000000000000>:0)
AnyRPG.PlayerManagerClient.PostInit (AnyRPG.UnitController unitController) (at <00000000000000000000000000000000>:0)
AnyRPG.CharacterManager.CompleteModelRequest (AnyRPG.UnitController unitController, System.Boolean isServerOwner) (at <00000000000000000000000000000000>:0)
AnyRPG.CharacterManager.SpawnCharacterPrefab (AnyRPG.CharacterRequestData characterRequestData, UnityEngine.Transform parent, UnityEngine.Vector3 position, UnityEngine.Vector3 forward) (at <00000000000000000000000000000000>:0)
AnyRPG.PlayerManagerServer.RequestSpawnPlayerUnit (System.Int32…
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 1.0.79
cihaz: samsung SM-A346E
sistem: Android OS 15 / API-35 (AP3A.240905.015.A2/A346EXXU9DYF4)
grafik: Vulkan / Mali-G68 MC4 / bellek 7471 MB
ekran: 2340x1007
ayarlar: kalite 0, çözünürlük 2, gölge 0, fps sınırı 1
sahne: FeaturesDemoZone
oyuncu: seviye 2
sure: 112 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.79 | samsung SM-A346E | Android OS 15 / API-35 (AP3A.240905.015.A2/A346EXXU9DYF4)
Vulkan Mali-G68 MC4 | Bellek 7471 MB | 2340x1007 | FPS 37 | Süre 109 sn
Kare süresi: işlemci 27.8 ms | ekran kartı 23.8 ms | ekran 120 Hz | hedef 60 | kalite Low | çizim 1872x806 (ekran 2340x1007, ölçek 0.80) | pil 54%
Sahneler: FeaturesDemoZone
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 2181,48 -> Bilgi/Işınlan [DunyaHaritasiCanvas 31]
Oyuncu karakteri: oluştu (MecanimMale214) | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
02:10:09 [Exception] NullReferenceException: Object reference not set to an instance of an object.
02:08:52 [Warning] [Reklam] onay bilgisi: Publisher misconfiguration: Failed to read publisher's account configuration; no form(s) configured for the input app ID. Verify that you have configured one or more forms for t...
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 16.10.2026 02:10 · 1.0.79 · samsung SM-A346E · FeaturesDemoZone · seviye 2 · 2340x1007 |

Anahtar: `istisna-708ac3ce`
