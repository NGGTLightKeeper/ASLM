---
title: "ModuleStartThrottleTests"
draft: false
---

## Class `ModuleStartThrottleTests`

`ASLM/Tests/Services/ModuleStartThrottleTests.cs` — concurrency cap and cancellation on [ModuleStartThrottle](../../Services/ModuleStartThrottle/).

---

## Test methods

#### `public async Task WaitAsync_limits_concurrent_acquires_to_default_max()`

**Purpose:** At most `DefaultMaxConcurrentStarts` workers hold the throttle simultaneously.

| Step | Action |
| --- | --- |
| 1 | Start 6 `Task.Run` workers; each `WaitAsync`, increment `acquired`, track `maxObserved`, delay 50 ms, decrement, `Release` |
| 2 | `await Task.WhenAll(workers)` |
| 3 | Assert `maxObserved <= ModuleStartThrottle.DefaultMaxConcurrentStarts` |

---

#### `public async Task WaitAsync_respects_cancellation()`

**Purpose:** Third `WaitAsync` with a cancelled token throws `TaskCanceledException` when slots are exhausted.

| Step | Action |
| --- | --- |
| 1 | `WaitAsync()` twice (fill default slots) |
| 2 | Cancel `CancellationTokenSource` |
| 3 | `WaitAsync(cts.Token)` → assert `ThrowAsync<TaskCanceledException>` |
| 4 | `Release()` twice to restore throttle |

---

## Related

- [ModuleStartThrottle](../../Services/ModuleStartThrottle/)
