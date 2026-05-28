---
title: "ASLM"
draft: false
---

**.NET MAUI** Windows host (`ASLM/ASLM/`, assembly **`ASLM`**). Boots services, shows the shell UI, runs modules, and coordinates updates. Entry is **[Platforms/Windows/App](Platforms/Windows/App/)** → [MauiProgram](MauiProgram/) → [App](App/).

## Documented areas

| Section | Description |
| --- | --- |
| [App](App/) | Application host, window, shutdown |
| [MauiProgram](MauiProgram/) | DI registration and startup |
| [GlobalUsings](GlobalUsings/) | Project-wide usings |
| [Localization](Localization/) | UI string access and refresh hooks |
| [Models](Models/) | JSON manifests and persisted DTOs |
| [Platforms](Platforms/) | Windows WinUI host and packaging |
| [Resources](Resources/) | RESX strings, styles, images |
| [Pages](Pages/) | MAUI UI — shell, overlays, first-run wizard |
| [Services](Services/) | Host singletons — persistence, modules, theme, updates |
| [Tests](Tests/) | xUnit project — service unit tests |

## Other

| Section | Notes |
| --- | --- |
| [ASLM.Test.targets](ASLM.Test.targets) | Runs `dotnet test` after main app build |
