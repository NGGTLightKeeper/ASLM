---
title: "TestSupport"
draft: false
---

Shared helpers in `ASLM/Tests/TestSupport/` used across [Services](Services/) test classes.

| Type | Doc | Role |
| --- | --- | --- |
| `AslmFileSystemLayout` | [AslmFileSystemLayout](AslmFileSystemLayout/) | Layout root + `Data/App` paths |
| `TestLoggerFactory` | [TestLoggerFactory](TestLoggerFactory/) | Null `ILogger<T>` |
| `ModuleConfigBuilder` | [ModuleConfigBuilder](ModuleConfigBuilder/) | Factory for `ModuleConfig` |

---

## Layout contract

Production services resolve layout root as **parent of** `AppDomain.CurrentDomain.BaseDirectory`. The test project sets **`OutputPath`** to `Tests/_layout_root/App/`, so `BaseDirectory` is `{repo}/ASLM/Tests/_layout_root/App` and root is `_layout_root`.

This matches [AppDataStore](../Services/AppDataStore/) and [PortRegistry](../Services/PortRegistry/) path logic without mocking the file system.
