// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using ASLM.Models;
using ASLM.Services;

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
        private const string IconDownload = "icon_download.png";
        private const string IconSettings = "icon_settings.png";
        private const string IconPage = "icon_page.png";

        private const string LabelHome = "Home";
        private const string LabelConsoles = "Consoles";
        private const string LabelModules = "Modules";
        private const string LabelDownload = "Download";
        private const string LabelSettings = "Settings";

        private static readonly Color ActiveTextColor = Colors.White;
        private static readonly Color InactiveTextColor = Color.FromArgb("#8E8E93");
        private static readonly Color ActiveBackground = Color.FromArgb("#2C2C2E");
        private static readonly Color TransparentBackground = Colors.Transparent;

        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly PortManager _portManager;
        private readonly IServiceProvider _services;

        private List<ModuleConfig> _allModules = [];
        private ModuleConfig? _activeModule;
        private bool _panelExpanded;
        private bool _hasLoaded;

        private View? _homeView;
        private View? _consolesView;
        private View? _moduleManagementView;
        private View? _downloadModulesView;
        private View? _settingsView;

        private Button? _activeNavButton;
        private Button[] _navButtons = [];

        // Initialization

        /// <summary>
        /// Creates the application shell and restores the saved sidebar state.
        /// </summary>
        public AppShellPage(
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            PortManager portManager,
            IServiceProvider services)
        {
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _portManager = portManager;
            _services = services;

            InitializeComponent();
            BindingContext = this;
            Loaded += OnPageLoaded;

            // Restore the sidebar width before the first render so the shell opens in the saved state.
            _panelExpanded = Preferences.Default.Get("SidebarExpanded", false);
            SidePanel.WidthRequest = _panelExpanded ? PanelExpandedWidth : PanelCollapsedWidth;

            _navButtons = [HomeButton, ConsolesButton, ModuleManagementButton, UploadModulesButton, SettingsButton];

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
            ModuleManagementButton.ImageSource = IconModules;
            UploadModulesButton.ImageSource = IconDownload;
            SettingsButton.ImageSource = IconSettings;

            // Initialize button labels and image layout based on the current sidebar width.
            foreach (var button in _navButtons)
            {
                button.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 14);
                button.Text = _panelExpanded ? GetButtonLabel(button) : string.Empty;
            }
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
            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;
            await RefreshModulesAsync();
            NavigateTo(ModuleManagementButton);
            _ = StartEnabledModulesAsync();
        }

        // Module refresh

        /// <summary>
        /// Reloads modules, rebuilds page buttons, and refreshes the dashboard if needed.
        /// </summary>
        private async Task RefreshModulesAsync()
        {
            _allModules = await _moduleInstaller.DiscoverModulesAsync();
            BuildPageButtons();

            if (_moduleManagementView is ModuleManagementView moduleManagementView)
            {
                moduleManagementView.RefreshModules(_allModules, _moduleInstaller, _moduleRunner);
            }
        }

        // Module state callback

        /// <summary>
        /// Refreshes module page buttons after a module changes state.
        /// </summary>
        internal void OnModuleStateChanged()
        {
            BuildPageButtons();
        }


        // Sidebar

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
                NavigateTo(button);
            }
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

        // View resolution

        /// <summary>
        /// Returns the cached shell view for the selected navigation button.
        /// </summary>
        private View GetViewForButton(Button button)
        {
            if (button == HomeButton)
            {
                _homeView ??= _services.GetRequiredService<HomeView>();
                return _homeView;
            }

            if (button == ConsolesButton)
            {
                _consolesView ??= _services.GetRequiredService<ConsolesView>();
                return _consolesView;
            }

            if (button == ModuleManagementButton)
            {
                if (_moduleManagementView == null)
                {
                    var moduleManagementView = _services.GetRequiredService<ModuleManagementView>();
                    moduleManagementView.Initialize(this, _allModules, _moduleInstaller, _moduleRunner);
                    _moduleManagementView = moduleManagementView;
                }

                return _moduleManagementView;
            }

            if (button == UploadModulesButton)
            {
                _downloadModulesView ??= _services.GetRequiredService<DownloadModulesView>();
                return _downloadModulesView;
            }

            if (button == SettingsButton)
            {
                _settingsView ??= _services.GetRequiredService<SettingsView>();
                return _settingsView;
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

            if (button == ModuleManagementButton)
            {
                return LabelModules;
            }

            if (button == UploadModulesButton)
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

            // Re-apply ContentLayout in a retry loop until images from disk are fully loaded.
            // At restart the first iteration is usually enough; at cold start it may take longer.
            _ = Task.Run(async () =>
            {
                for (var i = 0; i < 20; i++)
                {
                    await Task.Delay(50);
                    Dispatcher.Dispatch(() =>
                    {
                        var spacing = _panelExpanded ? 14 : 0;
                        foreach (var child in ModulePagePanel.Children)
                        {
                            if (child is Button btn)
                            {
                                btn.ContentLayout = new Button.ButtonContentLayout(
                                    Button.ButtonContentLayout.ImagePosition.Left, spacing);
                                UpdateButtonAlignment(btn);
                            }
                        }
                    });
                }
            });
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
        private Task StartEnabledModulesAsync()
        {
            var enabledModules = _allModules
                .Where(module => module.Status.Enabled && module.Commands.Run.Count > 0)
                .ToList();

            if (enabledModules.Count == 0)
            {
                return Task.CompletedTask;
            }

            // Start all enabled modules in parallel — no artificial delay between them.
            foreach (var module in enabledModules)
            {
                var logProgress = new Progress<string>(message =>
                    System.Diagnostics.Debug.WriteLine($"[ModuleStart:{module.Name}] {message}"));

                _ = Task.Run(() => _moduleRunner.ExecuteRunAsync(module, logProgress, CancellationToken.None));
            }

            return Task.CompletedTask;
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
