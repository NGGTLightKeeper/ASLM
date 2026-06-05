---
title: "ThemeColorPickerView"
draft: false
---

## Class `ThemeColorPickerView`

`ASLM/Pages/ThemeColorPickerView.xaml` + `ThemeColorPickerView.xaml.cs` — modal **HSV color picker** (~360×480 card) for custom theme editing in [SettingsView](SettingsView/). Same dimmed overlay pattern as settings/downloads dialogs.

Implements **`ILocalizable`**.

HS plane: saturation/hue field depends only on **`_value`** and size — painted on bottom **`HsGradient`** (not invalidated every pointer move). Selector ring on top **`HsCursor`**.

---

### Fields

| Name | Description |
| --- | --- |
| `_completion` | `TaskCompletionSource<Color?>` |
| `_modalHostPage` | Temporary modal **`ContentPage`** |
| `_hue`, `_saturation`, `_value`, `_alpha` | Normalized HSV + alpha |
| `_hsPointerDown` | Pointer drag state |
| `_suppressSync` | Guards slider ↔ text loops |

---

### XAML elements

| Name | Role |
| --- | --- |
| (root) | `OverlayBackground`, backdrop tap → cancel |
| `PickerCard` | Centered card; **`OnCardTapped`** swallows taps |
| `TitleLabel` | `ThemeColorPicker_CustomColor` |
| `HsPlaneHost` | Hosts gradient + cursor layers |
| `HsGradient` | `GraphicsView` + **`HsGradientDrawable`** (`InputTransparent`) |
| `HsCursor` | `GraphicsView` + **`HsCursorDrawable`** (pointer gestures) |
| `BrightnessLabel`, `ValueSlider` | Brightness (`_value`) — invalidates gradient |
| `OpacityLabel`, `AlphaSlider` | Opacity (`_alpha`) |
| `PreviewBorder`, `PreviewFill` | Live swatch + contrast stroke |
| `HexEntry`, `REntry`, `GEntry`, `BEntry` | Manual entry |
| `DoneButton`, `CancelButton` | Complete / cancel (settings footer styles) |

---

## Constructor

#### `ThemeColorPickerView(Color initial, AppLocalizationService localization)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | `InitializeComponent()`, **`LocalizableAttach.Hook`** |
| 2 | **`RgbToHsv(initial)`** → `_hue`, `_saturation`, `_value`; `_alpha = initial.Alpha` |
| 3 | Assign **`HsGradientDrawable`**, **`HsCursorDrawable`** |
| 4 | `HsPlaneHost.SizeChanged` → invalidate both graphics views |
| 5 | Pointer recognizer on **`HsCursor`** (pressed/moved/released) |
| 6 | Slider / text change handlers |
| 7 | Footer button styles + **`CompleteAsync`** handlers |
| 8 | Seed sliders, **`SyncFromHsv()`**, invalidate graphics |

---

## Member reference
#### `void ApplyLocalization()`

**Purpose:** Title, brightness/opacity labels, hex placeholder, Done/Cancel.

---

#### `public Task<Color?> WaitForResultAsync()`

**Purpose:** Returns **`_completion.Task`**.

---

#### `public static async Task<Color?> PickAsync(Color initial)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Resolve host **`Page`** and **`AppLocalizationService`** from app |
| 2 | 
ew ThemeColorPickerView(initial, localization)` |
| 3 | Wrap in transparent modal **`ContentPage`**, **`AttachModalHost`** |
| 4 | **`PushModalAsync`**, **`await WaitForResultAsync()`** |

Not registered in [MauiProgram](../MauiProgram/) DI.

---

#### `internal void AttachModalHost(Page modalHost)`

**Purpose:** Stores host; **`Disappearing`** → complete with 
ull` if still pending.

---

#### `private void OnModalHostDisappearing(object? sender, EventArgs e)`

**Purpose:** **`_completion.TrySetResult(null)`** if not completed.

---

#### `private void OnBackdropTapped(object? sender, TappedEventArgs e)`

**Purpose:** **`CompleteAsync(null)`**.

---

#### `private static void OnCardTapped(object? sender, TappedEventArgs e)`

**Purpose:** No-op — prevents backdrop close.

---

#### `private void OnHsPointerPressed(object? sender, PointerEventArgs e)`

**Purpose:** **`_hsPointerDown = true`**, **`ApplyHsFromPointer(..., syncTextFields: true)`**.

---

#### `private void OnHsPointerMoved(object? sender, PointerEventArgs e)`

**Purpose:** If down: **`ApplyHsFromPointer(..., syncTextFields: false)`** (preview + hex/RGB only).

---

#### `private void OnHsPointerReleased(object? sender, PointerEventArgs e)`

**Purpose:** Clears down flag; **`ApplyHsFromPointer(..., syncTextFields: true)`**.

---

#### `private void ApplyHsFromPointer(PointerEventArgs e, bool syncTextFields)`

**Purpose:** Maps position in **`HsPlaneHost`** to **`_hue`**, **`_saturation`**; **`HsCursor.Invalidate()`**; full **`SyncFromHsv()`** or preview + **`SyncHexRgbEntriesFromHsv()`**.

---

#### `private void SyncHexRgbEntriesFromHsv()`

**Purpose:** Updates hex/RGB entries from **`GetCurrentColor()`** without moving sliders.

---

#### `private Color GetCurrentColor()`

**Purpose:** **`HsvToColor(_hue, _saturation, _value, _alpha)`**.

---

#### `private void ApplyPreviewColor(Color c)`

**Purpose:** **`PreviewFill.BackgroundColor`**, **`PreviewBorder.Stroke`** via **`ThemePaletteResolver.SwatchContrastStroke`**.

---

#### `private void SyncFromHsv()`

**Purpose:** Preview, hex/RGB, value/alpha sliders (with **`_suppressSync`**).

---

#### `private async Task CompleteAsync(Color? result)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Guard if already completed |
| 2 | **`TrySetResult(result)`** |
| 3 | Unhook modal **`Disappearing`**, **`PopModalAsync`** if top of stack |

---

#### `private void OnHexTextChanged(object? sender, TextChangedEventArgs e)`

**Purpose:** **`ThemePaletteResolver.TryParseHex`** → update HSV/alpha, invalidate HS views, sync preview/RGB/sliders.

---

#### `private void OnRgbTextChanged(object? sender, TextChangedEventArgs e)`

**Purpose:** Parse R/G/B → **`RgbToHsv`**, update sliders and HS views, sync hex/preview.

---

#### `private static void ApplyFooterButtonStyle(Button button, bool isPrimary)`

**Purpose:** Applies **`SettingsFooterPrimaryButtonStyle`** or **`SettingsFooterButtonStyle`**.

---

#### `private static void RgbToHsv(Color color, out double h, out double s, out double v)`

**Purpose:** Standard RGB → HSV (normalized 0–1).

---

#### `private static Color HsvToColor(double h, double s, double v, double a)`

**Purpose:** HSV hex-sector interpolation → **`Color`**.

---

## Nested `HsGradientDrawable`

#### `void Draw(ICanvas canvas, RectF dirtyRect)`

**Purpose:** 3×3 pixel blocks across plane; hue from x, saturation from inverted y, fixed **`_owner._value`**.

---

## Nested `HsCursorDrawable`

#### `void Draw(ICanvas canvas, RectF dirtyRect)`

**Purpose:** White/black concentric circles at **`(_hue * w, (1 - _saturation) * h)`**.

---

## Usage from settings

```csharp
var picked = await ThemeColorPickerView.PickAsync(currentColor);
if (picked is Color c) { /* apply to custom theme */ }
```

---

## Dependencies

`AppLocalizationService`, **`ThemePaletteResolver`**, **`ThemeService`** (indirect via contrast stroke).
