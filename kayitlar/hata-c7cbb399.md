# Hata: [2026.10.08 02:27:54] Client encountered an error while parsing data for packetId 28451. Message: Syste…

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.79 | 08.10.2026 02:27 | 08.10.2026 02:27 |


## İlk rapor

**Hata** · sürüm **1.0.79** · samsung SM-S938B · Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3) · ekran 2340x1001 · FeaturesDemoDungeon

### Mesaj
```text
[2026.10.08 02:27:54] Client encountered an error while parsing data for packetId 28451. Message: System.NullReferenceException: Object reference not set to an instance of an object.
  at AnyRPG.AbilityEffectContext..ctor (AnyRPG.IAbilityCaster abilityCaster, AnyRPG.InteractableBase originalTarget, AnyRPG.SerializableAbilityEffectContext serializableAbilityEffectContext, AnyRPG.SystemGameManager systemGameManager) [0x00000] in <00000000000000000000000000000000>:0 
  at AnyRPG.FishNetUnitController.RpcLogic___ReceiveCombatTextEventClient___1257465331 (AnyRPG.FishNetInteractable , AnyRPG.FishNetUnitController , System.Int32 , AnyRPG.CombatTextType …
```

### Yığın izi
```text
UnityEngine.DebugLogHandler:Internal_Log(LogType, LogOption, String, Object)
FishNet.Managing.NetworkManagerExtensions:LogError(NetworkManager, String)
FishNet.Managing.Client.ClientManager:ParseReader(PooledReader, Channel, Boolean)
FishNet.Managing.Client.ClientManager:ParseReceived(ClientReceivedDataArgs)
FishNet.Managing.Client.ClientManager:Transport_OnClientReceivedData(ClientReceivedDataArgs)
FishNet.Transporting.Tugboat.Tugboat:HandleClientReceivedDataArgs(ClientReceivedDataArgs)
FishNet.Transporting.Tugboat.Client.ClientSocket:IterateIncoming()
FishNet.Managing.Transporting.TransportManager:IterateIncoming(Boolean)
FishNet.Managing.Timing.TimeManager:IncreaseTick()
FishNet.Managing.Timing.TimeManager:<TickUpdate>g__MethodLogic|112_0()
FishNet.Managing.Timing.TimeManager:TickUpdate()
```

<details><summary>Ortam</summary>

```text
tur: hata
surum: 1.0.79
cihaz: samsung SM-S938B
sistem: Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
grafik: Vulkan / Adreno (TM) 830 / bellek 11113 MB
ekran: 2340x1001
ayarlar: kalite 0, çözünürlük 0, gölge 0, fps sınırı 1
sahne: FeaturesDemoDungeon
oyuncu: seviye 1
sure: 137 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.79 | samsung SM-S938B | Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
Vulkan Adreno (TM) 830 | Bellek 11113 MB | 2340x1001 | FPS 101 | Süre 134 sn
Kare süresi: işlemci 11.4 ms | ekran kartı 8.6 ms | ekran 120 Hz | hedef 500 | kalite Low | çizim 2340x1001 (ekran 2340x1001, ölçek 1.00) | pil 21%
Sahneler: MovedObjectsHolder FeaturesDemoDungeon
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 1640,532 -> oyun dünyası
Oyuncu karakteri: oluştu (MecanimMale260) | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
02:27:54 [Error] [2026.10.08 02:27:54] Client encountered an error while parsing data for packetId 28451. Message: System.NullReferenceException: Object reference not set to an instance of an object.
02:26:01 [Warning] [Reklam] onay bilgisi: Publisher misconfiguration: Failed to read publisher's account configuration; no form(s) configured for the input app ID. Verify that you have configured one or more forms for t...
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 08.10.2026 02:27 · 1.0.79 · samsung SM-S938B · FeaturesDemoDungeon · seviye 1 · 2340x1001 |

Anahtar: `hata-c7cbb399`
