---
title: "Tests"
draft: false
---

Unit test project for the ASLM host: **`ASLM/Tests/ASLM.Tests.csproj`**. Targets **`net10.0-windows10.0.19041.0`**, references the built **`ASLM.dll`** from `Build/{Configuration}/ASLM-windows-amd64/App/`, and runs with **xUnit**, **FluentAssertions**, and **Moq**.

Tests focus on **pure service logic** and file-backed persistence under a synthetic layout root. Integration-heavy APIs are listed as **skipped placeholders** in [ServicesBacklogTests](Services/ServicesBacklogTests/).

---

## Build & run

| Step | Behavior |
| --- | --- |
| `BuildAslmApp` (BeforeBuild) | Builds main `ASLM.csproj` with `SkipUnitTestsOnBuild=true` |
| Test output | `Tests/_layout_root/App/` (same shape as production: parent = layout root) |
| `CopyAslmRuntime` | Copies `ASLM*.dll` from app build into test output |
| Main app hook | [ASLM.Test.targets](../ASLM.Test.targets) runs `dotnet test` after app build unless `SkipUnitTestsOnBuild=true` |

```powershell
dotnet test "ASLM\Tests\ASLM.Tests.csproj" -c Debug
```

---

## Conventions

- **`[assembly: CollectionBehavior(DisableTestParallelization = true)]`** — tests share layout files under `_layout_root`; run sequentially.
- **`AslmFileSystemLayout`** — ensures `{root}/Data/App` and `{root}/Modules`; optional reset of JSON state files before each test.
- **`TestLoggerFactory`** — silent `ILogger<T>` for services that require logging.
- **`ModuleConfigBuilder`** — minimal valid [ModuleConfig](../Models/ModuleConfig/) for port/console tests.

---

## Coverage map

| Area | Test classes | Service under test |
| --- | --- | --- |
| Persistence | [AppDataStoreTests](Services/AppDataStoreTests/), [CustomThemesStoreTests](Services/CustomThemesStoreTests/), [DownloadStateStoreTests](Services/DownloadStateStoreTests/) | [AppDataStore](../Services/AppDataStore/), etc. |
| Settings / ports | [SettingsServiceTests](Services/SettingsServiceTests/), [PortRegistryTests](Services/PortRegistryTests/) | [SettingsService](../Services/SettingsService/), [PortRegistry](../Services/PortRegistry/) |
| Localization / theme | [AppLocalizationServiceTests](Services/AppLocalizationServiceTests/), [ThemePaletteResolverTests](Services/ThemePaletteResolverTests/), payload builder tests | Theme/localization services |
| Module infra | [ModuleEnvironmentResolverTests](Services/ModuleEnvironmentResolverTests/), [ModuleTrustServiceTests](Services/ModuleTrustServiceTests/), [ModuleInteropHostStateTests](Services/ModuleInteropHostStateTests/), [ModuleConsoleStoreTests](Services/ModuleConsoleStoreTests/), [ModuleStartThrottleTests](Services/ModuleStartThrottleTests/) | Runtime helpers |
| Updates / GitHub | [ReleaseTagOrderingTests](Services/ReleaseTagOrderingTests/), [GitHubUpdateClientTests](Services/GitHubUpdateClientTests/) | [ReleaseTagOrdering](../Services/ReleaseTagOrdering/), [GitHubUpdateClient](../Services/GitHubUpdateClient/) |
| Engine install | [EngineInstallerStepContextTests](Services/EngineInstallerStepContextTests/) | `EngineInstaller.StepContext` (reflection) |
| Notifications / downloads | [NotificationCenterTests](Services/NotificationCenterTests/), [DownloadTransferSpeedEstimatorTests](Services/DownloadTransferSpeedEstimatorTests/) | Small pure helpers |
| Platform | [DockerServiceTests](Services/DockerServiceTests/) | [DockerService](../Services/DockerService/) |
| Backlog | [ServicesBacklogTests](Services/ServicesBacklogTests/) | Skipped integration stubs |

---

## Project files

| File | Doc |
| --- | --- |
| `GlobalUsings.cs` | [GlobalUsings](GlobalUsings/) |
| `AssemblyInfo.cs` | [AssemblyInfo](AssemblyInfo/) |
| `TestSupport/*` | [TestSupport](TestSupport/) |
| `Services/*Tests.cs` | [Services](Services/) |

---

## Related

- [Services](../Services/) — documented implementations under test
- [MauiProgram](../MauiProgram/) — production DI (not exercised in most unit tests)
