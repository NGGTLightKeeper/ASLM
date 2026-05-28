---
title: "ThemeService"
draft: false
---

## Class `ThemeService`

`ASLM/Services/ThemeService.cs` — **`public sealed`** — applies resolved color palettes to **`Application.Current.Resources`** so **`DynamicResource`** bindings update immediately.

**DI:** `AddSingleton<ThemeService>()` — [AppDataStore](AppDataStore/), [CustomThemesStore](CustomThemesStore/).

Must run on the **main thread** for apply/preview entry points.

---

### Events (static)

| Event | When |
| --- | --- |
| `PaletteApplied` | After **`WritePaletteToResources`**; icon tints refresh via [PackagedIconTintCache](PackagedIconTintCache/) |

---

## Public methods

#### `public ThemeService(AppDataStore appData, CustomThemesStore customThemesStore, ILogger<ThemeService> logger)`

**Purpose:** Stores dependencies.

---

#### `public void ApplyFromSettings()`

**Purpose:** **`ApplyPersonalization(_appData.Data.Personalization, customDraft: null)`**; logs errors.

---

#### `public void ApplyPersonalization(AppPersonalizationConfig config, CustomTheme? customDraft)`

**Purpose:** **`System`** → **`ApplySystemTheme`**. **`Custom`** → draft or persisted theme by **`CustomThemeId`**, else dark built-in. **`Light`** / default → built-in light/dark. Logs errors.

---

#### `public void PreviewCustomTheme(CustomTheme draft)`

**Purpose:** **`ApplyCustomTheme(draft)`** without persisting.

---

#### `public static bool IsSystemDark()`

**Purpose:** **`Application.Current.RequestedTheme != AppTheme.Light`**.

---

## Private methods

#### `private void ApplySystemTheme()`

**Purpose:** Sets **`UserAppTheme = Unspecified`**, subscribes **`RequestedThemeChanged`**, applies built-in palette from **`IsSystemDark()`**.

---

#### `private void StopTrackingSystemTheme()`

**Purpose:** Unsubscribes **`RequestedThemeChanged`**.

---

#### `private void OnSystemThemeChanged(object? sender, AppThemeChangedEventArgs e)`

**Purpose:** Main-thread **`ApplyBuiltInTheme`** from OS theme.

---

#### `private void ApplyBuiltInTheme(bool isDark)`

**Purpose:** Stops system tracking, sets **`UserAppTheme`**, **`WritePaletteToResources`** from [ThemePaletteResolver](ThemePaletteResolver/) dark/light palette.

---

#### `private void ApplyCustomTheme(CustomTheme theme)`

**Purpose:** Stops system tracking, sets **`UserAppTheme`** from **`BaseAppearance`**, writes **`BuildCustomPalette(theme)`**.

---

#### `private static void WritePaletteToResources(Dictionary<string, Color> palette)`

**Purpose:** Sets each **`resources[key]`** and **`{key}Brush`** **`SolidColorBrush`** when brush key exists; **`PackagedIconTintCache.Clear()`**; invokes **`PaletteApplied`**.

---

## Related

- [ThemePaletteResolver](ThemePaletteResolver/)
- [CustomThemesStore](CustomThemesStore/)
- [SettingsView](../Pages/SettingsView/)
