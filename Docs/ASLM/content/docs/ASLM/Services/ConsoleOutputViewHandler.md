---
title: "ConsoleOutputViewHandler"
draft: false
---

## Class `ConsoleOutputViewHandler`

`ASLM/Services/ConsoleOutputViewHandler.cs` — **`public sealed`** — MAUI **`ViewHandler<ConsoleOutputView, TextBox>`** for WinUI console output with stable selection, sizing, and autoscroll.

Maps [ConsoleOutputView](ConsoleOutputView/) properties **`Text`** and **`SessionKey`** via static **`Mapper`**.

Uses nested **`ConsoleOutputHostTextBox`** to throttle high-Hz mouse **`PointerMoved`** during text selection.

---

### Constants

| Name | Role |
| --- | --- |
| `ConsoleSurfaceColorKey` | `BackgroundSecondary` |
| `ConsoleTextColorKey` | `LabelPrimary` |
| `ConsoleSelectionColorKey` | `SystemBlueOverlay` |

---

## Public methods — handler lifecycle

#### `public ConsoleOutputViewHandler() : base(Mapper)`

**Purpose:** Creates handler with property mapper for text and session key.

---

#### `protected override TextBox CreatePlatformView()`

**Purpose:** Builds **`ConsoleOutputHostTextBox`**: read-only, Consolas 12, wrap, zero border, themed chrome via **`ApplyConsoleTheme`**.

---

#### `protected override void ConnectHandler(TextBox platformView)`

**Purpose:** Subscribes to app theme, **`ThemeService.PaletteApplied`**, loaded/size/text/pointer/selection events; **`EnsureScrollViewer`**, **`ApplySessionKey`**, **`ApplyText`**, initial viewport refresh (3 passes).

---

#### `protected override void DisconnectHandler(TextBox platformView)`

**Purpose:** Unsubscribes all handlers, clears cached **`ScrollViewer`**.

---

## Private methods — theme

#### `private void OnApplicationRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)`

**Purpose:** Calls **`RefreshConsoleTheme`** on OS theme change.

---

#### `private void OnPaletteApplied()`

**Purpose:** Calls **`RefreshConsoleTheme`** after palette rewrite.

---

#### `private void RefreshConsoleTheme()`

**Purpose:** **`MainThread.BeginInvokeOnMainThread`** → **`ApplyConsoleTheme(PlatformView)`**.

---

#### `private static void ApplyConsoleTheme(TextBox textBox)`

**Purpose:** Sets background, foreground, selection highlight; **`ApplyConsoleChrome`** for TextControl resource brushes.

---

#### `private static Color ResolveThemeColor(string key)`

**Purpose:** Reads MAUI **`Application.Current.Resources`**, else **`ThemePaletteResolver`** light/dark palette.

---

#### `private static WinUIColor ToWinUiColor(Color color)`

**Purpose:** Converts MAUI **`Color`** to WinUI **`Windows.UI.Color`**.

---

#### `private static void ApplyConsoleChrome(TextBox textBox, WinUIColor surfaceColor, WinUIColor textColor)`

**Purpose:** Overrides TextControl background/foreground/border/header/placeholder brushes for hover/focus/disabled states.

---

## Private methods — platform events

#### `private void OnPlatformViewLoaded(object sender, RoutedEventArgs e)`

**Purpose:** **`EnsureScrollViewer`**, sets **`_pendingScrollToEnd`**, queues 3-pass viewport refresh.

---

#### `private void OnPlatformViewSizeChanged(object sender, SizeChangedEventArgs e)`

**Purpose:** Skips when user blocks autoscroll; queues scroll-to-end (2 passes) when near bottom or pending, else layout-only refresh.

---

#### `private void OnPlatformViewTextChanged(object sender, TextChangedEventArgs e)`

**Purpose:** Ignored when **`_suppressNativeTextChanged`**; defers or queues scroll when **`_pendingScrollToEnd`**.

---

#### `private void OnScrollViewerViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)`

**Purpose:** Updates **`_isNearBottom`** unless programmatic scroll in progress.

---

#### `private void OnPlatformViewPointerPressed(object sender, PointerRoutedEventArgs e)`

**Purpose:** Sets **`_pointerDownOnConsole = true`**.

---

#### `private void OnPlatformViewPointerEnded(object sender, PointerRoutedEventArgs e)`

**Purpose:** Clears pointer flag; **`TryFlushDeferredScroll`**.

---

#### `private void OnPlatformViewPointerCaptureLost(object sender, PointerRoutedEventArgs e)`

**Purpose:** Same as pointer ended.

---

#### `private void OnPlatformViewSelectionChanged(object sender, RoutedEventArgs e)`

**Purpose:** **`TryFlushDeferredScroll`**.

---

## Private methods — scroll and viewport

#### `private bool UserBlocksAutoscrollForScroll()`

**Purpose:** **`true`** while pointer down or **`SelectionLength > 0`**.

---

#### `private void TryFlushDeferredScroll()`

**Purpose:** When deferred scroll pending and user no longer blocks → **`QueueViewportRefresh`** (3 passes).

---

#### `private void ApplySessionKey(string? sessionKey)`

**Purpose:** On session change: reset scroll flags, pin to end, queue 3-pass refresh.

---

#### `private void ApplyText(string? text)`

**Purpose:** Updates native text (suppresses recursive **`TextChanged`**); pins to bottom when forced, near bottom, or empty; defers when user is selecting.

---

#### `private void QueueViewportRefresh(bool scrollToEnd, int passCount)`

**Purpose:** Coalesces dispatcher passes (1–3) via **`_viewportRefreshQueued`** / **`QueueViewportRefreshPass`**.

---

#### `private void QueueViewportRefreshPass()`

**Purpose:** One dispatcher tick: **`RefreshViewport`**, optional **`ScrollToEnd`**, chains remaining passes.

---

#### `private void RefreshViewport()`

**Purpose:** **`UpdateLayout`** on text box and scroll viewer when not blocked by pointer/selection.

---

#### `private void ScrollToEnd()`

**Purpose:** **`ScrollViewer.ChangeView`** to max vertical offset, or caret to end if no viewer yet. Clears pending/defer flags.

---

#### `private bool IsNearBottom()`

**Purpose:** Within 8 px of **`ScrollableHeight`** or no scrollable content.

---

#### `private void EnsureScrollViewer()`

**Purpose:** Finds descendant **`ScrollViewer`**, hooks **`ViewChanged`**, initializes **`_isNearBottom`**.

---

## Private methods — visual tree

#### `private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)`

**Purpose:** Depth-first search for first **`ScrollViewer`**.

---

## Nested class `ConsoleOutputHostTextBox`

#### `protected override void OnPointerPressed(PointerRoutedEventArgs e)`

**Purpose:** Resets throttle baseline so first move after press is forwarded.

---

#### `protected override void OnPointerMoved(PointerRoutedEventArgs e)`

**Purpose:** For mouse LMB drag: forwards **`base.OnPointerMoved`** at most every **`PointerMoveMinIntervalMs`** (10 ms); sets **`Handled`** on skipped high-rate reports.

---

## Related

- [ConsoleOutputView](ConsoleOutputView/)
- [ModuleConsoleStore](ModuleConsoleStore/)
- [ThemeService](ThemeService/)
