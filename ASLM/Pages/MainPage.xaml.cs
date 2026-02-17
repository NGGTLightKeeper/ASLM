using System.Diagnostics;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Main application page.
    /// Left sidebar: collapsible nav with home, module pages, settings.
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

        private const double PanelExpandedWidth = 240;
        private const double PanelCollapsedWidth = 60;

        // Colors
        private static readonly Color ActiveTextColor = Colors.White;
        private static readonly Color InactiveTextColor = Color.FromArgb("#888");
        private static readonly Color ActiveBg = Color.FromArgb("#2D2D30");
        private static readonly Color TransparentBg = Colors.Transparent;

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

            // Left-align static sidebar buttons via native handler
            HomeButton.HandlerChanged += AlignButtonLeft;
            SettingsButton.HandlerChanged += AlignButtonLeft;
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
            // --- Main layout: icon (left) | content (right) ---
            var cardGrid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),  // icon
                    new ColumnDefinition(GridLength.Star)    // text + buttons
                ]
            };

            // Square icon — full card height, left side
            var iconPath = module.IconFullPath;
            if (iconPath != null && File.Exists(iconPath))
            {
                var iconBorder = new Border
                {
                    StrokeThickness = 0,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                    {
                        CornerRadius = new CornerRadius(0, 0, 0, 0)
                    },
                    BackgroundColor = Color.FromArgb("#252526"),
                    WidthRequest = 164,
                    HeightRequest = 164,
                    Padding = new Thickness(0),
                    Margin = new Thickness(8, 8, 0, 8),
                    Content = new Image
                    {
                        Source = ImageSource.FromFile(iconPath),
                        Aspect = Aspect.AspectFit,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Fill
                    }
                };
                Grid.SetColumn(iconBorder, 0);
                cardGrid.Children.Add(iconBorder);
            }

            // Right side: Grid with text on top, buttons pinned to bottom
            var rightGrid = new Grid
            {
                RowDefinitions =
                [
                    new RowDefinition(GridLength.Star),  // text area
                    new RowDefinition(GridLength.Auto)    // buttons at bottom
                ],
                Padding = new Thickness(8, 8, 8, 8)
            };

            // Text area (top)
            var textArea = new VerticalStackLayout { Spacing = 2 };
            textArea.Children.Add(new Label
            {
                Text = module.Name,
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            });
            textArea.Children.Add(new Label
            {
                Text = $"v{module.Version}",
                FontSize = 14,
                TextColor = Color.FromArgb("#888")
            });
            Grid.SetRow(textArea, 0);
            rightGrid.Children.Add(textArea);

            // Action buttons (bottom, full width)
            var captured = module;

            if (module.Status.Enabled)
            {
                // Running — Stop + Restart, equal width
                var buttonsGrid = new Grid
                {
                    ColumnDefinitions =
                    [
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star)
                    ],
                    ColumnSpacing = 8,
                    Margin = new Thickness(0, 0, 0, 0)
                };

                var stopBtn = new Button
                {
                    Text = "⏹ Stop",
                    BackgroundColor = Color.FromArgb("#8B0000"),
                    TextColor = Colors.White,
                    FontSize = 12,
                    HeightRequest = 24,
                    CornerRadius = 4,
                    HorizontalOptions = LayoutOptions.Fill
                };
                stopBtn.Clicked += (s, e) =>
                {
                    captured.Status.Enabled = false;
                    _moduleInstaller.SaveModuleConfig(captured);
                    BuildModuleCards();
                    BuildPageButtons();
                };

                var restartBtn = new Button
                {
                    Text = "🔄 Restart",
                    BackgroundColor = Color.FromArgb("#333"),
                    TextColor = Colors.White,
                    FontSize = 12,
                    HeightRequest = 24,
                    CornerRadius = 4,
                    HorizontalOptions = LayoutOptions.Fill
                };
                restartBtn.Clicked += async (s, e) =>
                {
                    var logProgress = new Progress<string>(msg =>
                        Debug.WriteLine($"[Restart] {msg}"));
                    await Task.Run(() =>
                        _moduleRunner.ExecuteRunAsync(captured, logProgress, CancellationToken.None));
                };

                Grid.SetColumn(stopBtn, 0);
                Grid.SetColumn(restartBtn, 1);
                buttonsGrid.Children.Add(stopBtn);
                buttonsGrid.Children.Add(restartBtn);

                Grid.SetRow(buttonsGrid, 1);
                rightGrid.Children.Add(buttonsGrid);
            }
            else
            {
                // Stopped — Launch, full width
                var launchBtn = new Button
                {
                    Text = "▶ Launch",
                    BackgroundColor = Color.FromArgb("#0078D4"),
                    TextColor = Colors.White,
                    FontSize = 12,
                    HeightRequest = 24,
                    CornerRadius = 4,
                    HorizontalOptions = LayoutOptions.Fill,
                    Margin = new Thickness(0, 0, 0, 0)
                };
                launchBtn.Clicked += async (s, e) =>
                {
                    captured.Status.Enabled = true;
                    _moduleInstaller.SaveModuleConfig(captured);
                    BuildModuleCards();
                    BuildPageButtons();

                    if (captured.Commands.Run.Count > 0)
                    {
                        var logProgress = new Progress<string>(msg =>
                            Debug.WriteLine($"[Launch] {msg}"));
                        await Task.Run(() =>
                            _moduleRunner.ExecuteRunAsync(captured, logProgress, CancellationToken.None));
                    }
                };

                Grid.SetRow(launchBtn, 1);
                rightGrid.Children.Add(launchBtn);
            }

            Grid.SetColumn(rightGrid, 1);
            cardGrid.Children.Add(rightGrid);

            // Card border
            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#2D2D30"),
                Stroke = Color.FromArgb("#3F3F46"),
                StrokeThickness = 1,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Padding = 0,
                HeightRequest = 180,
                Margin = new Thickness(0, 0, 10, 10),
                HorizontalOptions = LayoutOptions.Fill,
                Content = cardGrid
            };

            // FlexLayout: fixed 400px width, NO grow (0) to behave like a strict grid
            FlexLayout.SetBasis(card, new Microsoft.Maui.Layouts.FlexBasis(400));
            FlexLayout.SetGrow(card, 0);
            FlexLayout.SetShrink(card, 0);

            return card;
        }

        // --- Module Toggle ---------------------------------------------------

        private void OnModuleToggled(ModuleConfig module, bool enabled)
        {
            module.Status.Enabled = enabled;
            _moduleInstaller.SaveModuleConfig(module);
            BuildModuleCards();
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
                var icon = "📄";
                var btn = new Button
                {
                    Text = _panelExpanded ? $"{icon}  {module.Name}" : icon,
                    AutomationId = module.Name,
                    ClassId = icon,
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
                    TextColor = Color.FromArgb("#444"),
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
}
