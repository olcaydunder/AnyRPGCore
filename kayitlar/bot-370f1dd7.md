# Oyun testi: NullReferenceException: Object reference not set to an instance of an object

Durum: 🔴 açık

| Tekrar | Cihaz | Sürümler | İlk görülme | Son görülme |
|---|---|---|---|---|
| 5 | 1 | 0.1.52 | 05.10.2026 17:13 | 05.10.2026 17:13 |


## İlk rapor

**İstisna** · sürüm **0.1.52** · Otomatik oyun testi (CI) · Unity editör · ekran - · BoruTepesi

### Mesaj
```text
NullReferenceException: Object reference not set to an instance of an object
```

### Yığın izi
```text
AnyRPG.FixedLengthEffectProperties.Cast (AnyRPG.IAbilityCaster source, AnyRPG.InteractableBase target, AnyRPG.InteractableBase originalTarget, AnyRPG.AbilityEffectContext abilityEffectInput) (at Assets/AnyRPG/Core/System/Scripts/Abilities/Effect/FixedLengthEffectProperties.cs:103)
AnyRPG.InstantEffectProperties.Cast (AnyRPG.IAbilityCaster source, AnyRPG.InteractableBase target, AnyRPG.InteractableBase originalTarget, AnyRPG.AbilityEffectContext abilityEffectContext) (at Assets/AnyRPG/Core/System/Scripts/Abilities/Effect/InstantEffectProperties.cs:23)
AnyRPG.AbilityEffectProperties.PerformAbilityEffect (AnyRPG.IAbilityCaster source, AnyRPG.InteractableBase target, AnyRPG.AbilityEffectContext abilityEffectContext, AnyRPG.AbilityEffectProperties abilityEffect) (at Assets/AnyRPG/Core/System/Scripts/Abilities/Effect/AbilityEffectProperties.cs:224)
AnyRPG.AbilityEffectProperties.PerformAbilityEffects (AnyRPG.IAbilityCaster source, AnyRPG.InteractableBase target, AnyRPG.AbilityEffectContext abilityEffectContext, System.Collections.Generic.List`1[T] abilityEffectList) (at Assets/AnyRPG/Core/System/Scripts/Abilities/Effect/AbilityEffectProperties.cs:180)
AnyRPG.AbilityEffectProperties.PerformAbilityHitEffects (AnyRPG.IAbilityCaster source, AnyRPG.InteractableBase target, AnyRPG.AbilityEffectContext effectOutput) (at Assets/AnyRPG/Core/System/Scripts/Abilities/Effect/AbilityEffectProperties.cs:233)
AnyRPG.AbilityEffectProperties.PerformAbilityHit (AnyRPG.IAbilityCaster source, AnyRPG.I…
```

<details><summary>Ortam</summary>

```text
tur: istisna
surum: 0.1.52
cihaz: Otomatik oyun testi (CI)
sistem: Unity editör
ekran: -
sahne: BoruTepesi
```

</details>




## Görülmeler (yeniden eskiye)

| Zaman · sürüm · cihaz · harita · oyuncu · ekran |
|---|
| 05.10.2026 17:13 · derleme 52 · 5 kez · BoruTepesi |

Anahtar: `bot-370f1dd7`
