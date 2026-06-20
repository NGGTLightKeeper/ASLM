---
title: "AppPaths"
draft: false
---

## Class `AppPaths`

`ASLM/Launcher/AppPaths.cs` — **`internal static class`** — resolves installation paths for both the shared launcher location and the per-user application directory.

### Overview

Two installation layouts are supported:

- **Monolithic (Debug):** Launcher lives next to `App\`, `Patcher\`, `Data\`, etc. in one directory.
- **Dual-location (Release):** Launcher lives in a shared directory; the application and its data live in the per-user local application data directory.

All path helpers are cross-platform by design: they rely on the .NET SpecialFolder API and Path combiners rather than hard-coded OS paths.

---

### Constants

| Name | Value |
| --- | --- |
| `AppName` | `"ASLM"` |
| `AppFolderName` | `"App"` |
| `AppExeName` | `"ASLM.exe"` |

---

## Public Methods

#### `public static string GetSharedInstallDir()`

**Purpose:** Returns the directory that contains the currently running Launcher executable. In a Release installation this is the shared installation directory.

---

#### `public static string GetUserAppDir()`

**Purpose:** Returns the per-user application directory where App, Patcher and data folders are stored.

- **Windows:** `%LOCALAPPDATA%\ASLM`
- **Linux:** `~/.local/share/ASLM` (XDG_DATA_HOME)
- **macOS:** `~/Library/Application Support/ASLM`

---

#### `public static string GetPayloadArchivePath(string sharedInstallDir)`

**Purpose:** Returns the path of the payload archive used to bootstrap a new user's application directory. The archive is placed next to the Launcher during installation.

---

#### `public static bool IsMonolithicLayout(string launcherDir)`

**Purpose:** Returns true when the Launcher is running from a monolithic (Debug) layout where `App\` lives next to the Launcher itself. In this layout the per-user bootstrapping logic is skipped and everything runs from the Launcher's own directory.
