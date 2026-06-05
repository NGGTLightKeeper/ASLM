---
title: "OsAppThemeReader"
draft: false
---

## Class `OsAppThemeReader`

`ASLM/Services/OsAppThemeReader.cs` — **`public static`** — reads whether Windows apps use dark mode, independent of the MAUI app theme.

Used when resolving missing keys in custom themes so fallbacks follow **OS** appearance, not only MAUI `UserAppTheme`.

---

## Public methods

#### `public static bool IsWindowsAppDarkMode()`

**Purpose:** Returns **`true`** when Windows “app theme” is dark, or when the registry value is unavailable (falls back to MAUI).

| Step | Platform | Action |
| --- | --- | --- |
| 1 | Windows (`#if WINDOWS`) | Open `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize` |
| 2 | Windows | Read **`AppsUseLightTheme`** (`int` or `long`) |
| 3 | Windows | Return **`true`** when value is **`0`** (dark); non-zero = light |
| 4 | Windows (catch) | Fall through to MAUI fallback |
| 5 | All | `Application.Current?.RequestedTheme != AppTheme.Light` |

---

## Related

- [ThemePaletteResolver](ThemePaletteResolver/) — custom theme fallbacks
- [ThemeService](ThemeService/) — `IsSystemDark()` for MAUI-requested theme
