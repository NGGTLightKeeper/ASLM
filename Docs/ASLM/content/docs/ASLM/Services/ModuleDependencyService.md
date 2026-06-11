---
title: "ModuleDependencyService"
draft: false
---

## Overview

`ASLM/Services/ModuleDependencyService.cs` — **`public`** — ensures declared module dependencies are installed and ready before a dependent module runs. It also detects circular dependencies during discovery.

**DI:** `AddSingleton<ModuleDependencyService>()`.

Types in same file: **`ModuleDependencyService`**.

---

## Class `ModuleDependencyService`

**Dependencies:** [ModuleInstaller](ModuleInstaller/), [ModuleRunner](ModuleRunner/), `ILogger<ModuleDependencyService>`.

---

## Public methods

#### `public ModuleDependencyService(ModuleInstaller installer, ModuleRunner runner, ILogger<ModuleDependencyService> logger)`

**Purpose:** Creates the module dependency service.

---

#### `public async Task<bool> EnsureFirstRunCompletedAsync(ModuleConfig module, IProgress<string> log, CancellationToken ct)`

**Purpose:** Runs first-run setup for every declared module dependency that is not ready yet.
Sets **`Status.Installed = true`** and **`Status.FirstRunCompleted = true`** on the dependency module upon successful completion of its setup.

**Parameters:**
- `module`: The dependent `ModuleConfig` requesting execution.
- `log`: Progress reporter to log installation and execution output.
- `ct`: Cancellation token to abort the process.

**Returns:**
- `Task<bool>`: Returns `true` if all dependencies are verified or successfully setup; `false` on missing dependencies, setup failure, or if a circular dependency is detected.

**Usage:**
```csharp
var dependencyService = _serviceProvider.GetRequiredService<ModuleDependencyService>();
if (!await dependencyService.EnsureFirstRunCompletedAsync(module, log, ct))
{
    return false; // Stop execution, dependency setup failed
}
```

---

## Related

- [ModuleInstaller](ModuleInstaller/)
- [ModuleRunner](ModuleRunner/)
- [ModuleLaunchCoordinator](ModuleLaunchCoordinator/)
