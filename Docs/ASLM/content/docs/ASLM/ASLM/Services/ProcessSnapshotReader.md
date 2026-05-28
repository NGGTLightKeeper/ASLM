---
title: "ProcessSnapshotReader"
draft: false
---

## Overview

`ASLM/Services/ProcessSnapshotReader.cs` — captures and briefly caches the OS process table for dashboard and runner diagnostics.

Types in same file: **`ProcessSnapshotReader`**, **`ProcessSnapshotEntry`**.

---

## Class `ProcessSnapshotReader`

**`public sealed`** — default cache max age **250 ms**.

---

### Instance fields

| Name | Description |
| --- | --- |
| `DefaultMaxAge` | `250` ms (static) |
| `_cacheLock` | Sync for snapshot cache |
| `_cachedSnapshot` | Last captured entries |
| `_cachedSnapshotUtc` | Timestamp of last capture |

---

## Public methods

#### `public IReadOnlyList<ProcessSnapshotEntry> GetSnapshot(TimeSpan? maxAge = null)`

**Purpose:** Returns a cached process snapshot when still fresh; otherwise captures a new one.

**Steps:**

1. Use `maxAge ?? DefaultMaxAge`.
2. Under lock: if cache non-empty and age ≤ max age → return cached list.
3. **`CaptureSnapshot()`**, update cache + UTC time, return.

---

## Private methods

#### `private static IReadOnlyList<ProcessSnapshotEntry> CaptureSnapshot()`

**Purpose:** Captures the current OS process table, or an empty list on non-Windows platforms.

**Steps (Windows):**

1. **`CreateToolhelp32Snapshot(Th32csSnapProcess, 0)`**; return empty if invalid handle.
2. Initialize **`ProcessEntry32`** with `dwSize`.
3. Loop **`Process32First`** / **`Process32Next`** → add **`ProcessSnapshotEntry`** (pid, parent pid, exe name).
4. **`CloseHandle`** in `finally`.

**Steps (non-Windows):** Return empty list.

---

## Private Win32 interop (same file)

| Member | Role |
| --- | --- |
| `Th32csSnapProcess` | `0x00000002` snapshot flag |
| `InvalidHandleValue` | `(IntPtr)(-1)` |
| `ProcessEntry32` | Toolhelp process entry struct (`szExeFile` 260 chars) |
| `CreateToolhelp32Snapshot` | Create snapshot handle |
| `Process32First` / `Process32Next` | Enumerate entries |
| `CloseHandle` | Release snapshot handle |

---

## Related types (same file)

### `ProcessSnapshotEntry` (public sealed record)

`ProcessId`, `ParentProcessId`, `ExecutableName` — one raw process-table row with parent-child data.

---

## Related

- [ModuleRunner](ModuleRunner/)
- [ProcessTracker](ProcessTracker/)
