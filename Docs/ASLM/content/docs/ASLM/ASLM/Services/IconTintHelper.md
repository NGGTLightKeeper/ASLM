---
title: "IconTintHelper"
draft: false
---

## Class `IconTintHelper`

`ASLM/Services/IconTintHelper.cs` — **internal static** palette lookup for tinting sidebar/module icons.

---

## Internal methods

#### `internal static Color ResolvePaletteColor(string resourceKey)`

**Purpose:** Reads **`Application.Current.Resources[resourceKey]`** as `Color`; returns **white** when missing.

Used with [PackagedIconTintCache](PackagedIconTintCache/) from UI behaviors (e.g. [AppShellPage](../Pages/AppShellPage/) sidebar).

---

## Related

- Cleared indirectly via [ThemeService.PaletteApplied](ThemeService/)
