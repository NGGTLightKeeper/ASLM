---
title: "ModuleStartThrottle"
draft: false
---

## Class `ModuleStartThrottle`

`ASLM/Services/ModuleStartThrottle.cs` — **`public sealed`** — limits concurrent module launch operations shared by the shell and the interop HTTP API.

**DI:** `AddSingleton<ModuleStartThrottle>()`.

---

### Constants

| Name | Value |
| --- | --- |
| `DefaultMaxConcurrentStarts` | `2` |

---

## Public methods

#### `public ModuleStartThrottle()`

**Purpose:** Creates the shared throttle with `SemaphoreSlim(DefaultMaxConcurrentStarts, DefaultMaxConcurrentStarts)`.

---

#### `public Task WaitAsync(CancellationToken cancellationToken = default)`

**Purpose:** Waits until a launch slot is available.

Delegates to `_semaphore.WaitAsync(cancellationToken)`.

---

#### `public void Release()`

**Purpose:** Releases one launch slot.

Delegates to `_semaphore.Release()`.

---

## Consumers

- [ModuleLaunchCoordinator](ModuleLaunchCoordinator/)
- [AslmModuleInteropServer](AslmModuleInteropServer/)
- [ModuleRunner](ModuleRunner/) (indirect)
