---
title: "Patcher"
draft: false
---

The **Patcher** is a standalone **.NET MAUI** Windows app (`ASLM/Patcher/`) that applies a staged ASLM self-update while the main host is not running. It shows a fixed-size status window, runs file replacement on a background thread, then restarts **`ASLM.exe`** (the launcher) at the install root.

## Why a separate process

The host cannot replace its own binaries in place. `UpdateManager` in the main app downloads a release, extracts it to `.aslm-update/staging/`, and writes **`.aslm-update/pending.json`**. On the next start, **Launcher** copies the patcher to a temp folder and runs it with **`--root`**. The patcher performs backup → replace → cleanup, then starts the launcher again.

## Distribution layout

| Path | Purpose |
| --- | --- |
| `Patcher/ASLM Patcher.exe` | MAUI patcher UI (assembly name `ASLM Patcher`) |
| `.aslm-update/pending.json` | Update manifest (kind, version, paths, preserve list) |
| `.aslm-update/logs/Patcher.log` | Patcher file log |
| `.aslm-update/staging/` | Extracted payload (from host) |
| `.aslm-update/backup/` | Backup created during apply (removed on success) |
| `ASLM.exe` | Launcher restarted after patch completes |

## Components

| Topic | Page |
| --- | --- |
| File replacement engine | [Program](Program/) (`PatcherRunner` in `Program.cs`) |
| MAUI host bootstrap | [MauiProgram](MauiProgram/) |
| Application shell | [App](App/) |
| Status UI | [MainPage](MainPage/) |
| Windows WinUI host | [App (Windows)](Platforms/Windows/App/) |

## Pending update manifest

Written by the host (`PendingAppUpdate` in `UpdateManager`). Consumed by `PatcherRunner` (see [Program](Program/)).

| JSON field | Description |
| --- | --- |
| `kind` | Must be `"app"` |
| `version` | Release tag applied to `Data/App/ASLM_Data.json` on success |
| `stagingPath` | Directory with the new payload |
| `targetRoot` | Install root to update (defaults to `--root`) |
| `backupPath` | Optional backup directory (default: timestamp under `.aslm-update/backup/`) |
| `preserve` | Relative paths/globs kept during replace (from update source config) |

Paths under `.aslm-update/` are never replaced by the patcher.

## Command-line arguments

| Argument | Description |
| --- | --- |
| `--root {path}` | Install root (required when started from launcher shadow copy) |
| `--wait-process {pid}` | Wait for ASLM process exit before applying (optional) |
