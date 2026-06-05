---
title: "ProcessTracker"
draft: false
---

## Class `ProcessTracker`

`ASLM/Services/ProcessTracker.cs` — **`public sealed`** — groups ASLM and child processes into one Windows Job Object (`KILL_ON_JOB_CLOSE` + `BREAKAWAY_OK`). No-op on non-Windows.

Implements **`IDisposable`**.

Nested types in same file: **`JobObjectInfoType`**, **`JOBOBJECTLIMIT`**, **`JOBOBJECT_BASIC_LIMIT_INFORMATION`**, **`IO_COUNTERS`**, **`JOBOBJECT_EXTENDED_LIMIT_INFORMATION`**.

---

### Instance fields

| Name | Description |
| --- | --- |
| `_jobHandle` | `SafeFileHandle?` for `"ASLM_ProcessGroup"` job |
| `_logger` | Initialization and assignment warnings |
| `_disposed` | Disposal guard |

---

## Public methods

#### `public ProcessTracker(ILogger<ProcessTracker> logger)`

**Purpose:** Creates the Windows Job Object and assigns the current process to it.

**Steps:**

1. On non-Windows → log warning, return without handle.
2. **`CreateJobObject`** named `ASLM_ProcessGroup`; log error if invalid.
3. Build **`JOBOBJECT_EXTENDED_LIMIT_INFORMATION`** with kill-on-close and breakaway OK.
4. Marshal struct → **`SetInformationJobObject`** (`ExtendedLimitInformation`).
5. **`AssignProcessToJobObject`** for current process; log success or warning.

---

#### `public bool AddProcess(Process process)`

**Purpose:** Adds one child process to the shared Job Object.

**Steps:**

1. Return false if disposed or handle invalid/null.
2. **`AssignProcessToJobObject`**; log Win32 error on failure.
3. Return result; catch exceptions → log, return false.

---

#### `public void Dispose()`

**Purpose:** Releases the job handle.

**Steps:**

1. Return if already disposed.
2. Set `_disposed`, dispose `_jobHandle`.

---

## Private Win32 interop (same file)

#### `private static extern SafeFileHandle CreateJobObject(IntPtr lpJobAttributes, string lpName)`

**Purpose:** Creates a named job object (`kernel32.dll`).

---

#### `private static extern bool SetInformationJobObject(SafeFileHandle hJob, JobObjectInfoType infoType, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength)`

**Purpose:** Applies extended limit information to the job.

---

#### `private static extern bool AssignProcessToJobObject(SafeFileHandle hJob, IntPtr hProcess)`

**Purpose:** Associates a process handle with the job.

---

## Related types (same file)

### `JobObjectInfoType` (private enum)

| Value | Numeric |
| --- | --- |
| `ExtendedLimitInformation` | 9 |

---

### `JOBOBJECTLIMIT` (private flags enum)

| Flag | Hex |
| --- | --- |
| `JOB_OBJECT_LIMIT_BREAKAWAY_OK` | `0x800` |
| `JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE` | `0x2000` |

---

### `JOBOBJECT_BASIC_LIMIT_INFORMATION` (private struct)

Win32 layout: per-process/job time limits, `LimitFlags`, working set, active process limit, affinity, priority, scheduling class.

---

### `IO_COUNTERS` (private struct)

Read/write/other operation and transfer counts.

---

### `JOBOBJECT_EXTENDED_LIMIT_INFORMATION` (private struct)

`BasicLimitInformation`, `IoInfo`, process/job memory limits and peak usage fields.

---

## Related

- [DetachedProcessStarter](DetachedProcessStarter/)
- [ModuleRunner](ModuleRunner/)
