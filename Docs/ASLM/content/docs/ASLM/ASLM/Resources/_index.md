---
title: "Resources"
draft: false
---

Embedded assets and shared UI resources for the MAUI host (`ASLM/Resources/`).

## Layout

| Path | Contents |
| --- | --- |
| [Strings](Strings/) | Localized RESX + `AppResources.Designer.cs` |
| `Styles/` | `Colors.xaml`, `Colors.Light.xaml`, `Styles.xaml`, `AppStyles.xaml` — theme and control styles |
| `Images/` | SVG icons (navigation, actions, status) |
| `AppIcon/` | Application icon vectors |
| `Splash/` | Splash screen vector |

Fonts referenced from [MauiProgram](../MauiProgram/) live under **`Resources/Fonts/`** (OpenSans, Segoe, Fluent icons).

## Localization

User-visible strings are defined in **`Strings/AppResources.resx`** (default **en**) with satellite **`AppResources.{culture}.resx`** files. Access in code via [L](../Localization/L/) and [LocalizationKeys](../Localization/LocalizationKeys/).

## Theming

**`Styles/Colors.xaml`** defines semantic color keys consumed by **`AppStyles.xaml`** and pages. Light variant **`Colors.Light.xaml`** pairs with appearance mode from [AppData](../Models/AppData/) personalization.

Custom palettes from [CustomThemesModels](../Models/CustomThemesModels/) override keys at runtime through **`ThemeService`** (Services).
