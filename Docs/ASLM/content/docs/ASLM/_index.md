---
title: "ASLM"
draft: false
icon: "developer_board"
weight: 100
---

C# sources for the MAUI host (`ASLM/`), **Launcher**, **Patcher**, **Installer**, and related tooling.

| Section | Doc |
| --- | --- |
| [Launcher](Launcher/) | Root `ASLM.exe` entry and patcher handoff |
| [Patcher](Patcher/) | Self-update UI and `PatcherRunner` |
| [Installer](Installer/) | Bootstrapper + MAUI setup wizard |
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
| [ASLM.Test.targets](ASLM.Test.targets) | Runs `dotnet test` after main app build |

---

## Documentation conventions

Reference pages share the same outline:

| Level | Use |
| --- | --- |
| `## Class \`Name\`` | Type overview and source path |
| `### Constants` / `### Fields` | Static data (tables) |
| `## Public methods` / `## Private methods` | Grouped members |
| `#### \`signature\`` | One section per method with **Purpose** and steps |
| `## Related` | Cross-links |

Top-level sections under [Documentation](../) use **`weight`** in each section’s `_index.md` (lower first). Inside **ASLM**, the sidebar is **alphabetical** (subsections before sibling pages). [Patch Notes](../PatchNotes/) are ordered by the **`YYYYMMDDHHmm`** prefix in each file name (newest first).
