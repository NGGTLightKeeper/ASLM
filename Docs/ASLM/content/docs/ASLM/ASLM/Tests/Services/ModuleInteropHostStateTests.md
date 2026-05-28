---
title: "ModuleInteropHostStateTests"
draft: false
---

## Class `ModuleInteropHostStateTests`

`ASLM/Tests/Services/ModuleInteropHostStateTests.cs` — listener URL/port bookkeeping in [ModuleInteropHostState](../../Services/ModuleInteropHostState/).

---

## Test methods

#### `public void TryGetListening_returns_false_when_cleared()`

**Purpose:** Fresh or cleared state reports no active listener.

| Step | Action |
| --- | --- |
| 1 | `new ModuleInteropHostState()` |
| 2 | `TryGetListening(out _, out _)` → assert `false` |

---

#### `public void SetListening_and_TryGetListening_round_trip()`

**Purpose:** Valid endpoint set via `SetListening` is returned by `TryGetListening`.

| Step | Action |
| --- | --- |
| 1 | `SetListening("http://127.0.0.1:12345/", 12345)` |
| 2 | `TryGetListening` → `true` |
| 3 | Assert `baseUrl` and `port` match |

---

#### `public void TryGetListening_rejects_invalid_endpoints(string baseUrl, int port)`

**Purpose:** Blank URL, zero port, or out-of-range port are treated as not listening.

| `baseUrl` | `port` |
| --- | --- |
| `""` | `8080` |
| `http://127.0.0.1/` | `0` |
| `http://127.0.0.1/` | `70000` |

| Step | Action |
| --- | --- |
| 1 | `SetListening(baseUrl, port)` |
| 2 | `TryGetListening` → assert `false` |

---

#### `public void Clear_removes_active_listener()`

**Purpose:** `Clear` drops a previously set listener.

| Step | Action |
| --- | --- |
| 1 | `SetListening("http://127.0.0.1:9000/", 9000)` |
| 2 | `Clear()` |
| 3 | `TryGetListening` → assert `false` |

---

## Related

- [ModuleInteropHostState](../../Services/ModuleInteropHostState/)
