---
title: "ModuleConsoleStore"
draft: false
---

## Class `ModuleConsoleStore`

`ASLM/Services/ModuleConsoleStore.cs` — **`public sealed`** — in-memory console sessions for modules: overview lines, tracked processes, observed child processes, UI snapshots, unified log views.

Nested public types: **`ModuleConsoleModuleSnapshot`**, **`ModuleConsoleSessionSnapshot`**, **`ModuleConsoleSessionHandle`**, **`ObservedProcessInfo`**. Internal: **`ModuleConsoleModuleState`**, **`ModuleConsoleSessionState`**, **`ModuleConsoleLineState`**.

---

### Constants (private)

| Name | Value |
| --- | --- |
| `OverviewSessionId` | `overview` |
| `MaxLinesPerSession` | 5000 |
| `MaxDisplayedLines` | 250 |
| `MaxDisplayedCharacters` | 60000 |
| `MaxStoredLineLength` | 4000 |
| `MaxDisplayedLineLength` | 1200 |
| `StateChangedDebounceMilliseconds` | 75 |

---

### Events

| Event | When |
| --- | --- |
| `StateChanged` | Store mutated; debounced 75 ms on background task |

---

## Public methods

#### `public void EnsureModules(IEnumerable<ModuleConfig> modules)`

**Purpose:** Registers each module under lock; raises **`StateChanged`** if any new/changed metadata.

---

#### `public void EnsureModule(ModuleConfig module)`

**Purpose:** Single-module variant of **`EnsureModules`**.

---

#### `public void AppendOverviewLine(ModuleConfig module, string message)`

**Purpose:** Appends to module overview session; updates **`LastActivityUtc`**.

---

#### `public void AppendOverviewLine(string moduleSourcePath, string message)`

**Purpose:** Overview append by source path when module already registered.

---

#### `public void UpdateModuleEnabledState(string moduleSourcePath, bool isEnabled)`

**Purpose:** Updates cached enabled flag when it changed.

---

#### `public ModuleConsoleSessionHandle StartProcessSession(ModuleConfig module, ModuleCommand command, string stage, string commandLine, Process process, bool isTrackedProcess)`

**Purpose:** Creates session with new GUID id, metadata from command/process, optional initial command line line; returns **`ModuleConsoleSessionHandle`**.

---

#### `public void AppendProcessLine(ModuleConsoleSessionHandle handle, string message)`

**Purpose:** Appends line to resolved session.

---

#### `public void CompleteProcessSession(ModuleConsoleSessionHandle handle, int? exitCode)`

**Purpose:** Sets **`IsRunning = false`**, **`ExitCode`**, **`EndedUtc`**.

---

#### `public void SyncObservedProcesses(ModuleConfig module, ModuleConsoleSessionHandle ownerHandle, IReadOnlyList<ObservedProcessInfo> observedProcesses)`

**Purpose:** Creates/updates **`observed:{pid}`** sessions for child processes; marks missing PIDs ended with exit message when only placeholder line exists.

---

#### `public IReadOnlyList<ModuleConsoleModuleSnapshot> GetSnapshot()`

**Purpose:** Immutable snapshots ordered by module name; sessions ordered running first, overview first among idle, then by start time.

---

#### `public string GetSessionText(string moduleSourcePath, string sessionId)`

**Purpose:** **`string.Join`** of **`GetSessionLines`**.

---

#### `public IReadOnlyList<string> GetSessionLines(string moduleSourcePath, string sessionId)`

**Purpose:** Display-trimmed lines for one session; **`["No output yet."]`** when missing/empty.

---

#### `public IReadOnlyList<string> GetUnifiedModuleLines(string moduleSourcePath)`

**Purpose:** Merged overview + **`Stage == "Settings"`** sessions with timestamps and **`[module]`** prefixes.

---

#### `public IReadOnlyList<string> GetUnifiedOverviewLines(IReadOnlyCollection<string> moduleSourcePaths)`

**Purpose:** Cross-module unified lines for supplied source paths.

---

## Private methods

#### `private static IEnumerable<ModuleConsoleLineState> BuildUnifiedModuleEntries(ModuleConsoleModuleState module)`

**Purpose:** Yields prefixed lines from overview and settings sessions only.

---

#### `private bool EnsureModuleCore(ModuleConfig module)`

**Purpose:** Creates module state; updates **`Name`** / **`IsEnabled`** when changed; returns whether metadata changed.

---

#### `private ModuleConsoleSessionState GetOrCreateOverviewSessionCore(ModuleConsoleModuleState moduleState)`

**Purpose:** Lazy **`overview`** session with title **Overview**, stage **Lifecycle**.

---

#### `private static string GetFallbackTitle(ModuleCommand command, Process process)`

**Purpose:** **`command.Exec`** or **`process.StartInfo.FileName`**.

---

#### `private void AppendLineCore(ModuleConsoleSessionState sessionState, string message)`

**Purpose:** Stores line with monotonic **`Sequence`**, truncates at **`MaxStoredLineLength`**, trims buffer to **`MaxLinesPerSession`**.

---

#### `private static string TrimLineForDisplay(string line)`

**Purpose:** Truncates to **`MaxDisplayedLineLength`**.

---

#### `private static (List<ModuleConsoleLineState> Lines, int TotalCount) SelectLatestDisplayEntries(IEnumerable<ModuleConsoleLineState> entries)`

**Purpose:** Min-heap keeps newest **`MaxDisplayedLines`** by sequence.

---

#### `private static IReadOnlyList<string> BuildDisplayLines(IReadOnlyList<ModuleConsoleLineState> lines, int? totalLineCountOverride = null)`

**Purpose:** Applies line/character budgets; may insert trim banner.

---

#### `private bool TryGetSessionCore(ModuleConsoleSessionHandle handle, out ModuleConsoleModuleState moduleState, out ModuleConsoleSessionState sessionState)`

**Purpose:** Resolves handle under store lock.

---

#### `private static ModuleConsoleModuleSnapshot CreateModuleSnapshotCore(ModuleConsoleModuleState moduleState)`

**Purpose:** Projects sessions to **`ModuleConsoleSessionSnapshot`** ( **`Text`** empty in snapshot).

---

#### `private void RaiseStateChanged()`

**Purpose:** Debounced **`StateChanged`** via **`Interlocked`** gate + **`Task.Delay(75)`**.

---

## Related types (same file)

### `ModuleConsoleModuleSnapshot` (class)

`SourcePath`, `Name`, `IsEnabled`, `ActiveProcessCount`, `LastActivityUtc`, `Sessions`.

### `ModuleConsoleSessionSnapshot` (class)

`Id`, `Title`, `CommandDescription`, `Stage`, `CommandLine`, `ProcessId`, `IsRunning`, `IsTrackedProcess`, `IsObservedProcess`, `ExitCode`, `StartedUtc`, `EndedUtc`, `LineCount`, `Preview`, `Text`.

### `ModuleConsoleSessionHandle` (record struct)

`ModuleSourcePath`, `SessionId`.

### `ObservedProcessInfo` (class)

`ProcessId`, `ProcessName`.

### `ModuleConsoleModuleState` (internal class)

Mutable per-module dictionary of sessions.

### `ModuleConsoleSessionState` (internal class)

Mutable session lines and process metadata.

### `ModuleConsoleLineState` (internal class)

`Sequence`, `TimestampUtc`, `Text`.

---

## Related

- [ModuleRunner](ModuleRunner/)
- [ConsoleOutputView](ConsoleOutputView/)
- [ConsolesView](../Pages/ConsolesView/)
