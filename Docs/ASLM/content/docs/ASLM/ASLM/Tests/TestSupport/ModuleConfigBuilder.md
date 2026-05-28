---
title: "ModuleConfigBuilder"
draft: false
---

## Class `ModuleConfigBuilder`

`ASLM/Tests/TestSupport/ModuleConfigBuilder.cs` — **`public static`** — builds minimal valid [ModuleConfig](../../../Models/ModuleConfig/) instances for port/console tests.

---

## Public methods
#### `public static ModuleConfig Create(string id = "test-module", string name = "Test Module", Action<ModuleConfig>? configure = null)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | New `ModuleConfig` with id, name, version `1.0.0`, type `web`, GitHub source, run command |
| 2 | `SourcePath` under `{layout}/Modules/{id}/ASLM_Module.json` |
| 3 | Default `http` port [ModuleSetting](../../../Models/ModuleConfig/) |
| 4 | `Normalize()`; optional `configure(module)` |
| 5 | Return module |

---

## Related

- [ModuleConfig](../../../Models/ModuleConfig/)
- [PortRegistryTests](../../Services/PortRegistryTests/)
