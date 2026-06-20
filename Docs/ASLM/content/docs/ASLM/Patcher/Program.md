---
title: "Program"
draft: false
---

## Overview

`ASLM/Patcher/Program.cs` — **`PatcherRunner`** (`internal static`) and **`PatcherProgress`** (`internal sealed record`). No `Main` method; MAUI hosts the process and [MainPage](MainPage/) calls `PatcherRunner.RunAsync`.

---

## Record `PatcherProgress`

| Member | Type | Description |
| --- | --- | --- |
| `Message` | `string` | One status line for UI + log |

---

## Class `PatcherRunner`

Applies pending ASLM self-update from `.aslm-update/pending.json`.

### Constants

| Name | Value |
| --- | --- |
| `PendingRelativePath` | `.aslm-update/pending.json` |
| `LogRelativePath` | `.aslm-update/logs/Patcher.log` |
| `LauncherExeName` | `ASLM.exe` |
| `LauncherRefFileName` | `launcher-ref.json` |
| `WaitProcessArgument` | `--wait-process` |
| `LauncherArgument` | `--launcher` |

### Static fields

| Name | Type | Description |
| --- | --- | --- |
| `_logPath` | `string` | Absolute log file path for current run |
| `_progress` | `IProgress<PatcherProgress>?` | UI reporter from `RunAsync` |

---

### Public methods

#### `public static Task<int> RunAsync(string[] args, IProgress<PatcherProgress>? progress)`

**Purpose:** Assigns `_progress`, runs `RunCore(args)` on thread pool, returns task with exit code (`0` / `1`).

---

### Private methods — startup & core

#### `private static int RunCore(string[] args)`

**Purpose:** Main algorithm.

1. `root = ResolveRoot(args)`; `_logPath` under `.aslm-update/logs/`
2. Create log directory; `Log("Patcher started.")`
3. `WaitForRequestedProcessExit(args)`
4. If no `pending.json` → log, `StartLauncher(root)`, return `0`
5. `LoadPendingUpdate` + `ValidatePendingUpdate` (`kind` == `app`)
6. Resolve `targetRoot`, `stagingPath`, `backupPath`, `preservePatterns`
7. **Try:** backup → clear replaceable → copy staging → `PersistInstalledReleaseTag` → delete pending/staging/backup → log success → `StartLauncher` → `0`
8. **Catch inner:** rollback from backup, rethrow
9. **Catch outer:** log fatal, `MarkPendingUpdateFailed`, `StartLauncher`, return `1`

#### `private static void WaitForRequestedProcessExit(string[] args)`

Same contract as [Launcher Program](../../Launcher/Program/): `--wait-process {pid}`, 30 s wait, logs.

#### `private static PendingAppUpdate LoadPendingUpdate(string pendingPath)`

**Purpose:** `JsonSerializer.Deserialize<PendingAppUpdate>` + `Normalize()`.

#### `private static void ValidatePendingUpdate(PendingAppUpdate pending)`

Throws if `Kind` is not `app` (case-insensitive).

#### `private static string ResolveRoot(string[] args)`

**Purpose:** `--root {path}` if present; else `BaseDirectory` trimmed.

---

### Private methods — preserve & replace

#### `private static bool IsInternalUpdatePath(string relativePath)`

**Purpose:** `true` for `.aslm-update` or `.aslm-update/**` (normalized slashes).

#### `private static List<string> NormalizePreservePatterns(IEnumerable<string> preservePatterns)`

Trim, normalize `\` → `/`, drop empty/`..`/`.`, distinct ignore-case.

#### `private static string NormalizeRelativePattern(string pattern)`

**Purpose:** `Trim`, `\` → `/`, trim `/`.

#### `private static bool ShouldReplacePath(string relativePath, IReadOnlyCollection<string> preservePatterns)`

`true` when not internal update path and not preserved.

#### `private static bool IsPreservedPath(string relativePath, IReadOnlyCollection<string> preservePatterns)`

**Purpose:** Any preserve pattern matches via `IsPatternMatch`.

#### `private static bool IsPatternMatch(string pattern, string relativePath)`

- No `*`: exact match or child under `pattern/`
- With `*`: regex `^escaped(pattern with * → [^/]*)($|/.*)` ignore case

---

### Private methods — filesystem

#### `private static void CopyDirectory(string sourceDir, string destDir, Func<string, bool>? includeRelative = null)`

**Purpose:** Creates `destDir`, delegates to `CopyDirectoryContents` from empty relative root.

#### `private static void CopyDirectoryContents(string sourceRoot, string destRoot, string relativeDirectory, Func<string, bool>? includeRelative)`

Recursive copy: skips directories/files where `includeRelative(relative) == false`; uses `ResolveChildPath` for destinations.

#### `private static void ClearDirectory(string directory, Func<string, bool>? deleteRelative = null)`

**Purpose:** Ensures directory exists; `ClearDirectoryContents` from root.

#### `private static void ClearDirectoryContents(string root, string relativeDirectory, Func<string, bool>? deleteRelative)`

Deletes files (normalizes attributes), recurses into subdirs, removes empty subdirs when allowed.

#### `private static string CombineRelativePath(string parent, string child)`

**Purpose:** `Path.Combine` or `child` if parent empty.

#### `private static string ResolveChildPath(string rootPath, string relativePath)`

`GetFullPath` combine; throws if result escapes `rootPath` (prefix check with trailing separator).

#### `private static string EnsureTrailingSeparator(string path)`

**Purpose:** Appends directory separator if missing.

---

### Private methods — launcher & cleanup

#### `private static void StartLauncher(string root, string[] args)`

**Purpose:** Restarts the Launcher after patching or after a fatal error. Uses a three-tier strategy (see `ResolveLauncherPath`) to find the Launcher, then starts it with `WorkingDirectory` set to the directory containing the executable, and `UseShellExecute = false`. Logs if missing or start fails.

---

#### `private static string? ResolveLauncherPath(string root, string[] args)`

**Purpose:** Resolves the Launcher executable path using a three-tier strategy:

1. The `--launcher` command-line argument passed by the Launcher itself.
2. The `launcher-ref.json` file written by the Launcher in the application root.
3. Fallback: `ASLM.exe` adjacent to the application root (legacy / monolithic layout).

#### `private static void TryDeleteDirectory(string path)`

Best-effort `Directory.Delete(recursive: true)`.

#### `private static void MarkPendingUpdateFailed(string root)`

**Purpose:** Renames `pending.json` → `pending.json.failed-{utc}` so launcher does not loop on bad payload.

#### `private static void PersistInstalledReleaseTag(string root, string releaseTag)`

Merges `updates.installedReleaseTag` into `Data/App/ASLM_Data.json` via `JsonNode` (indented write). No-op if tag empty or file missing.

---

### Private methods — logging

#### `private static void Log(string message)`

**Purpose:** `_progress?.Report(new PatcherProgress(message))`; append timestamped line to `_logPath` (errors ignored).

---

### Nested type `PendingAppUpdate` (private sealed)

JSON model for `pending.json`.

| Property | JSON | Description |
| --- | --- | --- |
| `Kind` | `kind` | Default `"app"` |
| `Version` | `version` | Release tag |
| `StagingPath` | `stagingPath` | Extracted payload |
| `TargetRoot` | `targetRoot` | Install root |
| `BackupPath` | `backupPath` | Optional backup dir |
| `Preserve` | `preserve` | Path/glob list |

#### `public void Normalize()`

**Purpose:** Trims `Kind` (default `app`), null-coalesces strings and `Preserve` list.

---

## Related

- [MainPage](MainPage/) — calls `RunAsync`
- Host: [UpdateManager](../../Services/UpdateManager/)
