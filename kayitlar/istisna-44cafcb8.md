# İstisna: NullReferenceException: Object reference not set to an instance of an object.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 0.1.36 | 04.10.2026 19:08 | 04.10.2026 19:08 |


## İlk rapor

**İstisna** · sürüm **0.1.36** · samsung SM-S938B · Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3) · ekran 3120x1335 · UmayTarlalari

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object.
```

### Yığın izi
```text
AnyRPG.CharacterEquipmentButton.HandleLeftClick () (at <00000000000000000000000000000000>:0)
AnyRPG.NavigableElement.OnPointerClick (UnityEngine.EventSystems.PointerEventData eventData) (at <00000000000000000000000000000000>:0)
UnityEngine.EventSystems.ExecuteEvents.Execute[T] (UnityEngine.GameObject target, UnityEngine.EventSystems.BaseEventData eventData, UnityEngine.EventSystems.ExecuteEvents+EventFunction`1[T1] functor) (at <00000000000000000000000000000000>:0)
UnityEngine.InputSystem.UI.InputSystemUIInputModule.ProcessPointerButton (UnityEngine.InputSystem.UI.PointerModel+ButtonState& button, UnityEngine.EventSystems.PointerEventData eventData) (at <00000000000000000000000000000000>:0)
UnityEngine.InputSystem.UI.InputSystemUIInputModule.ProcessPointer (UnityEngine.InputSystem.UI.PointerModel& state) (at <00000000000000000000000000000000>:0)
UnityEngine.InputSystem.UI.InputSystemUIInputModule.Process () (at <00000000000000000000000000000000>:0)
UnityEngine.InputSystem.UI.InputSystemUIInputModule:Process()
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 0.1.36
cihaz: samsung SM-S938B
sistem: Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
grafik: Vulkan / Adreno (TM) 830 / bellek 11113 MB
ekran: 3120x1335
ayarlar: kalite 2, çözünürlük 3, gölge 3, fps sınırı 1
sahne: UmayTarlalari
oyuncu: seviye 1
sure: 48 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 0.1.36 | samsung SM-S938B | Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
Vulkan Adreno (TM) 830 | Bellek 11113 MB | 3120x1335 | FPS 60 | Süre 46 sn
Kare süresi: işlemci 16.7 ms | ekran kartı 11.0 ms | ekran 60 Hz | hedef 60 | kalite High | pil 2%
Sahneler: UmayTarlalari
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 1006,636 -> Background/Outline [PopupWindowContainerCanvas 12]
Oyuncu karakteri: oluştu (MecanimMale1) | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
19:08:09 [Exception] NullReferenceException: Object reference not set to an instance of an object.
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 04.10.2026 19:08 · 0.1.36 · samsung SM-S938B · UmayTarlalari · seviye 1 · 3120x1335 |

Anahtar: `istisna-44cafcb8`
