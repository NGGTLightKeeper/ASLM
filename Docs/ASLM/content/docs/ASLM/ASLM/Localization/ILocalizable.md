---
title: "ILocalizable"
draft: false
---

## Interface `ILocalizable`

`ASLM/Localization/ILocalizable.cs` — **`public interface`** for views that rebuild user-visible strings after the UI culture changes.

---

## Interface methods

#### `void ApplyLocalization()`

**Purpose:** Reapplies localized text to static and dynamically built UI elements. Called when:

- The view loads (via [LocalizableAttach](LocalizableAttach/))
- [AppLocalizationService](../Services/AppLocalizationService/) notifies registered targets after a language change

Implement on pages/content views that construct labels in code rather than XAML `{x:Static}` bindings.

---

## Related

- [LocalizableAttach](LocalizableAttach/)
- [L](L/)
