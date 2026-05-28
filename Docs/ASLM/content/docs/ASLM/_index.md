---
title: "ASLM"
draft: false
---

C# sources for the MAUI host (`ASLM/ASLM/`), **Launcher**, **Patcher**, **Installer**, and related tooling.

| Section | Doc |
| --- | --- |
| [Launcher](Launcher/) | Root `ASLM.exe` entry and patcher handoff |
| [Patcher](Patcher/) | Self-update UI and `PatcherRunner` |
| [Installer](Installer/) | Bootstrapper + MAUI setup wizard |
| [ASLM host](ASLM/) | MAUI app — [Pages](ASLM/Pages/), [Services](ASLM/Services/), [Models](ASLM/Models/), [Tests](ASLM/Tests/), and supporting areas |

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

Sidebar order is **alphabetical** (subsections before sibling pages). [Patch Notes](../PatchNotes/) keep their own order.
