using ASLM.Models;
using ASLM.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ASLM.Pages
{
    /// <summary>
    /// Settings content view — displays user profile and port allocation settings.
    /// No separate sidebar needed; hosted inside AppShellPage.
    /// </summary>
    public partial class SettingsView : ContentView
    {
        private readonly AppDataService _appData;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly List<SettingControlMapping> _settingMappings = [];

        private record SettingControlMapping(ModuleConfig Module, ModuleSetting Setting, Entry Entry);

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsView"/> class.
        /// </summary>
        public SettingsView(AppDataService appData, ModuleInstaller moduleInstaller)
        {
            _appData = appData;
            _moduleInstaller = moduleInstaller;
            InitializeComponent();
            LoadSettings();
        }

        /// <summary>Populates the UI fields with current persisted values.</summary>
        private async void LoadSettings()
        {
            UsernameEntry.Text = _appData.Data.User.Name;
            OfficialPortEntry.Text = _appData.Data.Ports.OfficialStart.ToString();
            ThirdPartyPortEntry.Text = _appData.Data.Ports.ThirdPartyStart.ToString();

            await PopulateModuleSettingsAsync();
        }

        private async Task PopulateModuleSettingsAsync()
        {
            ModuleSettingsContainer.Children.Clear();
            _settingMappings.Clear();

            var modules = await _moduleInstaller.DiscoverModulesAsync();
            foreach (var module in modules)
            {
                var settingsToShow = module.Settings?.Where(s => s.Type != "port").ToList() ?? [];
                if (settingsToShow.Count == 0) continue;

                // Create a Border section for each module
                var border = new Border
                {
                    BackgroundColor = Color.FromArgb("#1D1D1F"),
                    Stroke = Color.FromArgb("#3A3A3C"),
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = 20
                };

                var stack = new VerticalStackLayout { Spacing = 12 };
                stack.Children.Add(new Label { Text = $"{module.Name} Settings", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.White });

                foreach (var setting in settingsToShow)
                {
                    stack.Children.Add(new Label { Text = setting.Name, FontSize = 12, TextColor = Color.FromArgb("#8E8E93") });
                    
                    var entry = new Entry
                    {
                        Text = (setting.Value ?? setting.Default)?.ToString(),
                        FontSize = 14,
                        TextColor = Colors.White,
                        BackgroundColor = Colors.Black
                    };
                    
                    stack.Children.Add(entry);
                    _settingMappings.Add(new SettingControlMapping(module, setting, entry));
                }

                border.Content = stack;
                ModuleSettingsContainer.Children.Add(border);
            }
        }

        // --- Save ---

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            if (!int.TryParse(OfficialPortEntry.Text, out var op) || op < 1024 || op > 65000)
            {
                ShowPortError("Official port must be between 1024 and 65000.");
                return;
            }
            if (!int.TryParse(ThirdPartyPortEntry.Text, out var tp) || tp < 1024 || tp > 64000)
            {
                ShowPortError("Third-party port must be between 1024 and 64000.");
                return;
            }

            // Check overlap: [op, op+100) vs [tp, tp+1000)
            int opEnd = op + 100;
            int tpEnd = tp + 1000;
            if (op < tpEnd && tp < opEnd)
            {
                ShowPortError($"Port ranges overlap! Official {op}–{opEnd - 1} conflicts with Third-party {tp}–{tpEnd - 1}.");
                return;
            }

            PortErrorLabel.IsVisible = false;

            // 1. Save App Settings
            _appData.Data.User.Name = UsernameEntry.Text?.Trim() ?? "";
            _appData.Data.Ports.OfficialStart = op;
            _appData.Data.Ports.ThirdPartyStart = tp;
            await _appData.SaveAsync();

            // 2. Save Module Settings
            var touchedModules = new HashSet<ModuleConfig>();
            foreach (var mapping in _settingMappings)
            {
                var newValue = mapping.Entry.Text;
                if (mapping.Setting.Value?.ToString() != newValue)
                {
                    mapping.Setting.Value = newValue;
                    touchedModules.Add(mapping.Module);
                }
            }

            foreach (var module in touchedModules)
            {
                _moduleInstaller.SaveModuleConfig(module);
            }

            if (Application.Current?.Windows.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlertAsync("Success", "Settings saved. Changes will apply on next module start.", "OK");
            }
        }

        private void ShowPortError(string message)
        {
            PortErrorLabel.Text = message;
            PortErrorLabel.IsVisible = true;
        }
    }
}
