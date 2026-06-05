---
title: "ModuleTrustServiceTests"
draft: false
---

## Class `ModuleTrustServiceTests`

`ASLM/Tests/Services/ModuleTrustServiceTests.cs` — catalog vs unknown module trust levels via [ModuleTrustService](../../Services/ModuleTrustService/).

**Helpers:** [ModuleConfigBuilder](../TestSupport/ModuleConfigBuilder/), [TestLoggerFactory](../TestSupport/TestLoggerFactory/).

---

## Test methods

#### `public void Resolve_returns_official_for_catalog_module()`

**Purpose:** Known catalog module (`aslm-chat` / `NGGTLightKeeper/ASLM-Chat`) resolves to `Official`.

| Step | Action |
| --- | --- |
| 1 | `ModuleConfigBuilder.Create(id: "aslm-chat", repo: "NGGTLightKeeper/ASLM-Chat")` |
| 2 | `Resolve(module)` → assert `ModuleTrustLevel.Official` |

---

#### `public void Resolve_returns_unreviewed_for_unknown_module()`

**Purpose:** Modules not in the trust catalog resolve to `Unreviewed`.

| Step | Action |
| --- | --- |
| 1 | `ModuleConfigBuilder.Create(id: "unknown-module")` |
| 2 | `Resolve(module)` → assert `ModuleTrustLevel.Unreviewed` |

---

## Related

- [ModuleTrustService](../../Services/ModuleTrustService/)
