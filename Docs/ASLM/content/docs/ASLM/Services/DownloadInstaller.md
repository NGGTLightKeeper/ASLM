---
title: "DownloadInstaller"
draft: false
---

## Class `DownloadInstaller`

`ASLM/Services/DownloadInstaller.cs` — **`public`** — resolves bridge install/uninstall manifests and runs whitelisted actions in ASLM-managed directories.

**DI:** [ModuleInstaller](ModuleInstaller/), [ModuleDownloadBridge](ModuleDownloadBridge/), [EngineInstaller](EngineInstaller/), [ModuleEnvironmentResolver](ModuleEnvironmentResolver/), [DownloadStateStore](DownloadStateStore/), [NotificationCenter](NotificationCenter/).

Supported manifest actions: `download_file`, `extract_zip`, `python_package`, `ollama_pull`, `ollama_remove`.

---

## Public methods

#### `public DownloadInstaller(ModuleInstaller moduleInstaller, ModuleDownloadBridge bridge, EngineInstaller engineInstaller, ModuleEnvironmentResolver environmentResolver, DownloadStateStore stateStore, NotificationCenter notifications, ILogger<DownloadInstaller> logger)`

**Purpose:** Stores collaborators for manifest resolution and execution.

---

#### `public async Task<DownloadInstallResult> InstallAsync(DownloadCatalogItem item, DownloadCatalogVariant? selectedVariant, IProgress<string>? log = null, CancellationToken ct = default)`

**Purpose:** Picks variant resource key (selected → default → item). Tries each **`item.Sources`**: load module, **`ResolveInstallAsync`**, **`ExecuteManifestAsync`**, **`MarkInstalledAsync`**. Notification via **`NotificationCenter`**. Returns first success or last error.

---

#### `public async Task<DownloadInstallResult> UninstallAsync(DownloadCatalogItem item, DownloadCatalogVariant? selectedVariant, IProgress<string>? log = null, CancellationToken ct = default)`

**Purpose:** Same source iteration with **`ResolveUninstallAsync`**, **`MarkUninstalledAsync`** on success.

---

## Private methods — manifest execution

#### `private async Task ExecuteManifestAsync(ModuleConfig module, ModuleDownloadInstallManifest manifest, string operationKey, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Creates **`InstallExecutionContext`**, runs each action in order, **`TryCleanup`** in **`finally`**.

---

#### `private async Task ExecuteDownloadFileAsync(...)`

**Purpose:** Downloads **`action.Url`** to managed target (**`targetRef`** + **`relativePath`**) or temp artifact (**`artifactId`**). Optional SHA-256. Registers artifact path in context.

---

#### `private void ExecuteExtractZip(...)`

**Purpose:** Extracts **`sourceArtifactId`** zip into resolved target with zip-slip guard.

---

#### `private async Task ExecutePythonPackageAsync(...)`

**Purpose:** Runs engine package manager install for **`action.Packages`** via **`ModuleEnvironmentResolver`**.

---

#### `private async Task ExecuteOllamaPullAsync(...)`

**Purpose:** Starts temporary **`ollama serve`** on ephemeral port with **`OLLAMA_MODELS`** → **`WaitForOllamaAsync`** → **`StreamOllamaPullAsync`**.

---

#### `private async Task ExecuteOllamaRemoveAsync(...)`

**Purpose:** Same temporary server → **`DeleteOllamaModelAsync`**.

---

#### `private async Task WaitForOllamaAsync(int port, CancellationToken ct)`

**Purpose:** Polls **`/api/tags`** up to 25 s.

---

#### `private async Task StreamOllamaPullAsync(int port, string modelName, string operationKey, IProgress<string>? log, CancellationToken ct)`

**Purpose:** POST **`/api/pull`** with streaming JSON lines; forwards progress to log and notifications.

---

#### `private static string TruncateForNotificationStatus(string status)`

**Purpose:** Caps status text at 140 characters for notification UI.

---

#### `private async Task DeleteOllamaModelAsync(int port, string modelName, CancellationToken ct)`

**Purpose:** DELETE **`/api/delete`**.

---

## Private methods — processes and targets

#### `private static async Task RunProcessAsync(string fileName, string arguments, string workingDirectory, IProgress<string>? log, CancellationToken ct, IDictionary<string, string?>? environment = null)`

**Purpose:** Streams stdout/stderr; throws on non-zero exit. **`ConfigurePythonProcess`** applied.

---

#### `private static string GetProcessEnvironmentValue(ProcessStartInfo psi, string key)`

**Purpose:** Reads env from PSI or OS (case-insensitive).

---

#### `private static string GetEffectiveTargetRef(ModuleDownloadInstallManifest manifest, ModuleDownloadInstallAction action)`

**Purpose:** **`action.TargetRef`** or manifest default.

---

#### `private string ResolveTargetDirectory(ModuleConfig module, string targetRef)`

**Purpose:** Maps bridge target **`root`** bucket (`root`, `data`, `models`, `tools`, `modules`, `engines`) + **`relative`** under app root.

---

#### `private static string ResolveChildPath(string rootPath, string relativePath)`

**Purpose:** Full path under root; rejects absolute relative paths and traversal.

---

#### `private static string EnsureTrailingSeparator(string path)`

**Purpose:** Trailing directory separator for prefix checks.

---

#### `private static string SanitizeFileName(string name)`

**Purpose:** Invalid filename chars → `_`; empty → GUID.

---

#### `private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)`

**Purpose:** Lowercase hex SHA-256.

---

#### `private static int AllocateLocalPort()`

**Purpose:** Binds **`TcpListener`** to loopback port 0.

---

#### `private static void TryStopProcess(Process process)`

**Purpose:** **`Kill(true)`** best-effort.

---

#### `private static void ConfigurePythonProcess(ProcessStartInfo psi, string fileName)`

**Purpose:** Adds **`-u`** and UTF-8 env vars for Python executables.

---

#### `private static string GetRootDirectory()`

**Purpose:** Parent of app base directory.

---

## Nested class `InstallExecutionContext`

#### `public InstallExecutionContext(string rootDirectory)`

**Purpose:** **`TempDir`** = `%TEMP%/ASLM_Downloads/{guid}`; empty **`Artifacts`** dictionary.

---

#### `public void TryCleanup()`

**Purpose:** Best-effort recursive delete of **`TempDir`**.

---

## Related

- [DownloadCatalog](DownloadCatalog/)
- [DownloadStateStore](DownloadStateStore/)
- [EngineInstaller](EngineInstaller/)
