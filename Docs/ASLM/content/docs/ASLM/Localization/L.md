---
title: "L"
draft: false
---

## Class `L`

`ASLM/Localization/L.cs` — **`public static`** accessor for localized UI strings backed by **`AppResources`** and **`AppLocalizationService`**.

---

### Fields

| Name | Type | Description |
| --- | --- | --- |
| `_service` | `AppLocalizationService?` | Set by `Initialize`; when null, falls back to `AppResources` |

---

## Public methods
#### `public static void Initialize(AppLocalizationService service)`

**Purpose:** Assigns `_service`. Called from [MauiProgram](../MauiProgram/) after `builder.Build()`.

---

#### `public static string Get(string key)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | If `_service` != null → `return _service.GetString(key)` |
| 2 | Else → `AppResources.ResourceManager.GetString(key, AppResources.Culture) ?? key` |

---

#### `public static string Get(string key, params object[] args)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `format = Get(key)` |
| 2 | If `args.Length == 0` → return `format` |
| 3 | Else → `string.Format(format, args)` |

---

## Related

- [LocalizationKeys](LocalizationKeys/)
- [AppLocalizationService](../Services/AppLocalizationService/)
- [AppResources.Designer](../Resources/Strings/AppResources.Designer/)
