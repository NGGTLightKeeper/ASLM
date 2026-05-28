---
title: "LoadingPage"
draft: false
---

## Class `LoadingPage`

`ASLM/Pages/LoadingPage.xaml` + `LoadingPage.xaml.cs` — startup **`ContentPage`**. Shown from [App](../App/) as the initial `Window.Page`, runs background initialization, then replaces the window with [SetupWizardPage](SetupWizardPage/) or [AppShellPage](AppShellPage/).

Implements **`ILocalizable`**.

---

### Fields

| Name | Type | Description |
| --- | --- | --- |
| `_initialized` | `bool` | Ensures `OnAppearing` pipeline runs once |
| `_appData` … `_services` | services | Injected singletons (see constructor) |

---

### XAML elements

| Name | Role |
| --- | --- |
| `LoadingLabel` | Status text (`LocalizationKeys.Loading_Text`) |
| `ActivityIndicator` | Indeterminate spinner (`ActionBlue`) |

Layout: centered `VerticalStackLayout` on `BackgroundPrimary`.

---

## Constructor

#### `LoadingPage(AppDataStore, NotificationCenter, UpdateScheduler, AslmApiServer, AslmModuleInteropServer, ThemeService, CustomThemesStore, ModuleTrustService, AppLocalizationService, IServiceProvider)`

**Purpose:** Calls `InitializeComponent()`, stores services, **`LocalizableAttach.Hook(this, _localization, this)`**.

---

## Public methods

#### `void ApplyLocalization()`

**Purpose:** Sets `LoadingLabel.Text` from **`L.Get(LocalizationKeys.Loading_Text)`**.

---

#### `protected override async void OnAppearing()`

**Purpose:** If `_initialized`, returns immediately. Otherwise:

| Step | Call | Thread |
| --- | --- | --- |
| 1 | `_appData.InitializeAsync()` | background |
| 2 | `_localization.ApplyCulture()` | UI |
| 3 | `_moduleTrustService.InitializeAsync()` | background |
| 4 | `_customThemesStore.LoadAsync()` | background |
| 5 | `_notifications.InitializeAsync()` | background |
| 6 | `_apiServer.StartIfEnabledAsync()` | background |
| 7 | `_moduleInteropServer.EnsureStartedAsync()` | background |
| 8 | `_updateScheduler.Start()` | background |
| 9 | `_themeService.ApplyFromSettings()` | UI |

**Navigation:**

```csharp
Page nextPage = _appData.IsFirstRun
    ? _services.GetRequiredService<SetupWizardPage>()
    : _services.GetRequiredService<AppShellPage>();
```

If `nextPage` implements **`ILocalizable`**, calls **`ApplyLocalization()`** before assign. Sets **`Window.Page = nextPage`** and **`_localization.SyncFlowDirection()`**.

---

## Registration

Registered as **transient** in [MauiProgram](../MauiProgram/). Resolved only via DI (constructor on [App](../App/) uses `GetRequiredService<LoadingPage>()`).
