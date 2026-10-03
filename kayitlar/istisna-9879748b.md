# İstisna: UnityException: Failed to create texture because of invalid parameters.

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 2 | 1 | 0.1.17 | 03.10.2026 23:12 | 03.10.2026 23:44 |


## İlk rapor

**İstisna** · sürüm **0.1.17** · samsung SM-S938B · Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3) · ekran 3120x1335 · AyDedeKoyu

### Mesaj
```text
UnityException: Failed to create texture because of invalid parameters.
```

### Yığın izi
```text
UnityEngine.Texture2D.Internal_Create (UnityEngine.Texture2D mono, System.Int32 w, System.Int32 h, System.Int32 mipCount, UnityEngine.Experimental.Rendering.GraphicsFormat format, UnityEngine.TextureColorSpace colorSpace, UnityEngine.Experimental.Rendering.TextureCreationFlags flags, System.IntPtr nativeTex, System.Boolean ignoreMipmapLimit, System.String mipmapLimitGroupName) (at <00000000000000000000000000000000>:0)
UnityEngine.Texture2D..ctor (System.Int32 width, System.Int32 height) (at <00000000000000000000000000000000>:0)
AnyRPG.MapManager+<WaitForRender>d__18.MoveNext () (at <00000000000000000000000000000000>:0)
UnityEngine.SetupCoroutine.InvokeMoveNext (System.Collections.IEnumerator enumerator, System.IntPtr returnValueAddress) (at <00000000000000000000000000000000>:0)
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 0.1.17
cihaz: samsung SM-S938B
sistem: Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
grafik: Vulkan / Adreno (TM) 830 / bellek 11113 MB
ekran: 3120x1335
ayarlar: kalite 2, çözünürlük 3, gölge 3, fps sınırı 1
sahne: AyDedeKoyu
oyuncu: seviye 1
sure: 161 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 0.1.17 | samsung SM-S938B | Android OS 16 / API-36 (BP4A.251205.006/S938BXXSBCZG3)
Vulkan Adreno (TM) 830 | Bellek 11113 MB | 3120x1335 | FPS 55 | Süre 159 sn
Kare süresi: işlemci 17.5 ms | ekran kartı 11.2 ms | ekran 60 Hz | hedef 60 | kalite High | pil 30%
Sahneler: AyDedeKoyu
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 2302,84 -> Panel/Işınlan [IsinlanmaCanvas 31]
Oyuncu karakteri: oluştu (MecanimMale396) | Önizleme karakteri: yok | Önizleme kamerası: kapalı
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
23:12:22 [Exception] UnityException: Failed to create texture because of invalid parameters.
23:12:22 [Error] Texture has out of range width (got 46070 max supported 16384)
23:12:22 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:12:18 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:12:12 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:11:52 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:11:51 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:11:50 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:11:50 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:11:34 [Warning] Your project uses a scriptable render pipeline. You can use Camera.layerCullSpherical only with the built-in renderer.
23:11:28 [Exception] NullReferenceException: Object reference not set to an instance of an object.
23:11:28 [Exception] NullReferenceException: Object reference not set to an instance of an object.
23:11:28 [Exception] NullReferenceException: Object reference not set to an instance of an object.
23:11:28 [Exception] NullReferenceException: Object reference not set to an instance of an object.
23:11:27 [Exception] NullReferenceException: Object reference not set to an instance of an object.
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 03.10.2026 23:44 · 0.1.17 · samsung SM-S938B · KaganOrdasi · seviye 1 · 3120x1335 |
| 03.10.2026 23:12 · 0.1.17 · samsung SM-S938B · AyDedeKoyu · seviye 1 · 3120x1335 |

Anahtar: `istisna-9879748b`
