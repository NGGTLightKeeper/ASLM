---
title: "Launcher"
draft: false
---

The **Launcher** is a small .NET console project (`Launcher/`) that ships as **`ASLM.exe`** at the install root. It is the user-facing entry point: it either starts the MAUI host under `App/ASLM.exe` or hands control to the **Patcher** when a self-update is pending.

## Role in the distribution layout

| Path (relative to install root) | Purpose |
| --- | --- |
| `ASLM.exe` | Launcher executable (`AssemblyName` is `ASLM` in `Launcher.csproj`) |
| `Launcher.log` | Timestamped launcher log written next to `ASLM.exe` |
| `App/ASLM.exe` | Main ASLM MAUI application |
| `Patcher/` | External updater (`ASLM Patcher.exe` or `Patcher.exe`) |
| `.aslm-update/pending.json` | Written by the host when an app update is staged; read by the launcher |

In **Debug** builds, the launcher project also copies `Data/`, `Engines/`, `Modules/`, and `Models/` into the output tree so local runs match the installed layout.

## Startup flow

1. Optional **`--wait-process {pid}`** — wait up to 30 seconds for a previous ASLM process to exit (used during restart after an update).
2. If **`.aslm-update/pending.json`** exists, copy `Patcher/` to a temp shadow directory and start the patcher with **`--root {installRoot}`**; the launcher then exits.
3. Otherwise start **`App/ASLM.exe`** with the same working directory and forward all arguments except launcher-only flags.

## Related documentation

- [Program](Program/) — launcher implementation (`Launcher/Program.cs`)

## Related host behavior

The MAUI host stages updates under `.aslm-update/` (see `UpdateManager` in the main `ASLM` project). The launcher and patcher only consume that layout; they do not download releases themselves.
