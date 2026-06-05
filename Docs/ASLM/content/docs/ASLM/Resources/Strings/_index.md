---
title: "Strings"
draft: false
---

Localized string resources for the host UI (`ASLM/Resources/Strings/`).

## Source files

| File | Role |
| --- | --- |
| `AppResources.resx` | Default (English) strings — edit here |
| `AppResources.{culture}.resx` | Translations (e.g. `ru`, `de`, `zh-Hans`) |
| [AppResources.Designer](AppResources.Designer/) | Strongly typed accessor generated from RESX |

## Cultures shipped

Satellite RESX files align with languages supported in [AppPersonalizationConfig](../Models/AppData/):  
`ar`, `de`, `es`, `fr`, `hi`, `id`, `it`, `ja`, `ko`, `nl`, `pl`, `pt`, `pt-BR`, `ru`, `tr`, `uk`, `vi`, `zh-Hans`, `zh-Hant`.

## Code usage

| Approach | When |
| --- | --- |
| `L.Get(LocalizationKeys.Key_Name)` | Preferred in C# and dynamic UI |
| `AppResources.Key_Name` | Direct strongly typed access |
| XAML `{x:Static ...}` | Static bindings where culture does not change at runtime |

Changing strings: edit **`.resx`**, rebuild to refresh **`.Designer.cs`** and [LocalizationKeys](../../Localization/LocalizationKeys/) (if your toolchain regenerates keys).

Runtime culture is applied by **`AppLocalizationService`** (`AppResources.Culture` + `ResourceManager`).
