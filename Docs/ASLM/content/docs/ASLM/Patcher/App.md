---
title: "App"
draft: false
---

## Class `App`

`Patcher/App.xaml` + `App.xaml.cs` — MAUI **`Application`** for the patcher.

---

## Constructor

#### `public App()`

**Purpose:** `InitializeComponent()` only. Window created in `CreateWindow`.

---

## Public methods
#### `protected override Window CreateWindow(IActivationState? activationState)`

| Setting | Value |
| --- | --- |
| Content | `new MainPage()` |
| Title | `ASLM Patcher` |
| Size | 720×540 fixed (`Min`/`Max` = same) |

Fixed size keeps the patcher a lightweight status dialog.

---

## Related

- [MainPage](MainPage/)
- [MauiProgram](MauiProgram/)
