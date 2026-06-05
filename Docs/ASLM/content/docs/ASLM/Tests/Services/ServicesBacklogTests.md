---
title: "ServicesBacklogTests"
draft: false
---

## Class `ServicesBacklogTests`

`ASLM/Tests/Services/ServicesBacklogTests.cs` — documents **public service APIs not covered** by runnable unit tests. Each method is an empty `[Fact(Skip = "...")]` stub so the backlog appears in test explorers and CI reports.

---

## Skipped test methods

#### `public void AslmApiServer_lifecycle()`

**Purpose:** Document `AslmApiServer.StartAsync` / `StopAsync` lifecycle (proxy loop + `HttpListener`).

**Skip reason:** Requires live `HttpListener` and proxy loop.

| Step | Action (future) |
| --- | --- |
| 1 | Start API server on test port |
| 2 | Exercise proxy route |
| 3 | Stop and assert clean shutdown |

---

#### `public void AslmModuleInteropServer_lifecycle()`

**Purpose:** Document module interop HTTP server start/stop.

**Skip reason:** Requires live `HttpListener` (`AslmModuleInteropServer.StartAsync`).

| Step | Action (future) |
| --- | --- |
| 1 | Start interop server |
| 2 | Hit health or handshake endpoint |
| 3 | Stop server |

---

#### `public void ModuleDownloadBridge_invoke()`

**Purpose:** Document bridge subprocess RPC for downloads.

**Skip reason:** Requires module bridge subprocess stdio (`ModuleDownloadBridge.InvokeAsync`).

| Step | Action (future) |
| --- | --- |
| 1 | Launch bridge process |
| 2 | Invoke download operation |
| 3 | Assert structured response |

---

#### `public void ModuleLaunchCoordinator_launch()`

**Purpose:** Document coordinated module launch across installer/runner graph.

**Skip reason:** Requires full module runner/installer graph (`ModuleLaunchCoordinator`).

| Step | Action (future) |
| --- | --- |
| 1 | Configure test module |
| 2 | Launch via coordinator |
| 3 | Assert running state |

---

#### `public void ModuleRunner_execute_run()`

**Purpose:** Document `ModuleRunner.ExecuteRunAsync` with real processes.

**Skip reason:** Requires real module processes and ports.

| Step | Action (future) |
| --- | --- |
| 1 | Assign ports |
| 2 | Execute run command |
| 3 | Assert process and exit handling |

---

#### `public void DownloadInstaller_install()`

**Purpose:** Document full install pipeline with archives.

**Skip reason:** Requires install pipeline with archives and processes (`DownloadInstaller.InstallAsync`).

| Step | Action (future) |
| --- | --- |
| 1 | Stage test archive |
| 2 | Run install |
| 3 | Assert installed state and notifications |

---

#### `public void ProcessTracker_job_object()`

**Purpose:** Document Win32 Job Object child process tracking.

**Skip reason:** Requires Win32 Job Object child tracking (`ProcessTracker.AddProcess`).

| Step | Action (future) |
| --- | --- |
| 1 | Create job object |
| 2 | Add child process |
| 3 | Assert tracking on exit |

---

#### `public void ProcessSnapshotReader_snapshot()`

**Purpose:** Document Toolhelp-based process snapshot enumeration.

**Skip reason:** Requires Toolhelp process snapshot (`ProcessSnapshotReader.GetSnapshot`).

| Step | Action (future) |
| --- | --- |
| 1 | Start known child |
| 2 | Read snapshot |
| 3 | Assert process entry present |

---

#### `public void DetachedProcessStarter_breakaway()`

**Purpose:** Document breakaway process creation outside job.

**Skip reason:** Requires breakaway `CreateProcess` (`DetachedProcessStarter.TryStartBreakawayProcess`).

| Step | Action (future) |
| --- | --- |
| 1 | Parent in job |
| 2 | Start breakaway child |
| 3 | Assert not assigned to parent job |

---

#### `public void ThemeService_apply()`

**Purpose:** Document applying theme from settings to MAUI resources.

**Skip reason:** Requires MAUI `Application.Current` resources (`ThemeService.ApplyFromSettings`).

| Step | Action (future) |
| --- | --- |
| 1 | Host MAUI app |
| 2 | Apply theme from settings |
| 3 | Assert resource dictionary values |

---

#### `public void ConsoleOutputViewHandler_mapper()`

**Purpose:** Document WinUI console output view mapping.

**Skip reason:** Requires WinUI handler host (`ConsoleOutputViewHandler`).

| Step | Action (future) |
| --- | --- |
| 1 | Instantiate handler with platform host |
| 2 | Map console lines |
| 3 | Assert UI model |

---

#### `public void OsAppThemeReader_dark_mode()`

**Purpose:** Document OS / registry dark-mode probe.

**Skip reason:** Requires registry or MAUI theme (`OsAppThemeReader.IsWindowsAppDarkMode`).

| Step | Action (future) |
| --- | --- |
| 1 | Set OS theme fixture |
| 2 | Read dark mode |
| 3 | Assert matches expectation |

---

#### `public void PackagedIconTintCache_tint()`

**Purpose:** Document SkiaSharp tinted icon cache.

**Skip reason:** Requires SkiaSharp and packaged assets (`PackagedIconTintCache.Get`).

| Step | Action (future) |
| --- | --- |
| 1 | Load packaged icon |
| 2 | Request tint |
| 3 | Assert cached bitmap |

---

#### `public void AslmApiServer_proxy_route_resolution()`

**Purpose:** Document private `ProxyRoute` factory resolution (indirect HTTP integration).

**Skip reason:** Private nested `ProxyRoute` factories; covered indirectly via HTTP integration backlog.

| Step | Action (future) |
| --- | --- |
| 1 | Integration test via public HTTP surface |
| 2 | Assert route targets module endpoint |

---

## Purpose

Serves as a **living checklist** for future integration or UI tests without failing the build. When adding coverage, implement the test and remove or enable the skip.

---

## Related

- [Services (tests) _index](_index/) — what *is* tested today
