---
title: "ModuleEnvironmentResolver"
draft: false
---

## Class `ModuleEnvironmentResolver`

`ASLM/Services/ModuleEnvironmentResolver.cs` — **`public`** — creates and resolves per-module engine environments (venv paths, executables, package manager commands, env vars) from engine manifests.

**DI:** `AddSingleton<ModuleEnvironmentResolver>()` — [EngineInstaller](EngineInstaller/).

Record at file bottom: **`ModuleEnvironmentResolution`**.

---

## Public methods

#### `public ModuleEnvironmentResolver(EngineInstaller engineInstaller)`

**Purpose:** Stores **`EngineInstaller`** and a **`SemaphoreSlim(1,1)`** for environment creation.

---

#### `public static bool HasModuleEnvironment(EngineConfig engine)`

**Purpose:** **`true`** when **`engine.ModuleEnvironment`** exists and **`Enabled`** is **`true`**.

---

#### `public async Task<ModuleEnvironmentResolution?> EnsureEnvironmentAsync(ModuleConfig module, EngineConfig engine, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Returns **`null`** when engine has no module environment. Otherwise **`ResolveEnvironment`**, **`Directory.CreateDirectory`**, returns immediately if **`IsEnvironmentReady`**. Under lock: re-check readiness, run **`CreateCommand`** via **`RunProcessAsync`** (optional Python **`virtualenv`** fallback), throws **`InvalidOperationException`** if still not ready.

---

#### `public ModuleEnvironmentResolution ResolveEnvironment(ModuleConfig module, EngineConfig engine)`

**Purpose:** Builds **`{engineDir}/{DirectoryPrefix}{moduleSlug}`**, resolves executable/package-manager paths and commands from templates, copies **`ModuleEnvironment.Environment`** dictionary with token substitution. Throws when engine has no module environment or no source directory.

---

#### `public void ApplyEnvironmentVariables(ModuleConfig module, EngineConfig engine, ProcessStartInfo psi)`

**Purpose:** When module environment enabled: applies resolved env vars to **`psi.Environment`**, replacing **`{path}`** with current PATH; sets **`ASLM_ENGINE_ENV_DIR`**.

---

#### `public string BuildPackageInstallArguments(ModuleEnvironmentResolution? resolution, EngineConfig engine, IEnumerable<string> packages)`

**Purpose:** **`{packageManagerCommand} {quoted packages}`** or command alone when no packages.

---

#### `public string ResolvePackageManagerExecutable(ModuleEnvironmentResolution? resolution, EngineConfig engine)`

**Purpose:** Prefers resolution path, else engine **`PackageManager.Executable`** under engine dir, else **`ResolveEngineExecutable`**.

---

#### `public string ResolveCommandExecutable(ModuleEnvironmentResolution? resolution, EngineConfig engine)`

**Purpose:** Prefers resolution **`ExecutablePath`**, else engine executable.

---

## Private methods

#### `private string ResolveEngineExecutable(EngineConfig engine)`

**Purpose:** **`EngineInstaller.GetEngineExecutablePath(engine.Id)`** or throws.

---

#### `private static bool IsEnvironmentReady(EngineConfig engine, ModuleEnvironmentResolution resolution)`

**Purpose:** Directory exists when no **`CreateCommand`**; else requires **`File.Exists(ExecutablePath)`**.

---

#### `private static bool IsPythonVirtualEnvironment(EngineConfig engine)`

**Purpose:** **`Kind`** contains **`python`**, or executable name starts with **`python`** / **`py`**.

---

#### `private async Task<bool> TryCreatePythonVirtualenvFallbackAsync(...)`

**Purpose:** Installs **`virtualenv`** via package manager, then **`python -m virtualenv "{environmentDir}"`**.

---

#### `private static async Task<bool> RunProcessAsync(string fileName, string arguments, string workingDirectory, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Starts process with redirected UTF-8 stdout/stderr, drains to log, returns exit code **0**.

---

#### `private static async Task DrainOutputAsync(StreamReader reader, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Non-whitespace lines reported as **`  {line}`**.

---

#### `private string ResolvePathTemplate(string template, ModuleConfig module, EngineConfig engine, ModuleEnvironmentResolution resolution)`

**Purpose:** **`ResolveTemplate`** then **`Path.GetFullPath`** (rooted or relative to engine dir).

---

#### `private string ResolveTemplate(string template, ModuleConfig module, EngineConfig engine, ModuleEnvironmentResolution resolution)`

**Purpose:** Replaces **`{engineDir}`**, **`{runtimeDir}`**, **`{engineExecutable}`**, **`{environmentDir}`**, **`{moduleDir}`** (ordinal ignore case).

---

#### `private static string NormalizeModuleSlug(ModuleConfig module)`

**Purpose:** ASCII slug from **`Id`** or **`Name`**; hash fallback **`module-{8 hex}`** when empty after normalization.

---

#### `private static string GetProcessEnvironmentValue(ProcessStartInfo psi, string key)`

**Purpose:** From **`psi.Environment`** first, else **`Environment.GetEnvironmentVariable`**.

---

#### `private static string QuoteArgument(string argument)`

**Purpose:** Quotes when whitespace present; escapes embedded **`"`**.

---

#### `private static string JoinArguments(IEnumerable<string> arguments)`

**Purpose:** Space-joined **`QuoteArgument`** results.

---

#### `private static string GetRootDirectory()`

**Purpose:** Application root above deployed **`App`** folder.

---

## Related types (same file)

### `ModuleEnvironmentResolution` (record)

| Member | Description |
| --- | --- |
| `DirectoryPath` | Per-module environment folder |
| `ExecutablePath` | Resolved module command executable |
| `PackageManagerExecutablePath` | Package manager binary |
| `PackageManagerCommand` | Base install command line |
| `EnvironmentVariables` | Extra env vars for processes |

---

## Related

- [EngineInstaller](EngineInstaller/)
- [ModuleRunner](ModuleRunner/)
- [ModuleDownloadBridge](ModuleDownloadBridge/)
