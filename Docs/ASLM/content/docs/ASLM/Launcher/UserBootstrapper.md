---
title: "UserBootstrapper"
draft: false
---

## Class `UserBootstrapper`

`ASLM/Launcher/UserBootstrapper.cs` — **`internal static class`** — bootstraps the per-user ASLM application directory for a user who does not yet have one.

### Overview

When a second (or any subsequent) user launches the shared Launcher, their per-user application directory does not exist yet. This class extracts the payload archive that was placed next to the Launcher during installation and sets up the user's own copy of the application. The Launcher executable itself is intentionally excluded because it already lives in the shared installation directory.

---

### Constants

| Name | Value |
| --- | --- |
| `LauncherExeName` | `"ASLM.exe"` |
| `LauncherRefFileName` | `"launcher-ref.json"` |

---

## Public Methods

#### `public static bool IsUserAppDirReady(string userAppDir)`

**Purpose:** Returns true when the per-user application directory is already initialized (i.e. checking if `App/ASLM.exe` exists in `userAppDir`).

---

#### `public static bool TryBootstrap(string sharedInstallDir, string userAppDir, string logPath)`

**Purpose:** Extracts the payload archive from the shared installation directory into the per-user application directory, skipping the root-level Launcher executable.

Returns true when bootstrapping completed successfully. On failure, logs the error, attempts to remove a potentially partial extraction, and returns false.

---

#### `public static void WriteLauncherRef(string launcherExePath, string userAppDir)`

**Purpose:** Writes (or updates) the launcher reference file (`launcher-ref.json`) inside the per-user application directory so the Patcher can restart the correct Launcher after a self-update.

---

## Private Methods

#### `private static void ExtractPayload(string archivePath, string targetDir, string logPath)`

**Purpose:** Extracts archive entries into the target directory, skipping the root-level Launcher executable. Applies directory traversal safety checks (`IsChildPath`).

---

#### `private static bool IsRootLauncherEntry(string entryFullName)`

**Purpose:** Returns true when the archive entry represents the root-level Launcher executable (`ASLM.exe` at the top of the zip).

---

#### `private static bool IsChildPath(string parentPath, string childPath)`

**Purpose:** Returns true when `childPath` is located under `parentPath`.

---

#### `private static void TryDeleteDirectory(string path)`

**Purpose:** Removes a directory tree on a best-effort basis.

---

#### `private static void Log(string message, string logPath)`

**Purpose:** Appends a timestamped message to the log file.
