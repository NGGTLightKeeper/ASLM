using System.Diagnostics;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Main application page.
    /// Left sidebar: Home button, module page buttons, settings.
    /// Main area: module dashboard (cards with enable/disable) or WebView.
    /// </summary>
    public partial class MainPage : ContentPage
    {
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly IServiceProvider _services;

        private List<ModuleConfig> _allModules = [];
        private ModuleConfig? _activeModule;
        private bool _panelExpanded = true;

        private const double PanelExpandedWidth = 300;
        private const double PanelCollapsedWidth = 48;

        public MainPage(
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            IServiceProvider services)
        {
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _services = services;
            InitializeComponent();
            Loaded += OnPageLoaded;
        }

        private void OnPageLoaded(object? sender, EventArgs e)
        {
            _allModules = _moduleInstaller.DiscoverModules();
            BuildPageButtons();
            BuildModuleCards();
            ShowDashboard();
            _ = StartEnabledModulesAsync();
        }

        // --- Panel Collapse/Expand -------------------------------------------

        private void OnCollapseClicked(object? sender, EventArgs e)
        {
            _panelExpanded = !_panelExpanded;

            SidePanel.WidthRequest = _panelExpanded ? PanelExpandedWidth : PanelCollapsedWidth;
            CollapseButton.Text = _panelExpanded ? "◀" : "▶";
            PagesHeader.IsVisible = _panelExpanded;
            HomeButton.Text = _panelExpanded ? "🏠 Home" : "🏠";

            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                {
                    btn.Text = _panelExpanded ? (btn.AutomationId ?? "?") : "";
                    btn.WidthRequest = _panelExpanded ? -1 : PanelCollapsedWidth - 16;
                    btn.HeightRequest = _panelExpanded ? 40 : PanelCollapsedWidth - 16;
                }
            }

            SettingsButton.Text = _panelExpanded ? "⚙ Settings" : "⚙";
        }

        // --- Home / Dashboard ------------------------------------------------

        private void OnHomeClicked(object? sender, EventArgs e)
        {
            ShowDashboard();
        }

        private void ShowDashboard()
        {
            _activeModule = null;
            Browser.IsVisible = false;
            DashboardView.IsVisible = true;

            // Reset page button highlights
            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                    btn.BackgroundColor = Color.FromArgb("#333");
            }

            // Highlight home button
            HomeButton.BackgroundColor = Color.FromArgb("#0078D4");
        }

        // --- Module Cards (Dashboard) ----------------------------------------

        private void BuildModuleCards()
        {
            ModuleCardsPanel.Children.Clear();

            foreach (var module in _allModules)
            {
                var card = CreateModuleCard(module);
                ModuleCardsPanel.Children.Add(card);
            }

            if (_allModules.Count == 0)
            {
                ModuleCardsPanel.Children.Add(new Label
                {
                    Text = "No modules found. Install modules via the Setup Wizard.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#888"),
                    Margin = new Thickness(0, 20, 0, 0)
                });
            }
        }

        private Border CreateModuleCard(ModuleConfig module)
        {
            // Status indicator
            var statusDot = new BoxView
            {
                WidthRequest = 10,
                HeightRequest = 10,
                CornerRadius = 5,
                Color = module.Status.Enabled
                    ? Color.FromArgb("#4EC9B0")
                    : Color.FromArgb("#555"),
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            // Module name
            var nameLabel = new Label
            {
                Text = module.Name,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                VerticalOptions = LayoutOptions.Center
            };

            // Header row
            var header = new HorizontalStackLayout { Spacing = 0 };
            header.Children.Add(statusDot);
            header.Children.Add(nameLabel);

            // Version
            var versionLabel = new Label
            {
                Text = $"v{module.Version}",
                FontSize = 11,
                TextColor = Color.FromArgb("#888"),
                Margin = new Thickness(0, 2, 0, 4)
            };

            // Description
            var descLabel = new Label
            {
                Text = module.Description,
                FontSize = 12,
                TextColor = Color.FromArgb("#AAA"),
                LineBreakMode = LineBreakMode.WordWrap,
                MaxLines = 2
            };

            // Enable/Disable toggle row
            var toggleLabel = new Label
            {
                Text = module.Status.Enabled ? "Enabled" : "Disabled",
                FontSize = 12,
                TextColor = module.Status.Enabled
                    ? Color.FromArgb("#4EC9B0")
                    : Color.FromArgb("#888"),
                VerticalOptions = LayoutOptions.Center
            };

            var toggle = new Microsoft.Maui.Controls.Switch
            {
                IsToggled = module.Status.Enabled,
                OnColor = Color.FromArgb("#0078D4"),
                ThumbColor = Colors.White,
                VerticalOptions = LayoutOptions.Center
            };

            var captured = module;
            toggle.Toggled += (s, e) =>
            {
                OnModuleToggled(captured, e.Value);
                // Update card visuals
                statusDot.Color = e.Value
                    ? Color.FromArgb("#4EC9B0")
                    : Color.FromArgb("#555");
                toggleLabel.Text = e.Value ? "Enabled" : "Disabled";
                toggleLabel.TextColor = e.Value
                    ? Color.FromArgb("#4EC9B0")
                    : Color.FromArgb("#888");
            };

            var toggleRow = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                ],
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetColumn(toggleLabel, 0);
            Grid.SetColumn(toggle, 1);
            toggleRow.Children.Add(toggleLabel);
            toggleRow.Children.Add(toggle);

            // Card content
            var content = new VerticalStackLayout { Spacing = 2 };
            content.Children.Add(header);
            content.Children.Add(versionLabel);
            content.Children.Add(descLabel);
            content.Children.Add(toggleRow);

            // Card border
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#2D2D30"),
                Stroke = Color.FromArgb("#3F3F46"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(16),
                MaximumWidthRequest = 500,
                HorizontalOptions = LayoutOptions.Start,
                Content = content
            };

            return card;
        }

        // --- Module Toggle ---------------------------------------------------

        private void OnModuleToggled(ModuleConfig module, bool enabled)
        {
            module.Status.Enabled = enabled;
            _moduleInstaller.SaveModuleConfig(module);

            if (enabled && module.Commands.Run.Count > 0)
            {
                var logProgress = new Progress<string>(msg =>
                    Debug.WriteLine($"[ModuleToggle] {msg}"));
                _ = Task.Run(() =>
                    _moduleRunner.ExecuteRunAsync(module, logProgress, CancellationToken.None));
            }

            // Rebuild sidebar page buttons
            BuildPageButtons();
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
                var btn = new Button
                {
                    Text = module.Name,
                    AutomationId = module.Name,
                    BackgroundColor = Color.FromArgb("#333"),
                    TextColor = Colors.White,
                    FontSize = 13,
                    HeightRequest = 40,
                    HorizontalOptions = LayoutOptions.Fill
                };

                var captured = module;
                btn.Clicked += (s, e) => ActivateModulePage(captured);
                ModulePagePanel.Children.Add(btn);
            }

            if (pageModules.Count == 0)
            {
                ModulePagePanel.Children.Add(new Label
                {
                    Text = "No module pages",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#555"),
                    Margin = new Thickness(4, 2, 0, 0)
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

            // Highlight active page button, dim home
            HomeButton.BackgroundColor = Color.FromArgb("#333");
            foreach (var child in ModulePagePanel.Children)
            {
                if (child is Button btn)
                {
                    var isActive = btn.AutomationId == module.Name;
                    btn.BackgroundColor = Color.FromArgb(isActive ? "#0078D4" : "#333");
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
    }
}
