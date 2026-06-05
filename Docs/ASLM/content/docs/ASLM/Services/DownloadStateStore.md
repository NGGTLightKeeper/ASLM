---
title: "DownloadStateStore"
draft: false
---

## Class `DownloadStateStore`

`ASLM/Services/DownloadStateStore.cs` — **`public`** — persists shared download catalog installation state to **`Data/App/ASLM_Downloads.json`**. Models: [DownloadCatalogStateFile](../Models/DownloadCatalogStateFile/), [DownloadCatalogResourceState](../Models/DownloadCatalogResourceState/).

Thread-safe via `_lock`; lazy-loads on first access.

---

### Fields

| Name | Type | Description |
| --- | --- | --- |
| `_filePath` | `string` | `ASLM_Downloads.json` path |
| `_logger` | `ILogger<DownloadStateStore>` | Load/save errors |
| `_lock` | `object` | Serializes state access |
| `_jsonOptions` | `JsonSerializerOptions` | Indented; ignore nulls |
| `_state` | `DownloadCatalogStateFile?` | In-memory snapshot |

---

## Public methods

#### `public DownloadStateStore(ILogger<DownloadStateStore> logger)`

**Purpose:** Creates the state store and resolves the on-disk storage path.

**Steps:**

1. Store logger.
2. Set `_filePath` to `{GetRootDirectory()}/Data/App/ASLM_Downloads.json`.

---

#### `public DownloadCatalogResourceState? GetResourceState(string resourceKey)`

**Purpose:** Returns the persisted state for one resource.

**Steps:**

1. Return null if `resourceKey` is null/whitespace.
2. Under lock: **`EnsureLoaded()`** → lookup in `Resources` dictionary.

---

#### `public async Task MarkInstalledAsync(string resourceKey, string version, string providerModuleId)`

**Purpose:** Marks one resource as installed and persists the change.

**Steps:**

1. Return if `resourceKey` is null/whitespace.
2. Under lock: set entry with `Installed = true`, version, `LastInstalledUtc` (ISO `o`), `ProviderModuleId`.
3. Await **`SaveAsync()`**.

---

#### `public async Task MarkUninstalledAsync(string resourceKey)`

**Purpose:** Marks one resource as removed and persists the change.

**Steps:**

1. Return if `resourceKey` is null/whitespace.
2. Under lock: set entry with all fields cleared / `Installed = false`.
3. Await **`SaveAsync()`**.

---

## Private methods

#### `private DownloadCatalogStateFile EnsureLoaded()`

**Purpose:** Loads the persisted state on first access.

**Steps:**

1. Return `_state` if already loaded.
2. If file exists and JSON non-empty → deserialize, **`Normalize()`**, cache and return.
3. On failure → log error.
4. Create empty **`DownloadCatalogStateFile`**, normalize, cache, return.

---

#### `private async Task SaveAsync()`

**Purpose:** Persists the current state snapshot to disk.

**Steps:**

1. Under lock: snapshot via **`EnsureLoaded()`**.
2. Create parent directory if needed.
3. Serialize and **`WriteAllTextAsync`**; log errors without rethrowing.

---

#### `private static string GetRootDirectory()`

**Purpose:** Resolves the application root directory.

**Steps:**

1. Parent of `BaseDirectory`, or base dir if no parent.

---

## Related

- [DownloadCatalog](DownloadCatalog/)
- [DownloadInstaller](DownloadInstaller/)
