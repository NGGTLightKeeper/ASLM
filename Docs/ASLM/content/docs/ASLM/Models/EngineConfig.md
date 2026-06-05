---
title: "EngineConfig"
draft: false
---

## Overview

`ASLM/Models/EngineConfig.cs` — deserializes **`ASLM_Engine.json`** for an engine runtime package under **`Engines/{id}/`**.

---

## Class `EngineConfig`

| Property | JSON | Description |
| --- | --- | --- |
| `FileVersion` | `fileVersion` | Schema version (default 1) |
| `Id` | `id` | Stable engine id |
| `Name` | `name` | UI label |
| `Description` | `description` | Short text |
| `Version` | `version` | Packaged version |
| `Type` | `type` | e.g. `runtime` |
| `ExecutablePath` | `executablePath` | Relative path to main binary |
| `PackageManager` | `packageManager` | pip-style install command |
| `ModuleEnvironment` | `moduleEnvironment` | Per-module venv/prefix |
| `Requirements` | `requirements` | OS / arch / disk |
| `Install` | `install` | Ordered install steps |
| `PostInstall` | `postInstall` | Steps after main pipeline |
| `Status` | `status` | Persisted install state |
| `SourcePath` | *(ignored)* | Absolute manifest path at runtime |

---

## `InstallStep`

Declarative install action; meaningful fields depend on **`action`**:

| JSON field | Used by |
| --- | --- |
| `action` | Dispatcher (required) |
| `name` | Progress label |
| `url`, `sha256`, `dest` | Download |
| `source`, `dest` | Extract / move |
| `path`, `find`, `replace` | File modify |
| `command` | Execute |
| `target` | Cleanup |

---

## `EnginePackageManager`

| Property | JSON |
| --- | --- |
| `Command` | `command` — e.g. `-m pip install` |
| `Executable` | `executable` — optional override |

---

## `EngineModuleEnvironment`

| Property | JSON | Default / notes |
| --- | --- | --- |
| `Enabled` | `enabled` | `true` |
| `DirectoryPrefix` | `directoryPrefix` | `venv-` |
| `Kind` | `kind` | e.g. `python-venv` |
| `CreateCommand` | `createCommand` | Run with engine exe |
| `ExecutablePath` | `executablePath` | Module command exe |
| `PackageManagerExecutable` | `packageManagerExecutable` | |
| `PackageManagerCommand` | `packageManagerCommand` | |
| `Environment` | `environment` | Env var map |

---

## `EngineRequirements`

| Property | JSON |
| --- | --- |
| `Os` | `os` |
| `Arch` | `arch` |
| `DiskSpaceMb` | `diskSpaceMb` |

---

## `EngineStatus`

| Property | JSON |
| --- | --- |
| `Installed` | `installed` |
| `InstalledVersion` | `installedVersion` |
| `LastChecked` | `lastChecked` |

**`Normalize()`** on root and nested types restores collections and trims strings.
