---
title: "CustomThemesModels"
draft: false
---

## Overview

`ASLM/Models/CustomThemesModels.cs` — user-defined color themes persisted in **`Data/App/ASLM_CustomThemes.json`**. Consumed by **`CustomThemesStore`** and **`ThemeService`**.

---

## Class `CustomThemesRoot`

| Property | JSON | Description |
| --- | --- | --- |
| `Themes` | `themes` | Ordered list of themes |

**`Normalize()`** — ensures list exists, normalizes each theme, removes entries without **`Id`** or **`Name`**.

---

## Class `CustomTheme`

| Property | JSON | Description |
| --- | --- | --- |
| `Id` | `id` | Unique theme id |
| `Name` | `name` | Picker label |
| `BaseAppearance` | `baseAppearance` | `dark` or `light` base palette |
| `Colors` | `colors` | Sparse map: palette key → `#RRGGBB` or `#AARRGGBB` |

**`Normalize()`** — trims strings, drops invalid hex entries, calls **`ThemePaletteResolver.RemoveUnknownColorKeys`**.

#### `NormalizeBaseAppearance(string? value)`

**Purpose:** Returns **`light`** only for explicit light; otherwise **`dark`**.

#### `IsValidHex(string? value)`

Accepts `#` + 6 or 8 hex digits.
