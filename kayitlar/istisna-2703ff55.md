# İstisna: IndexOutOfRangeException: Index was outside the bounds of the array.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.79 | 13.10.2026 12:46 | 13.10.2026 12:46 |


## İlk rapor

**İstisna** · sürüm **1.0.79** · samsung SM-S938B · Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3) · ekran 2340x1001 · UmayTarlalari

### Mesaj
```text
IndexOutOfRangeException: Index was outside the bounds of the array.
```

### Yığın izi
```text
AnyRPG.FishNetNetworkController.HandleLoadEndClient (FishNet.Managing.Scened.SceneLoadEndEventArgs obj) (at <00000000000000000000000000000000>:0)
FishNet.Managing.Scened.SceneManager.InvokeOnSceneLoadEnd (FishNet.Managing.Scened.LoadQueueData qd, System.Collections.Generic.List`1[T] requestedLoadScenes, System.Collections.Generic.List`1[T] loadedScenes, System.String[] unloadedSceneNames) (at <00000000000000000000000000000000>:0)
FishNet.Managing.Scened.SceneManager+<__LoadScenes>d__109.MoveNext () (at <00000000000000000000000000000000>:0)
UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <00000000000000000000000000000000>:0)
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 1.0.79
cihaz: samsung SM-S938B
sistem: Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
grafik: Vulkan / Adreno (TM) 830 / bellek 11113 MB
ekran: 2340x1001
ayarlar: kalite 2, çözünürlük 0, gölge 0, fps sınırı 1
sahne: UmayTarlalari
oyuncu: seviye 101
sure: 18 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.79 | samsung SM-S938B | Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
Vulkan Adreno (TM) 830 | Bellek 11113 MB | 2340x1001 | FPS 105 | Süre 16 sn
Kare süresi: işlemci 30.3 ms | ekran kartı 5.2 ms | ekran 120 Hz | hedef 500 | kalite High | çizim 2340x1001 (ekran 2340x1001, ölçek 1.00) | pil 21%
Sahneler: MovedObjectsHolder UmayTarlalari
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 1608,64 -> LoadGameBtn_Start/Text [SystemMenuUICanvas 19]
Oyuncu karakteri: oluştu (MecanimMale64) | Önizleme karakteri: yok…
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
12:46:51 [Exception] IndexOutOfRangeException: Index was outside the bounds of the array.
12:46:51 [Warning] [Reklam] onay bilgisi: Publisher misconfiguration: Failed to read publisher's account configuration; no form(s) configured for the input app ID. Verify that you have configured one or more forms for t...
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 13.10.2026 12:46 · 1.0.79 · samsung SM-S938B · UmayTarlalari · seviye 101 · 2340x1001 |

Anahtar: `istisna-2703ff55`
