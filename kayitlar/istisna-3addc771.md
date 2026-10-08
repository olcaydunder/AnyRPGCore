# İstisna: NullReferenceException: Object reference not set to an instance of an object.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.79 | 08.10.2026 22:22 | 08.10.2026 22:22 |


## İlk rapor

**İstisna** · sürüm **1.0.79** · samsung SM-S938B · Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3) · ekran 2340x1001 · FeaturesDemoDungeon

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object.
```

### Yığın izi
```text
FishNet.Object.Prediction.PredictionRigidbody.Reconcile (FishNet.Object.Prediction.PredictionRigidbody pr) (at <00000000000000000000000000000000>:0)
AnyRPG.FishNetUnitController.ReconcileState___UL (AnyRPG.ReconcileData data, FishNet.Transporting.Channel channel) (at <00000000000000000000000000000000>:0)
FishNet.Object.NetworkBehaviour.Reconcile_Client[T,T2] (FishNet.Object.Prediction.Delegating.ReconcileUserLogicDelegate`1[T] reconcileDel, GameKit.Dependencies.Utilities.Types.RingBuffer`1[T] replicatesHistory, GameKit.Dependencies.Utilities.Types.RingBuffer`1[T] reconcilesHistory, T data) (at <00000000000000000000000000000000>:0)
AnyRPG.FishNetUnitController.Reconcile_Client_Start () (at <00000000000000000000000000000000>:0)
FishNet.Object.NetworkObject.PredictionManager_OnReconcile (System.UInt32 clientReconcileTick, System.UInt32 serverReconcileTick) (at <00000000000000000000000000000000>:0)
FishNet.Managing.Predicting.PredictionManager.ReconcileToStates () (at <00000000000000000000000000000000>:0)
FishNet.Managing.Timing.TimeManager.IncreaseTick () (at <00000000000000000000000000000000>:0)
FishNet.Managing.Timing.TimeManager.<TickUpdate>g__MethodLogic|112_0 () (at <00000000000000000000000000000000>:0)
FishNet.Managing.Timing.TimeManager.TickUpdate () (at <00000000000000000000000000000000>:0)
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 1.0.79
cihaz: samsung SM-S938B
sistem: Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
grafik: Vulkan / Adreno (TM) 830 / bellek 11113 MB
ekran: 2340x1001
ayarlar: kalite 3, çözünürlük 0, gölge 0, fps sınırı 1
sahne: FeaturesDemoDungeon
oyuncu: oyunda değil
sure: 59 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.79 | samsung SM-S938B | Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
Vulkan Adreno (TM) 830 | Bellek 11113 MB | 2340x1001 | FPS 82 | Süre 56 sn
Kare süresi: işlemci 15.4 ms | ekran kartı 12.1 ms | ekran 120 Hz | hedef 500 | kalite Ultra | çizim 3840x1643 (ekran 2340x1001, ölçek 1.64) | pil 58%
Sahneler: MovedObjectsHolder FeaturesDemoDungeon
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 1374,351 -> oyun dünyası
Oyuncu karakteri: yok | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
22:22:34 [Exception] NullReferenceException: Object reference not set to an instance of an object.
22:22:34 [Warning] To Debug, run app with -diag-job-temp-memory-leak-validation cmd line argument. This will output the callstacks of the leaked allocations.
22:22:34 [Warning] Internal: JobTempAlloc has allocations that are more than the maximum lifespan of 4 frames old - this is not allowed and likely a leak
22:21:57 [Warning] [Reklam] onay bilgisi: Publisher misconfiguration: Failed to read publisher's account configuration; no form(s) configured for the input app ID. Verify that you have configured one or more forms for t...
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 08.10.2026 22:22 · 1.0.79 · samsung SM-S938B · FeaturesDemoDungeon · oyunda değil · 2340x1001 |

Anahtar: `istisna-3addc771`
