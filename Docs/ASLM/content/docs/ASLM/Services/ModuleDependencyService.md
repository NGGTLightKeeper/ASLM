---
title: "ModuleDependencyService"
draft: false
---

## Overview

`ASLM/Services/ModuleDependencyService.cs` — **`public sealed`** — ensures declared module dependencies are installed and ready before a dependent module runs.

**DI:** `AddSingleton<ModuleDependencyService>()`.

---

## Public methods

#### `public ModuleDependencyService(ModuleInstaller installer, ModuleRunner runner, ILogger<ModuleDependencyService> logger)`

**Purpose:** Creates the module dependency service.

**Steps:**

1. Stores references to the installer, runner, and logger.

---

#### `public async Task<bool> EnsureFirstRunCompletedAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct)`

**Purpose:** Runs first-run setup for every declared module dependency that is not ready yet.

**Steps:**

1. Manages a thread-local visit stack (`AsyncLocal<HashSet<string>>`) to detect circular dependencies.
2. Delegates to `EnsureFirstRunCompletedCoreAsync` for recursive dependency resolution.
3. Cleans up the visit stack if the current invocation owns it.
4. Returns `true` if all dependencies are successfully resolved and ready, `false` otherwise.

---

## Private methods

#### `private async Task<bool> EnsureFirstRunCompletedCoreAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct, HashSet<string> visitStack)`

**Purpose:** Internal recursive implementation for checking and setting up dependencies.

**Steps:**

1. Iterates over `Dependencies.Modules` in the given `module`.
2. Checks for self-dependency or circular dependency using `visitStack`. Returns `false` and logs an error if found.
3. Resolves the installed dependency using `ResolveInstalledModuleAsync`.
4. Recursively calls `EnsureFirstRunCompletedCoreAsync` for the dependency module itself.
5. If the dependency module's `FirstRunCompleted` is true, skips further setup.
6. Otherwise, logs progress and calls `ModuleRunner.ExecuteFirstRunAsync` with `skipModuleDependencies: true`.
7. Updates the dependency module's status and saves the config.
8. Ensures `visitStack` is cleaned up after each dependency is processed.

---

#### `private async Task<ModuleConfig?> ResolveInstalledModuleAsync(string moduleId, IProgress<string> log, CancellationToken ct)`

**Purpose:** Finds a matching installed module configuration by its dependency identifier.

**Steps:**

1. Calls `ModuleInstaller.DiscoverModulesAsync()` to get the current catalog.
2. Filters the catalog for modules matching `moduleId` (case-insensitive).
3. Warns if multiple matching modules are found and selects the one sorted first by `SourcePath`.
4. Logs an error and returns `null` if no match is found.
5. Returns the matched `ModuleConfig`.

---

## Related

- [ModuleConfig](../Models/ModuleConfig/)
- [ModuleRunner](ModuleRunner/)
- [ModuleInstaller](ModuleInstaller/)
