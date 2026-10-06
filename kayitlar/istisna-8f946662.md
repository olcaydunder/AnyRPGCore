# İstisna: NullReferenceException: Object reference not set to an instance of an object.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 0.1.64 | 06.10.2026 11:26 | 06.10.2026 11:26 |


## İlk rapor

**İstisna** · sürüm **0.1.64** · PC · Linux 6.8 Ubuntu 24.04 64bit · ekran 640x480 · FeaturesDemoZone

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object.
```

### Yığın izi
```text
UnityEngine.Bindings.ThrowHelper.ThrowNullReferenceException (System.Object obj) (at <3d65f019da6044168f2ccd81096ed5c5>:0)
UnityEngine.Component.get_gameObject () (at <3d65f019da6044168f2ccd81096ed5c5>:0)
AnyRPG.AggroTable.get_TopAgroNode () (at <4279f169e03540928411c6f637104929>:0)
AnyRPG.CharacterCombat.TryToDropCombat () (at <4279f169e03540928411c6f637104929>:0)
AnyRPG.CharacterCombat.Tick () (at <4279f169e03540928411c6f637104929>:0)
AnyRPG.UnitController.Update () (at <4279f169e03540928411c6f637104929>:0)
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 0.1.64
cihaz: PC
sistem: Linux 6.8 Ubuntu 24.04 64bit
grafik: Null / Null Device / bellek 7940 MB
ekran: 640x480
ayarlar: kalite -1, çözünürlük 0, gölge 0, fps sınırı 1
sahne: FeaturesDemoZone
oyuncu: oyunda değil
sure: 20667 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 0.1.64 | PC | Linux 6.8 Ubuntu 24.04 64bit
Null Null Device | Bellek 7940 MB | 640x480 | FPS ? | Süre 20666 sn
Kare süresi: işlemci ? | ekran kartı ? | ekran 0 Hz | hedef 45 | kalite Very Low | çizim 640x480 (ekran 640x480, ölçek 1.00) | pil -100%
Sahneler: MovedObjectsHolder FeaturesDemoZone FeaturesDemoDungeon(yükleniyor)
Giriş: dokunmatik yok | fare yok | ekran tuşları kapalı
Son dokunuş: -
Oyuncu karakteri: yok | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>



## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 06.10.2026 11:26 · 0.1.64 · PC · FeaturesDemoZone · oyunda değil · 640x480 |

Anahtar: `istisna-8f946662`
