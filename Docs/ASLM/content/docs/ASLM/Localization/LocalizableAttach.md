---
title: "LocalizableAttach"
draft: false
---

## Class `LocalizableAttach`

`ASLM/Localization/LocalizableAttach.cs` — **`public static`** — wires **`ILocalizable`** views to **`AppLocalizationService`** lifetime.

---

## Public methods
#### `public static void Hook(VisualElement element, AppLocalizationService localization, ILocalizable target)`

| Event | Handler |
| --- | --- |
| `element.Loaded` | `localization.Register(target)`; `target.ApplyLocalization()` |
| `element.Unloaded` | `localization.Unregister(target)` |

Call from a view constructor or `OnAppearing` after resolving `AppLocalizationService` from DI.

---

## Related

- [ILocalizable](ILocalizable/)
- [AppLocalizationService](../Services/AppLocalizationService/)
