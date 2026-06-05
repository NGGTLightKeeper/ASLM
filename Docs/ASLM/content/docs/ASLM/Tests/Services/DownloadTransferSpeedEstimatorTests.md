---
title: "DownloadTransferSpeedEstimatorTests"
draft: false
---

## Class `DownloadTransferSpeedEstimatorTests`

`ASLM/Tests/Services/DownloadTransferSpeedEstimatorTests.cs` — byte-delta sampling and human-readable speed labels in [DownloadTransferSpeedEstimator](../../Services/DownloadTransferSpeedEstimator/).

---

## Test methods

#### `public void Sample_returns_null_until_second_sample()`

**Purpose:** First sample per operation id has no prior delta; second sample yields a speed label.

| Step | Action |
| --- | --- |
| 1 | `Sample("op-1", 0)` → assert `null` |
| 2 | `Sample("op-1", 1024)` → assert not `null` |

---

#### `public void Reset_clears_state_for_operation()`

**Purpose:** `Reset` drops prior samples so the next call behaves like a fresh operation.

| Step | Action |
| --- | --- |
| 1 | Two samples on `"op-1"` |
| 2 | `Reset("op-1")` |
| 3 | `Sample("op-1", 100)` → assert `null` |

---

#### `public void Sample_formats_kilobytes_per_second()`

**Purpose:** After a short delay, reported label matches `N B|KB|MB|GB/s` pattern.

| Step | Action |
| --- | --- |
| 1 | Unique key; `Sample(key, 0)` |
| 2 | `Thread.Sleep(120)`; `Sample(key, 256 * 1024)` |
| 3 | Assert non-whitespace label matches speed regex |
| 4 | Assert elapsed < 2 seconds |

---

#### `public void Sample_handles_negative_byte_delta()`

**Purpose:** Regressions (bytes counter going backward) still produce a label without throwing.

| Step | Action |
| --- | --- |
| 1 | `Sample(key, 10_000)` then `Sample(key, 100)` |
| 2 | Assert label not `null` |

---

## Related

- [DownloadTransferSpeedEstimator](../../Services/DownloadTransferSpeedEstimator/)
