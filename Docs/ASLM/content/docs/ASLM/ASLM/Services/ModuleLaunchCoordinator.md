---
title: "ModuleLaunchCoordinator"
draft: false
---

## Overview

`ASLM/Services/ModuleLaunchCoordinator.cs` — orchestrates the same launch sequence as the module dashboard card: discover → first-run → enable → start run commands.

**DI:** `AddSingleton<ModuleLaunchCoordinator>()`.

Types in same file: **`ModuleLaunchStatus`**, **`ModuleLaunchResult`**, **`ModuleLaunchCoordinator`**.

---

## Enum `ModuleLaunchStatus`

| Value | Meaning |
| --- | --- |
| `Started` | Run commands scheduled on background thread |
| `AlreadyRunning` | Module source path already in runner |
| `NotFound` | No matching module or manifest unloadable |
| `NoRunCommands` | `Commands.Run` is empty |
| `FirstRunFailed` | First-run setup did not succeed |
| `Error` | Invalid input, discovery failure, or load exception |

---

## Record `ModuleLaunchResult`

`Status` (`ModuleLaunchStatus`), optional `Message`, optional `EffectiveConfig` (`ModuleConfig?`).

---

## Class `ModuleLaunchCoordinator`

**Dependencies:** [ModuleInstaller](ModuleInstaller/), [ModuleRunner](ModuleRunner/), [ModuleStartThrottle](ModuleStartThrottle/).

---

## Public methods

#### `public ModuleLaunchCoordinator(ModuleInstaller installer, ModuleRunner runner, ModuleStartThrottle startThrottle, ILogger<ModuleLaunchCoordinator> logger)`

**Purpose:** Creates the coordinator.

**Steps:**

1. Store installer, runner, throttle, and logger references.

---

#### `public async Task<ModuleLaunchResult> LaunchOrEnsureRunningAsync(string moduleId, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Resolves a module by stable id and starts it when it is not already running.

**Steps:**

1. Return **`Error`** if `moduleId` is null/whitespace.
2. **`DiscoverModulesAsync`**; filter by id (ignore case), order by `SourcePath`.
3. On exception → log, return **`Error`** with message.
4. Zero matches → **`NotFound`**.
5. Multiple matches → log warning, use first by sort order.
6. Delegate to **`LaunchOrEnsureRunningBySourcePathAsync(matches[0].SourcePath, …)`**.

---

#### `public async Task<ModuleLaunchResult> LaunchOrEnsureRunningBySourcePathAsync(string moduleSourcePath, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Starts one installed module identified by its manifest path.

**Steps:**

1. Return **`Error`** if path is null/whitespace.
2. **`LoadModuleConfig`** trimmed path; on exception → **`Error`**.
3. Null config → **`NotFound`**.
4. **`_startThrottle.WaitAsync(ct)`**; try **`LaunchOrEnsureRunningCoreAsync`**; **`Release`** in `finally`.

---

## Private methods

#### `private async Task<ModuleLaunchResult> LaunchOrEnsureRunningCoreAsync(ModuleConfig discovered, IProgress<string>? log, CancellationToken ct)`

**Purpose:** Reloads the manifest, runs first-run when needed, enables the module, and starts run commands.

**Steps:**

1. Use `log` or no-op **`Progress<string>`**.
2. Reload manifest from `discovered.SourcePath`; null → **`NotFound`**.
3. Empty `Commands.Run` → **`NoRunCommands`** with fresh config.
4. If `SourcePath` in **`GetRunningModuleSourcePaths()`** → **`AlreadyRunning`**.
5. If `!FirstRunCompleted` → **`ExecuteFirstRunAsync`** on thread pool; failure → **`FirstRunFailed`**; else set flag and **`SaveConfigAsync`**.
6. Set `Status.Enabled = true`, **`SaveConfigAsync`**.
7. Fire-and-forget **`ExecuteRunAsync`** (no cancellation) → **`Started`**.

---

## Related

- [ModuleInstaller](ModuleInstaller/)
- [ModuleRunner](ModuleRunner/)
- [ModuleStartThrottle](ModuleStartThrottle/)
