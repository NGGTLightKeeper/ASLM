---
title: "ThemePaletteResolverTests"
draft: false
---

## Class `ThemePaletteResolverTests`

`ASLM/Tests/Services/ThemePaletteResolverTests.cs` — hex parsing, palette build/prefill, and contrast helpers on [ThemePaletteResolver](../../Services/ThemePaletteResolver/).

---

## Test methods

#### `public void TryParseHex_parses_hex_strings(string hex, bool shouldSucceed)`

**Purpose:** `TryParseHex` accepts 6- and 3-digit `#` forms; rejects empty input.

| `hex` | `shouldSucceed` |
| --- | --- |
| `#FF0000` | `true` |
| `#F00` | `true` |
| `""` | `false` |

| Step | Action |
| --- | --- |
| 1 | `TryParseHex(hex, out color)` |
| 2 | Assert `success`; on success assert red channel > 0.9 for `#FF0000` |

---

#### `public void ToHex_round_trips_six_digit_colors()`

**Purpose:** `ToHex` preserves six-digit RGB suffix after parse.

| Step | Action |
| --- | --- |
| 1 | Parse `#0A84FF` |
| 2 | `ToHex(color)` ends with `0A84FF` |

---

#### `public void RemoveUnknownColorKeys_drops_stale_entries()`

**Purpose:** Keys not in the built-in palette catalog are removed from custom color dictionaries.

| Step | Action |
| --- | --- |
| 1 | Dictionary with `SystemBlue` and `NotARealPaletteKey` |
| 2 | `RemoveUnknownColorKeys(colors)` |
| 3 | Assert keeps `SystemBlue`, removes unknown key |

---

#### `public void BuildCustomPalette_applies_overrides_on_top_of_base()`

**Purpose:** Custom theme color overrides merge onto dark base palette including structural keys.

| Step | Action |
| --- | --- |
| 1 | `CustomTheme` dark base, `SystemBlue = #123456`, `Normalize()` |
| 2 | `BuildCustomPalette(theme)` |
| 3 | Assert `SystemBlue` hex ends with `123456`; palette contains `BackgroundPrimary` |

---

#### `public void PrefillCustomThemeFromBuiltIn_populates_missing_keys()`

**Purpose:** Empty custom theme gains all built-in palette keys after prefill.

| Step | Action |
| --- | --- |
| 1 | Light theme with empty `Colors`, `Normalize()` |
| 2 | `PrefillCustomThemeFromBuiltIn(theme)` |
| 3 | Assert colors non-empty, includes `SystemBlue` |

---

#### `public void AllKeys_matches_built_in_palette_keys()`

**Purpose:** `AllKeys` catalog matches keys from `BuildDarkPalette()`.

| Step | Action |
| --- | --- |
| 1 | Compare `AllKeys` to `BuildDarkPalette().Keys` |
| 2 | Assert equivalent sets |

---

#### `public void SwatchContrastStroke_returns_high_contrast_stroke()`

**Purpose:** Swatch border stroke color contrasts dark vs light fill.

| Step | Action |
| --- | --- |
| 1 | Parse black and white swatch colors |
| 2 | `SwatchContrastStroke(dark)` hex ends with `9A9A9E` |
| 3 | `SwatchContrastStroke(light)` hex ends with `FFFFFF` |

---

## Related

- [ThemePaletteResolver](../../Services/ThemePaletteResolver/)
