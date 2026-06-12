---
title: "GitHubRateLimitModels"
draft: false
---

## Classes in `ASLM/Models/GitHubRateLimitModels.cs`

This file contains models used for storing and persisting GitHub API rate limit data and request records.

---

## Static class `GitHubRequestSources`

**Purpose:** Known GitHub request source values persisted with each API call record.

| Constant | Value |
| --- | --- |
| `Auto` | `"auto"` |
| `Manual` | `"manual"` |

---

## Static class `GitHubRequestTypes`

**Purpose:** Known GitHub request type values persisted with each API call record.

| Constant | Value |
| --- | --- |
| `Releases` | `"releases"` |
| `Branches` | `"branches"` |
| `Download` | `"download"` |
| `RateLimit` | `"rate_limit"` |

---

## Class `GitHubRateLimitData`

**Purpose:** Stores persisted GitHub API usage for rate-limit budgeting across restarts. **`public sealed`**.

**Example Usage:**

```csharp
var data = new GitHubRateLimitData();
data.KnownLimit = 60;
data.KnownRemaining = 59;
data.ResetUtc = DateTime.UtcNow.AddHours(1).ToString("o");
data.Normalize();
```

### Properties

| Name | Type | Description |
| --- | --- | --- |
| `FileVersion` | `int` | Version of the file format, defaults to `1`. |
| `KnownLimit` | `int` | The most recently known rate limit, defaults to `60`. |
| `KnownRemaining` | `int` | Remaining requests in the current window, defaults to `60`. |
| `ResetUtc` | `string?` | ISO 8601 UTC timestamp of the next window reset. |
| `Requests` | `List<GitHubRequestRecord>` | Historical request records. |

### Methods

#### `public void Normalize()`

**Purpose:** Restores safe defaults and trims request history to the active rate-limit window. Clamps `KnownLimit` and `KnownRemaining`, prunes old requests, and bounds the history size.

#### `public void PruneRequestsOutsideCurrentWindow()`

**Purpose:** Removes request records that fall outside the current GitHub rate-limit window (resolved via `ResolveWindowStartUtc`).

#### `public void TrimRequestHistory()`

**Purpose:** Keeps the request history bounded to the configured GitHub rate limit (`KnownLimit`) by removing the oldest records.

#### `public DateTimeOffset ResolveWindowStartUtc()`

**Purpose:** Returns the UTC timestamp when the active GitHub rate-limit window started. Typically 1 hour before the `ResetUtc` timestamp.

---

## Class `GitHubRequestRecord`

**Purpose:** Describes one GitHub API request recorded for rate-limit planning. **`public sealed`**.

**Example Usage:**

```csharp
var record = new GitHubRequestRecord
{
    Url = "https://api.github.com/repos/test/releases",
    Type = GitHubRequestTypes.Releases,
    Source = GitHubRequestSources.Auto,
    StatusCode = 200
};
record.Normalize();
bool isAuto = record.IsAutoRequest();
```

### Properties

| Name | Type | Description |
| --- | --- | --- |
| `TimestampUtc` | `string` | ISO 8601 UTC timestamp of the request. |
| `Url` | `string` | URL requested. |
| `Type` | `string` | Request type (e.g., releases, branches, download). |
| `Source` | `string` | Source of the request (e.g., auto, manual). |
| `StatusCode` | `int?` | HTTP status code returned. |

### Methods

#### `public void Normalize()`

**Purpose:** Restores safe defaults after JSON deserialization. Trims strings and ensures valid values for timestamp, type, and source.

#### `public bool IsWithinWindow(DateTimeOffset windowStartUtc)`

**Purpose:** Returns whether this record belongs to the active GitHub rate-limit window.

- **Parameters:** `windowStartUtc` - the start timestamp of the window.

- **Returns:** `true` if `TimestampUtc` is after or equal to `windowStartUtc`.

#### `public bool IsAutoRequest()`

**Purpose:** Returns whether this record was initiated by automatic update checks.

- **Returns:** `true` if `Source` equals `GitHubRequestSources.Auto`.
