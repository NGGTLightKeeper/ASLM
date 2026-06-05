---
title: "OllamaSettingsStore"
draft: false
---

## Class `OllamaSettingsStore`

`ASLM/Services/OllamaSettingsStore.cs` — **`public sealed`** — managed Ollama engine (**`ollama-service`**) account state: cached settings snapshot, **`ollama signin`/`signout`**, optional **`serve`** on port **11434**, **`/api/me`** probe.

**DI:** `AddSingleton<OllamaSettingsStore>()` — [EngineInstaller](EngineInstaller/).

Models: **`OllamaPersistentSettings`**, **`OllamaAccountActionResult`** ([Models](../Models/)).

Private type: **`ManagedRuntimeState`** record struct.

---

### Constants

| Name | Value |
| --- | --- |
| `ManagedOllamaPort` | 11434 |

---

## Public methods

#### `public OllamaSettingsStore(EngineInstaller engineInstaller, ILogger<OllamaSettingsStore> logger)`

**Purpose:** **`HttpClient`** timeout **2 s**.

---

#### `public OllamaPersistentSettings LoadSettings()`

**Purpose:** Cached snapshot without HTTP; clears sign-in cache when CLI executable missing.

---

#### `public async Task<OllamaPersistentSettings> RefreshSettingsAsync(CancellationToken ct = default)`

**Purpose:** **`EnsureManagedRuntimeAsync`** then **`TryRefreshSignInStateAsync`** when runtime available.

---

#### `public async Task<OllamaAccountActionResult> SignInAsync(CancellationToken ct = default)`

**Purpose:** Runs **`signin`**; on success refreshes sign-in; **`IsPendingVerification`** when not yet signed in per API.

---

#### `public async Task<OllamaAccountActionResult> SignOutAsync(CancellationToken ct = default)`

**Purpose:** Runs **`signout`**; refreshes state on success.

---

#### `public void StopManagedRuntime()`

**Purpose:** Kills and disposes owned **`serve`** process (entire tree), clears reference.

---

## Private methods

#### `private OllamaPersistentSettings BuildSettingsSnapshot(bool isCliAvailable)`

**Purpose:** **`IsSignedIn`** / **`UserName`** only when CLI available and verified.

---

#### `private async Task<ManagedRuntimeState> EnsureManagedRuntimeAsync(CancellationToken ct)`

**Purpose:** Resolves executable; probes **`api/version`**; starts **`serve`** with **`OLLAMA_HOST`**, **`OLLAMA_MODELS`** under **`Models/ollama-service`**; waits up to **25 s**.

---

#### `private async Task<bool?> TryRefreshSignInStateAsync(int port, CancellationToken ct)`

**Purpose:** POST **`api/me`**: **200** → signed in + username parse; **401** → signed out; other → **`null`** (unknown).

---

#### `private async Task<OllamaAccountActionResult> RunAccountCommandAsync(string command, string successFallbackMessage, ManagedRuntimeState runtime, CancellationToken ct)`

**Purpose:** Runs **`ollama {command}`** with managed env; captures stdout/stderr message.

---

#### `private static int ResolveManagedPort()`

**Purpose:** Returns **`ManagedOllamaPort`**.

---

#### `private string? ResolveExecutable()`

**Purpose:** **`EngineInstaller.GetEngineExecutablePath("ollama-service")`** when file exists.

---

#### `private static string GetManagedModelsDirectory()`

**Purpose:** **`{root}/Models/ollama-service`**.

---

#### `private async Task<bool> IsRuntimeAvailableAsync(int port, CancellationToken ct)`

**Purpose:** GET **`api/version`** → OK.

---

#### `private async Task<bool> WaitForRuntimeAsync(int port, CancellationToken ct)`

**Purpose:** 500 ms poll until deadline.

---

#### `private static string TryExtractUserName(string payload)`

**Purpose:** JSON root parse → **`TryExtractUserName(JsonElement)`**.

---

#### `private static string TryExtractUserName(JsonElement element)`

**Purpose:** Checks **`username`**, **`user`**, **`name`**, **`email`** (nested objects supported).

---

#### `private static OllamaAccountActionResult CreateCliUnavailableResult()`

**Purpose:** Message when engine not installed.

---

#### `private static async Task ReadStreamAsync(StreamReader reader, StringBuilder buffer, CancellationToken ct)`

**Purpose:** Line-append redirected output.

---

#### `private static string BuildCommandMessage(StringBuilder output, StringBuilder error)`

**Purpose:** Merged stdout/stderr trim.

---

#### `private static Uri BuildApiUri(int port, string relativePath)`

**Purpose:** **`http://127.0.0.1:{port}/{path}`**.

---

#### `private static string GetRootDirectory()`

**Purpose:** Application root above **`App`**.

---

## Related types (same file)

### `ManagedRuntimeState` (private record struct)

| Member | Description |
| --- | --- |
| `IsAvailable` | Runtime reachable |
| `ExecutablePath` | Ollama binary path |
| `Port` | HTTP port |

#### `public static ManagedRuntimeState Unavailable`

**Purpose:** **`(false, "", 0)`**.

---

## Related

- [EngineInstaller](EngineInstaller/)
- [SettingsView](../Pages/SettingsView/)
