---
title: "ModuleThemePayloadBuilder"
draft: false
---

## Class `ModuleThemePayloadBuilder`

`ASLM/Services/ModuleThemePayloadBuilder.cs` — **`public sealed`** — builds a JSON snapshot of the host ASLM theme for modules that declare a **`theme`** setting.

Delivered through standard **`setExec`** integration (typically via a temp file path).

**DI:** `AddSingleton<ModuleThemePayloadBuilder>()` — [AppDataStore](AppDataStore/), [CustomThemesStore](CustomThemesStore/).

---

## Public methods

#### `public ModuleThemePayloadBuilder(AppDataStore appData, CustomThemesStore customThemes, ILogger<ModuleThemePayloadBuilder> logger)`

**Purpose:** Creates the payload builder and stores dependencies.

---

#### `public string BuildJson()`

**Purpose:** Serializes the active host theme (appearance + resolved palette) to a single-line JSON string.

| Step | Action |
| --- | --- |
| 1 | Read `Personalization`; `appearance` ← `AppPersonalizationConfig.NormalizeAppearance` |
| 2 | Resolve palette per appearance (see table) |
| 3 | Convert each palette entry to hex via `ThemePaletteResolver.ToHex` |
| 4 | Serialize `ModuleHostThemePayloadDto` (camelCase) |
| On error | Log; return `{"theme":"dark","appearance":"Dark","colors":{}}` |

| `appearance` | `theme` (effective) | Palette source |
| --- | --- | --- |
| `System` | `dark` / `light` from `IsHostSystemDark()` | `BuildDarkPalette` / `BuildLightPalette` |
| `Custom` | From `CustomTheme.BaseAppearance` when theme found | `BuildCustomPalette` + id/name; else dark fallback |
| `Light` | `light` | Built-in light |
| Other / `Dark` | `dark` | Built-in dark |

| JSON field | Content |
| --- | --- |
| `appearance` | Normalized setting (`System`, `Custom`, …) |
| `theme` | Effective `dark` / `light` |
| `customThemeId` | Optional |
| `customThemeName` | Optional |
| `colors` | Palette key → `#AARRGGBB` hex |

---

## Private methods

#### `private static bool IsHostSystemDark()`

**Purpose:** Resolves effective dark mode when appearance is **System** (mirrors [ThemeService](ThemeService/) `IsSystemDark()`).

| Step | Action |
| --- | --- |
| 1 | `ThemeService.IsSystemDark()` |
| On any exception | Return **`true`** (default dark) |

---

## Private types (same file)

### `ModuleHostThemePayloadDto` (private sealed class)

| Property | Default |
| --- | --- |
| `Appearance` | `"Dark"` |
| `Theme` | `"dark"` |
| `CustomThemeId` | optional |
| `CustomThemeName` | optional |
| `Colors` | empty dictionary (ordinal-ignore-case keys) |

---

## Related

- [ModuleRunner](ModuleRunner/) — theme-type module settings
- [ThemeService](ThemeService/) / [ThemePaletteResolver](ThemePaletteResolver/)
