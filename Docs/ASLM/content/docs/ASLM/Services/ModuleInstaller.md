---
title: "ModuleInstaller"
draft: false
---

## Class `ModuleInstaller`

`ASLM/Services/ModuleInstaller.cs` — **`public`** — discovers **`Modules/**/ASLM_Module.json`**, installs module archives from URLs, refreshes GitHub source trees, and persists manifests.

**DI:** `AddSingleton<ModuleInstaller>()` — [ModuleRunner](ModuleRunner/), [ModuleTrustService](ModuleTrustService/).

Uses **`DownloadProgress`** from [Models/DownloadProgress](../Models/DownloadProgress/).

---

### Events

| Event | When |
| --- | --- |
| `ModulesChanged` | After manifest save when `raiseModulesChanged` is true (default) |

---

## Public methods

#### `public ModuleInstaller(ModuleRunner moduleRunner, ModuleTrustService moduleTrustService)`

**Purpose:** Creates the installer, stores runner/trust dependencies, sets HTTP **`User-Agent: ASLM-ModuleInstaller`**.

---

#### `public async Task<List<ModuleConfig>> DiscoverModulesAsync()`

**Purpose:** Scans **`Modules/*/ASLM_Module.json`** (all subdirectories), loads each via **`LoadModuleConfig`**, returns sorted list by **`Name`** (ordinal ignore case). Returns empty list when **`Modules`** root is missing.

---

#### `public async Task<ModuleConfig?> LoadModuleConfig(string jsonFile)`

**Purpose:** Reads one manifest: deserialize with indented JSON (nulls omitted), **`Normalize()`**, **`HasDeclaredUpdateConfig`** from raw JSON **`update`** property, default **`FileVersion`** to **1**, skip unsupported versions. Sets **`SourcePath`**, **`Status.Installed = true`**. Returns **`null`** on missing file, parse errors, or unsupported **`fileVersion`**.

---

#### `public async Task<bool> DownloadSourceAsync(ModuleConfig module, IProgress<string> log, IProgress<DownloadProgress>? downloadProgress = null, CancellationToken ct = default)`

**Purpose:** When **`module.Source.Type`** is **`github`** with **`Repo`**: downloads **`https://api.github.com/repos/{repo}/zipball/{branch}`** (branch from **`module.Update.Branch`** when update block declared, else **`main`**), extracts to temp, merges inner GitHub folder into module directory via **`CopyDirectory`**. Returns **`true`** when no GitHub source (skipped) or success; **`false`** on failure. Cleans temp zip/extract dirs in **`finally`**.

---

#### `public async Task<ModuleConfig> InstallFromUrlAsync(string zipUrl, IProgress<string> log, IProgress<DownloadProgress>? downloadProgress = null, CancellationToken ct = default)`

**Purpose:** Full install: download zip → extract → find **`ASLM_Module.json`** → validate id/version → replace **`Modules/{id}`** → set status fields → **`SaveConfigAsync`** → **`ModuleRunner.ExecuteFirstRunAsync`** (second save on success) → **`ModuleTrustService.RefreshReviewedListAsync`**. Throws **`InvalidOperationException`** for invalid archives.

---

#### `public void SaveModuleConfig(ModuleConfig config, bool raiseModulesChanged = true)`

**Purpose:** Serializes manifest to **`config.SourcePath`** synchronously. No-op when **`SourcePath`** empty. Optionally raises **`ModulesChanged`** (set **`false`** for preference-only saves that should not rebuild module cards).

---

#### `public async Task SaveConfigAsync(ModuleConfig config, bool raiseModulesChanged = true)`

**Purpose:** Async variant of **`SaveModuleConfig`**.

---

## Private methods

#### `private static void CopyDirectory(string sourceDir, string destDir)`

**Purpose:** Recursive file/directory copy with overwrite.

---

#### `private async Task DownloadFileAsync(string url, string destinationPath, IProgress<string> log, IProgress<DownloadProgress>? downloadProgress, CancellationToken ct)`

**Purpose:** Streams download with 64 KiB buffer; throttled **`DownloadProgress`** reports every 50 ms when **`Content-Length`** known; final report at fraction **1.0**.

---

#### `private static string GetRootDirectory()`

**Purpose:** Parent of **`AppDomain.CurrentDomain.BaseDirectory`** (application root above **`App`**).

---

#### `private static void TryDeleteFile(string filePath)`

**Purpose:** Best-effort file delete; swallows errors.

---

#### `private static void TryDeleteDirectory(string directoryPath)`

**Purpose:** Best-effort recursive directory delete; swallows errors.

---

#### `private static bool HasDeclaredUpdateBlock(string json)`

**Purpose:** **`true`** when root JSON object contains property **`update`**.

---

#### `private void RaiseModulesChanged()`

**Purpose:** Invokes **`ModulesChanged`** with **`EventArgs.Empty`**.

---

## Related

- [ModuleRunner](ModuleRunner/)
- [ModuleTrustService](ModuleTrustService/)
- [UpdateManager](UpdateManager/)
- [DownloadProgress](../Models/DownloadProgress/)
