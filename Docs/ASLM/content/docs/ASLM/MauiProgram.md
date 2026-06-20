---
title: "MauiProgram"
draft: false
---

## Class `MauiProgram`

`ASLM/MauiProgram.cs` — **`public static`** MAUI host bootstrap: fonts, handlers, DI, page registration, **`Localization.L`** initialization.

---

## Public methods

#### `public static MauiApp CreateMauiApp()`

### Builder configuration

| Step | Call |
| --- | --- |
| 1 | `MauiApp.CreateBuilder()` |
| 2 | `UseMauiApp<App>()` |
| 3 | `ConfigureMauiHandlers` — `ConsoleOutputView` → `ConsoleOutputViewHandler` |
| 4 | `ConfigureFonts` — OpenSans Regular/Semibold, Segoe Semibold, Fluent UI icons |
| 5 | `#if DEBUG` — `Logging.AddDebug()` on builder and services |

### Singleton services

`AppDataStore`, `DockerService`, `EngineInstaller`, `ModuleEnvironmentResolver`, `ModuleTrustService`, `ModuleInstaller`, `ModuleConsoleStore`, `ProcessSnapshotReader`, `ProcessTracker`, `ModuleThemePayloadBuilder`, `ModuleLocalePayloadBuilder`, `AppLocalizationService`, `ModuleInteropHostState`, `ModuleStartThrottle`, `PortRegistry`, `ModuleRunner`, `ModuleDownloadBridge`, `DownloadStateStore`, `DownloadCatalog`, `DownloadInstaller`, `NotificationCenter`, `OllamaSettingsStore`, `GitHubRateLimitStore`, `GitHubUpdateClient`, `UpdateManager`, `UpdateScheduler`, `ModuleLaunchCoordinator`, `AslmModuleInteropServer`, `AslmApiServer`, `SettingsService`, `CustomThemesStore`, `ThemeService`.

### Transient UI

| Kind | Types |
| --- | --- |
| Pages | `AppShellPage`, `SetupWizardPage`, `LoadingPage` |
| Content views | `HomeView`, `ConsolesView`, `ModulesView`, `AslmApiView`, `NotificationsView`, `DownloadsView`, `SettingsView`, `ModuleUpdateView` |

### Post-build

| Step | Action |
| --- | --- |
| 1 | `var app = builder.Build()` |
| 2 | `Localization.L.Initialize(app.Services.GetRequiredService<AppLocalizationService>())` |
| 3 | Return `app` |

---

## Related

- [Localization](Localization/)
- [Services](Services/)
- [Platforms/Windows/App](Platforms/Windows/App/)
