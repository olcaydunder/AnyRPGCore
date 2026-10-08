# İstisna: NullReferenceException: Object reference not set to an instance of an object

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.79 | 08.10.2026 22:32 | 08.10.2026 22:32 |


## İlk rapor

**İstisna** · sürüm **1.0.79** · PC · Linux 6.8 Ubuntu 24.04 64bit · ekran 640x480 · KaganOrdasi

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object
```

### Yığın izi
```text
AnyRPG.CharacterInventoryManager.GetNewInstantiatedItem (AnyRPG.Item item, AnyRPG.ItemQuality itemQuality, AnyRPG.IInstantiatedItemRequestor requestor) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.LootTableState.GetLootDrop (AnyRPG.UnitController sourceUnitController, AnyRPG.Loot loot, System.Boolean lootGroupUnlimitedDrops, System.Boolean ignoreDropLimit, System.Boolean lootTableUnlimitedDrops, System.Int32& lootGroupRemainingDrops) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.LootTableState.RollLoot (AnyRPG.UnitController sourceUnitController, AnyRPG.LootTable lootTable) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.LootTableState.GetLoot (AnyRPG.UnitController sourceUnitController, AnyRPG.LootTable lootTable, System.Boolean rollLoot) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.LootHolder.GetLoot (AnyRPG.UnitController sourceUnitController, AnyRPG.LootTable lootTable, System.Boolean rollLoot) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.LootableCharacterComponent.DropLoot (AnyRPG.UnitController sourceUnitController) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.LootableCharacterComponent.HandleBeforeDie (AnyRPG.UnitController sourceUnitController) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.UnitEventController.NotifyOnBeforeDie (AnyRPG.UnitController targetUnitController) (at <b7cb74514d1143989d294b3eff07b130>:0)
AnyRPG.CharacterStats.Die () (at <b7cb74514d11439…
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
sure: 75124 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.79 | PC | Linux 6.8 Ubuntu 24.04 64bit
Null Null Device | Bellek 7940 MB | 640x480 | FPS ? | Süre 75124 sn
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
| 08.10.2026 22:32 · 1.0.79 · PC · KaganOrdasi · oyunda değil · 640x480 |

Anahtar: `istisna-46cdc5d0`
