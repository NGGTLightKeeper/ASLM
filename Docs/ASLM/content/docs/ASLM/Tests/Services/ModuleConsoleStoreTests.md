---
title: "ModuleConsoleStoreTests"
draft: false
---

## Class `ModuleConsoleStoreTests`

`ASLM/Tests/Services/ModuleConsoleStoreTests.cs` — overview lines, process sessions, and snapshots in [ModuleConsoleStore](../../Services/ModuleConsoleStore/).

**Helpers:** [ModuleConfigBuilder](../TestSupport/ModuleConfigBuilder/).

---

## Test methods

#### `public void AppendOverviewLine_appears_in_unified_overview()`

**Purpose:** Overview text appended for a module is visible in unified overview aggregation.

| Step | Action |
| --- | --- |
| 1 | `EnsureModule(module)` → `AppendOverviewLine(module, "hello overview")` |
| 2 | `GetUnifiedOverviewLines([module.SourcePath])` |
| 3 | Assert a line contains `"hello overview"` |

---

#### `public void StartProcessSession_and_AppendProcessLine_capture_output()`

**Purpose:** Starting a tracked process session and appending stdout/stderr lines updates snapshot and session text.

| Step | Action |
| --- | --- |
| 1 | `StartProcessSession` with current process, command `Install`, stage `Install` |
| 2 | `AppendProcessLine(handle, "Collecting package")` |
| 3 | `GetSnapshot()` — single module matching `SourcePath` |
| 4 | `GetSessionText` contains `"Collecting package"` |

---

#### `public void CompleteProcessSession_marks_session_not_running()`

**Purpose:** Completing a session records exit code and clears running flag.

| Step | Action |
| --- | --- |
| 1 | Start session with command `Run` |
| 2 | `CompleteProcessSession(handle, exitCode: 0)` |
| 3 | Find session in snapshot by `SessionId` |
| 4 | Assert `IsRunning == false`, `ExitCode == 0` |

---

## Related

- [ModuleConsoleStore](../../Services/ModuleConsoleStore/)
