---
title: "AppResources.Designer"
draft: false
---

## Class `AppResources`

`ASLM/Resources/Strings/AppResources.Designer.cs` — **`public class`**, auto-generated from **`AppResources.resx`**. Do not edit by hand.

---

## Constructor

#### `internal AppResources()`

**Purpose:** Empty; prevents external instantiation. Use static members only.

---

## Static members (infrastructure)

#### `public static ResourceManager ResourceManager` (get)

Lazy-creates `ResourceManager` for `ASLM.Resources.Strings.AppResources` assembly resource.

---

#### `public static CultureInfo Culture` (get/set)

Overrides `CurrentUICulture` for all `GetString` calls from generated properties.

---

## Generated string properties

Each RESX entry becomes:

```csharp
public static string AppShell_Title {
    get { return ResourceManager.GetString("AppShell_Title", resourceCulture); }
}
```

Hundreds of keys — regenerate from RESX when strings change (Visual Studio **Run Custom Tool** on `AppResources.resx`).

---

## Application usage

| Approach | When |
| --- | --- |
| [L](../../Localization/L/) + [LocalizationKeys](../../Localization/LocalizationKeys/) | Normal UI after [MauiProgram](../../MauiProgram/) init |
| `AppResources.Key` directly | Fallback before `L.Initialize`, tooling, or designer |

---

## Related

- [Strings _index](../_index/)
- [LocalizationKeys](../../Localization/LocalizationKeys/)
