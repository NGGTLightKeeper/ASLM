---
title: "Services (tests)"
draft: false
---

xUnit test classes in `ASLM/Tests/Services/`. Each file targets one [Services](../../Services/) type (or a private nested helper via reflection).

---

## Active tests

| Test class | Service | Doc |
| --- | --- | --- |
| `AppDataStoreTests` | [AppDataStore](../../Services/AppDataStore/) | [AppDataStoreTests](AppDataStoreTests/) |
| `SettingsServiceTests` | [SettingsService](../../Services/SettingsService/) | [SettingsServiceTests](SettingsServiceTests/) |
| `AppLocalizationServiceTests` | [AppLocalizationService](../../Services/AppLocalizationService/) | [AppLocalizationServiceTests](AppLocalizationServiceTests/) |
| `PortRegistryTests` | [PortRegistry](../../Services/PortRegistry/) | [PortRegistryTests](PortRegistryTests/) |
| `NotificationCenterTests` | [NotificationCenter](../../Services/NotificationCenter/) | [NotificationCenterTests](NotificationCenterTests/) |
| `CustomThemesStoreTests` | [CustomThemesStore](../../Services/CustomThemesStore/) | [CustomThemesStoreTests](CustomThemesStoreTests/) |
| `ThemePaletteResolverTests` | [ThemePaletteResolver](../../Services/ThemePaletteResolver/) | [ThemePaletteResolverTests](ThemePaletteResolverTests/) |
| `ModuleThemePayloadBuilderTests` | [ModuleThemePayloadBuilder](../../Services/ModuleThemePayloadBuilder/) | [ModuleThemePayloadBuilderTests](ModuleThemePayloadBuilderTests/) |
| `ModuleLocalePayloadBuilderTests` | [ModuleLocalePayloadBuilder](../../Services/ModuleLocalePayloadBuilder/) | [ModuleLocalePayloadBuilderTests](ModuleLocalePayloadBuilderTests/) |
| `ModuleInteropHostStateTests` | [ModuleInteropHostState](../../Services/ModuleInteropHostState/) | [ModuleInteropHostStateTests](ModuleInteropHostStateTests/) |
| `ModuleConsoleStoreTests` | [ModuleConsoleStore](../../Services/ModuleConsoleStore/) | [ModuleConsoleStoreTests](ModuleConsoleStoreTests/) |
| `ModuleEnvironmentResolverTests` | [ModuleEnvironmentResolver](../../Services/ModuleEnvironmentResolver/) | [ModuleEnvironmentResolverTests](ModuleEnvironmentResolverTests/) |
| `ModuleTrustServiceTests` | [ModuleTrustService](../../Services/ModuleTrustService/) | [ModuleTrustServiceTests](ModuleTrustServiceTests/) |
| `ModuleStartThrottleTests` | [ModuleStartThrottle](../../Services/ModuleStartThrottle/) | [ModuleStartThrottleTests](ModuleStartThrottleTests/) |
| `EngineInstallerStepContextTests` | `EngineInstaller.StepContext` | [EngineInstallerStepContextTests](EngineInstallerStepContextTests/) |
| `DownloadStateStoreTests` | [DownloadStateStore](../../Services/DownloadStateStore/) | [DownloadStateStoreTests](DownloadStateStoreTests/) |
| `DownloadTransferSpeedEstimatorTests` | [DownloadTransferSpeedEstimator](../../Services/DownloadTransferSpeedEstimator/) | [DownloadTransferSpeedEstimatorTests](DownloadTransferSpeedEstimatorTests/) |
| `GitHubUpdateClientTests` | [GitHubUpdateClient](../../Services/GitHubUpdateClient/) | [GitHubUpdateClientTests](GitHubUpdateClientTests/) |
| `ReleaseTagOrderingTests` | [ReleaseTagOrdering](../../Services/ReleaseTagOrdering/) | [ReleaseTagOrderingTests](ReleaseTagOrderingTests/) |
| `DockerServiceTests` | [DockerService](../../Services/DockerService/) | [DockerServiceTests](DockerServiceTests/) |

---

## Integration backlog (skipped)

[ServicesBacklogTests](ServicesBacklogTests/) — placeholder `[Fact(Skip = "...")]` methods documenting APIs that need HttpListener, processes, MAUI, or Skia. See that page for the full list.

---

## Helpers used here

- [AslmFileSystemLayout](../TestSupport/AslmFileSystemLayout/)
- [TestLoggerFactory](../TestSupport/TestLoggerFactory/)
- [ModuleConfigBuilder](../TestSupport/ModuleConfigBuilder/)
