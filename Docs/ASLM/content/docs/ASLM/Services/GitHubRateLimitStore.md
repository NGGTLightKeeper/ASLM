---
title: "GitHubRateLimitStore"
draft: false
---

## Class `GitHubRateLimitStore`

`ASLM/Services/GitHubRateLimitStore.cs` — **`public sealed`** — Loads and saves GitHub API usage in `Data/App/ASLM_GitHubRateLimit.json`. This service helps manage rate limit budgeting across application restarts.

**Example Usage:**

```csharp
// Store is typically injected via DI
public class UpdateService
{
    private readonly GitHubRateLimitStore _rateLimitStore;

    public UpdateService(GitHubRateLimitStore rateLimitStore)
    {
        _rateLimitStore = rateLimitStore;
    }

    public async Task CheckUpdatesAsync()
    {
        if (_rateLimitStore.CanMakeAutoRequest())
        {
            // Proceed with automatic update check
            _rateLimitStore.RecordRequest("https://api.github.com/...", GitHubRequestTypes.Releases, GitHubRequestSources.Auto, 200);
        }
        else
        {
            var delay = _rateLimitStore.CalculateInterCheckDelay();
            // Wait before next check
        }
    }
}
```

---

### Constructor

#### `public GitHubRateLimitStore(ILogger<GitHubRateLimitStore> logger)`

**Purpose:** Creates the store and resolves the persisted data file path.

- **Parameters:** `logger` - Logger instance for dependency injection.

---

### Properties

| Name | Type | Description |
| --- | --- | --- |
| `Data` | `GitHubRateLimitData` | Gets the current persisted GitHub rate-limit state. |

---

### Methods

#### `public Task InitializeAsync()`

**Purpose:** Initializes the store by loading persisted data once at startup.

#### `public Task LoadAsync()`

**Purpose:** Loads persisted GitHub usage data or recreates defaults when the file is missing or invalid.

#### `public void UpdateFromHeaders(int limit, int remaining, long resetEpochSeconds)`

**Purpose:** Updates the known GitHub rate-limit window from response headers and saves the data.

- **Parameters:**
  - `limit` - The maximum requests allowed in the current window.
  - `remaining` - The requests remaining in the current window.
  - `resetEpochSeconds` - Unix epoch timestamp when the current window resets.

#### `public void RecordRequest(string url, string type, string source, int? statusCode)`

**Purpose:** Records one GitHub API request and persists the updated history.

- **Parameters:**
  - `url` - The requested URL.
  - `type` - The request type (e.g., releases, download).
  - `source` - The request source (e.g., auto, manual).
  - `statusCode` - The HTTP response status code.

#### `public bool CanMakeAutoRequest()`

**Purpose:** Returns whether automatic update checks still have budget in the current window.

- **Returns:** `true` if the count of automatic requests is less than half the known limit.

#### `public int GetAutoRequestsRemaining()`

**Purpose:** Returns how many automatic requests remain in the current window budget (half the total limit).

#### `public TimeSpan GetDelayUntilReset()`

**Purpose:** Returns the remaining time until the GitHub rate-limit window resets.

#### `public TimeSpan CalculateInterCheckDelay()`

**Purpose:** Calculates the delay before the next automatic update check request based on the remaining budget and time until reset. Returns a timespan between 5 seconds and 30 minutes.

#### `public Task SaveAsync()`

**Purpose:** Saves the current GitHub usage data asynchronously to disk.
