---
title: "ModuleInteropHostState"
draft: false
---

## Class `ModuleInteropHostState`

`ASLM/Services/ModuleInteropHostState.cs` — **`public sealed`** — thread-safe snapshot of the module interop listener base URL for child process environment injection.

**DI:** `AddSingleton<ModuleInteropHostState>()`.

Updated by [AslmModuleInteropServer](AslmModuleInteropServer/); read by [ModuleRunner](ModuleRunner/) when building env vars.

---

## Public methods

#### `public void SetListening(string baseUrl, int port)

**Purpose:** Records the listening interop server endpoint while the listener is active.

| Step | Action |
| --- | --- |
| 1 | `lock (_lock)` |
| 2 | Assign `_baseUrl`, `_port` |

---

#### `public void Clear()

**Purpose:** Clears the listener endpoint after shutdown.

| Step | Action |
| --- | --- |
| 1 | `lock (_lock)` |
| 2 | Set `_baseUrl` and `_port` to **
ull`** |

---

#### `public bool TryGetListening(out string baseUrl, out int port)

**Purpose:** Returns the active interop base URL and port when the host server is listening.

| Condition | Result |
| --- | --- |
| `_baseUrl` non-empty and `_port` in **1–65535** | **`true`**, `baseUrl` / `port` from stored values |
| Otherwise | **`false`**, `baseUrl = ""`, `port = 0` |

All reads/writes synchronize on **`_lock`**.

---

## Related

- [PortRegistry](PortRegistry/) — allocates `__aslm-module-interop` port
- [AslmModuleInteropServer](AslmModuleInteropServer/)
