using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Main application page.
    /// Left sidebar: collapsible nav with home, module pages, settings.
    /// Main area: module dashboard (cards with enable/disable) or WebView.
    /// </summary>
    public partial class MainPage : ContentPage, INotifyPropertyChanged
    {
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly IServiceProvider _services;

        private List<ModuleConfig> _allModules = [];
        private ModuleConfig? _activeModule;
        private bool _panelExpanded = true;

        private const double PanelExpandedWidth = 240;
        private const double PanelCollapsedWidth = 60;
        private const double MinCardWidth = 400;

        // Colors
        private static readonly Color ActiveTextColor = Colors.White;
        private static readonly Color InactiveTextColor = Color.FromArgb("#8E8E93");
        private static readonly Color ActiveBg = Color.FromArgb("#1D1D1F");
        private static readonly Color TransparentBg = Colors.Transparent;

        /// <summary>
        /// Collection of view models for the dashboard.
        /// </summary>
        public ObservableCollection<ModuleViewModel> Modules { get; } = new();

        private int _gridSpan = 1;
        /// <summary>
        /// Gets or sets the column span for the responsive grid layout.
        /// </summary>
        public int GridSpan
        {
            get => _gridSpan;
            set
            {
                if (_gridSpan != value)
                {
                    _gridSpan = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainPage"/> class.
        /// </summary>
        public MainPage(
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

            // Left-align static sidebar buttons via native handler
            HomeButton.HandlerChanged += AlignButtonLeft;
            SettingsButton.HandlerChanged += AlignButtonLeft;
        }

        /// <inheritdoc />
        public new event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Invokes the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed.</param>
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <inheritdoc />
        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);

            // Calculate available width for the dashboard
            // We subtract the SidePanel width (approximated if not laid out yet)
            var sidePanelWidth = SidePanel.Width > 0 ? SidePanel.Width : (_panelExpanded ? PanelExpandedWidth : PanelCollapsedWidth);
            // Also subtract margins (30 on left + 30 on right = 60)
            var dashboardMargin = 60;
            var availableWidth = width - sidePanelWidth - dashboardMargin;

            if (availableWidth > 0)
            {
                var newSpan = Math.Max(1, (int)(availableWidth / MinCardWidth));
                GridSpan = newSpan;
            }
        }

        private async void OnPageLoaded(object? sender, EventArgs e)
        {
            await RefreshModulesAsync();
            ShowDashboard();
            _ = StartEnabledModulesAsync();
        }

        private async Task RefreshModulesAsync()
        {
            _allModules = await _moduleInstaller.DiscoverModulesAsync();

            Modules.Clear();
            foreach (var module in _allModules)
            {
                Modules.Add(new ModuleViewModel(module, _moduleInstaller, _moduleRunner, OnModuleStateChanged));
            }

            BuildPageButtons();
        }

        private void OnModuleStateChanged()
        {
            // Update sidebar buttons when module state changes (e.g. enabled/disabled)
            BuildPageButtons();
        }

        // --- Panel Collapse/Expand -------------------------------------------

        private void OnCollapseClicked(object? sender, EventArgs e)
        {
            _panelExpanded = !_panelExpanded;

            SidePanel.WidthRequest = _panelExpanded ? PanelExpandedWidth : PanelCollapsedWidth;
            CollapseButton.Text = _panelExpanded ? "◀" : "▶";
            CollapseButton.HorizontalOptions = _panelExpanded ? LayoutOptions.End : LayoutOptions.Center;
            PagesHeader.IsVisible = _panelExpanded;

            // Update button text for expanded/collapsed
            HomeButton.Text = _panelExpanded ? "🏠  Home" : "🏠";

            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                {
                    var icon = btn.ClassId ?? "📄";
                    btn.Text = _panelExpanded ? $"{icon}  {btn.AutomationId}" : icon;
                }
            }

            SettingsButton.Text = _panelExpanded ? "⚙  Settings" : "⚙";
        }

        // --- Home / Dashboard ------------------------------------------------

        private void OnHomeClicked(object? sender, EventArgs e) => ShowDashboard();

        private void ShowDashboard()
        {
            _activeModule = null;
            Browser.IsVisible = false;
            DashboardView.IsVisible = true;

            // Highlight home button
            HomeButton.TextColor = ActiveTextColor;
            HomeButton.BackgroundColor = ActiveBg;

            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                {
                    btn.TextColor = InactiveTextColor;
                    btn.BackgroundColor = TransparentBg;
                }
            }
        }

        // --- Module Page Buttons (Sidebar) -----------------------------------

        private void BuildPageButtons()
        {
            ModulePagePanel.Children.Clear();

            var pageModules = _allModules
                .Where(m => m.HasPage && m.Status.Enabled)
                .ToList();

            foreach (var module in pageModules)
            {
                var icon = "📄";
                var btn = new Button
                {
                    Text = _panelExpanded ? $"{icon}  {module.Name}" : icon,
                    AutomationId = module.Name,
                    ClassId = icon,
                    Style = (Style)Application.Current!.Resources["SidebarButton"],
                    BackgroundColor = TransparentBg,
                    TextColor = InactiveTextColor,
                    FontSize = 12,
                    HeightRequest = 24,
                    HorizontalOptions = LayoutOptions.Fill
                };

                var captured = module;
                btn.Clicked += (s, e) => ActivateModulePage(captured);
                btn.HandlerChanged += AlignButtonLeft;
                ModulePagePanel.Children.Add(btn);
            }

            if (pageModules.Count == 0)
            {
                ModulePagePanel.Children.Add(new Label
                {
                    Text = _panelExpanded ? "No module pages" : "",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#8E8E93"),
                    Margin = new Thickness(8, 2, 0, 0)
                });
            }
        }

        // --- Module Page Activation ------------------------------------------

        private void ActivateModulePage(ModuleConfig module)
        {
            _activeModule = module;

            var url = "http://127.0.0.1:8000/";

            DashboardView.IsVisible = false;
            Browser.Source = url;
            Browser.IsVisible = true;

            // Dim home, highlight active page button
            HomeButton.TextColor = InactiveTextColor;
            HomeButton.BackgroundColor = TransparentBg;

            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                {
                    var isActive = btn.AutomationId == module.Name;
                    btn.TextColor = isActive ? ActiveTextColor : InactiveTextColor;
                    btn.BackgroundColor = isActive ? ActiveBg : TransparentBg;
                }
            }
        }

        // --- Module Startup --------------------------------------------------

        private async Task StartEnabledModulesAsync()
        {
            var enabledModules = _allModules
                .Where(m => m.Status.Enabled && m.Commands.Run.Count > 0)
                .ToList();

            if (enabledModules.Count == 0) return;

            var logProgress = new Progress<string>(msg =>
                Debug.WriteLine($"[ModuleStart] {msg}"));

            foreach (var module in enabledModules)
            {
                _ = Task.Run(() =>
                    _moduleRunner.ExecuteRunAsync(module, logProgress, CancellationToken.None));
                await Task.Delay(500);
            }
        }

        // --- Navigation & Settings -------------------------------------------

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

        private void OnSettingsClicked(object? sender, EventArgs e)
        {
            if (Application.Current?.Windows.Count > 0)
            {
                var settingsPage = _services.GetRequiredService<SettingsPage>();
                Application.Current.Windows[0].Page = settingsPage;
            }
        }

        /// <summary>Sets native WinUI button content alignment to left.</summary>
        private static void AlignButtonLeft(object? sender, EventArgs e)
        {
#if WINDOWS
            if (sender is Button btn && btn.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Button native)
            {
                native.HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
            }
#endif
        }
    }

    /// <summary>
    /// ViewModel wrapper for ModuleConfig to handle UI state and commands.
    /// </summary>
    public class ModuleViewModel : INotifyPropertyChanged
    {
        private readonly ModuleConfig _config;
        private readonly ModuleInstaller _installer;
        private readonly ModuleRunner _runner;
        private readonly Action _onStateChanged;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleViewModel"/> class.
        /// </summary>
        public ModuleViewModel(ModuleConfig config, ModuleInstaller installer, ModuleRunner runner, Action onStateChanged)
        {
            _config = config;
            _installer = installer;
            _runner = runner;
            _onStateChanged = onStateChanged;

            LaunchCommand = new Command(OnLaunch);
            StopCommand = new Command(OnStop);
            RestartCommand = new Command(OnRestart);
        }

        /// <summary>Module Name.</summary>
        public string Name => _config.Name;
        /// <summary>Formatted Version.</summary>
        public string VersionString => $"v{_config.Version}";
        /// <summary>Icon path.</summary>
        public string? IconFullPath => _config.IconFullPath;

        /// <summary>Is the module currently enabled/running.</summary>
        public bool IsRunning => _config.Status.Enabled;
        /// <summary>Is the module currently stopped.</summary>
        public bool IsStopped => !_config.Status.Enabled;

        /// <summary>Command to launch the module.</summary>
        public ICommand LaunchCommand { get; }
        /// <summary>Command to stop the module.</summary>
        public ICommand StopCommand { get; }
        /// <summary>Command to restart the module.</summary>
        public ICommand RestartCommand { get; }

        private async void OnLaunch()
        {
            _config.Status.Enabled = true;
            _installer.SaveModuleConfig(_config);
            NotifyStateChanged();
            _onStateChanged?.Invoke();

            if (_config.Commands.Run.Count > 0)
            {
                var logProgress = new Progress<string>(msg =>
                    Debug.WriteLine($"[Launch] {msg}"));
                await Task.Run(() =>
                    _runner.ExecuteRunAsync(_config, logProgress, CancellationToken.None));
            }
        }

        private async void OnStop()
        {
            // Actually kill the running processes
            await _runner.StopModuleAsync(_config.SourcePath);

            _config.Status.Enabled = false;
            _installer.SaveModuleConfig(_config);
            NotifyStateChanged();
            _onStateChanged?.Invoke();
        }

        private async void OnRestart()
        {
            // Stop existing processes first
            await _runner.StopModuleAsync(_config.SourcePath);

            var logProgress = new Progress<string>(msg =>
                Debug.WriteLine($"[Restart] {msg}"));
            await Task.Run(() =>
                _runner.ExecuteRunAsync(_config, logProgress, CancellationToken.None));
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsStopped));
        }

        /// <summary>
        /// Invokes the PropertyChanged event.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
