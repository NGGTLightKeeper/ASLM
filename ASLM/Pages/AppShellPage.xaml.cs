// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ASLM.Models;
using ASLM.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ASLM.Pages
{
    // Application shell

    /// <summary>
    /// Hosts the shared sidebar, system views, and module pages.
    /// </summary>
    public partial class AppShellPage : ContentPage, INotifyPropertyChanged
    {
        private const double PanelExpandedWidth = 240;
        private const double PanelCollapsedWidth = 48;

        private const string IconMenu = "icon_menu.png";
        private const string IconHome = "icon_home.png";
        private const string IconConsole = "icon_console.png";
        private const string IconModules = "icon_modules.png";
        private const string IconApi = "icon_api.png";
        private const string IconNotifications = "icon_notifications.png";
        private const string IconDownload = "icon_download.png";
        private const string IconSettings = "icon_settings.png";
        private const string IconPage = "icon_page.png";

        private const string LabelHome = "Home";
        private const string LabelConsoles = "Consoles";
        private const string LabelModules = "Modules";
        private const string LabelApi = "ASLM API";
        private const string LabelNotifications = "Notifications";
        private const string LabelDownload = "Download";
        private const string LabelSettings = "Settings";
        private const int MaxConcurrentModuleStarts = 2;

        private static readonly Color ActiveTextColor = Colors.White;
        private static readonly Color InactiveTextColor = Color.FromArgb("#8E8E93");
        private static readonly Color ActiveBackground = Color.FromArgb("#2C2C2E");
        private static readonly Color TransparentBackground = Colors.Transparent;

        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly AppDataService _appData;
        private readonly PortManager _portManager;
        private readonly NotificationService _notificationService;
        private readonly UpdateService _updateService;
        private readonly AslmApiServerService _apiServer;
        private readonly IServiceProvider _services;

        private List<ModuleConfig> _allModules = [];
        private ModuleConfig? _activeModule;
        private bool _panelExpanded;
        private bool _hasLoaded;

        private View? _homeView;
        private View? _consolesView;
        private View? _modulesView;
        private View? _aslmApiView;
        private View? _notificationsView;
        private View? _downloadsView;
        private View? _settingsView;
        private View? _moduleUpdateView;

        private Button? _activeNavButton;
        private Button[] _navButtons = [];
        private CancellationTokenSource? _pageButtonLayoutCts;
        private readonly SemaphoreSlim _moduleRefreshLock = new(1, 1);
        private bool _shellEventsHooked;
        private int _moduleRefreshQueued;

        // Initialization

        /// <summary>
        /// Creates the application shell and restores the saved sidebar state.
        /// </summary>
        public AppShellPage(
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            AppDataService appData,
            PortManager portManager,
            NotificationService notificationService,
            UpdateService updateService,
            AslmApiServerService apiServer,
            IServiceProvider services)
        {
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _appData = appData;
            _portManager = portManager;
            _notificationService = notificationService;
            _updateService = updateService;
            _apiServer = apiServer;
            _services = services;

            InitializeComponent();
            BindingContext = this;
            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;

            // Restore the sidebar width before the first render so the shell opens in the saved state.
            _panelExpanded = Preferences.Default.Get("SidebarExpanded", false);
            SidePanel.WidthRequest = _panelExpanded ? PanelExpandedWidth : PanelCollapsedWidth;

            _navButtons = [HomeButton, ConsolesButton, ModulesButton, AslmApiButton, NotificationsButton, DownloadsButton, SettingsButton];

            // Hook alignment updates once so WinUI buttons keep the same content layout.
            CollapseButton.HandlerChanged += (sender, _) => UpdateButtonAlignment((Button)sender!);
            foreach (var button in _navButtons)
            {
                button.HandlerChanged += (sender, _) => UpdateButtonAlignment((Button)sender!);
            }

            // Assign all static sidebar icons up front.
            CollapseButton.ImageSource = IconMenu;
            HomeButton.ImageSource = IconHome;
            ConsolesButton.ImageSource = IconConsole;
            ModulesButton.ImageSource = IconModules;
            AslmApiButton.ImageSource = IconApi;
            NotificationsButton.ImageSource = IconNotifications;
            DownloadsButton.ImageSource = IconDownload;
            SettingsButton.ImageSource = IconSettings;

            // Initialize button labels and image layout based on the current sidebar width.
            foreach (var button in _navButtons)
            {
                button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 14);
                button.Text = _panelExpanded ? GetButtonLabel(button) : string.Empty;
            }

            ApplyAslmApiNavigationState();
            ApplyConsoleNavigationState();
        }


        // Notifications

        /// <inheritdoc />
        public new event PropertyChangedEventHandler? PropertyChanged;

        // Property change

        /// <summary>
        /// Raises the shell-level property changed event.
        /// </summary>
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        // Startup

        /// <summary>
        /// Loads module state once and opens the default shell view.
        /// </summary>
        private async void OnPageLoaded(object? sender, EventArgs e)
        {
            HookShellEvents();

            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;
            await RefreshModulesAsync();
            NavigateTo(HomeButton);
            ApplyAslmApiNavigationState();
            ApplyConsoleNavigationState();
            _ = StartEnabledModulesAsync();
            _ = CheckStartupUpdatesAsync();
            _ = _notificationService.PublishStartupTestNotificationsAsync();
        }

        /// <summary>
        /// Unhooks shell-level notification events when the page leaves the visual tree.
        /// </summary>
        private void OnPageUnloaded(object? sender, EventArgs e)
        {
            UnhookShellEvents();
        }

        /// <summary>
        /// Hooks shell-wide service events once per visual lifetime.
        /// </summary>
        private void HookShellEvents()
        {
            if (_shellEventsHooked)
            {
                return;
            }

            _notificationService.NotificationPublished += OnNotificationPublished;
            _apiServer.StateChanged += OnApiServerStateChanged;
            _moduleInstaller.ModulesChanged += OnModulesChanged;
            _shellEventsHooked = true;
        }

        /// <summary>
        /// Unhooks shell-wide service events when the page leaves the visual tree.
        /// </summary>
        private void UnhookShellEvents()
        {
            if (!_shellEventsHooked)
            {
                return;
            }

            _notificationService.NotificationPublished -= OnNotificationPublished;
            _apiServer.StateChanged -= OnApiServerStateChanged;
            _moduleInstaller.ModulesChanged -= OnModulesChanged;
            _shellEventsHooked = false;
        }

        /// <summary>
        /// Checks ASLM and modules for updates once after the main shell opens.
        /// </summary>
        private async Task CheckStartupUpdatesAsync()
        {
            try
            {
                await Task.Run(() => _updateService.CheckAllUpdatesAsync());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StartupUpdates] Check failed: {ex.Message}");
            }
        }

        // Module refresh

        /// <summary>
        /// Reloads modules, rebuilds page buttons, and refreshes the dashboard if needed.
        /// </summary>
        private async Task RefreshModulesAsync()
        {
            Interlocked.Exchange(ref _moduleRefreshQueued, 1);
            if (!await _moduleRefreshLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                do
                {
                    Interlocked.Exchange(ref _moduleRefreshQueued, 0);
                    var modules = await Task.Run(() => _moduleInstaller.DiscoverModulesAsync());
                    await MainThread.InvokeOnMainThreadAsync(() => ApplyModules(modules));
                }
                while (Interlocked.Exchange(ref _moduleRefreshQueued, 0) == 1);
            }
            finally
            {
                _moduleRefreshLock.Release();
            }
        }

        /// <summary>
        /// Applies the latest module API snapshot to the sidebar and loaded module-backed views.
        /// </summary>
        private void ApplyModules(List<ModuleConfig> modules)
        {
            var activeModuleSourcePath = _activeModule?.SourcePath;
            _allModules = modules;

            if (!string.IsNullOrWhiteSpace(activeModuleSourcePath))
            {
                _activeModule = _allModules.FirstOrDefault(module =>
                    string.Equals(module.SourcePath, activeModuleSourcePath, StringComparison.OrdinalIgnoreCase));
            }

            BuildPageButtons();

            if (_activeModule is { HasPage: true, Status.Enabled: false })
            {
                NavigateTo(HomeButton);
            }

            if (_modulesView is ModulesView modulesView)
            {
                modulesView.RefreshModules(_allModules, _moduleInstaller, _moduleRunner);
            }

            if (_consolesView is ConsolesView consolesView)
            {
                _ = consolesView.RefreshAsync();
            }

            if (_homeView is HomeView homeView)
            {
                _ = homeView.RefreshAsync();
            }

            if (_aslmApiView is AslmApiView aslmApiView)
            {
                _ = aslmApiView.RefreshAsync();
            }
        }

        /// <summary>
        /// Refreshes module-backed UI after a module manifest is saved.
        /// </summary>
        private void OnModulesChanged(object? sender, EventArgs e)
        {
            _ = RefreshModulesAsync();
        }

        // Module state callback

        /// <summary>
        /// Refreshes module page buttons after a module changes state.
        /// </summary>
        internal void OnModuleStateChanged()
        {
            _ = RefreshModulesAsync();
        }


        // Sidebar

        /// <summary>
        /// Refreshes ASLM API sidebar visibility when the server setting changes.
        /// </summary>
        private void OnApiServerStateChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(ApplyAslmApiNavigationState);
        }

        /// <summary>
        /// Shows the ASLM API navigation item only when the API server is enabled.
        /// </summary>
        private void ApplyAslmApiNavigationState()
        {
            var isVisible = _apiServer.IsEnabled;
            AslmApiButton.IsVisible = isVisible;

            if (!isVisible && _activeNavButton == AslmApiButton)
            {
                NavigateTo(HomeButton);
            }
        }

        /// <summary>
        /// Shows the consoles navigation item according to the saved user preference.
        /// </summary>
        private void ApplyConsoleNavigationState()
        {
            _appData.Data.Consoles.Normalize();
            var isVisible = _appData.Data.Consoles.SidebarVisible;
            ConsolesButton.IsVisible = isVisible;

            if (!isVisible && _activeNavButton == ConsolesButton)
            {
                NavigateTo(HomeButton);
            }
        }

        /// <summary>
        /// Expands or collapses the sidebar and updates every visible button.
        /// </summary>
        private void OnCollapseClicked(object? sender, EventArgs e)
        {
            _panelExpanded = !_panelExpanded;
            Preferences.Default.Set("SidebarExpanded", _panelExpanded);

            var targetWidth = _panelExpanded ? PanelExpandedWidth : PanelCollapsedWidth;
            var animation = new Animation(
                value => SidePanel.WidthRequest = value,
                SidePanel.WidthRequest,
                targetWidth);

            animation.Commit(this, "SidebarAnimation", 16, 150, Easing.CubicOut);
            UpdateButtonAlignment(CollapseButton);

            var spacing = _panelExpanded ? 14 : 0;

            // Update the fixed shell navigation buttons first.
            foreach (var button in _navButtons)
            {
                button.Text = _panelExpanded ? GetButtonLabel(button) : string.Empty;
                button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, spacing);
                button.HorizontalOptions = LayoutOptions.Fill;
                UpdateButtonAlignment(button);
            }

            // Apply the same visual rules to dynamically created module page buttons.
            foreach (var child in ModulePagePanel.Children)
            {
                if (child is not Button button)
                {
                    continue;
                }

                button.Text = _panelExpanded && button.BindingContext is ModuleConfig module
                    ? module.Name
                    : string.Empty;
                button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, spacing);
                button.HorizontalOptions = LayoutOptions.Fill;
                UpdateButtonAlignment(button);
            }
        }


        // Shell navigation

        /// <summary>
        /// Routes button clicks to the shared navigation handler.
        /// </summary>
        private void OnNavClicked(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                if (button == DownloadsButton)
                {
                    OpenDownloadOverlay();
                    return;
                }

                if (button == NotificationsButton)
                {
                    OpenNotificationsOverlay();
                    return;
                }

                if (button == SettingsButton)
                {
                    OpenSettingsOverlay();
                    return;
                }

                NavigateTo(button);
            }
        }

        // Overlay navigation

        /// <summary>
        /// Opens the shared settings overlay and refreshes it before showing.
        /// </summary>
        private void OpenSettingsOverlay()
        {
            _settingsView ??= _services.GetRequiredService<SettingsView>();
            if (_settingsView is SettingsView settingsView)
            {
                settingsView.CloseRequested -= OnSettingsCloseRequested;
                settingsView.CloseRequested += OnSettingsCloseRequested;
                _ = settingsView.RefreshAsync();
            }

            OverlayContainer.Content = _settingsView;
            OverlayContainer.IsVisible = true;
        }

        /// <summary>
        /// Opens the shared notifications overlay and refreshes it before showing.
        /// </summary>
        private void OpenNotificationsOverlay()
        {
            _notificationsView ??= _services.GetRequiredService<NotificationsView>();
            if (_notificationsView is NotificationsView notificationsView)
            {
                notificationsView.CloseRequested -= OnNotificationsCloseRequested;
                notificationsView.CloseRequested += OnNotificationsCloseRequested;
                var anchorBounds = GetElementBoundsInShell(NotificationsButton);
                _ = notificationsView.OpenAtAsync(anchorBounds, Width, Height);
            }

            OverlayContainer.Content = _notificationsView;
            OverlayContainer.IsVisible = true;
        }

        /// <summary>
        /// Opens the shared download overlay and refreshes it before showing.
        /// </summary>
        private void OpenDownloadOverlay()
        {
            _downloadsView ??= _services.GetRequiredService<DownloadsView>();
            if (_downloadsView is DownloadsView downloadsView)
            {
                downloadsView.CloseRequested -= OnDownloadCloseRequested;
                downloadsView.CloseRequested += OnDownloadCloseRequested;
                _ = downloadsView.OpenAsync();
            }

            OverlayContainer.Content = _downloadsView;
            OverlayContainer.IsVisible = true;
        }

        /// <summary>
        /// Opens the shared module update overlay for the selected module.
        /// </summary>
        internal void OpenModuleUpdateOverlay(ModuleViewModel module, ModuleUpdateMode mode)
        {
            _ = OpenModuleUpdateOverlayAsync(module, mode);
        }

        /// <summary>
        /// Loads and shows the module update overlay before binding it to the selected module.
        /// </summary>
        private async Task OpenModuleUpdateOverlayAsync(ModuleViewModel module, ModuleUpdateMode mode)
        {
            _moduleUpdateView ??= _services.GetRequiredService<ModuleUpdateView>();
            if (_moduleUpdateView is not ModuleUpdateView moduleUpdateView)
            {
                return;
            }

            moduleUpdateView.CloseRequested -= OnModuleUpdateCloseRequested;
            moduleUpdateView.CloseRequested += OnModuleUpdateCloseRequested;
            OverlayContainer.Content = _moduleUpdateView;
            OverlayContainer.IsVisible = true;

            await moduleUpdateView.OpenAsync(module, mode);
        }

        /// <summary>
        /// Hides the overlay container when the settings view requests close.
        /// </summary>
        private void OnSettingsCloseRequested(object? sender, EventArgs e)
        {
            OverlayContainer.IsVisible = false;
            ApplyConsoleNavigationState();

            if (_consolesView is ConsolesView consolesView)
            {
                _ = consolesView.RefreshAsync();
            }

            if (_homeView is HomeView homeView)
            {
                _ = homeView.RefreshAsync();
            }
        }

        /// <summary>
        /// Hides the overlay container when the notifications view requests close.
        /// </summary>
        private void OnNotificationsCloseRequested(object? sender, EventArgs e)
        {
            OverlayContainer.IsVisible = false;
            OverlayContainer.Content = null;
        }

        /// <summary>
        /// Hides the overlay container when the download view requests close.
        /// </summary>
        private void OnDownloadCloseRequested(object? sender, EventArgs e)
        {
            OverlayContainer.IsVisible = false;
        }

        /// <summary>
        /// Hides the overlay container when the module update view requests close.
        /// </summary>
        private void OnModuleUpdateCloseRequested(object? sender, EventArgs e)
        {
            OverlayContainer.IsVisible = false;
        }

        // Toast notifications

        /// <summary>
        /// Shows an in-app toast when a new notification is published.
        /// </summary>
        private void OnNotificationPublished(object? sender, AppNotification notification)
        {
            MainThread.BeginInvokeOnMainThread(() => ShowToast(notification));
        }

        /// <summary>
        /// Adds one toast card to the bottom-right stack for a short fixed lifetime.
        /// </summary>
        private void ShowToast(AppNotification notification)
        {
            var toast = CreateToast(notification);
            ToastPanel.Children.Insert(0, toast);

            while (ToastPanel.Children.Count > 4)
            {
                ToastPanel.Children.RemoveAt(ToastPanel.Children.Count - 1);
            }

            toast.Opacity = 0;
            _ = toast.FadeToAsync(1, 120, Easing.CubicOut);
            _ = RemoveToastAfterDelayAsync(toast, TimeSpan.FromSeconds(10));
        }

        /// <summary>
        /// Builds the compact visual toast card for one notification.
        /// </summary>
        private Border CreateToast(AppNotification notification)
        {
            var title = new Label
            {
                Text = notification.Title,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            var message = new Label
            {
                Text = notification.Message,
                FontSize = 11,
                TextColor = Color.FromArgb("#D8D8DC"),
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            var detail = new Label
            {
                Text = notification.DetailLine,
                FontSize = 10,
                TextColor = Color.FromArgb("#9A9AA0"),
                MaxLines = 1,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            var content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(4)),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 10
            };

            content.Children.Add(new BoxView
            {
                BackgroundColor = notification.AccentColor,
                WidthRequest = 4,
                VerticalOptions = LayoutOptions.Fill
            });

            var textStack = new VerticalStackLayout { Spacing = 2 };
            textStack.Children.Add(title);
            textStack.Children.Add(message);
            textStack.Children.Add(detail);
            content.Children.Add(textStack);
            Grid.SetColumn(textStack, 1);

            var toast = new Border
            {
                BindingContext = notification,
                BackgroundColor = Color.FromArgb("#28282A"),
                Stroke = Color.FromArgb("#454548"),
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(10),
                Content = content,
                Shadow = new Shadow
                {
                    Brush = Brush.Black,
                    Opacity = 0.35f,
                    Radius = 12,
                    Offset = new Point(0, 4)
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                RemoveToast(toast);
            };
            toast.GestureRecognizers.Add(tap);
            return toast;
        }

        /// <summary>
        /// Removes a toast after the requested delay unless it has already been dismissed.
        /// </summary>
        private async Task RemoveToastAfterDelayAsync(Border toast, TimeSpan delay)
        {
            await Task.Delay(delay);
            await MainThread.InvokeOnMainThreadAsync(() => RemoveToast(toast));
        }

        /// <summary>
        /// Animates and removes one toast from the stack.
        /// </summary>
        private void RemoveToast(Border toast)
        {
            if (!ToastPanel.Children.Contains(toast))
            {
                return;
            }

            _ = toast.FadeToAsync(0, 120, Easing.CubicIn).ContinueWith(_ =>
                MainThread.BeginInvokeOnMainThread(() => ToastPanel.Children.Remove(toast)));
        }

        /// <summary>
        /// Calculates one child element's bounds in the shell coordinate space.
        /// </summary>
        private Rect GetElementBoundsInShell(VisualElement element)
        {
            var x = element.Bounds.X;
            var y = element.Bounds.Y;
            var parent = element.Parent;

            // Walk the MAUI visual parent chain so popovers can anchor to controls inside nested layouts.
            while (parent is VisualElement parentElement && parentElement != this)
            {
                x += parentElement.Bounds.X;
                y += parentElement.Bounds.Y;
                parent = parentElement.Parent;
            }

            return new Rect(x, y, element.Bounds.Width, element.Bounds.Height);
        }

        // View activation

        /// <summary>
        /// Activates one shell view and updates button highlighting.
        /// </summary>
        private void NavigateTo(Button navButton)
        {
            _activeModule = null;
            Browser.IsVisible = false;

            // Clear active styling from both fixed shell buttons and dynamic module buttons.
            foreach (var button in _navButtons)
            {
                button.TextColor = InactiveTextColor;
                button.BackgroundColor = TransparentBackground;
            }

            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button button)
                {
                    button.TextColor = InactiveTextColor;
                    button.BackgroundColor = TransparentBackground;
                }
            }

            navButton.TextColor = ActiveTextColor;
            navButton.BackgroundColor = ActiveBackground;
            _activeNavButton = navButton;

            ContentArea.Content = GetViewForButton(navButton);
        }

        /// <summary>
        /// Opens the home dashboard.
        /// </summary>
        internal void OpenHome()
        {
            NavigateTo(HomeButton);
        }

        /// <summary>
        /// Opens the consoles page and optionally focuses one module.
        /// </summary>
        internal void OpenConsoles(string? moduleSourcePath = null)
        {
            NavigateTo(ConsolesButton);

            if (_consolesView is not ConsolesView consolesView)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(moduleSourcePath))
            {
                _ = consolesView.RefreshAsync();
                return;
            }

            _ = consolesView.ShowModuleAsync(moduleSourcePath);
        }

        /// <summary>
        /// Opens the modules page and optionally scrolls one module card into view.
        /// </summary>
        internal void OpenModules(string? moduleSourcePath = null)
        {
            NavigateTo(ModulesButton);

            if (string.IsNullOrWhiteSpace(moduleSourcePath) ||
                _modulesView is not ModulesView modulesView)
            {
                return;
            }

            modulesView.FocusModule(moduleSourcePath);
        }

        // View resolution

        /// <summary>
        /// Returns the cached shell view for the selected navigation button.
        /// </summary>
        private View GetViewForButton(Button button)
        {
            if (button == HomeButton)
            {
                if (_homeView == null)
                {
                    var homeView = _services.GetRequiredService<HomeView>();
                    homeView.Initialize(this);
                    _homeView = homeView;
                }

                return _homeView;
            }

            if (button == ConsolesButton)
            {
                _consolesView ??= _services.GetRequiredService<ConsolesView>();
                if (_consolesView is ConsolesView consolesView)
                {
                    _ = consolesView.RefreshAsync();
                }

                return _consolesView;
            }

            if (button == ModulesButton)
            {
                if (_modulesView == null)
                {
                    var modulesView = _services.GetRequiredService<ModulesView>();
                    modulesView.Initialize(this, _allModules, _moduleInstaller, _moduleRunner);
                    _modulesView = modulesView;
                }

                return _modulesView;
            }

            if (button == DownloadsButton)
            {
                if (_homeView == null)
                {
                    var homeView = _services.GetRequiredService<HomeView>();
                    homeView.Initialize(this);
                    _homeView = homeView;
                }

                return ContentArea.Content ?? _homeView;
            }

            if (button == NotificationsButton)
            {
                if (_homeView == null)
                {
                    var homeView = _services.GetRequiredService<HomeView>();
                    homeView.Initialize(this);
                    _homeView = homeView;
                }

                return ContentArea.Content ?? _homeView;
            }

            if (button == AslmApiButton)
            {
                _aslmApiView ??= _services.GetRequiredService<AslmApiView>();
                if (_aslmApiView is AslmApiView aslmApiView)
                {
                    _ = aslmApiView.RefreshAsync();
                }

                return _aslmApiView;
            }

            return new Label { Text = "Unknown page", TextColor = Colors.White };
        }

        // Button labels

        /// <summary>
        /// Returns the display label for one static shell button.
        /// </summary>
        private string GetButtonLabel(Button button)
        {
            if (button == HomeButton)
            {
                return LabelHome;
            }

            if (button == ConsolesButton)
            {
                return LabelConsoles;
            }

            if (button == ModulesButton)
            {
                return LabelModules;
            }

            if (button == AslmApiButton)
            {
                return LabelApi;
            }

            if (button == NotificationsButton)
            {
                return LabelNotifications;
            }

            if (button == DownloadsButton)
            {
                return LabelDownload;
            }

            if (button == SettingsButton)
            {
                return LabelSettings;
            }

            return string.Empty;
        }


        // Module page buttons

        /// <summary>
        /// Rebuilds sidebar buttons for enabled modules that expose a page.
        /// </summary>
        private void BuildPageButtons()
        {
            ModulePagePanel.Children.Clear();

            var pageModules = _allModules
                .Where(module => module.HasPage && module.Status.Enabled)
                .ToList();

            foreach (var module in pageModules)
            {
                ImageSource sidebarIcon;
                if (!string.IsNullOrEmpty(module.SidebarIconFullPath) && File.Exists(module.SidebarIconFullPath))
                {
                    sidebarIcon = ImageSource.FromFile(module.SidebarIconFullPath);
                }
                else
                {
                    sidebarIcon = IconPage;
                }

                var button = new Button
                {
                    Text = _panelExpanded ? module.Name : string.Empty,
                    AutomationId = module.Id,
                    BindingContext = module,
                    ClassId = "PAGE",
                    ImageSource = sidebarIcon,
                    ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 0),
                    Style = (Style)Application.Current!.Resources["SidebarButton"],
                    BackgroundColor = TransparentBackground,
                    TextColor = InactiveTextColor,
                    HeightRequest = 36,
                    HorizontalOptions = LayoutOptions.Fill
                };

                var capturedModule = module;
                button.Clicked += (_, _) => ActivateModulePage(capturedModule);
                button.HandlerChanged += (sender, _) => UpdateButtonAlignment((Button)sender!);
                ModulePagePanel.Children.Add(button);
            }

            _pageButtonLayoutCts?.Cancel();
            _pageButtonLayoutCts?.Dispose();
            _pageButtonLayoutCts = new CancellationTokenSource();
            _ = RefreshPageButtonLayoutAsync(_pageButtonLayoutCts.Token);
        }

        /// <summary>
        /// Re-applies page button layout after image-backed buttons finish their first native measure.
        /// </summary>
        private async Task RefreshPageButtonLayoutAsync(CancellationToken ct)
        {
            try
            {
                foreach (var delay in new[] { 16, 80, 160, 320 })
                {
                    await Task.Delay(delay, ct);
                    Dispatcher.Dispatch(ApplyPageButtonLayout);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>
        /// Applies text/icon spacing and native alignment to all dynamic module page buttons.
        /// </summary>
        private void ApplyPageButtonLayout()
        {
            var spacing = _panelExpanded ? 14 : 0;
            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button button)
                {
                    button.ContentLayout = new Button.ButtonContentLayout(
                        Button.ButtonContentLayout.ImagePosition.Left,
                        spacing);
                    UpdateButtonAlignment(button);
                }
            }
        }

        // Module page activation

        /// <summary>
        /// Opens one module page inside the embedded browser and updates highlighting.
        /// </summary>
        private void ActivateModulePage(ModuleConfig module)
        {
            _activeModule = module;
            var url = _portManager.GetModuleUrl(module);

            ContentArea.Content = null;
            Browser.Source = url;
            Browser.IsVisible = true;

            // Clear active styling from shell buttons before highlighting the module page button.
            foreach (var button in _navButtons)
            {
                button.TextColor = InactiveTextColor;
                button.BackgroundColor = TransparentBackground;
            }

            _activeNavButton = null;

            foreach (var child in ModulePagePanel.Children)
            {
                if (child is not Button button)
                {
                    continue;
                }

                if (button.ClassId == "PAGE" && button.AutomationId == module.Id)
                {
                    button.TextColor = ActiveTextColor;
                    button.BackgroundColor = ActiveBackground;
                    continue;
                }

                button.TextColor = InactiveTextColor;
                button.BackgroundColor = TransparentBackground;
            }
        }


        // Module startup

        /// <summary>
        /// Starts enabled modules that expose run commands when the shell opens.
        /// </summary>
        private async Task StartEnabledModulesAsync()
        {
            var enabledModules = _allModules
                .Where(module => module.Status.Enabled && module.Commands.Run.Count > 0)
                .ToList();

            if (enabledModules.Count == 0)
            {
                return;
            }

            using var startThrottle = new SemaphoreSlim(MaxConcurrentModuleStarts);
            var startTasks = enabledModules.Select(async module =>
            {
                await startThrottle.WaitAsync();
                try
                {
                    var logProgress = new Progress<string>(message => System.Diagnostics.Debug.WriteLine($"[ModuleStart:{module.Name}] {message}"));

                    await Task.Run(() => _moduleRunner.ExecuteRunAsync(module, logProgress, CancellationToken.None));
                }
                finally
                {
                    startThrottle.Release();
                }
            });

            await Task.WhenAll(startTasks);
        }


        // Browser safety

        /// <summary>
        /// Keeps the embedded browser on local module pages and opens external links outside the app.
        /// </summary>
        private void Browser_Navigating(object? sender, WebNavigatingEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Url))
            {
                return;
            }

            Uri uri;
            try
            {
                uri = new Uri(e.Url);
            }
            catch
            {
                return;
            }

            var isInternal =
                (uri.Scheme == "http" || uri.Scheme == "https") &&
                (uri.Host == "127.0.0.1" || uri.Host == "localhost");

            if (!isInternal)
            {
                e.Cancel = true;
                _ = Launcher.OpenAsync(uri);
            }
        }


        // Native alignment

        /// <summary>
        /// Aligns WinUI button content to the left to match the sidebar layout.
        /// </summary>
        private void UpdateButtonAlignment(Button button)
        {
#if WINDOWS
            if (button.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Button nativeButton)
            {
                nativeButton.HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
            }
#endif
        }
    }
}
