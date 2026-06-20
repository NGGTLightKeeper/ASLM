---
title: "Program"
draft: false
---

## Class `Program`

`ASLM/Launcher/Program.cs` — **`internal static`** entry point. Assembly name **`ASLM`** (`Launcher.csproj` → `ASLM.exe` at install root).

Two layouts are supported:

- **Monolithic (Debug):** The Launcher executable sits next to `App\`, `Patcher\`, `Data\`, etc. in one directory. This layout is detected automatically and behaves exactly as before.
- **Dual-location (Release):** The Launcher sits in a shared installation directory alongside `aslm-base.zip`. The application itself lives in the per-user `%LOCALAPPDATA%\ASLM` directory. If that directory is not yet populated, the Launcher bootstraps it from `aslm-base.zip` before starting the application.

---

### Constants

| Name | Value | Role |
| --- | --- | --- |
| `AppFolderName` | `"App"` | Subfolder containing the MAUI host |
| `ExeName` | `"ASLM.exe"` | Main application executable |
| `LogFileName` | `"Launcher.log"` | Log file next to launcher |
| `PatcherFolderName` | `"Patcher"` | Bundled patcher directory |
| `PatcherExeNames` | `ASLM Patcher.exe`, `Patcher.exe` | Tried in order |
| `PendingUpdateRelativePath` | `.aslm-update\pending.json` | Self-update handoff marker |
| `WaitProcessArgument` | `--wait-process` | Launcher-only CLI flag |
| `LauncherArgument` | `--launcher` | Argument to pass the Launcher's own path to the Patcher |

---

## Private methods

#### `private static void Main(string[] args)`

**Purpose:** Application entry — wait for a prior process if requested, conditionally bootstrap the user application directory based on the layout, hand off to the patcher when an update is pending, and otherwise start the main application.

| Step | Action |
| --- | --- |
| 1 | Resolve `sharedInstallDir` and `logPath`. |
| 2 | `WaitForRequestedProcessExit(args, logPath)`. |
| 3 | Determine `appRoot`: if `AppPaths.IsMonolithicLayout`, `appRoot = sharedInstallDir`. Otherwise, `appRoot = AppPaths.GetUserAppDir()`. If the user directory is not ready, bootstrap it using `UserBootstrapper.TryBootstrap` and write the launcher reference file. |
| 4 | `TryStartPendingPatcher(sharedInstallDir, appRoot, logPath)`. If true, return (patcher owns restart). |
| 5 | `StartApp(appRoot, args, logPath)`. |
| Catch | Log critical error and exit 1. |

---

#### `private static void StartApp(string appRoot, string[] args, string logPath)`

**Purpose:** Starts the main ASLM application from the resolved application root directory. Validates the executable path, builds `ProcessStartInfo` (with `WorkingDirectory = App`), forwards arguments, and starts the process. Exits if the target is not found or fails to start.

---

#### `private static bool TryStartPendingPatcher(string sharedInstallDir, string appRoot, string logPath)`

**Purpose:** Start the patcher from a temp shadow copy when `pending.json` exists.

| Step | Action |
| --- | --- |
| 1 | If `{appRoot}/.aslm-update/pending.json` is missing → return `false`. |
| 2 | `ResolvePatcherExecutableName(patcherDir)`. If null → log and return `false`. |
| 3 | Create `shadowDir` at `%TEMP%/Patcher_{guid}`. |
| 4 | `CopyDirectory(patcherDir, shadowDir)`. |
| 5 | Start patcher with `--root {appRoot}` and `--launcher {launcherExePath}`. |
| 6 | Return `true` on success. |

On exception: log and return `false`.

---

#### `private static void WaitForRequestedProcessExit(string[] args, string logPath)`

**Purpose:** Optional wait for a previous ASLM process (used during self-update restart).

| Step | Action |
| --- | --- |
| 1 | Scan `args` for `--wait-process {pid}` |
| 2 | `Process.GetProcessById` + `WaitForExit(30000)` (log on timeout) |
| 3 | Ignore `ArgumentException` (process already exited) |
| 4 | Log other exceptions; stop after first matching pair |

Used when [SettingsService](../Services/SettingsService/) starts the launcher with `--wait-process`.

---

#### `private static void AppendForwardedArguments(ProcessStartInfo startInfo, string[] args)`

**Purpose:** Forward CLI arguments to the MAUI host, excluding launcher-only flags.

Copies each argument to `startInfo.ArgumentList` except `--wait-process` and the following PID value.

---

#### `private static string? ResolvePatcherExecutableName(string patcherDir)`

**Purpose:** Pick the first existing patcher executable name under `Patcher/`.

Iterates `PatcherExeNames`; returns first file that exists, else `null`.

---

#### `private static void CopyDirectory(string sourceDir, string destDir)`

**Purpose:** Recursive directory copy for patcher shadow deployment.

Creates directories and copies all files with `overwrite: true` so the live `Patcher/` folder is not locked during update.

---

#### `private static void Log(string message, string logPath)`

**Purpose:** Append a timestamped line to `Launcher.log` (swallows IO errors).

---

## Related

- [Launcher _index](_index/)
- [Patcher Program](../Patcher/Program/)
- [UpdateManager](../Services/UpdateManager/)
