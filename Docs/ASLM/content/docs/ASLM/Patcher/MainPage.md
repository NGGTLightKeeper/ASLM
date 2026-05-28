---
title: "MainPage"
draft: false
---

## Class `MainPage`

`Patcher/MainPage.xaml` + `MainPage.xaml.cs` — **`partial ContentPage`** showing live patch status.

---

### Constants

| Name | Value |
| --- | --- |
| `CloseAutomaticallyAfterPatch` | `true` — quit MAUI after ~900 ms when run completes |

---

### Fields

| Name | Type | Description |
| --- | --- | --- |
| `_log` | `StringBuilder` | Accumulated log lines |
| `_started` | `bool` | Ensures single `OnLoaded` run |

---

### XAML elements (referenced in code)

| Name | Role |
| --- | --- |
| `StatusLabel` | Current status headline |
| `SubtitleLabel` | `Finished` / `Failed` |
| `LogEditor` | Full log text |
| `LogScroll` | ScrollView for log |

---

## Constructor

#### `public MainPage()`

**Purpose:** `InitializeComponent()`; subscribes `Loaded += OnLoaded`.

---

## Member reference
#### `private async void OnLoaded(object? sender, EventArgs e)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Return if `_started` |
| 2 | `_started = true` |
| 3 | `args = Environment.GetCommandLineArgs().Skip(1)` |
| 4 | `exitCode = await PatcherRunner.RunAsync(args, progress)` |
| 5 | Set `StatusLabel` / `SubtitleLabel` from exit code |
| 6 | If `CloseAutomaticallyAfterPatch` → delay 900 ms → `Application.Current?.Quit()` |

Delegates work to [Program](Program/) (`PatcherRunner`).

---

#### `private void OnProgress(PatcherProgress progress)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `StatusLabel.Text = progress.Message` |
| 2 | Append line to `_log`; `LogEditor.Text = _log` |
| 3 | `MainThread.BeginInvokeOnMainThread` → `LogScroll.ScrollToAsync` bottom |

---

## Related

- [App](App/)
- [Program](Program/)
