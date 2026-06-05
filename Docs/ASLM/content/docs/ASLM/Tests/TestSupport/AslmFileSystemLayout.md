---
title: "AslmFileSystemLayout"
draft: false
---

## Class `AslmFileSystemLayout`

`ASLM/Tests/TestSupport/AslmFileSystemLayout.cs` — **`public sealed`**, **`IDisposable`** — synthetic install layout matching production (`{root}/App`, `{root}/Data/App`, `{root}/Modules`).

---

### Properties

| Property | Description |
| --- | --- |
| `AppDir` | Test output `.../_layout_root/App` |
| `Root` | Parent of `AppDir` (layout root passed to `GetRootDirectory()`) |
| `DataAppDir` | `{Root}/Data/App` |
| `ModulesDir` | `{Root}/Modules` |
| `AppDataFilePath` | `ASLM_Data.json` |
| `CustomThemesFilePath` | `ASLM_CustomThemes.json` |
| `DownloadsFilePath` | `ASLM_Downloads.json` |
| `PortsFilePath` | `ASLM_Ports.json` |
| `NotificationsFilePath` | `ASLM_Notifications.json` |

---

## Constructor

#### `public AslmFileSystemLayout(bool resetData = true)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `AppDir` = assembly base directory |
| 2 | `Root` = parent of `AppDir` (throws if not `{root}/App`) |
| 3 | `EnsureStandardDirectories()` |
| 4 | If `resetData` → `ResetDataAppDirectory()` |

---

## Public methods
#### `public void EnsureStandardDirectories()`

**Purpose:** Creates `Data/App` and `Modules` under `Root`.

---

#### `public void ResetDataAppDirectory()`

**Purpose:** Deletes all files in `Data/App` (not subdirectories beyond files).

---

#### `public void WriteAppDataJson(string json)`

**Purpose:** Writes `ASLM_Data.json`.

---

#### `public void WritePortsJson(string json)`

**Purpose:** Writes `ASLM_Ports.json`.

---

#### `public void Dispose()`

**Purpose:** If `_ownsRoot`, deletes `Root` recursively (default ctor does not own root).

---

## Related

- [AssemblyInfo](../AssemblyInfo/)
