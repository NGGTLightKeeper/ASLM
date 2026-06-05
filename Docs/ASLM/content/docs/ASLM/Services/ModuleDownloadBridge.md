---
title: "ModuleDownloadBridge"
draft: false
---

## Class `ModuleDownloadBridge`

`ASLM/Services/ModuleDownloadBridge.cs` — **`public`** — runs module **`downloadsBridge`** processes, sends JSON requests on stdin, parses JSON from stdout (max **2_000_000** chars per stream).

**DI:** `AddSingleton<ModuleDownloadBridge>()` — [EngineInstaller](EngineInstaller/), [ModuleEnvironmentResolver](ModuleEnvironmentResolver/), [ModuleRunner](ModuleRunner/).

Bridge models: [Models/ModuleDownloadsBridge](../Models/) (request/response payloads).

---

### Constants

| Name | Value |
| --- | --- |
| `MaxBridgeOutputCharacters` | 2_000_000 |

---

## Public methods

#### `public ModuleDownloadBridge(EngineInstaller engineInstaller, ModuleEnvironmentResolver environmentResolver, ModuleRunner moduleRunner, ILogger<ModuleDownloadBridge> logger)`

**Purpose:** Stores dependencies and case-insensitive JSON options.

---

#### `public async Task<List<ModuleDownloadCategoryPayload>> GetCategoriesAsync(ModuleConfig module, bool preferCached = false, bool forceRefresh = false, CancellationToken ct = default)`

**Purpose:** When bridge not configured → **`[]`**. Without **`list_categories`** operation → static **`bridge.Categories`**. Else **`InvokeAsync`** with operation **`list_categories`**; throws on **`!Success`**.

---

#### `public async Task<ModuleDownloadBridgeResponse> GetItemsAsync(ModuleConfig module, string categoryId, string? queryText = null, IReadOnlyCollection<string>? filters = null, bool preferCached = false, bool forceRefresh = false, CancellationToken ct = default)`

**Purpose:** **`list_items`** via **`InvokeAsync`**; empty response when operation unsupported.

---

#### `public async Task<ModuleDownloadItemDetailPayload?> GetItemDetailAsync(ModuleConfig module, string categoryId, string resourceKey, bool preferCached = false, bool forceRefresh = false, CancellationToken ct = default)`

**Purpose:** **`describe_item`**; **`null`** when unsupported.

---

#### `public async Task<ModuleDownloadInstallManifest?> ResolveInstallAsync(ModuleConfig module, string categoryId, string resourceKey, CancellationToken ct = default)`

**Purpose:** **`resolve_install`** with **`ForceRefresh = true`**.

---

#### `public async Task<ModuleDownloadInstallManifest?> ResolveUninstallAsync(ModuleConfig module, string categoryId, string resourceKey, CancellationToken ct = default)`

**Purpose:** **`resolve_uninstall`** with **`ForceRefresh = true`**.

---

#### `public async Task<ModuleDownloadBridgeResponse> InvokeAsync(ModuleConfig module, ModuleDownloadBridgeRequest request, CancellationToken ct = default)`

**Purpose:** Core RPC: **`request.Normalize()`**, ensure engine env when **`bridge.Engine`** set, start process (**`CreateProcessStartInfo`**), write JSON to stdin, read bounded stdout/stderr, extract JSON object, deserialize **`ModuleDownloadBridgeResponse`**, **`Normalize()`**. Returns error response on failure (logged).

Supported operations when **`bridge.Operations`** empty: all; else case-insensitive match required.

---

## Private methods

#### `private static bool SupportsOperation(ModuleDownloadsBridge bridge, string operation)`

**Purpose:** Empty **`Operations`** → all supported.

---

#### `private static async Task<string> ReadBoundedToEndAsync(StreamReader reader, CancellationToken ct)`

**Purpose:** 8 KiB char buffer; appends **`[output truncated]`** when over cap.

---

#### `private ProcessStartInfo CreateProcessStartInfo(ModuleConfig module, ModuleDownloadsBridge bridge, string moduleDir)`

**Purpose:** Engine-backed: resolved executable + placeholder-expanded **`EntryPoint`** args. Raw entry: **`SplitCommand`**. Sets working dir, stdio redirect, **`InjectModuleEnvironment`**, engine env vars, **`ConfigurePythonProcess`**.

---

#### `private string ResolveCommandPlaceholders(ModuleConfig module, string command)`

**Purpose:** Replaces **`{settingKey}`** with **`ModuleRunner.GetResolvedSettingValue`** display strings.

---

#### `private void InjectModuleEnvironment(ModuleConfig module, string moduleDir, ProcessStartInfo psi)`

**Purpose:** **`ASLM_{KEY}`** per setting, **`ASLM_MODULE_ID`**, **`ASLM_MODULE_DIR`**.

---

#### `private static void ConfigurePythonProcess(ProcessStartInfo psi, string fileName, string? engineId)`

**Purpose:** Adds **`-u`**, sets **`PYTHONUNBUFFERED`**, **`PYTHONIOENCODING`**, **`PYTHONUTF8`** for Python engines/executables.

---

#### `private static bool IsPythonProcess(string fileName, string? engineId)`

**Purpose:** Engine id or executable name hints Python.

---

#### `private static bool HasPythonUnbufferedFlag(string arguments)`

**Purpose:** Token **`-u`** in parsed arguments.

---

#### `private static string ExtractJsonPayload(string output)`

**Purpose:** First **`{`** through last **`}`** substring.

---

#### `private static List<string> SplitCommand(string command)`

**Purpose:** Quote-aware command tokenizer.

---

#### `private static string JoinArguments(IEnumerable<string> arguments)`

**Purpose:** Quotes args containing spaces.

---

#### `private static ModuleDownloadBridgeResponse CreateErrorResponse(string error)`

**Purpose:** **`Success = false`**, trimmed **`Error`** message.

---

## Related

- [DownloadCatalog](DownloadCatalog/)
- [DownloadInstaller](DownloadInstaller/)
- [ModuleEnvironmentResolver](ModuleEnvironmentResolver/)
