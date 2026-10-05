# İstisna: NullReferenceException: Object reference not set to an instance of an object.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 0.1.52 | 05.10.2026 17:12 | 05.10.2026 17:12 |


## İlk rapor

**İstisna** · sürüm **0.1.52** · PC · Linux 6.17 Ubuntu 24.04 64bit · ekran 640x480 · FeaturesDemoZone

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object.
```

### Yığın izi
```text
UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <3d65f019da6044168f2ccd81096ed5c5>:0)
UnityEngine.Component.get_gameObject () (at <3d65f019da6044168f2ccd81096ed5c5>:0)
AnyRPG.AggroTable.get_TopAgroNode () (at <17dd357545d54fc1a5592b386b164ec8>:0)
AnyRPG.UnitController.UpdateTarget () (at <17dd357545d54fc1a5592b386b164ec8>:0)
AnyRPG.AttackState.Update () (at <17dd357545d54fc1a5592b386b164ec8>:0)
AnyRPG.UnitController.FixedUpdate () (at <17dd357545d54fc1a5592b386b164ec8>:0)
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 0.1.52
cihaz: PC
sistem: Linux 6.17 Ubuntu 24.04 64bit
grafik: Null / Null Device / bellek 15989 MB
ekran: 640x480
ayarlar: kalite -1, çözünürlük 0, gölge 0, fps sınırı 1
sahne: FeaturesDemoZone
oyuncu: oyunda değil
sure: 194 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 0.1.52 | PC | Linux 6.17 Ubuntu 24.04 64bit
Null Null Device | Bellek 15989 MB | 640x480 | FPS ? | Süre 193 sn
Kare süresi: işlemci ? | ekran kartı ? | ekran 0 Hz | hedef 45 | kalite Very Low | pil -100%
Sahneler: MovedObjectsHolder FeaturesDemoZone
Giriş: dokunmatik yok | fare yok | ekran tuşları kapalı
Son dokunuş: -
Oyuncu karakteri: yok | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>



## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 05.10.2026 17:12 · 0.1.52 · PC · FeaturesDemoZone · oyunda değil · 640x480 |

Anahtar: `istisna-9434295e`
