---
title: "DownloadTransferSpeedEstimator"
draft: false
---

## Class `DownloadTransferSpeedEstimator`

`ASLM/Services/DownloadTransferSpeedEstimator.cs` — **`internal sealed`** — tracks per-operation byte deltas to produce a smoothed transfer speed for download notifications.

Used only by [NotificationCenter](NotificationCenter/) (`ReportDownloadProgress` → `ApplyDownloadTransferSample`).

---

### Constants

| Name | Value |
| --- | --- |
| `SmoothingAlpha` | `0.38` (EMA weight for instant speed) |

---

## Public methods

#### `public void Reset(string operationKey)`

**Purpose:** Clears all samples for one operation so the next report starts a fresh speed window.

| Step | Action |
| --- | --- |
| 1 | `lock (_gate)` |
| 2 | `_states.Remove(operationKey)` |

---

#### `public string? Sample(string operationKey, long downloadedBytes)`

**Purpose:** Records one progress sample and returns a formatted speed label when enough data exists.

Returns **
ull`** until the second sample; otherwise a non-empty speed string (or empty when rate ≤ 0).

| Step | Action |
| --- | --- |
| 1 | `lock (_gate)`; 
ow = UtcNow` |
| 2 | No prior state → store `SampleState`, return **
ull`** |
| 3 | `deltaBytes = downloadedBytes - LastBytes`; `deltaSeconds = now - LastAt` |
| 4 | `deltaBytes < 0` → reset state, return `FormatSpeed` (may be empty) |
| 5 | `deltaSeconds < 0.04` → return last smoothed speed (debounce) |
| 6 | `deltaBytes == 0` → update `LastAt`, return smoothed speed |
| 7 | `instant = deltaBytes / deltaSeconds`; EMA into `SmoothedBytesPerSecond` |
| 8 | Update `LastAt` / `LastBytes`; return `FormatSpeed` |

---

## Private methods

#### `private static string FormatSpeed(double bytesPerSecond)

**Purpose:** Formats one smoothed bytes-per-second estimate for notification detail text.

| Condition | Output |
| --- | --- |
| NaN, infinity, or ≤ 0 | `""` |
| ≥ 1 GiB/s | `{n:F1} GB/s` |
| ≥ 1 MiB/s | `{n:F1} MB/s` |
| ≥ 1024 B/s | `{n:F1} KB/s` |
| Else | `{n:F0} B/s` |

---

## Related types (same file)

### `SampleState` (private sealed class)

Stores the last sample used to smooth transfer speed for one download operation.

#### `public SampleState(DateTimeOffset lastAt, long lastBytes, double smoothedBytesPerSecond)

**Purpose:** Initializes `LastAt`, `LastBytes`, `SmoothedBytesPerSecond`.

| Property | Description |
| --- | --- |
| `LastAt` | Timestamp of last sample |
| `LastBytes` | Last reported byte count |
| `SmoothedBytesPerSecond` | EMA-smoothed rate |

---

## Related

- [NotificationCenter](NotificationCenter/)
