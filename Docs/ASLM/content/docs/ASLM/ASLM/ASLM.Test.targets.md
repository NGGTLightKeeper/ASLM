---
title: "ASLM.Test.targets"
draft: false
---

## File `ASLM.Test.targets`

`ASLM/ASLM.Test.targets` — MSBuild import from the main [ASLM](ASLM/) project. Runs unit tests after a successful app build.

---

## Properties

| Property | Default | Meaning |
| --- | --- | --- |
| `ASLMTestsProjectPath` | `Tests/ASLM.Tests.csproj` | Test project path |
| `SkipUnitTestsOnBuild` | `false` | When `true`, skips `dotnet test` |

The test project sets `SkipUnitTestsOnBuild=true` when building itself to avoid recursion.

---

## Target `RunAslmUnitTestsAfterBuild`

- **After:** `Build`
- **Condition:** tests not skipped, project exists, single-TFM inner build
- **Command:** `dotnet test` with `--no-restore` and `/p:SkipUnitTestsOnBuild=true`

---

## Related

- [Tests](Tests/) — test project documentation
