---
title: "Windows"
draft: false
---

Windows **WinUI 3** host for the MAUI application (`ASLM/Platforms/Windows/`).

## Files

| File | Role |
| --- | --- |
| [App](App/) | `MauiWinUIApplication` — calls `MauiProgram.CreateMauiApp()` |
| `App.xaml` | WinUI application definition |
| `Package.appxmanifest` | MSIX identity, capabilities, visual elements (build placeholders) |
| `app.manifest` | Win32 compatibility / DPI manifest |

Namespace for the entry type is **`ASLM.WinUI`** (separate from **`ASLM.App`** MAUI application class).

## Startup chain

```
ASLM.exe (launcher) → App\ASLM.exe
  → WinUI App (Platforms/Windows/App.xaml.cs)
  → MauiProgram.CreateMauiApp()
  → ASLM.App (MAUI Application)
```

See [App (Windows)](App/) for the WinUI wrapper API.
