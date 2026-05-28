---
title: "NotificationsView"
draft: false
---

## Class `NotificationsView`

`ASLM/Pages/NotificationsView.xaml` + `NotificationsView.xaml.cs` — anchored popover over the shell (not full-screen). Lists **`AppNotification`** rows from **`NotificationCenter`**.

Implements **`ILocalizable`**, **`INotifyPropertyChanged`**.

`BindingContext`: **`this`**.

---

### Layout constants

| Name | Value | Matches XAML |
| --- | --- | --- |
| `PopupWidth` | `360` | `NotificationsPopup.WidthRequest` |
| `PopupHeight` | `480` | Height |
| `PopupGap` | `21` | Offset right of anchor |
| `ScreenPadding` | `14` | Clamp inside host |

---

### Fields

| Name | Description |
| --- | --- |
| `_lastAnchorBounds` | Last shell button rect for reposition |
| `_popupPositioningActive` | Reposition on host resize |
| `_dismissIconSource` | Themed row dismiss glyph |

---

### XAML elements

| Name | Role |
| --- | --- |
| (root grid) | Transparent; backdrop tap → close |
| `NotificationsPopup` | Card; positioned via **`Margin`** in code |
| `TitleLabel` | `Notifications_Title` |
| `ClearAllButton` | **`DismissAll`** |
| Close button (header) | **`OnCloseClicked`** |
| `CollectionView` | `ItemsSource={Binding VisibleNotifications}` |
| `EmptyTitleLabel`, `EmptyMessageLabel` | Empty state |
| Footer labels | `SummaryText`, `SectionTitle` bindings |

Item template: dismiss, optional update actions when notification offers them.

---

## Constructor

#### `NotificationsView(NotificationCenter notifications, AppLocalizationService localization)

**Purpose:** See steps below.

| Step | Action |
| --- | --- |
| 1 | Store services |
| 2 | `InitializeComponent()`, `BindingContext = this` |
| 3 | `Loaded` / `Unloaded`, **`LocalizableAttach.Hook`** |
| 4 | **`RefreshDismissIconChrome()`** |

---

## Events

#### `event EventHandler? CloseRequested`

**Purpose:** Shell hides overlay when raised.

---

## Member reference
#### `void ApplyLocalization()`

**Purpose:** Header, clear-all, empty state; **`RefreshLocalizedPresentation()`** on each visible notification; refresh **`SummaryText`**, **`SectionTitle`**.

---

#### `public Task OpenAtAsync(Rect anchorBounds, double hostWidth, double hostHeight)`

**Purpose:** Stores anchor, enables positioning, **`PositionPopup`**, **`RefreshVisibleNotifications()`**.

---

#### `public Task RefreshAsync()`

**Purpose:** **`RefreshVisibleNotifications()`** before show.

---

#### `private void OnLoaded(object? sender, EventArgs e)`

**Purpose:** Subscribe theme + **`NotificationsChanged`** + **`SizeChanged`**; refresh chrome and list.

---

#### `private void OnUnloaded(object? sender, EventArgs e)`

**Purpose:** Unsubscribe; **`_popupPositioningActive = false`**.

---

#### `private void OnPaletteAppliedForDismissIcon()`

**Purpose:** **`MainThread.BeginInvokeOnMainThread(RefreshDismissIconChrome)`**.

---

#### `private void OnApplicationRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e)`

**Purpose:** **`MainThread.BeginInvokeOnMainThread(RefreshDismissIconChrome)`**.

---

#### `private void RefreshDismissIconChrome()`

**Purpose:** **`DismissIconSource = PackagedIconTintCache.Get("icon_delete.png", ...)`**.

---

#### `private void OnHostSizeChanged(object? sender, EventArgs e)`

**Purpose:** If positioning active, re-**`PositionPopup`** with last anchor and current **`Width`/`Height`**.

---

#### `private void PositionPopup(Rect anchorBounds, double hostWidth, double hostHeight)`

**Purpose:** Sets **`NotificationsPopup.Margin`** to place card to the right of anchor, vertically centered, clamped to host bounds.

---

#### `private void OnBackgroundTapped(object? sender, EventArgs e)`

**Purpose:** **`RequestClose()`**.

---

#### `private void OnDialogTapped(object? sender, EventArgs e)`

**Purpose:** No-op — keeps taps inside popover.

---

#### `private void OnCloseClicked(object? sender, EventArgs e)`

**Purpose:** **`RequestClose()`**.

---

#### `private void RequestClose()`

**Purpose:** Raises **`CloseRequested`**.

---

#### `private void OnClearAllClicked(object? sender, EventArgs e)`

**Purpose:** **`_notifications.DismissAll()`**.

---

#### `private void OnDismissClicked(object? sender, EventArgs e)`

**Purpose:** Walks visual tree for **`AppNotification`** binding context → **`Dismiss`**.

---

#### `private void OnUpdateNotificationNowClicked(object? sender, EventArgs e)`

**Purpose:** **`RequestUpdateNotificationAction(notification, updateNow: true)`**.

---

#### `private void OnUpdateNotificationLaterClicked(object? sender, EventArgs e)`

**Purpose:** **`RequestUpdateNotificationAction(notification, updateNow: false)`**.

---

#### `private static bool TryFindNotificationBindingContext(object? sender, out AppNotification notification)`

**Purpose:** Parent-walk from sender for **`BindingContext`**.

---

#### `private void OnNotificationsChanged(object? sender, EventArgs e)`

**Purpose:** **`RefreshVisibleNotifications()`**.

---

#### `private void RefreshVisibleNotifications()`

**Purpose:** **`RaiseViewStateProperties()`**.

---

#### `private void RaiseViewStateProperties()`

**Purpose:** **`OnPropertyChanged`** for **`SummaryText`**, **`SectionTitle`**, **`HasVisibleNotifications`**, **`IsEmptyVisible`**, **`HasAnyNotifications`**.

---

#### `protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** INPC raise.

---

## Bound properties

| Property | Description |
| --- | --- |
| `VisibleNotifications` | **`ReadOnlyObservableCollection`** → `NotificationCenter.Notifications` (granular CollectionView updates) |
| `SummaryText` | Footer count or empty title |
| `SectionTitle` | `Notifications_SectionRecent` |
| `HasVisibleNotifications` / `IsEmptyVisible` | Empty vs list |
| `HasAnyNotifications` | Enables Clear all |
| `DismissIconSource` | Row dismiss icon |

---

## Dependencies

`NotificationCenter`, `AppLocalizationService`.
