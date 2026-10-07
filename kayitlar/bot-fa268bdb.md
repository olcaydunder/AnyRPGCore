# Oyun testi: ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 0.1.72 | 07.10.2026 14:23 | 07.10.2026 14:23 |


## İlk rapor

**İstisna** · sürüm **0.1.72** · Otomatik oyun testi (CI) · Unity editör · ekran - · FeaturesDemoMainMenu

### Mesaj
```text
ArgumentOutOfRangeException: Index was out of range. Must be non-negative and less than the size of the collection.
Parameter name: index
```

### Yığın izi
```text
System.Collections.Generic.List`1[T].get_Item (System.Int32 index) (at <e605204d165a49d89ae7579f3d894429>:0)
AnyRPG.BagBarController.AddBagButton (AnyRPG.BagNode bagNode) (at Assets/AnyRPG/Core/System/Scripts/UI/PanelControllers/BagBarController.cs:95)
AnyRPG.BankPanel.HandleAddBankBagNode (AnyRPG.BagNode bagNode) (at Assets/AnyRPG/Core/System/Scripts/UI/WindowPanels/BankPanel.cs:55)
AnyRPG.SystemEventManager.NotifyOnAddBankBagNode (AnyRPG.BagNode bagNode) (at Assets/AnyRPG/Core/System/Scripts/GameManager/SystemEventManager.cs:107)
AnyRPG.PlayerManagerClient.HandleAddBankBagNode (AnyRPG.BagNode bagNode) (at Assets/AnyRPG/Core/System/Scripts/GameManager/PlayerManagerClient.cs:897)
AnyRPG.CharacterInventoryManager.InitializeBankNodes () (at Assets/AnyRPG/Core/System/Scripts/Characters/BaseClasses/CharacterInventoryManager.cs:308)
AnyRPG.CharacterInventoryManager.PerformSetupActivities () (at Assets/AnyRPG/Core/System/Scripts/Characters/BaseClasses/CharacterInventoryManager.cs:269)
AnyRPG.UnitController.SetCharacterConfiguration () (at Assets/AnyRPG/Core/System/Scripts/Characters/BaseClasses/UnitController.cs:1319)
AnyRPG.CharacterManager.SetUnitControllerConfiguration (AnyRPG.UnitController unitController) (at Assets/AnyRPG/Core/System/Scripts/GameManager/CharacterManager.cs:266)
AnyRPG.CharacterManager.CompleteCharacterRequest (AnyRPG.UnitController unitController) (at Assets/AnyRPG/Core/System/Scripts/GameManager/CharacterManager.cs:165)
AnyRPG.CharacterManager.SpawnCharacter…
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 0.1.72
cihaz: Otomatik oyun testi (CI)
sistem: Unity editör
ekran: -
sahne: FeaturesDemoMainMenu
```

</details>




## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 07.10.2026 14:23 · derleme 72 · 1 kez · FeaturesDemoMainMenu |

Anahtar: `bot-fa268bdb`
