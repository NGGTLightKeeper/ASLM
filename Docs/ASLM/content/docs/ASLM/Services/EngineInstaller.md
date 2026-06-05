---
title: "EngineInstaller"
draft: false
---

## Class `EngineInstaller`

`ASLM/Services/EngineInstaller.cs` — **`public`** — discovers and installs runtimes from **`Engines/**/ASLM_Engine.json`**.

**DI:** `AddSingleton<EngineInstaller>()`.

Caches discovered engines under `_cacheLock`. Install runs on the thread pool with per-run **`StepContext`** (`{baseDir}`, `{tempDir}`).

---

## Public methods — discovery

#### `public List<EngineConfig> DiscoverEngines()`

**Purpose:** Scans **`Engines/*/ASLM_Engine.json`**, deserializes with indented JSON (nulls omitted), **`Normalize()`**, treats missing **`fileVersion`** as **1**, skips unsupported versions. If **`status.installed`** but **`runtime/`** is missing or empty → resets installed flag and rewrites manifest. Caches list and id lookups; returns a copy.

---

#### `public string? GetEngineExecutablePath(string engineId)`

**Purpose:** Resolves **`{engineDir}/{ExecutablePath}`** via **`GetEngineConfig`**; returns full path only when the file exists.

---

#### `public EngineConfig? GetEngineConfig(string engineId)`

**Purpose:** Returns installed engine config from **`_cachedInstalledEnginesById`** (calls **`DiscoverEngines()`** first).

---

#### `public bool HasEngine(string engineId)`

**Purpose:** **`true`** when any manifest exists for the id (installed or not).

---

## Public methods — installation

#### `public async Task InstallAsync(EngineConfig config, IProgress<string> log, IProgress<DownloadProgress>? downloadProgress = null, CancellationToken ct = default)`

**Purpose:** Runs on thread pool. Temp dir: **`%TEMP%/ASLM/{engineId}`**. Executes **`config.Install`** steps in order, then **`config.PostInstall`**.

| Action | Handler |
| --- | --- |
| `download` | **`ExecuteDownloadAsync`** |
| `extract` | **`ExecuteExtract`** |
| `modify_file` | **`ExecuteModifyFile`** |
| `execute` | **`ExecuteCommandAsync`** |
| `move` | **`ExecuteMove`** |
| `cleanup` | **`ExecuteCleanup`** |
| `rename_file` | **`ExecuteRenameFile`** |
| `delete_file` | **`ExecuteDeleteFile`** |

On success: sets **`status.installed`**, **`installedVersion`**, **`lastChecked`**, **`SaveConfigAsync`**, clears engine cache.

---

## Private methods — cache and steps

#### `private void EnsureEngineLookups(IReadOnlyList<EngineConfig> engines)`

**Purpose:** Builds **`_cachedEnginesById`** and **`_cachedInstalledEnginesById`** (first wins per id, ordinal ignore case).

---

#### `private async Task ExecuteDownloadAsync(InstallStep step, StepContext ctx, IProgress<string> log, IProgress<DownloadProgress>? downloadProgress, CancellationToken ct)`

**Purpose:** Downloads **`step.Url`** to resolved **`step.Dest`**. Up to 3 attempts with **`Range`** resume; optional SHA-256 verify against **`step.Sha256`**. Throttled **`DownloadProgress`** (~50 ms).

---

#### `private static void ExecuteExtract(InstallStep step, StepContext ctx, IProgress<string> log)`

**Purpose:** Zip extract with zip-slip guard (destination prefix check). Skips locked files with warning.

---

#### `private static void ExecuteModifyFile(InstallStep step, StepContext ctx, IProgress<string> log)`

**Purpose:** Find/replace in **`step.Path`**; warns and skips when pattern not found.

---

#### `private static async Task ExecuteCommandAsync(InstallStep step, StepContext ctx, IProgress<string> log, CancellationToken ct)`

**Purpose:** Splits **`step.Command`**, resolves exe and path-like args, streams stdout/stderr to log. Sets **`PYTHONIOENCODING`** / **`PYTHONUTF8`**. Non-zero exit logs warning only.

---

#### `private static void ExecuteMove(InstallStep step, StepContext ctx, IProgress<string> log)`

**Purpose:** Deletes existing dest directory if present, then **`Directory.Move`**.

---

#### `private static void ExecuteCleanup(InstallStep step, StepContext ctx, IProgress<string> log)`

**Purpose:** Recursively deletes **`step.Target`** or **`ctx.TempDir`** when target omitted.

---

#### `private static void ExecuteRenameFile(InstallStep step, StepContext ctx, IProgress<string> log)`

**Purpose:** **`File.Move`** after optional delete of dest; skips when source missing.

---

#### `private static void ExecuteDeleteFile(InstallStep step, StepContext ctx, IProgress<string> log)`

**Purpose:** Deletes file at resolved **`step.Target`**; skips when missing.

---

## Private methods — helpers

#### `private static string GetRootDirectory()`

**Purpose:** Parent of **`AppDomain.CurrentDomain.BaseDirectory`** (application root above **`App/`**).

---

#### `private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)`

**Purpose:** Lowercase hex SHA-256 of file contents.

---

#### `private async Task SaveConfigAsync(EngineConfig config)`

**Purpose:** Writes manifest JSON to **`config.SourcePath`** when set.

---

#### `private static List<string> SplitCommand(string command)`

**Purpose:** Tokenizes command line respecting single/double quotes.

---

#### `private static string JoinArguments(IEnumerable<string> args)`

**Purpose:** Joins tokens; quotes arguments containing spaces.

---

## Nested class `StepContext`

Per-install scoped paths (not stored on the singleton).

#### `public StepContext(string baseDir, string tempDir)`

**Purpose:** Normalizes **`BaseDir`** and **`TempDir`** with trailing directory separators.

---

#### `private static string EnsureTrailingSeparator(string path)`

**Purpose:** Appends directory separator when absent.

---

#### `public string ResolveVariables(string input)`

**Purpose:** Replaces **`{temp}`** with the temp directory path (trimmed).

---

#### `public string ResolvePath(string path)`

**Purpose:** Resolves **`{temp}`**, combines relative paths under **`BaseDir`**, validates result stays under **`BaseDir`** or **`TempDir`**. Throws on path traversal.

---

#### `public string ResolveArgPaths(IEnumerable<string> args)`

**Purpose:** Resolves tokens containing **`/`** or **`\`** via **`ResolvePath`**, then **`JoinArguments`**.

---

## Related

- [ModuleRunner](ModuleRunner/)
- [SettingsService](SettingsService/)
- [DownloadInstaller](DownloadInstaller/)
- [OllamaSettingsStore](OllamaSettingsStore/)
