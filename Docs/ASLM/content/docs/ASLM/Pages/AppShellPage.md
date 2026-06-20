---
title: "AppShellPage"
draft: false
---

## Class `AppShellPage`

`ASLM/Pages/AppShellPage.xaml` + `AppShellPage.xaml.cs` — **`public partial`** **`ContentPage`** — main host after setup. Collapsible sidebar, `ContentView` workspace, embedded **WebView2** for module UIs, toast stack, and full-span overlays.

Implements **`ILocalizable`**, **`INotifyPropertyChanged`**.

---

### Constants

| Name | Value | Role |
| --- | --- | --- |
| `PanelExpandedWidth` | `240` | Expanded sidebar width (px) |
| `PanelCollapsedWidth` | `48` | Collapsed sidebar width (px) |
| `SidebarIconLogicalSize` | `20` | WinUI icon size inside nav buttons |
| `IconMenu` | `icon_menu.png` | Collapse toggle |
| `IconHome` | `icon_home.png` | Home nav |
| `IconConsole` | `icon_console.png` | Consoles nav |
| `IconModules` | `icon_modules.png` | Modules nav |
| `IconApi` | `icon_api.png` | ASLM API nav |
| `IconNotifications` | `icon_notifications.png` | Notifications nav |
| `IconDownload` | `icon_download.png` | Downloads nav |
| `IconSettings` | `icon_settings.png` | Settings nav |
| `IconPage` | `icon_page.png` | Default module page icon |

Preference key **`SidebarExpanded`** (`bool`, default `false`) — restored before first render, saved on collapse toggle.

---

### Static property

#### `private static Color TransparentBackground`

**Purpose:** Returns **`Colors.Transparent`** — used as inactive module page button background.

---

### Fields

#### Injected services (readonly)

| Field | Type |
| --- | --- |
| `_moduleInstaller` | `ModuleInstaller` |
| `_moduleRunner` | `ModuleRunner` |
| `_appData` | `AppDataStore` |
| `_ports` | `PortRegistry` |
| `_notifications` | `NotificationCenter` |
| `_updateManager` | `UpdateManager` |
| `_moduleTrustService` | `ModuleTrustService` |
| `_apiServer` | `AslmApiServer` |
| `_moduleStartThrottle` | `ModuleStartThrottle` |
| `_moduleLaunchCoordinator` | `ModuleLaunchCoordinator` |
| `_settingsService` | `SettingsService` |
| `_localization` | `AppLocalizationService` |
| `_services` | `IServiceProvider` |

#### Shell state

| Field | Type | Role |
| --- | --- | --- |
| `_allModules` | `List<ModuleConfig>` | Latest `DiscoverModulesAsync` snapshot |
| `_activeModule` | `ModuleConfig?` | Module whose page is shown in `Browser` |
| `_panelExpanded` | `bool` | Sidebar expanded vs collapsed |
| `_hasLoaded` | `bool` | One-shot guard for `OnPageLoaded` |
| `_activeNavButton` | `Button?` | Highlighted shell nav button |
| `_navButtons` | `Button[]` | Fixed nav buttons (home … settings) |
| `_shellEventsHooked` | `bool` | Service events subscribed |
| `_moduleRefreshQueued` | `int` | Coalesce flag for `RefreshModulesAsync` |
| `_moduleRefreshLock` | `SemaphoreSlim(1,1)` | Serializes module refresh |
| `_sidebarButtonLayoutCts` | `CancellationTokenSource?` | Cancels deferred layout passes |

#### Lazy views

| Field | Type |
| --- | --- |
| `_homeView` | `View?` |
| `_consolesView` | `View?` |
| `_modulesView` | `View?` |
| `_aslmApiView` | `View?` |
| `_notificationsView` | `View?` |
| `_downloadsView` | `View?` |
| `_settingsView` | `View?` |
| `_moduleUpdateView` | `View?` |

#### Module browser

| Field | Type | Role |
| --- | --- | --- |
| `_moduleBrowserUrl` | `string?` | Last applied MAUI `WebView` URL |
| `_moduleBrowserExpectedUrl` | `string?` | Authoritative target URL |
| `_moduleBrowserNavigationSequence` | `int` | Invalidates stale navigations |
| `_pendingModuleBrowserUrl` | `string?` | WinUI only — URL before CoreWebView2 ready |
| `_moduleWebView2` | `WebView2?` | WinUI native reference |
| `_moduleWebViewNavigationHooked` | `bool` | WinUI — `NavigationCompleted` wired |

---

### XAML layout

Root: **`Grid`** `ColumnDefinitions="Auto, *"`.

| Region | `x:Name` | Role |
| --- | --- | --- |
| Sidebar | `SidePanel` | `Grid.Column=0`, rows: collapse, scroll, separator, system nav |
| Collapse | `CollapseButton` | `Clicked="OnCollapseClicked"` |
| Module pages | `ModulePagePanel` | Inside `ScrollView`; dynamic buttons from code |
| System nav | `HomeButton`, `ConsolesButton`, `ModulesButton`, `AslmApiButton`, `NotificationsButton`, `DownloadsButton`, `SettingsButton` | `Clicked="OnNavClicked"` |
| Main | `ContentArea` | `ContentView` — shell views |
| Module UI | `Browser` | `WebView`, `IsVisible=False`, `Navigating`, `HandlerChanged` (WinUI) |
| Overlay | `OverlayContainer` | `Grid.ColumnSpan=2`, transparent full-shell host |
| Toasts | `ToastPanel` | Bottom-right stack, 360px wide, max ~4 children |

`Browser`: **`FlowDirection="LeftToRight"`** — reinforced in code after RTL culture changes.

---

## Constructor

#### `public AppShellPage(ModuleInstaller moduleInstaller, ModuleRunner moduleRunner, AppDataStore appData, PortRegistry ports, NotificationCenter notifications, UpdateManager updateManager, ModuleTrustService moduleTrustService, AslmApiServer apiServer, ModuleStartThrottle moduleStartThrottle, ModuleLaunchCoordinator moduleLaunchCoordinator, SettingsService settingsService, AppLocalizationService localization, IServiceProvider services)

**Purpose:** Creates the application shell and restores the saved sidebar state.

| Step | Action |
| --- | --- |
| 1 | Store injected services |
| 2 | `InitializeComponent()` |
| 3 | `LocalizableAttach.Hook(this, _localization, this)` |
| 4 | `BindingContext = this` |
| 5 | Subscribe `Loaded` → `OnPageLoaded`, `Unloaded` → `OnPageUnloaded` |
| 6 | Read `Preferences.Default.Get("SidebarExpanded", false)` → set `SidePanel.WidthRequest` |
| 7 | Build `_navButtons` array from named XAML buttons |
| 8 | Hook `CollapseButton` + each nav `HandlerChanged` → `OnSidebarButtonHandlerChanged` |
| 9 | Assign packaged `ImageSource` to each static nav button |
| 10 | `ApplySidebarButtonIconFromPalette(CollapseButton, "LabelPrimary")` |
| 11 | `ApplySidebarButtonLayout()` + `ScheduleSidebarButtonLayoutRefresh()` |
| 12 | `ApplyAslmApiNavigationState()`, `ApplyConsoleNavigationState()` |
| 13 | Subscribe `_localization.CultureChanged` |

---

## Property change

#### `public new event PropertyChangedEventHandler? PropertyChanged`

**Purpose:** Shell-level **`INotifyPropertyChanged`** surface (hides base `ContentPage` member).

---

#### `protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)`

**Purpose:** Raises **`PropertyChanged`** with the optional caller-supplied property name.

---

## Lifecycle

#### `private async void OnPageLoaded(object? sender, EventArgs e)

**Purpose:** Loads module state once and opens the default shell view.

| Step | Action |
| --- | --- |
| 1 | `HookShellEvents()` |
| 2 | If `_hasLoaded`, return |
| 3 | Set `_hasLoaded = true` |
| 4 | `ScheduleSidebarButtonLayoutRefresh()` |
| 5 | `await RefreshModulesAsync()` |
| 6 | `NavigateTo(HomeButton)` |
| 7 | Re-apply `ApplyAslmApiNavigationState()`, `ApplyConsoleNavigationState()` |
| 8 | `ScheduleEnsureModuleBrowserLeftToRight()` |
| 9 | Fire-and-forget `StartEnabledModulesAsync()` |

---

#### `private void OnPageUnloaded(object? sender, EventArgs e)

**Purpose:** Unhooks shell-level events when the page leaves the visual tree.

| Step | Action |
| --- | --- |
| 1 | Unsubscribe `_localization.CultureChanged` |
| 2 | `UnhookShellEvents()` |
| 3 | WinUI: `ReleaseModuleWebViewDropTarget()` |

---

#### `private void HookShellEvents()`

**Purpose:** Hooks shell-wide service events once per visual lifetime.

If `_shellEventsHooked`, return. Otherwise subscribe:

| Source | Event | Handler |
| --- | --- | --- |
| `_notifications` | `NotificationPublished` | `OnNotificationPublished` |
| `_notifications` | `UpdateNotificationActionRequested` | `OnUpdateNotificationActionRequested` |
| `_apiServer` | `StateChanged` | `OnApiServerStateChanged` |
| `_moduleInstaller` | `ModulesChanged` | `OnModulesChanged` |
| `_ports` | `PortsRedistributed` | `OnPortsRedistributed` |
| `ThemeService` | `PaletteApplied` | `OnAppPaletteApplied` |

Set `_shellEventsHooked = true`.

---

#### `private void UnhookShellEvents()`

**Purpose:** Unhooks shell-wide service events when the page leaves the visual tree.

If not `_shellEventsHooked`, return. Otherwise unsubscribe the same events as **`HookShellEvents`** and set `_shellEventsHooked = false`.

---

#### `private void OnAppPaletteApplied()

**Purpose:** Refreshes sidebar icon tints after the application palette is rewritten.

| Step | Action |
| --- | --- |
| 1 | `ApplySidebarButtonLayout()` + `ScheduleSidebarButtonLayoutRefresh()` |
| 2 | `ApplyShellNavInactiveStyle` on every `_navButtons` entry |
| 3 | `ApplyShellNavInactiveStyle` on every `ModulePagePanel` `Button` |
| 4 | If `_activeNavButton` set → `ApplyShellNavActiveStyle` on it |
| 5 | Else if `_activeModule` set → find `ClassId="PAGE"` button with matching `AutomationId` → apply active style |

---

## Module refresh

#### `private async Task RefreshModulesAsync()`

**Purpose:** Reloads modules, rebuilds page buttons, and refreshes the dashboard if needed.

Uses coalescing: sets `_moduleRefreshQueued`; if lock not acquired immediately, returns (another refresh in flight). Loop:

| Step | Action |
| --- | --- |
| 1 | Clear queued flag |
| 2 | `DiscoverModulesAsync` on thread pool |
| 3 | `MainThread.InvokeOnMainThreadAsync(() => ApplyModules(modules))` |
| 4 | Repeat while another refresh was queued during the pass |

Always releases `_moduleRefreshLock` in `finally`.

---

#### `private void ApplyModules(List<ModuleConfig> modules)`

**Purpose:** Applies the latest module API snapshot to the sidebar and loaded module-backed views.

| Step | Action |
| --- | --- |
| 1 | Preserve `_activeModule` by matching `SourcePath` in new list |
| 2 | `BuildPageButtons()` |
| 3 | If active module disabled → `NavigateTo(HomeButton)` |
| 4 | Else if active enabled module + `Browser.IsVisible` and URL mismatch → `NavigateModuleBrowser` |
| 5 | Refresh `ModulesView`, `ConsolesView`, `HomeView`, `AslmApiView` when cached |

---

#### `private void OnModulesChanged(object? sender, EventArgs e)`

**Purpose:** Refreshes module-backed UI after a module manifest is saved. Fire-and-forget **`RefreshModulesAsync()`**.

---

#### `internal void OnModuleStateChanged()`

**Purpose:** Refreshes module page buttons after a module changes state. Fire-and-forget **`RefreshModulesAsync()`**.

---

#### `private void OnPortsRedistributed(object? sender, EventArgs e)`

**Purpose:** Reloads the embedded module browser when the shared port map changes.

On main thread: if `_activeModule` has page and `Browser.IsVisible` → **`NavigateModuleBrowser(_ports.GetModuleUrl(activeModule))`**.

---

## Navigation (shell views)

#### `private void OnNavClicked(object? sender, EventArgs e)

**Purpose:** Routes button clicks to the shared navigation handler.

| `sender` button | Result |
| --- | --- |
| `DownloadsButton` | `OpenDownloadOverlay()` — does not change `ContentArea` |
| `NotificationsButton` | `OpenNotificationsOverlay()` |
| `SettingsButton` | `OpenSettingsOverlay()` |
| Any other `Button` | `NavigateTo(button)` |

---

#### `private void NavigateTo(Button navButton)

**Purpose:** Activates one shell view and updates button highlighting.

| Step | Action |
| --- | --- |
| 1 | `_activeModule = null` |
| 2 | `ClearModuleBrowserNavigationTarget()` |
| 3 | `Browser.IsVisible = false` |
| 4 | `ApplyShellNavInactiveStyle` on all `_navButtons` and `ModulePagePanel` buttons |
| 5 | `ApplyShellNavActiveStyle(navButton)`; `_activeNavButton = navButton` |
| 6 | `ContentArea.Content = GetViewForButton(navButton)` |

---

#### `private View GetViewForButton(Button button)

**Purpose:** Returns the cached shell view for the selected navigation button.

| Button | View | First-use behavior |
| --- | --- | --- |
| `HomeButton` | `HomeView` | `GetRequiredService`, `Initialize(this)` |
| `ConsolesButton` | `ConsolesView` | Lazy resolve; `RefreshAsync()` |
| `ModulesButton` | `ModulesView` | `Initialize(this, _allModules, _moduleInstaller, _moduleRunner)` |
| `DownloadsButton` | Current content or `HomeView` | Ensures home exists; does not switch area for overlay |
| `NotificationsButton` | Current content or `HomeView` | Same as downloads |
| `AslmApiButton` | `AslmApiView` | Lazy resolve; `RefreshAsync()` |
| Other | `Label` "Unknown page" | Fallback |

---

#### `internal void OpenHome()`

**Purpose:** Opens the home dashboard. Calls **`NavigateTo(HomeButton)`**.

---

#### `internal void OpenConsoles(string? moduleSourcePath = null)

**Purpose:** Opens the consoles page and optionally focuses one module.

| Step | Action |
| --- | --- |
| 1 | `NavigateTo(ConsolesButton)` |
| 2 | If `_consolesView` not `ConsolesView`, return |
| 3 | If `moduleSourcePath` empty → `RefreshAsync()` |
| 4 | Else → `ShowModuleAsync(moduleSourcePath)` |

---

#### `internal void OpenModules(string? moduleSourcePath = null)

**Purpose:** Opens the modules page and optionally scrolls one module card into view.

| Step | Action |
| --- | --- |
| 1 | `NavigateTo(ModulesButton)` |
| 2 | If path empty or `_modulesView` not `ModulesView`, return |
| 3 | `modulesView.FocusModule(moduleSourcePath)` |

---

## Overlays

#### `private void OpenSettingsOverlay()

**Purpose:** Opens the shared settings overlay and refreshes it before showing.

| Step | Action |
| --- | --- |
| 1 | Lazy `_settingsView` from DI |
| 2 | Wire `CloseRequested` → `OnSettingsCloseRequested` |
| 3 | `RefreshAsync()` |
| 4 | `OverlayContainer.Content = _settingsView`; `IsVisible = true` |

---

#### `private void OpenNotificationsOverlay()

**Purpose:** Opens the shared notifications overlay and refreshes it before showing.

| Step | Action |
| --- | --- |
| 1 | Lazy `_notificationsView` from DI |
| 2 | Wire `CloseRequested` → `OnNotificationsCloseRequested` |
| 3 | `GetElementBoundsInShell(NotificationsButton)` → `OpenAtAsync(bounds, Width, Height)` |
| 4 | Set overlay content and visibility |

---

#### `private void OpenDownloadOverlay()

**Purpose:** Opens the shared download overlay and refreshes it before showing.

| Step | Action |
| --- | --- |
| 1 | Lazy `_downloadsView` from DI |
| 2 | Wire `CloseRequested` → `OnDownloadCloseRequested` |
| 3 | `OpenAsync()` |
| 4 | Set overlay content and visibility |

---

#### `internal void OpenModuleUpdateOverlay(ModuleViewModel module, ModuleUpdateMode mode)`

**Purpose:** Opens the shared module update overlay for the selected module. Fire-and-forget **`OpenModuleUpdateOverlayAsync`**.

---

#### `private async Task OpenModuleUpdateOverlayAsync(ModuleViewModel module, ModuleUpdateMode mode)`

**Purpose:** Loads and shows the module update overlay before binding it to the selected module.

| Step | Action |
| --- | --- |
| 1 | Lazy `_moduleUpdateView`; return if not `ModuleUpdateView` |
| 2 | Wire `CloseRequested` → `OnModuleUpdateCloseRequested` |
| 3 | Show overlay container |
| 4 | `await moduleUpdateView.OpenAsync(module, mode)` |

---

#### `private void OnSettingsCloseRequested(object? sender, EventArgs e)

**Purpose:** Hides the overlay container when the settings view requests close.

| Step | Action |
| --- | --- |
| 1 | `OverlayContainer.IsVisible = false` |
| 2 | `ApplyConsoleNavigationState()` |
| 3 | Refresh `ConsolesView` and `HomeView` if cached |

---

#### `private void OnNotificationsCloseRequested(object? sender, EventArgs e)`

**Purpose:** Hides the overlay and clears content: `IsVisible = false`, `Content = null`.

---

#### `private void OnDownloadCloseRequested(object? sender, EventArgs e)`

**Purpose:** Hides the overlay: `OverlayContainer.IsVisible = false`.

---

#### `private void OnModuleUpdateCloseRequested(object? sender, EventArgs e)`

**Purpose:** Hides the overlay: `OverlayContainer.IsVisible = false`.

---

#### `private Rect GetElementBoundsInShell(VisualElement element)`

**Purpose:** Calculates one child element's bounds in the shell coordinate space.

Walks the MAUI visual parent chain from `element` up to (but not including) `this`, summing `Bounds.X/Y`. Returns **`Rect(x, y, width, height)`** for popover anchoring.

---

## Toasts

#### `private void OnNotificationPublished(object? sender, AppNotification notification)`

**Purpose:** Shows an in-app toast when a new notification is published. **`MainThread.BeginInvokeOnMainThread(() => ShowToast(notification))`**.

---

#### `private void ShowToast(AppNotification notification)

**Purpose:** Adds one toast card to the bottom-right stack for a short fixed lifetime.

| Step | Action |
| --- | --- |
| 1 | `CreateToast(notification)`; insert at index 0 of `ToastPanel` |
| 2 | Trim stack to max **4** children (remove oldest) |
| 3 | `FadeToAsync(1, 120ms, CubicOut)` |
| 4 | Unless `SuppressToastAutoDismiss` → `RemoveToastAfterDelayAsync(10s)` |

---

#### `private Border CreateToast(AppNotification notification)

**Purpose:** Builds the compact visual toast card for one notification.

| Area | Behavior |
| --- | --- |
| Layout | Accent `BoxView` + title/message/detail labels + optional update action row + close **✕** |
| Styling | `BackgroundSecondary`, `Separator` stroke, shadow |
| Update actions | If `OffersUpdateActions`: **Update now** → `RequestUpdateNotificationAction(updateNow: true)`; **Update later** → dismiss only |
| Close button | `RemoveToast` only |
| Tap on text stack | `RemoveToast` + `OpenNotificationsOverlay()` |

Returns the root **`Border`** (`toastHost`).

---

#### `private void OnUpdateNotificationActionRequested(object? sender, UpdateNotificationActionEventArgs e)

**Purpose:** Routes update notification actions from toasts and the notifications popover.

| `e.UpdateNow` | Action |
| --- | --- |
| `false` | `ClearUpdateNotificationDeferredActions(e.Notification)` |
| `true` | Fire-and-forget `ProcessUpdateNotificationNowAsync(e.Notification)` |

---

#### `private async Task ProcessUpdateNotificationNowAsync(AppNotification notification)`

**Purpose:** Opens module update configuration or runs the ASLM self-update pipeline for one notification.

| Step | Action |
| --- | --- |
| 1 | Close notifications overlay if it is showing |
| 2 | If `SourceKind` == `"module"` → `OpenModuleUpdateFromNotificationAsync(SourceId)` |
| 3 | If `SourceKind` == `"app"` → `RunAppSelfUpdateFromNotificationAsync()` |
| — | On failure: `PublishSystemToast` with localized action-failed message |

---

#### `private async Task OpenModuleUpdateFromNotificationAsync(string moduleId)`

**Purpose:** Opens the module update overlay in configure mode for the module referenced by the notification.

| Step | Action |
| --- | --- |
| 1 | `DiscoverModulesAsync`; find config by `moduleId` |
| 2 | If missing → system toast (module not installed) |
| 3 | Prefer existing `ModulesView.Modules` view model by `SourcePath` |
| 4 | Else `ModulesView.CreateViewModelForDeferredUpdateOverlay(...)` |
| 5 | `OpenModuleUpdateOverlay(viewModel, ModuleUpdateMode.Configure)` |

---

#### `private async Task RunAppSelfUpdateFromNotificationAsync()

**Purpose:** Prepares a pending ASLM build when needed and restarts through the launcher.

| Step | Action |
| --- | --- |
| 1 | `CheckAppUpdateAsync` (no publish) |
| 2 | If no candidate and no pending update → toast “not available” |
| 3 | If not pending and candidate exists → `PrepareAppUpdateAsync`; on failure → toast |
| 4 | `StopAllModulesAsync()` |
| 5 | `StartLauncherForSelfUpdate` on thread pool |
| 6 | `Application.Current?.Quit()` |

---

#### `private async Task RemoveToastAfterDelayAsync(Border toast, TimeSpan delay)`

**Purpose:** Removes a toast after the requested delay unless it has already been dismissed.

`await Task.Delay(delay)` then **`MainThread.InvokeOnMainThreadAsync(() => RemoveToast(toast))`**.

---

#### `private void RemoveToast(Border toast)`

**Purpose:** Animates and removes one toast from the stack.

If toast not in `ToastPanel`, return. Else **`FadeToAsync(0, 120ms, CubicIn)`** then remove on main thread.

---

## Sidebar

#### `private void OnApiServerStateChanged(object? sender, EventArgs e)`

**Purpose:** Refreshes ASLM API sidebar visibility when the server setting changes. **`MainThread.BeginInvokeOnMainThread(ApplyAslmApiNavigationState)`**.

---

#### `private void ApplyAslmApiNavigationState()

**Purpose:** Shows the ASLM API navigation item only when the API server is enabled.

| Step | Action |
| --- | --- |
| 1 | `AslmApiButton.IsVisible = _apiServer.IsEnabled` |
| 2 | If hidden and active nav is API button → `NavigateTo(HomeButton)` |

---

#### `private void ApplyConsoleNavigationState()

**Purpose:** Shows the consoles navigation item according to the saved user preference.

| Step | Action |
| --- | --- |
| 1 | `_appData.Data.Consoles.Normalize()` |
| 2 | `ConsolesButton.IsVisible = Consoles.SidebarVisible` |
| 3 | If hidden and active nav is consoles → `NavigateTo(HomeButton)` |

---

#### `private void OnCollapseClicked(object? sender, EventArgs e)

**Purpose:** Expands or collapses the sidebar and updates every visible button.

| Step | Action |
| --- | --- |
| 1 | Toggle `_panelExpanded`; save `Preferences` **`SidebarExpanded`** |
| 2 | Animate `SidePanel.WidthRequest` 150ms, `CubicOut`, 16ms rate |
| 3 | `ApplySidebarButtonLayout()` + `ScheduleSidebarButtonLayoutRefresh()` |

---

#### `public void ApplyLocalization()`

**Purpose:** `ILocalizable` — sets page **`Title`** from `LocalizationKeys.AppShell_Title` and calls **`ApplySidebarButtonLayout()`**.

---

#### `private string GetButtonLabel(Button button)

**Purpose:** Returns the display label for one static shell button.

| Button | Localization key |
| --- | --- |
| `HomeButton` | `AppShell_Nav_Home` |
| `ConsolesButton` | `AppShell_Nav_Consoles` |
| `ModulesButton` | `AppShell_Nav_Modules` |
| `AslmApiButton` | `AppShell_Nav_Api` |
| `NotificationsButton` | `AppShell_Nav_Notifications` |
| `DownloadsButton` | `AppShell_Nav_Download` |
| `SettingsButton` | `AppShell_Nav_Settings` |
| Other | `string.Empty` |

---

#### `private void BuildPageButtons()

**Purpose:** Rebuilds sidebar buttons for enabled modules that expose a page.

| Step | Action |
| --- | --- |
| 1 | `ModulePagePanel.Children.Clear()` |
| 2 | Filter `_allModules` where `HasPage && Status.Enabled` |
| 3 | Per module: icon from `SidebarIconFullPath` or `IconPage` |
| 4 | Create `Button` (`ClassId="PAGE"`, `AutomationId=module.Id`, `SidebarButton` style) |
| 5 | `Clicked` → `ActivateModulePage`; `HandlerChanged` → sidebar layout |
| 6 | `ApplyShellNavInactiveStyle`; `ScheduleSidebarButtonLayoutRefresh()` |

---

#### `private void ScheduleSidebarButtonLayoutRefresh()`

**Purpose:** Schedules deferred sidebar layout passes until image-backed buttons finish their first native measure.

Cancels/disposes prior `_sidebarButtonLayoutCts`, creates new CTS, starts **`RefreshSidebarButtonLayoutAsync`**.

---

#### `private async Task RefreshSidebarButtonLayoutAsync(CancellationToken ct)`

**Purpose:** Re-applies sidebar button layout after image-backed buttons finish their first native measure.

Delays **16, 80, 160, 320** ms (respecting cancellation); each tick **`Dispatcher.Dispatch(ApplySidebarButtonLayout)`**. Swallows **`OperationCanceledException`**.

---

#### `private void ApplySidebarButtonLayout()`

**Purpose:** Applies text/icon spacing and native alignment to every sidebar button, including the collapse toggle.

| Target | When expanded | When collapsed |
| --- | --- | --- |
| `CollapseButton` | Icon only, spacing 0 | Same |
| `_navButtons` | `GetButtonLabel` text, spacing **14** | Empty text, spacing 0 |
| `ModulePagePanel` buttons | Module name from `BindingContext` | Empty text |

Each: `ContentLayout` image left, `HorizontalOptions.Fill`, **`UpdateButtonAlignment`**. Collapse button re-tints with **`LabelPrimary`**.

---

#### `private void OnSidebarButtonHandlerChanged(object? sender, EventArgs e)`

**Purpose:** Re-applies sidebar layout once the native button handler is attached.

If `sender` is `Button` with non-null `Handler` → **`UpdateButtonAlignment(button)`** + dispatch **`ApplySidebarButtonLayout`**.

---

#### `private void UpdateButtonAlignment(Button button)`

**Purpose:** Aligns WinUI button content to the left to match the sidebar layout.

WinUI: set **`HorizontalContentAlignment.Left`** on native `Button`; **`ConstrainSidebarIconSize`**.

---

#### `private static void ConstrainSidebarIconSize(Microsoft.UI.Xaml.DependencyObject root)` *(Windows only)*

Pins the WinUI image inside a sidebar button to the logical icon size (avoids stretch on first wide layout).

Depth-first visual tree walk; first **`Image`** child → width/height **`SidebarIconLogicalSize`**, **`Stretch.Uniform`**.

---

#### `private void ApplyShellNavInactiveStyle(Button button)`

**Purpose:** Applies inactive sidebar styling to one navigation button.

`TextColor` → **`LabelSecondary`**; `BackgroundColor` transparent; icon tint **`LabelPrimary`**.

---

#### `private void ApplyShellNavActiveStyle(Button button)`

**Purpose:** Applies active sidebar styling to one navigation button.

`TextColor` → **`LabelPrimary`**; `BackgroundColor` → **`BackgroundTertiary`**; icon tint from **`GetActiveNavIconPaletteKey(button)`**.

---

#### `private void ApplySidebarButtonIconFromPalette(Button button, string paletteResourceKey)`

**Purpose:** Replaces the sidebar button image with a Skia-tinted PNG so colors track the palette without WinUI behavior crashes.

Resolves path via **`ResolveSidebarIconFile`**; **`PackagedIconTintCache.Get(path, IconTintHelper.ResolvePaletteColor(key))`**.

---

#### `private string? ResolveSidebarIconFile(Button button)`

**Purpose:** Resolves the logical packaged icon file name or an absolute module asset path for one sidebar button.

| Button | Path |
| --- | --- |
| `CollapseButton` | `IconMenu` |
| `HomeButton` | `IconHome` |
| `ConsolesButton` | `IconConsole` |
| `ModulesButton` | `IconModules` |
| `AslmApiButton` | `IconApi` |
| `NotificationsButton` | `IconNotifications` |
| `DownloadsButton` | `IconDownload` |
| `SettingsButton` | `IconSettings` |
| `ClassId="PAGE"` + `ModuleConfig` | `SidebarIconFullPath` if file exists, else `IconPage` |
| Other | `null` |

---

#### `private string GetActiveNavIconPaletteKey(Button button)`

**Purpose:** Returns the palette resource key used to tint the sidebar icon when this navigation row is active.

| Button | Key |
| --- | --- |
| `HomeButton` | `SystemBlue` |
| `ConsolesButton` | `SystemPurple` |
| `ModulesButton` | `SystemGreen` |
| `AslmApiButton` | `SystemOrange` |
| Other | `LabelPrimary` |

---

## Module browser

#### `private void ActivateModulePage(ModuleConfig module)

**Purpose:** Opens one module page inside the embedded browser and updates highlighting.

| Step | Action |
| --- | --- |
| 1 | `ResolveModuleConfig(module)` — return if null |
| 2 | `_activeModule = resolved`; URL from `_ports.GetModuleUrl` |
| 3 | `ContentArea.Content = null`; `EnsureModuleBrowserLeftToRight()` |
| 4 | `NavigateModuleBrowser(url)`; `Browser.IsVisible = true` |
| 5 | `ScheduleEnsureModuleBrowserLeftToRight()` |
| 6 | Inactive style on `_navButtons`; `_activeNavButton = null` |
| 7 | Active style on matching `PAGE` button (`AutomationId == resolved.Id`) |

---

#### `private ModuleConfig? ResolveModuleConfig(ModuleConfig module)`

**Purpose:** Returns the latest discovered module matching **`module`**.

If `module.Id` empty, return **`module`**. Else first `_allModules` match by id (ordinal ignore case), else **`module`**.

---

#### `private void ClearModuleBrowserNavigationTarget()`

**Purpose:** Clears the module browser target so handler re-creation cannot restore a stale page.

Increments **`_moduleBrowserNavigationSequence`**; clears expected/actual URLs; WinUI clears **`_pendingModuleBrowserUrl`**; **`Browser.Source = null`**.

---

#### `private static bool ModuleBrowserUrlsMatch(string? actual, string? expected)`

**Purpose:** Compares two local module base URLs by scheme, host, and port.

If either blank → `false`. Parse as absolute URIs when possible → match scheme, host (ignore case), port. Else compare trimmed paths (ignore case, trailing `/` stripped).

---

#### `private void NavigateModuleBrowser(string url)`

**Purpose:** Navigates the shared module browser to one local module URL.

Increments sequence, sets **`_moduleBrowserExpectedUrl`**, **`MainThread.BeginInvokeOnMainThread(() => ApplyModuleBrowserNavigation(sequence, url))`**.

---

#### `private void ApplyModuleBrowserNavigation(int sequence, string url)

**Purpose:** Applies one module-browser navigation on the UI thread.

| Step | Action |
| --- | --- |
| 1 | Return if sequence or expected URL no longer current |
| 2 | Set `_moduleBrowserUrl`; `Browser.Source = new UrlWebViewSource { Url = url }` |
| 3 | WinUI: store pending URL; if no `CoreWebView2`, return |
| 4 | WinUI: `WireModuleWebViewNavigation`; if source already matches, return |
| 5 | WinUI: `core.Stop()` + `core.Navigate(url)` |

---

#### `private void Browser_Navigating(object? sender, WebNavigatingEventArgs e)`

**Purpose:** Keeps the embedded browser on local module pages and opens external links outside the app.

| Condition | Action |
| --- | --- |
| Empty URL | Return |
| Invalid URI | Return |
| `http`/`https` + host `127.0.0.1` or `localhost` | Allow |
| Otherwise | `e.Cancel = true`; **`Launcher.OpenAsync(uri)`** |

---

#### `private void OnLocalizationCultureChanged(object? sender, EventArgs e)`

**Purpose:** Reapplies LTR on the module browser after shell RTL layout runs. Calls **`ScheduleEnsureModuleBrowserLeftToRight()`**.

---

#### `private void ScheduleEnsureModuleBrowserLeftToRight()`

**Purpose:** Schedules a post-layout pass that pins the module WebView to LTR. **`Dispatcher.DispatchDelayed(0ms, EnsureModuleBrowserLeftToRight)`**.

---

#### `private void EnsureModuleBrowserLeftToRight()`

**Purpose:** Keeps the embedded module browser in LTR at the MAUI and WinUI layers.

Sets **`Browser.FlowDirection = LeftToRight`**. WinUI: native `WebView2.FlowDirection = LeftToRight` when handler attached.

---

#### `private async Task StartEnabledModulesAsync()`

**Purpose:** Starts enabled modules that expose run commands when the shell opens.

Filter modules with **`Status.Enabled`** and **`Commands.Run.Count > 0`**. For each: **`_moduleStartThrottle.WaitAsync`**, **`ExecuteRunAsync`** on thread pool, **`Release`** in `finally`. **`Task.WhenAll`** over all starts.

---

## Module WebView (Windows)

#### `private void WireModuleWebViewNavigation(CoreWebView2 core)`

**Purpose:** Subscribes to WebView2 navigation completion so stale async loads can be corrected.

If not yet hooked → **`NavigationCompleted += OnModuleBrowserNavigationCompleted`**; set **`_moduleWebViewNavigationHooked = true`**.

---

#### `private void OnModuleBrowserNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)`

**Purpose:** Re-navigates when an older in-flight request finishes after a newer module was selected.

If not success or browser hidden → return. If actual URL matches expected → return. Else main-thread **`ApplyModuleBrowserNavigation(_moduleBrowserNavigationSequence, _moduleBrowserExpectedUrl)`** when still visible.

---

#### `private void Browser_HandlerChanged(object? sender, EventArgs e)`

**Purpose:** Wires the native WebView2 once the MAUI handler attaches so module pages can receive drag-and-drop.

| Step | Action |
| --- | --- |
| 1 | `ReleaseModuleWebViewDropTarget()` |
| 2 | If no WinUI `WebView2` platform view, return |
| 3 | Store `_moduleWebView2`; subscribe `CoreWebView2Initialized` |
| 4 | `ApplyModuleWebViewDropTarget`; `EnsureModuleBrowserLeftToRight()` |
| 5 | Re-apply navigation for expected/pending/current URL |

---

#### `private void OnModuleWebViewCoreInitialized(WebView2 sender, CoreWebView2InitializedEventArgs e)`

**Purpose:** Enables drag-and-drop on the module WebView2 after the core is initialized.

`ApplyModuleWebViewDropTarget`, `EnsureModuleBrowserLeftToRight`, **`WireModuleWebViewNavigation`**, re-apply navigation if target URL known.

---

#### `private void ReleaseModuleWebViewDropTarget()`

**Purpose:** Unhooks module WebView2 handlers when the browser handler is released.

Unsubscribe **`NavigationCompleted`** and **`CoreWebView2Initialized`**; null **`_moduleWebView2`**; reset navigation hook flag.

---

#### `private static void ApplyModuleWebViewDropTarget(WebView2 native)`

**Purpose:** Sets **`AllowDrop = true`** on the native WebView2 so module pages accept HTML5 drag-and-drop.

---

## Dependencies

`ModuleInstaller`, `ModuleRunner`, `AppDataStore`, `PortRegistry`, `NotificationCenter`, `UpdateManager`, `ModuleTrustService`, `AslmApiServer`, `ModuleStartThrottle`, `ModuleLaunchCoordinator`, `SettingsService`, `AppLocalizationService`, `IServiceProvider`.

Lazy views via DI: `HomeView`, `ConsolesView`, `ModulesView`, `AslmApiView`, `NotificationsView`, `DownloadsView`, `SettingsView`, `ModuleUpdateView`.
