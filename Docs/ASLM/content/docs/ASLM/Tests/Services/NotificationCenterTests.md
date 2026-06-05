---
title: "NotificationCenterTests"
draft: false
---

## Class `NotificationCenterTests`

`ASLM/Tests/Services/NotificationCenterTests.cs` — operation key normalization on [NotificationCenter](../../Services/NotificationCenter/).

---

## Test methods

#### `public void BuildOperationKey_normalizes_source_parts(string kind, string id, string expected)`

**Purpose:** Kind and id are trimmed, lowercased, and joined as `kind:id`.

| `kind` | `id` | `expected` |
| --- | --- | --- |
| `Module` | `ASLM-Chat` | `module:aslm-chat` |
| ` Engine ` | ` Ollama ` | `engine:ollama` |

| Step | Action |
| --- | --- |
| 1 | `NotificationCenter.BuildOperationKey(kind, id)` |
| 2 | Assert equals `expected` |

---

## Related

- [NotificationCenter](../../Services/NotificationCenter/)
