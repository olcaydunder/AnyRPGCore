# İstisna: NullReferenceException: Object reference not set to an instance of an object.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.79 | 09.10.2026 21:49 | 09.10.2026 21:49 |


## İlk rapor

**İstisna** · sürüm **1.0.79** · samsung SM-S901E · Android OS 16 / API-36 (BP2A.250605.031.A3/S901EXXSEGZH4) · ekran 2340x996 · FeaturesDemoZone

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object.
```

### Yığın izi
```text
AnyRPG.FocusTargetManager.SpawnUnit (AnyRPG.UnitController targetUnitController) (at <00000000000000000000000000000000>:0)
AnyRPG.UnitFramePanel.PostTargetInitialization () (at <00000000000000000000000000000000>:0)
AnyRPG.UnitFramePanel.SetTarget (AnyRPG.UnitController unitController) (at <00000000000000000000000000000000>:0)
AnyRPG.PlayerController.HandleSetTarget (AnyRPG.InteractableBase newTarget) (at <00000000000000000000000000000000>:0)
AnyRPG.UnitController.SetTarget (AnyRPG.InteractableBase newTarget) (at <00000000000000000000000000000000>:0)
AnyRPG.PlayerController.RightMouseInteraction (AnyRPG.InteractableBase interactable) (at <00000000000000000000000000000000>:0)
AnyRPG.GorevOku.Git () (at <00000000000000000000000000000000>:0)
UnityEngine.Events.UnityEvent.Invoke () (at <00000000000000000000000000000000>:0)
UnityEngine.EventSystems.ExecuteEvents.Execute[T] (UnityEngine.GameObject target, UnityEngine.EventSystems.BaseEventData eventData, UnityEngine.EventSystems.ExecuteEvents+EventFunction`1[T1] functor) (at <00000000000000000000000000000000>:0)
UnityEngine.InputSystem.UI.InputSystemUIInputModule.ProcessPointerButton (UnityEngine.InputSystem.UI.PointerModel+ButtonState& button, UnityEngine.EventSystems.PointerEventData eventData) (at <00000000000000000000000000000000>:0)
UnityEngine.InputSystem.UI.InputSystemUIInputModule.ProcessPointer (UnityEngine.InputSystem.UI.PointerModel& sta…
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 1.0.79
cihaz: samsung SM-S901E
sistem: Android OS 16 / API-36 (BP2A.250605.031.A3/S901EXXSEGZH4)
grafik: Vulkan / Adreno (TM) 730 / bellek 7220 MB
ekran: 2340x996
ayarlar: kalite -1, çözünürlük 2, gölge 0, fps sınırı 1
sahne: FeaturesDemoZone
oyuncu: seviye 6
sure: 1035 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.79 | samsung SM-S901E | Android OS 16 / API-36 (BP2A.250605.031.A3/S901EXXSEGZH4)
Vulkan Adreno (TM) 730 | Bellek 7220 MB | 2340x996 | FPS 47 | Süre 1032 sn
Kare süresi: işlemci 24.8 ms | ekran kartı 19.5 ms | ekran 60 Hz | hedef 60 | kalite Very High | çizim 1872x797 (ekran 2340x996, ölçek 0.80) | pil 32%
Sahneler: FeaturesDemoZone
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 1408,200 -> GorevOkuCanvas/Yazi [GorevOkuCanvas 4]
Oyuncu karakteri: oluştu (MecanimMale1) | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
21:49:08 [Exception] NullReferenceException: Object reference not set to an instance of an object.
21:49:08 [Warning] CharacterConfigurationRequest.SetUnitProfileProperties() received a null UnitProfile
21:32:08 [Warning] [Reklam] onay bilgisi: Publisher misconfiguration: Failed to read publisher's account configuration; no form(s) configured for the input app ID. Verify that you have configured one or more forms for t...
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 09.10.2026 21:49 · 1.0.79 · samsung SM-S901E · FeaturesDemoZone · seviye 6 · 2340x996 |

Anahtar: `istisna-d52957a1`
