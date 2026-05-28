---
title: "App"
draft: false
---

## Class `App`

`ASLM/App.xaml.cs` — **`public partial class App : Application`**. Receives root **`IServiceProvider`**, creates the main window, runs shutdown cleanup on destroy.

---

### Fields

| Name | Type | Role |
| --- | --- | --- |
| `_services` | `IServiceProvider` | DI for pages and shutdown services |
| `_isShuttingDown` | `bool` | One-shot guard for `OnWindowDestroying` |

---

## Constructor

#### `public App(IServiceProvider services)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `InitializeComponent()` |
| 2 | Store `_services` |

---

## Member reference
#### `protected override Window CreateWindow(IActivationState? activationState)`

| Setting | Value |
| --- | --- |
| Content | `CreateStartupPage()` |
| Title | `ASLM` |
| Minimum size | 1280 × 720 |

Subscribes **`window.Destroying`** → `OnWindowDestroying`.

---

#### `public Page CreateStartupPage()`

**Purpose:** Returns **`_services.GetRequiredService<LoadingPage>()`**. Loading page continues startup (wizard or shell).

---

#### `private void OnWindowDestroying(object? sender, EventArgs e)

**Purpose:** Graceful teardown when the main window closes.

| Step | Action |
| --- | --- |
| 1 | Return if `_isShuttingDown` |
| 2 | `_isShuttingDown = true` |
| 3 | `ModuleRunner.StopAllModulesAsync().GetAwaiter().GetResult()` then `Dispose()` |
| 4 | `ProcessTracker.Dispose()` |
| 5 | `UpdateScheduler?.Dispose()` |
| 6 | `AslmApiServer?.Dispose()` |
| 7 | `AslmModuleInteropServer?.Dispose()` |
| 8 | On exception → `Debug.WriteLine` only (does not block close) |

---

## Related

- [MauiProgram](MauiProgram/)
- [LoadingPage](Pages/LoadingPage/)
