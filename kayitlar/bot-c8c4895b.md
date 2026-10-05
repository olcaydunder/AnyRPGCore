# Oyun testi: NullReferenceException: Object reference not set to an instance of an object

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 0.1.60 | 06.10.2026 01:33 | 06.10.2026 01:33 |


## İlk rapor

**İstisna** · sürüm **0.1.60** · Otomatik oyun testi (CI) · Unity editör · ekran - · ErgenekonMagarasi

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object
```

### Yığın izi
```text
AnyRPG.PlayerDeathPanel.ProcessOpenWindowNotification () (at Assets/AnyRPG/Core/System/Scripts/UI/WindowPanels/PlayerDeathPanel.cs:36)
AnyRPG.CloseableWindowContents.ReceiveOpenWindowNotification () (at Assets/AnyRPG/Core/System/Scripts/UI/CloseableWindows/CloseableWindowContents.cs:579)
AnyRPG.CloseableWindow.OpenWindow () (at Assets/AnyRPG/Core/System/Scripts/UI/CloseableWindows/CloseableWindow.cs:157)
AnyRPG.UIManager+<PerformDeathWindowDelay>d__306.MoveNext () (at Assets/AnyRPG/Core/System/Scripts/GameManager/UIManagers/UIManager.cs:1224)
UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at /home/bokken/build/output/unity/unity/Runtime/Export/Scripting/Coroutines.cs:17)

```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 0.1.60
cihaz: Otomatik oyun testi (CI)
sistem: Unity editör
ekran: -
sahne: ErgenekonMagarasi
```

</details>




## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 06.10.2026 01:33 · derleme 60 · 1 kez · ErgenekonMagarasi |

Anahtar: `bot-c8c4895b`
