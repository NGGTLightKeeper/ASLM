---
title: "Program"
draft: false
---

## Class `Program`

`ASLM/Launcher/Program.cs` — **`internal static`** entry point. Assembly name **`ASLM`** (`Launcher.csproj` → `ASLM.exe` at install root).

---

### Constants

| Name | Value | Role |
| --- | --- | --- |
| `FolderName` | `"App"` | Subfolder containing the MAUI host |
| `ExeName` | `"ASLM.exe"` | Main application executable |
| `LogFileName` | `"Launcher.log"` | Log file next to launcher |
| `PatcherFolderName` | `"Patcher"` | Bundled patcher directory |
| `PatcherExeNames` | `ASLM Patcher.exe`, `Patcher.exe` | Tried in order |
| `PendingUpdatePath` | `.aslm-update\pending.json` | Self-update handoff marker |
| `WaitProcessArgument` | `--wait-process` | Launcher-only CLI flag |

---

## Private methods

#### `private static void Main(string[] args)`

**Purpose:** Application entry — wait for a prior process if requested, hand off to the patcher when an update is pending, otherwise start `App/ASLM.exe`.

| Step | Action |
| --- | --- |
| 1 | Resolve `currentDir`, `logPath`, `targetPath` = `{currentDir}/App/ASLM.exe` |
| 2 | `WaitForRequestedProcessExit(args, logPath)` |
| 3 | If `TryStartPendingPatcher(currentDir, logPath)` → return (patcher owns restart) |
| 4 | If `App/ASLM.exe` missing → log and `Environment.Exit(1)` |
| 5 | Build `ProcessStartInfo` for `targetPath`, `WorkingDirectory = App` |
| 6 | `AppendForwardedArguments(startInfo, args)` |
| 7 | `Process.Start`; on failure log and exit 1 |
| Catch | Log critical error and exit 1 |

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

#### `private static bool TryStartPendingPatcher(string rootDir, string logPath)`

**Purpose:** Start the patcher from a temp shadow copy when `pending.json` exists.

| Step | Action |
| --- | --- |
| 1 | If `{root}/.aslm-update/pending.json` missing → return `false` |
| 2 | `ResolvePatcherExecutableName(patcherDir)`; if null → log and return `false` |
| 3 | `shadowDir = %TEMP%/Patcher_{guid}` |
| 4 | `CopyDirectory(patcherDir, shadowDir)` |
| 5 | Start patcher with `--root` + `rootDir` |
| 6 | Return `true` on success |

On exception: log and return `false`.

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
