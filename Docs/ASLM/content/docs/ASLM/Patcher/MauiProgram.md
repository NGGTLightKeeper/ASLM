---
title: "MauiProgram"
draft: false
---

## Class `MauiProgram`

`Patcher/MauiProgram.cs` — **`public static`** MAUI host bootstrap.

---

## Public methods
#### `public static MauiApp CreateMauiApp()

**Purpose:** See steps below.

| Step | Call |
| --- | --- |
| 1 | `MauiApp.CreateBuilder()` |
| 2 | `builder.UseMauiApp<App>()` |
| 3 | `#if DEBUG` → `builder.Logging.AddDebug()` |
| 4 | `builder.Build()` |

No DI services registered (patch logic is static `PatcherRunner`).

---

## Related

- [Platforms/Windows/App](Platforms/Windows/App/) — `CreateMauiApp()` entry on Windows
