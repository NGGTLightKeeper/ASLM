---
title: "AssemblyInfo"
draft: false
---

## File `AssemblyInfo.cs`

`ASLM/Tests/AssemblyInfo.cs` — xUnit assembly configuration.

```csharp
[assembly: CollectionBehavior(DisableTestParallelization = true)]
```

**Why:** tests share on-disk state under `Tests/_layout_root/Data/App/` (ports, app data, themes). Parallel runs could delete or overwrite the same JSON files.

---

## Related

- [AslmFileSystemLayout](TestSupport/AslmFileSystemLayout/) — per-test layout reset
