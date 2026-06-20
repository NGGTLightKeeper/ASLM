---
title: "GitHubUpdateClient"
draft: false
---

## Class `GitHubUpdateClient`

`ASLM/Services/GitHubUpdateClient.cs` — **`public sealed`** — GitHub REST API for ASLM update checks: releases, branches, file and zipball downloads. In-memory caches (5-minute TTL per repo). Manages rate limit metadata via `GitHubRateLimitStore`.

Branch results use [GitHubBranchInfo](../Models/UpdateModels/) from `ASLM/Models/UpdateModels.cs`.

Types in same file: **`CacheEntry<T>`**, **`GitHubReleaseInfo`**, **`GitHubReleaseAssetInfo`**, **`GitHubBranchPayload`**, **`GitHubBranchCommitPayload`**.

---

### Static constants

| Name | Value |
| --- | --- |
| `ReleaseCacheLifetime` | 5 minutes |
| `BranchCacheLifetime` | 5 minutes |

---

### Instance fields

| Name | Description |
| --- | --- |
| `_httpClient` | `HttpClient` with GitHub API headers |
| `_jsonOptions` | Case-insensitive deserialization |
| `_cacheGate` | `SemaphoreSlim(1)` for cache writes |
| `_releaseCache` | `Dictionary<string, CacheEntry<List<GitHubReleaseInfo>>>` |
| `_branchCache` | `Dictionary<string, CacheEntry<List<GitHubBranchInfo>>>` |

---

## Public methods

#### `public GitHubUpdateClient(GitHubRateLimitStore rateLimitStore)`

**Purpose:** Creates the GitHub client with headers required by the REST API.

**Steps:**

1. Stores `_rateLimitStore` dependency.
2. User-Agent: `ASLM-Updater`.
3. Accept: `application/vnd.github+json`.
4. `X-GitHub-Api-Version`: `2022-11-28`.

---

#### `public async Task RefreshRateLimitAsync(string requestSource = GitHubRequestSources.Auto, CancellationToken ct = default)`

**Purpose:** Refreshes the persisted GitHub rate-limit window from the dedicated API endpoint.

**Steps:**

1. Requests `https://api.github.com/rate_limit`.
2. Calls `ApplyRateLimitHeaders` and records the request.
3. Deserializes `GitHubRateLimitPayload` and updates `_rateLimitStore` from core limit metadata.

#### `public async Task<GitHubReleaseInfo?> GetLatestReleaseAsync(string repo, bool includePrerelease, string requestSource = GitHubRequestSources.Auto, CancellationToken ct = default)`

**Purpose:** Returns the most recent release allowed by the requested channel.

- **Parameters:**
  - `repo`: Repository name.
  - `includePrerelease`: Whether to include pre-releases.
  - `requestSource`: Source of request, auto or manual.
  - `ct`: Cancellation token.

**Steps:**

1. Await **`GetReleasesAsync`**.
2. Return **`FirstOrDefault()`** (newest after sort).

---

#### `public async Task<List<GitHubReleaseInfo>> GetReleasesAsync(string repo, bool includePrerelease, string requestSource = GitHubRequestSources.Auto, CancellationToken ct = default)`

**Purpose:** Returns the release list allowed by the requested channel.

- **Parameters:**
  - `repo`: Repository name.
  - `includePrerelease`: Whether to include pre-releases.
  - `requestSource`: Source of request, auto or manual.
  - `ct`: Cancellation token.

**Steps:**

1. Return empty list if `repo` is null/whitespace.
2. **`GetOrFetchCachedValueAsync`** on `_releaseCache` with **`FetchReleasesAsync`**.
3. Filter: not draft; prerelease only when `includePrerelease`.
4. Sort with **`ReleaseTagOrdering.CompareGitHubReleasesNewestFirst`**.

---

#### `public Task<List<GitHubBranchInfo>> GetBranchesAsync(string repo, string requestSource = GitHubRequestSources.Auto, CancellationToken ct = default)`

**Purpose:** Returns all repository branch names with their current commit SHA.

- **Parameters:**
  - `repo`: Repository name.
  - `requestSource`: Source of request, auto or manual.
  - `ct`: Cancellation token.

**Steps:**

1. Return empty list if `repo` is null/whitespace.
2. **`GetOrFetchCachedValueAsync`** on `_branchCache` with **`FetchBranchesAsync`**.

---

#### `public async Task DownloadFileAsync(string url, string destinationPath, IProgress<string>? log = null, IProgress<DownloadProgress>? downloadProgress = null, string requestSource = GitHubRequestSources.Auto, CancellationToken ct = default)`

**Purpose:** Downloads one URL to the requested file path with throttled progress.

- **Parameters:**
  - `url`: Target URL.
  - `destinationPath`: Output path.
  - `log`: Progress log.
  - `downloadProgress`: Download progress sink.
  - `requestSource`: Source of request, auto or manual.
  - `ct`: Cancellation token.

**Steps:**

1. Create destination parent directory; log URL.
2. `GET` with `ResponseHeadersRead`; **`EnsureSuccessStatusCode`**.
3. Stream to file (64 KiB buffer); report **`DownloadProgress`** at start, every ≥75 ms when length known, and at completion (100%).
4. Log `"Download complete."`.

---

#### `public Task DownloadRepositoryZipAsync(string repo, string reference, string destinationPath, IProgress<string>? log = null, IProgress<DownloadProgress>? downloadProgress = null, string requestSource = GitHubRequestSources.Auto, CancellationToken ct = default)`

**Purpose:** Downloads a repository ZIP archive for the requested ref.

- **Parameters:**
  - `repo`: Repository name.
  - `reference`: Branch or ref.
  - `destinationPath`: Output path.
  - `log`: Progress log.
  - `downloadProgress`: Download progress sink.
  - `requestSource`: Source of request, auto or manual.
  - `ct`: Cancellation token.

**Steps:**

1. Build URL via **`BuildApiUrl(repo, $"zipball/{Uri.EscapeDataString(reference)}")`**.
2. Delegate to **`DownloadFileAsync`**.

---

## Private methods

#### `private async Task<T> GetOrFetchCachedValueAsync<T>(Dictionary<string, CacheEntry<T>> cache, string cacheKey, TimeSpan lifetime, Func<CancellationToken, Task<T>> fetch, CancellationToken ct)`

**Purpose:** Returns a cached value when still fresh, otherwise fetches and stores a new one.

**Steps:**

1. If **`TryGetCachedValue`** → return cached.
2. Wait on `_cacheGate`; double-check cache.
3. Try `fetch(ct)` → store **`CacheEntry`** with `UtcNow + lifetime`.
4. On failure when **`TryGetAnyCachedValue`** has stale entry → return stale (rate-limit resilience).
5. Release gate in `finally`.

---

#### `private static bool TryGetCachedValue<T>(Dictionary<string, CacheEntry<T>> cache, string cacheKey, out T value)`

**Purpose:** Returns whether a cached value is still valid.

**Steps:**

1. If entry exists and `ExpiresAt > UtcNow` → return value, `true`.
2. Else `default!`, `false`.

---

#### `private static bool TryGetAnyCachedValue<T>(Dictionary<string, CacheEntry<T>> cache, string cacheKey, out T value)`

**Purpose:** Returns any cached value even when expired.

**Steps:**

1. If key exists → return entry value, `true`.
2. Else `default!`, `false`.

---

#### `private async Task<List<GitHubReleaseInfo>> FetchReleasesAsync(string repo, CancellationToken ct)`

**Purpose:** Loads the full release list from GitHub for the requested repository.

**Steps:**

1. `GET` stream `repos/{repo}/releases?per_page=100`.
2. Deserialize list or empty.

---

#### `private async Task<List<GitHubBranchInfo>> FetchBranchesAsync(string repo, CancellationToken ct)`

**Purpose:** Loads every branch name and commit SHA (paged).

**Steps:**

1. Page `branches?per_page=100&page={n}` until empty or &lt;100 results.
2. Map **`GitHubBranchPayload`** → **`GitHubBranchInfo`** (skip blank names).
3. Order by name (ignore case).

---

#### `private static string BuildApiUrl(string repo, string path)`

**Purpose:** Builds a GitHub REST API URL for the requested repository path.

**Steps:**

1. Return `https://api.github.com/repos/{trimmed repo}/{path}`.

---

## Related types (same file)

### `CacheEntry<T>` (private sealed record)

`Value`, `ExpiresAt` — one cached GitHub payload with expiration.

---

### `GitHubReleaseInfo` (public sealed class)

| JSON property | Member |
| --- | --- |
| `tag_name` | `TagName` |
| `name` | `Name` |
| `draft` | `Draft` |
| `prerelease` | `Prerelease` |
| `created_at` | `CreatedAt` |
| `published_at` | `PublishedAt` |
| `zipball_url` | `ZipballUrl` |
| `assets` | `List<GitHubReleaseAssetInfo>` |

---

### `GitHubReleaseAssetInfo` (public sealed class)

| JSON property | Member |
| --- | --- |
| `name` | `Name` |
| `browser_download_url` | `BrowserDownloadUrl` |
| `size` | `Size` |

---

### `GitHubBranchPayload` (internal sealed class)

`Name`, `Commit` (`GitHubBranchCommitPayload?`) — one branches API row.

---

### `GitHubBranchCommitPayload` (internal sealed class)

`Sha` — commit reference in branch payload.

---

## Related

- [UpdateManager](UpdateManager/)
- [ReleaseTagOrdering](ReleaseTagOrdering/)
- [GitHubBranchInfo](../Models/UpdateModels/)
