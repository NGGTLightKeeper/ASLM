---
title: "DownloadStateStoreTests"
draft: false
---

## Class `DownloadStateStoreTests`

`ASLM/Tests/Services/DownloadStateStoreTests.cs` — installed-resource tracking in [DownloadStateStore](../../Services/DownloadStateStore/).

**Helpers:** [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/), [TestLoggerFactory](../TestSupport/TestLoggerFactory/).

---

## Test methods

#### `public async Task MarkInstalledAsync_and_MarkUninstalledAsync_persist_state()`

**Purpose:** Install and uninstall markers update in-memory resource state for a key.

| Step | Action |
| --- | --- |
| 1 | `await MarkInstalledAsync("engine:ollama", "1.2.3", "aslm-chat")` |
| 2 | Assert `Installed == true`, `InstalledVersion == "1.2.3"` |
| 3 | `await MarkUninstalledAsync("engine:ollama")` |
| 4 | Assert `Installed == false` |

---

#### `public void GetResourceState_returns_null_for_unknown_key()`

**Purpose:** Unknown resource keys do not synthesize a state object.

| Step | Action |
| --- | --- |
| 1 | `GetResourceState("missing")` |
| 2 | Assert `null` |

---

## Related

- [DownloadStateStore](../../Services/DownloadStateStore/)
