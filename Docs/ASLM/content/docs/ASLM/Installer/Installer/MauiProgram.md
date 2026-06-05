---
title: "MauiProgram"
draft: false
---

## Class `MauiProgram`

`ASLM/Installer/Installer/MauiProgram.cs` — **`public static`** MAUI host for the standalone installer.

---

## Member reference
#### `public static MauiApp CreateMauiApp()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `MauiApp.CreateBuilder()` |
| 2 | `UseMauiApp<App>()` |
| 3 | `Services.AddSingleton<InstallerService>()` |
| 4 | `Services.AddSingleton<LegalDocumentService>()` |
| 5 | `#if DEBUG` → `AddDebug()` logging |
| 6 | `#if WINDOWS` → `ConfigureWindowsCompactControlSizing()` |
| 7 | `Build()` |

[MainPage](MainPage/) still constructs services directly; DI is available for future use.

---

#### `private static void ConfigureWindowsCompactControlSizing()` (`#if WINDOWS`)

Appends MAUI handler mappings so WinUI controls match the compact wizard layout:

| Handler | Mapping | Changes |
| --- | --- | --- |
| `CheckBoxHandler` | `"CompactWindowsCheckBox"` | `MinWidth`/`MinHeight` 16, padding 0 |
| `SwitchHandler` | `"CompactWindowsSwitch"` | Same |

---

## Related

- [Platforms/Windows/App](Platforms/Windows/App/)
- [Services](Services/)
