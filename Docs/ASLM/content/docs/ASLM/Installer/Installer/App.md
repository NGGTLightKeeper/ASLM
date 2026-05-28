---
title: "App"
draft: false
---

## Class `App`

`Installer/Installer/App.xaml` + `App.xaml.cs` — **`public partial class App : Application`**. Theme brushes in `App.xaml` (dark palette aligned with host).

---

## Constructor

#### `public App()`

**Purpose:** `InitializeComponent()` only.

---

## Public methods
#### `protected override Window CreateWindow(IActivationState? activationState)`

| Setting | Value |
| --- | --- |
| Content | `new MainPage()` |
| Title | `ASLM Installer` |
| Width / Height | 720 × 540 |
| Min / Max | Same as width/height (fixed dialog size) |

Wizard logic lives on [MainPage](MainPage/), not here.

---

## Related

- [MainPage](MainPage/)
- [MauiProgram](MauiProgram/)
