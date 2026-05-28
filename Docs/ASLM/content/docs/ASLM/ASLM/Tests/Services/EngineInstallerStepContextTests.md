---
title: "EngineInstallerStepContextTests"
draft: false
---

## Class `EngineInstallerStepContextTests`

`ASLM/Tests/Services/EngineInstallerStepContextTests.cs` — tests **`EngineInstaller.StepContext`** via reflection (internal install step variable/path resolution).

---

## Test methods

#### `public void StepContext_resolve_variables_and_paths_within_allowed_roots()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `CreateStepContext(baseDir, tempDir)` |
| 2 | Invoke `ResolvePath` / variable substitution for paths under allowed roots |
| 3 | Assert resolved paths stay inside engine/install roots |

---

#### `public void StepContext_resolve_path_rejects_traversal_outside_roots()

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `CreateStepContext` |
| 2 | Attempt path with `..` traversal |
| 3 | Assert rejection / exception |

---

## Private helpers

#### `private static object CreateStepContext(string baseDir, string tempDir)`

**Purpose:** Reflects internal `StepContext` constructor with base and temp directories.

---

#### `private static T Invoke<T>(object instance, string methodName, params object[] args)`

**Purpose:** Reflects and invokes non-public instance method; returns typed result.

---

## Related

- [EngineInstaller](../../../Services/EngineInstaller/)
