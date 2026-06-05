---
title: "ThemePaletteResolver"
draft: false
---

## Class `ThemePaletteResolver`

`ASLM/Services/ThemePaletteResolver.cs` — **`public static`** — builds built-in dark/light palettes, merges custom theme overrides, and provides hex/color helpers. Key names mirror **`Resources/Styles/Colors.xaml`**.

Resolution order for custom themes: base palette from **`CustomTheme.BaseAppearance`** → explicit **`Colors`** overrides.

---

### Properties

| Name | Description |
| --- | --- |
| `AllKeys` | Read-only ordered list of every canonical palette key (from dark palette) |

---

## Public methods

#### `public static void RemoveUnknownColorKeys(IDictionary<string, string> colors)`

**Purpose:** Removes keys not in the canonical set (stale keys from older builds).

---

#### `public static Dictionary<string, Color> BuildDarkPalette()`

**Purpose:** Full dark-mode **`Dictionary<string, Color>`** (system, backgrounds, labels, actions, overlays).

---

#### `public static Dictionary<string, Color> BuildLightPalette()`

**Purpose:** Full light-mode palette.

---

#### `public static void PrefillCustomThemeFromBuiltIn(CustomTheme theme)`

**Purpose:** **`theme.Normalize()`**, clears **`theme.Colors`**, fills every key from dark or light base as **`#AARRGGBB`** hex via **`ToHex`**.

---

#### `public static Dictionary<string, Color> BuildCustomPalette(CustomTheme theme)`

**Purpose:** Starts from light or dark base per **`BaseAppearance`**, applies valid hex entries from **`theme.Colors`** for known keys only.

---

#### `public static Color SwatchContrastStroke(Color fill)`

**Purpose:** Returns light or dark stroke color from relative luminance (~0.52 threshold) for color picker swatches.

---

#### `public static bool TryParseHex(string hex, out Color color)`

**Purpose:** **`Color.FromArgb`** wrapper; **`false`** on empty/invalid.

---

#### `public static string ToHex(Color color)`

**Purpose:** Canonical **`#AARRGGBB`** string.

---

## Private methods

#### `private static Color C(string hex)`

**Purpose:** **`Color.FromArgb(hex)`** for palette literals.

---

## Related

- [ThemeService](ThemeService/)
- [CustomThemesStore](CustomThemesStore/)
- [ThemeColorPickerView](../Pages/ThemeColorPickerView/)
