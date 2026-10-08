# İstisna: NullReferenceException: Object reference not set to an instance of an object.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.79 | 08.10.2026 23:02 | 08.10.2026 23:02 |


## İlk rapor

**İstisna** · sürüm **1.0.79** · PC · Linux 6.8 Ubuntu 24.04 64bit · ekran 640x480 · KaganOrdasi

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object.
```

### Yığın izi
```text
UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <3d65f019da6044168f2ccd81096ed5c5>:0)
UnityEngine.Component.get_gameObject () (at <3d65f019da6044168f2ccd81096ed5c5>:0)
AnyRPG.PlayerManagerServer.RemoveActivePlayer (System.Int32 accountId) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.PlayerManagerServer.PauseMonitoringPlayerUnit (System.Int32 accountId) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.PlayerManagerServer.ProcessDisconnect (System.Int32 accountId) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.AuthenticationService.ProcessClientDisconnect (System.Int32 clientId) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.NetworkManagerServer.ProcessClientDisconnect (System.Int32 clientId) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.FishNetNetworkController.HandleRemoteConnectionState (FishNet.Connection.NetworkConnection networkConnection, FishNet.Transporting.RemoteConnectionStateArgs args) (at <b7cb74514d1143989d294b3eff07b130>:0)
FishNet.Managing.Server.ServerManager.Transport_OnRemoteConnectionState (FishNet.Transporting.RemoteConnectionStateArgs args) (at <390e558a63404733a277412dab1b4d45>:0)
FishNet.Transporting.Tugboat.Tugboat.HandleRemoteConnectionState (FishNet.Transporting.RemoteConnectionStateArgs connectionStateArgs) (at <390e558a63404733a277412dab1b4d45>:0)
FishNet.Transporting.Tugboat.Server.ServerSocket.IterateIncoming () (at…
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 1.0.79
cihaz: PC
sistem: Linux 6.8 Ubuntu 24.04 64bit
grafik: Null / Null Device / bellek 7940 MB
ekran: 640x480
ayarlar: kalite -1, çözünürlük 0, gölge 0, fps sınırı 1
sahne: KaganOrdasi
oyuncu: oyunda değil
sure: 76925 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.79 | PC | Linux 6.8 Ubuntu 24.04 64bit
Null Null Device | Bellek 7940 MB | 640x480 | FPS ? | Süre 76925 sn
Kare süresi: işlemci ? | ekran kartı ? | ekran 0 Hz | hedef 45 | kalite Very Low | çizim 640x480 (ekran 640x480, ölçek 1.00) | pil -100%
Sahneler: MovedObjectsHolder UmayTarlalari FeaturesDemoDungeon TamuZindani FeaturesDemoZone KoncolosIni AyDedeKoyu OrdubalikKenti KaganOrdasi
Giriş: dokunmatik yok | fare yok | ekran tuşları kapalı
Son dokunuş: -
Oyuncu karakteri: yok | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>



## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 08.10.2026 23:02 · 1.0.79 · PC · KaganOrdasi · oyunda değil · 640x480 |

Anahtar: `istisna-14a3da0f`
