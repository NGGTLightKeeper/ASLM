---
title: "ASLM"
draft: false
icon: "developer_board"
weight: 100
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

Top-level sections under [Documentation](../) use **`weight`** in each section’s `_index.md` (lower first). Inside **ASLM**, the sidebar is **alphabetical** (subsections before sibling pages). [Patch Notes](../PatchNotes/) are ordered by the **`YYYYMMDDHHmm`** prefix in each file name (newest first).
