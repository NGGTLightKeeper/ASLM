---
title: "ModuleTrustService"
draft: false
---

## Class `ModuleTrustService`

`ASLM/Services/ModuleTrustService.cs` — **`public sealed`** — resolves module trust levels from a built-in official catalog, shipped trust-source config, and a signed community-reviewed list (remote + verified cache).

Trust models: [ModuleTrustModels](../Models/ModuleTrustModels/). Identity matching: **`ModuleTrustIdentity`**.

---

### Constants

| Name | Value |
| --- | --- |
| `TrustSourceFileName` | `ASLM_ModuleTrustSource.json` |
| `ReviewedCacheFileName` | `ASLM_ReviewedModules.cache.json` |

---

### Static data

| Name | Description |
| --- | --- |
| `OfficialModules` | Built-in `OfficialModuleTrustEntry[]` (e.g. `aslm-chat` → `NGGTLightKeeper/ASLM-Chat`) |

---

### Instance fields

| Name | Description |
| --- | --- |
| `_httpClient` | 30s timeout; User-Agent `ASLM-ModuleTrust` |
| `_logger` | Warnings for trust refresh/verify failures |
| `_jsonOptions` | Indented + case-insensitive read |
| `_canonicalJsonOptions` | Compact JSON for signature body |
| `_sync` | Protects reviewed list + last refresh time |
| `_sourceConfig` | Loaded trust-source settings |
| `_reviewedModules` | Verified community-reviewed entries |
| `_lastRemoteRefresh` | UTC timestamp of last successful fetch |

---

## Public methods

#### `public ModuleTrustService(ILogger<ModuleTrustService> logger)`

**Purpose:** Creates the module trust service.

**Steps:**

1. Store logger; create **`HttpClient`** with timeout and User-Agent.

---

#### `public async Task InitializeAsync(CancellationToken ct = default)`

**Purpose:** Loads shipped configuration, restores the verified cache, and refreshes when configured.

**Steps:**

1. **`LoadTrustSourceConfigAsync`** → normalize into `_sourceConfig`.
2. If **`TryLoadVerifiedCache`** fails → clear `_reviewedModules` under lock; else copy cached modules.
3. Await **`RefreshReviewedListAsync(ct)`**.

---

#### `public ModuleTrustLevel Resolve(ModuleConfig config)`

**Purpose:** Resolves the trust level for one installed module manifest.

**Steps:**

1. Throw if `config` is null.
2. **`config.Normalize()`**.
3. **`TryMatchOfficial`** → **`Official`**.
4. **`TryMatchReviewed`** → **`CommunityReviewed`**.
5. Else **`Unreviewed`**.

---

#### `public async Task RefreshReviewedListAsync(CancellationToken ct = default)`

**Purpose:** Downloads and verifies the signed community-reviewed list when a URL is configured.

**Steps:**

1. Return if `ReviewedListUrl` empty.
2. Return if **`ShouldRefreshRemoteList`** is false.
3. Return (log warning) if `PublicKeyBase64` missing.
4. `GET` JSON; **`TryParseSignedPayload`** and **`TryVerifySignature`** — log and return on failure.
5. Filter modules with non-empty `Id` and `Repo`.
6. **`SaveVerifiedCacheAsync`**; under lock update `_reviewedModules` and `_lastRemoteRefresh`.
7. Rethrow **`OperationCanceledException`**; log other exceptions.

---

## Private methods

#### `private static bool TryMatchOfficial(ModuleConfig config)`

**Purpose:** Returns whether the manifest matches a built-in official module entry.

**Steps:**

1. Any `OfficialModules` entry where **`ModuleTrustIdentity.Matches(config, id, repo)`**.

---

#### `private bool TryMatchReviewed(ModuleConfig config)`

**Purpose:** Returns whether the manifest matches a cached community-reviewed entry.

**Steps:**

1. Snapshot `_reviewedModules` under lock.
2. Any entry where **`ModuleTrustIdentity.Matches`**.

---

#### `private async Task<ModuleTrustSourceConfig?> LoadTrustSourceConfigAsync(CancellationToken ct)`

**Purpose:** Loads shipped trust-source JSON from `Data/App` when present.

**Steps:**

1. Path: `{GetRootDirectory()}/Data/App/ASLM_ModuleTrustSource.json`.
2. Return null if missing; else deserialize, normalize, return.

---

#### `private string GetReviewedCachePath()`

**Purpose:** Returns the on-disk path for the verified community-reviewed module cache.

**Steps:**

1. `{GetRootDirectory()}/Data/App/ASLM_ReviewedModules.cache.json`.

---

#### `private bool TryLoadVerifiedCache(out List<ReviewedModuleTrustEntry> modules)`

**Purpose:** Restores the in-memory reviewed list from disk after signature verification.

**Steps:**

1. Initialize `modules` empty; return false if cache file missing.
2. Deserialize **`ReviewedModulesCacheDocument`**; require `Payload`.
3. Resolve signature from wrapper or payload; require `PublicKeyBase64` from `_sourceConfig`.
4. **`TryVerifySignature`** on payload; on success copy filtered modules with id+repo.
5. On any failure → log warning, return false.

---

#### `private async Task SaveVerifiedCacheAsync(SignedReviewedModulesPayload payload, string signature, CancellationToken ct)`

**Purpose:** Persists a verified signed payload and signature for offline startup.

**Steps:**

1. Build **`ReviewedModulesCacheDocument`** with `FetchedAt` (ISO `o`).
2. Create parent directory; serialize to cache path.

---

#### `private bool TryParseSignedPayload(string json, out SignedReviewedModulesPayload payload)`

**Purpose:** Parses and normalizes a signed community-reviewed list JSON document.

**Steps:**

1. Deserialize **`SignedReviewedModulesPayload`**; normalize.
2. Require `FileVersion == 1` and non-empty `Signature`.
3. On **`JsonException`** → log, return false.

---

#### `private bool TryVerifySignature(SignedReviewedModulesPayload payload, string publicKeyBase64)`

**Purpose:** Verifies RSA PKCS#1 v1.5 + SHA-256 over canonical unsigned JSON.

**Steps:**

1. Import subject public key from Base64; decode signature bytes.
2. Serialize **`payload.ToUnsignedBody()`** with `_canonicalJsonOptions` (UTF-8).
3. **`RSA.VerifyData`** with SHA-256 and PKCS#1 padding.
4. Log and return false on any exception.

---

#### `private bool ShouldRefreshRemoteList(ModuleTrustSourceConfig source)`

**Purpose:** Returns whether the configured refresh interval has elapsed since the last remote fetch.

**Steps:**

1. Return true if `_lastRemoteRefresh == MinValue`.
2. Compare `UtcNow - _lastRemoteRefresh` to `Max(1, RefreshIntervalHours)` hours.

---

#### `private static string GetRootDirectory()`

**Purpose:** Returns the application root directory above the deployed app folder.

**Steps:**

1. Parent of `BaseDirectory`, or base dir if no parent.

---

## Related

- [ModuleTrustModels](../Models/ModuleTrustModels/)
- [ModuleInstaller](ModuleInstaller/)
