# Bellek: Bellek yetmedi, sistem oyunu kapattı: [REMOVE TASK] remove task

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 1 | 1 | 1.0.79 | 12.10.2026 12:35 | 12.10.2026 12:35 |


## İlk rapor

**Bellek** · sürüm **1.0.79** · samsung SM-S901E · Android OS 16 / API-36 (BP2A.250605.031.A3/S901EXXSEGZH4) · ekran 2340x996 · FeaturesDemoZone

### Mesaj
```text
Bellek yetmedi, sistem oyunu kapattı: [REMOVE TASK] remove task
```

### Ayrıntı
```text
Zaman 2026-10-09 21:57:31 | bellek (PSS) 0 MB | önem 400 (100 = ön planda)
Önceki oturumun kaydı:
Oturum başladı 2026-10-09 21:31:56
Oyuna dönüldü 21:31:59
21:49:08 [Exception] NullReferenceException: Object reference not set to an instance of an object.
   AnyRPG.FocusTargetManager.SpawnUnit (AnyRPG.UnitController targetUnitController) (at <00000000000000000000000000000000>:0)
   AnyRPG.UnitFramePanel.PostTargetInitialization () (at <00000000000000000000000000000000>:0)
   AnyRPG.UnitFramePanel.SetTarget (AnyRPG.UnitController unitController) (at <00000000000000000000000000000000>:0)
   AnyRPG.PlayerController.HandleSetTarget (AnyRPG.InteractableBase newTarget) (at <00000000000000000000000000000000>:0)
   AnyRPG.UnitController.SetTarget (AnyRPG.InteractableBase newTarget) (at <00000000000000000000000000000000>:0)
   AnyRPG.PlayerController.RightMouseInteraction (AnyRPG.InteractableBase interactable) (at <00000000000000000000000000000000>:0)
```

<details><summary>Ortam</summary>

```text
tur: bellek
surum: 1.0.79
cihaz: samsung SM-S901E
sistem: Android OS 16 / API-36 (BP2A.250605.031.A3/S901EXXSEGZH4)
grafik: Vulkan / Adreno (TM) 730 / bellek 7220 MB
ekran: 2340x996
ayarlar: kalite -1, çözünürlük 2, gölge 0, fps sınırı 1
sahne: FeaturesDemoZone
oyuncu: seviye 6
sure: 17 sn
```

</details>

<details><summary>Oyunun durumu</summary>

```text
Sürüm 1.0.79 | samsung SM-S901E | Android OS 16 / API-36 (BP2A.250605.031.A3/S901EXXSEGZH4)
Vulkan Adreno (TM) 730 | Bellek 7220 MB | 2340x996 | FPS 56 | Süre 14 sn
Kare süresi: işlemci 17.0 ms | ekran kartı 8.7 ms | ekran 60 Hz | hedef 60 | kalite Very High | çizim 1872x797 (ekran 2340x996, ölçek 0.80) | pil 65%
Sahneler: FeaturesDemoZone
Giriş: dokunmatik var | fare bağlı | ekran tuşları açık
Son dokunuş: 1036,555 -> PlayMenuPanel(Clone)/PlayBtn_Continue [SystemMenuUICanvas 19]
Oyuncu karakteri: oluştu (MecanimMale1) | Önizleme karakteri: yok…
```

</details>

<details><summary>Son kayıt satırları</summary>

```text
12:34:58 [Warning] [Reklam] onay bilgisi: Publisher misconfiguration: Failed to read publisher's account configuration; no form(s) configured for the input app ID. Verify that you have configured one or more forms for t...
```

</details>


## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 12.10.2026 12:35 · 1.0.79 · samsung SM-S901E · FeaturesDemoZone · seviye 6 · 2340x996 |

Anahtar: `bellek-e9291dad`
