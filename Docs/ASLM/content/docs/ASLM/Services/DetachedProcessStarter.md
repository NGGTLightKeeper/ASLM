---
title: "DetachedProcessStarter"
draft: false
---

## Class `DetachedProcessStarter`

`ASLM/Services/DetachedProcessStarter.cs` — **`internal static`** — starts one process outside the current Windows Job Object when self-update restart must survive app shutdown.

Used by [SettingsService](SettingsService/) **`StartLauncherForSelfUpdate()`**.

---

### Constants

| Name | Value |
| --- | --- |
| `CreateBreakawayFromJob` | `0x01000000` (`CREATE_BREAKAWAY_FROM_JOB`) |

---

## Public methods

#### `public static bool TryStartBreakawayProcess(string fileName, string workingDirectory, IReadOnlyList<string> arguments)`

**Purpose:** Tries to start one process with **`CREATE_BREAKAWAY_FROM_JOB`** so it is not terminated with the current job group.

| Step | Action |
| --- | --- |
| 1 | Return **`false`** if not Windows, `fileName` empty, or file missing |
| 2 | Build `STARTUPINFO` (`cb` = struct size) |
| 3 | `BuildCommandLine(fileName, arguments)` → `StringBuilder` command line |
| 4 | `CreateProcess` (`lpApplicationName: null`, `bInheritHandles: false`, `dwCreationFlags: CreateBreakawayFromJob`, `lpCurrentDirectory: workingDirectory`) |
| 5 | On failure → **`false`** |
| 6 | `CloseHandle` thread + process handles → **`true`** |

---

## Private methods

#### `private static string BuildCommandLine(string fileName, IReadOnlyList<string> arguments)

**Purpose:** Builds a CreateProcess-compatible command line that preserves argument boundaries.

| Step | Action |
| --- | --- |
| 1 | Start list with `QuoteArgument(fileName)` |
| 2 | For each argument (if non-null list): append `QuoteArgument(argument ?? "")` |
| 3 | Return space-joined parts |

---

#### `private static string QuoteArgument(string value)

**Purpose:** Quotes one process argument using Windows command-line escaping rules.

| Case | Result |
| --- | --- |
| 
ull` or empty | `""` |
| No whitespace or `"` | Raw value |
| Otherwise | Wrap in `"`; double backslashes before `"`; escape trailing backslashes before closing `"` |

---

#### `private static extern bool CreateProcess(...)`

**Purpose:** Kernel32 P/Invoke (`kernel32.dll`, Unicode). Starts the detached process; output **`PROCESS_INFORMATION`**.

---

#### `private static extern bool CloseHandle(IntPtr hObject)`

**Purpose:** Kernel32 P/Invoke. Closes process/thread handles after successful start.

---

## Related types (same file)

### `STARTUPINFO` (private struct)

Win32 startup info passed to `CreateProcess` (`cb`, desktop, window geometry, std handles, etc.).

### `PROCESS_INFORMATION` (private struct)

| Field | Description |
| --- | --- |
| `hProcess` / `hThread` | Handles closed after start |
| `dwProcessId` / `dwThreadId` | New process identifiers |

---

## Related

- [SettingsService](SettingsService/)
