using System.ComponentModel;
using System.Runtime.CompilerServices;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Main application shell. Contains the shared sidebar and hosts content views.
    /// </summary>
    public partial class AppShellPage : ContentPage, INotifyPropertyChanged
    {
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly IServiceProvider _services;

        private List<ModuleConfig> _allModules = [];
        private ModuleConfig? _activeModule;
        private bool _panelExpanded;

        private const double PanelExpandedWidth = 240;
        private const double PanelCollapsedWidth = 48;

        // Sidebar SVG icon file names (SVGs in Resources/Images/)
        private const string IconMenu = "icon_menu.png";
        private const string IconHome = "icon_home.png";
        private const string IconConsole = "icon_console.png";
        private const string IconModules = "icon_modules.png";
        private const string IconDownload = "icon_download.png";
        private const string IconSettings = "icon_settings.png";
        private const string IconPage = "icon_page.png";

        // Colors
        private static readonly Color ActiveTextColor = Colors.White;
        private static readonly Color InactiveTextColor = Color.FromArgb("#8E8E93");
        private static readonly Color ActiveBg = Color.FromArgb("#2C2C2E");
        private static readonly Color TransparentBg = Colors.Transparent;

        // Named labels for buttons (used when sidebar is expanded)
        private const string LabelHome = "Home";
        private const string LabelConsoles = "Consoles";
        private const string LabelModules = "Modules";
        private const string LabelDownload = "Download";
        private const string LabelSettings = "Settings";

        // Cached content views
        private View? _homeView;
        private View? _consolesView;
        private View? _moduleManagementView;
        private View? _downloadModulesView;
        private View? _settingsView;

        // Currently active nav button
        private Button? _activeNavButton;

        // All bottom nav buttons for easy iteration
        private Button[] _navButtons = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="AppShellPage"/> class.
        /// </summary>
        public AppShellPage(
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            IServiceProvider services)
        {
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _services = services;
            InitializeComponent();
            BindingContext = this;
            Loaded += OnPageLoaded;

            // Load saved sidebar state, default to collapsed
            _panelExpanded = Preferences.Default.Get("SidebarExpanded", false);
            SidePanel.WidthRequest = _panelExpanded ? PanelExpandedWidth : PanelCollapsedWidth;

            // Collect all nav buttons for easy iteration
            _navButtons = [HomeButton, ConsolesButton, ModuleManagementButton, UploadModulesButton, SettingsButton];

            // Set static sidebar buttons native alignment handler
            CollapseButton.HandlerChanged += (s, e) => UpdateButtonAlignment((Button)s!);
            foreach (var btn in _navButtons)
            {
                btn.HandlerChanged += (s, e) => UpdateButtonAlignment((Button)s!);
            }

            // Set SVG icons for all buttons
            CollapseButton.ImageSource = IconMenu;
            HomeButton.ImageSource = IconHome;
            ConsolesButton.ImageSource = IconConsole;
            ModuleManagementButton.ImageSource = IconModules;
            UploadModulesButton.ImageSource = IconDownload;
            SettingsButton.ImageSource = IconSettings;

            // Set content layout for all nav buttons
            foreach (var btn in _navButtons)
            {
                btn.ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 14);
                btn.Text = _panelExpanded ? GetButtonLabel(btn) : "";
            }
        }

        /// <inheritdoc />
        public new event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Invokes the PropertyChanged event.
        /// </summary>
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // --- Page Loaded ---

        private async void OnPageLoaded(object? sender, EventArgs e)
        {
            await RefreshModulesAsync();
            NavigateTo(ModuleManagementButton); // Default to module management
            _ = StartEnabledModulesAsync();
        }

        private async Task RefreshModulesAsync()
        {
            _allModules = await _moduleInstaller.DiscoverModulesAsync();
            BuildPageButtons();

            // If module management view is active, refresh it
            if (_moduleManagementView is ModuleManagementView mmv)
            {
                mmv.RefreshModules(_allModules, _moduleInstaller, _moduleRunner);
            }
        }

        /// <summary>
        /// Called by ModuleManagementView when module state changes (enable/disable).
        /// </summary>
        internal void OnModuleStateChanged()
        {
            BuildPageButtons();
        }

        // --- Panel Collapse/Expand ---

        private void OnCollapseClicked(object? sender, EventArgs e)
        {
            _panelExpanded = !_panelExpanded;
            Preferences.Default.Set("SidebarExpanded", _panelExpanded);

            double targetWidth = _panelExpanded ? PanelExpandedWidth : PanelCollapsedWidth;

            var animation = new Animation(v =>
            {
                SidePanel.WidthRequest = v;
            }, SidePanel.WidthRequest, targetWidth);

            animation.Commit(this, "SidebarAnimation", 16, 150, Easing.CubicOut);

            UpdateButtonAlignment(CollapseButton);

            // Update all nav buttons
            foreach (var btn in _navButtons)
            {
                btn.Text = _panelExpanded ? GetButtonLabel(btn) : "";
                btn.ContentLayout = new Button.ButtonContentLayout(
                    Button.ButtonContentLayout.ImagePosition.Left, 14);
                btn.HorizontalOptions = LayoutOptions.Fill;
                UpdateButtonAlignment(btn);
            }

            // Update module page buttons in scroll area
            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                {
                    btn.Text = _panelExpanded ? btn.AutomationId : "";
                    btn.ContentLayout = new Button.ButtonContentLayout(
                        Button.ButtonContentLayout.ImagePosition.Left, 14);
                    btn.HorizontalOptions = LayoutOptions.Fill;
                    UpdateButtonAlignment(btn);
                }
            }
        }

        // --- Navigation ---

        private void OnNavClicked(object? sender, EventArgs e)
        {
            if (sender is Button btn)
            {
                NavigateTo(btn);
            }
        }

        private void NavigateTo(Button navButton)
        {
            _activeModule = null;
            Browser.IsVisible = false;

            // Dim all nav buttons
            foreach (var btn in _navButtons)
            {
                btn.TextColor = InactiveTextColor;
                btn.BackgroundColor = TransparentBg;
            }
            // Dim all module page buttons
            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                {
                    btn.TextColor = InactiveTextColor;
                    btn.BackgroundColor = TransparentBg;
                }
            }

            // Highlight active
            navButton.TextColor = ActiveTextColor;
            navButton.BackgroundColor = ActiveBg;
            _activeNavButton = navButton;

            // Set content
            ContentArea.Content = GetViewForButton(navButton);
        }

        private View GetViewForButton(Button btn)
        {
            if (btn == HomeButton)
            {
                _homeView ??= _services.GetRequiredService<HomeView>();
                return _homeView;
            }
            if (btn == ConsolesButton)
            {
                _consolesView ??= _services.GetRequiredService<ConsolesView>();
                return _consolesView;
            }
            if (btn == ModuleManagementButton)
            {
                if (_moduleManagementView == null)
                {
                    var mmv = _services.GetRequiredService<ModuleManagementView>();
                    mmv.Initialize(this, _allModules, _moduleInstaller, _moduleRunner);
                    _moduleManagementView = mmv;
                }
                return _moduleManagementView;
            }
            if (btn == UploadModulesButton)
            {
                _downloadModulesView ??= _services.GetRequiredService<DownloadModulesView>();
                return _downloadModulesView;
            }
            if (btn == SettingsButton)
            {
                _settingsView ??= _services.GetRequiredService<SettingsView>();
                return _settingsView;
            }
            return new Label { Text = "Unknown page", TextColor = Colors.White };
        }

        private string GetButtonLabel(Button btn)
        {
            if (btn == HomeButton) return LabelHome;
            if (btn == ConsolesButton) return LabelConsoles;
            if (btn == ModuleManagementButton) return LabelModules;
            if (btn == UploadModulesButton) return LabelDownload;
            if (btn == SettingsButton) return LabelSettings;
            return "";
        }

        // --- Module Page Buttons (Sidebar top section) ---

        private void BuildPageButtons()
        {
            ModulePagePanel.Children.Clear();

            var pageModules = _allModules
                .Where(m => m.HasPage && m.Status.Enabled)
                .ToList();

            foreach (var module in pageModules)
            {
                ImageSource sidebarIcon;
                if (!string.IsNullOrEmpty(module.SidebarIconFullPath)
                    && File.Exists(module.SidebarIconFullPath))
                {
                    var capturedPath = module.SidebarIconFullPath;
                    sidebarIcon = ImageSource.FromStream(() => File.OpenRead(capturedPath));
                }
                else
                {
                    sidebarIcon = IconPage;
                }

                var btn = new Button
                {
                    Text = _panelExpanded ? module.Name : "",
                    AutomationId = module.Name,
                    ClassId = "PAGE",
                    ImageSource = sidebarIcon,
                    ContentLayout = new Button.ButtonContentLayout(
                        Button.ButtonContentLayout.ImagePosition.Left, 14),
                    Style = (Style)Application.Current!.Resources["SidebarButton"],
                    BackgroundColor = TransparentBg,
                    TextColor = InactiveTextColor,
                    HeightRequest = 36,
                    HorizontalOptions = LayoutOptions.Fill
                };

                var captured = module;
                btn.Clicked += (s, e) => ActivateModulePage(captured);
                btn.HandlerChanged += (s, e) => UpdateButtonAlignment((Button)s!);
                ModulePagePanel.Children.Add(btn);
            }
        }

        // --- Module Page Activation ---

        private void ActivateModulePage(ModuleConfig module)
        {
            _activeModule = module;
            var url = "http://127.0.0.1:8000/";

            ContentArea.Content = null;
            Browser.Source = url;
            Browser.IsVisible = true;

            // Dim all nav buttons
            foreach (var btn in _navButtons)
            {
                btn.TextColor = InactiveTextColor;
                btn.BackgroundColor = TransparentBg;
            }
            _activeNavButton = null;

            // Highlight active module page button
            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                {
                    var isActive = btn.ClassId == "PAGE" && btn.AutomationId == module.Name;
                    btn.TextColor = isActive ? ActiveTextColor : InactiveTextColor;
                    btn.BackgroundColor = isActive ? ActiveBg : TransparentBg;
                }
            }
        }

        // --- Module Startup ---

        private async Task StartEnabledModulesAsync()
        {
            var enabledModules = _allModules
                .Where(m => m.Status.Enabled && m.Commands.Run.Count > 0)
                .ToList();

            if (enabledModules.Count == 0) return;

            var logProgress = new Progress<string>(msg =>
                System.Diagnostics.Debug.WriteLine($"[ModuleStart] {msg}"));

            foreach (var module in enabledModules)
            {
                _ = Task.Run(() =>
                    _moduleRunner.ExecuteRunAsync(module, logProgress, CancellationToken.None));
                await Task.Delay(500);
            }
        }

        // --- Browser Navigation ---

        private void Browser_Navigating(object? sender, WebNavigatingEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Url)) return;

            Uri uri;
            try { uri = new Uri(e.Url); }
            catch { return; }

            bool isInternal = (uri.Scheme == "http" || uri.Scheme == "https") &&
                              (uri.Host == "127.0.0.1" || uri.Host == "localhost");

            if (!isInternal)
            {
                e.Cancel = true;
                _ = Launcher.OpenAsync(uri);
            }
        }

        /// <summary>Sets native WinUI button content alignment.</summary>
        private void UpdateButtonAlignment(Button btn)
        {
#if WINDOWS
            if (btn.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Button native)
            {
                native.HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
            }
#endif
        }
    }
}
