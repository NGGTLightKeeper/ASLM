---
title: "ModuleEnvironmentResolverTests"
draft: false
---

## Class `ModuleEnvironmentResolverTests`

`ASLM/Tests/Services/ModuleEnvironmentResolverTests.cs` — [ModuleEnvironmentResolver](../../../Services/ModuleEnvironmentResolver/).

**Helpers:** [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/), [ModuleConfigBuilder](../TestSupport/ModuleConfigBuilder/), [EngineInstaller](../../../Services/EngineInstaller/).

---

## Test methods

#### `public void HasModuleEnvironment_returns_true_when_enabled()`

**Purpose:** `EngineConfig` with `ModuleEnvironment.Enabled = true` → `HasModuleEnvironment` is `true`.

---

#### `public void ResolveEnvironment_builds_directory_under_engine_root()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Layout + `CreateDiscoveredEngine` with `tool.exe` |
| 2 | `EngineInstaller.GetEngineConfig("test-engine")` |
| 3 | `ResolveEnvironment(module, engine)` |
| 4 | Assert path contains `venv-demo-module`; `ASLM_TEST` env equals directory path |

---

#### `public void ApplyEnvironmentVariables_writes_values_to_process_start_info()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Same engine setup |
| 2 | `ProcessStartInfo` for `cmd.exe` |
| 3 | `ApplyEnvironmentVariables` |
| 4 | Assert `CUSTOM_FLAG` = `enabled`; `ASLM_ENGINE_ENV_DIR` non-empty |

---

## Private helpers

#### `private static void CreateDiscoveredEngine(string root, string executableFileName)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Create `Engines/test-engine/` with exe + `runtime/placeholder.txt` |
| 2 | Write `ASLM_Engine.json` with `ModuleEnvironment` enabled, `{environmentDir}` substitution |
| 3 | `manifest.Normalize()` before serialize |

---

## Related

- [ModuleEnvironmentResolver](../../../Services/ModuleEnvironmentResolver/)
- [EngineConfig](../../../Models/EngineConfig/)
