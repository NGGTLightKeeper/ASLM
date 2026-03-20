// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;
using ASLM.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ASLM.Pages
{
    // Settings view

    /// <summary>
    /// Displays application settings and editable module settings inside the shell.
    /// </summary>
    public partial class SettingsView : ContentView
    {
        private readonly AppDataService _appData;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly List<SettingControlMapping> _settingMappings = [];
        private bool _hasLoaded;

        private record SettingControlMapping(ModuleConfig Module, ModuleSetting Setting, Entry Entry);

        // Initialization

        /// <summary>
        /// Creates the settings view and hooks the first load handler.
        /// </summary>
        public SettingsView(AppDataService appData, ModuleInstaller moduleInstaller)
        {
            _appData = appData;
            _moduleInstaller = moduleInstaller;
            InitializeComponent();
            Loaded += OnLoaded;
        }


        // Loading

        /// <summary>
        /// Loads persisted settings into the view once.
        /// </summary>
        private async void LoadSettings()
        {
            UsernameEntry.Text = _appData.Data.User.Name;
            OfficialPortEntry.Text = _appData.Data.Ports.OfficialStart.ToString();
            ThirdPartyPortEntry.Text = _appData.Data.Ports.ThirdPartyStart.ToString();

            await PopulateModuleSettingsAsync();
        }

        // Load event

        /// <summary>
        /// Triggers the initial settings load after the view is ready.
        /// </summary>
        private void OnLoaded(object? sender, EventArgs e)
        {
            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;
            LoadSettings();
        }

        // Module settings

        /// <summary>
        /// Builds the dynamic settings editor for every module that exposes user settings.
        /// </summary>
        private async Task PopulateModuleSettingsAsync()
        {
            ModuleSettingsContainer.Children.Clear();
            _settingMappings.Clear();

            var modules = await _moduleInstaller.DiscoverModulesAsync();
            foreach (var module in modules)
            {
                var settingsToShow = module.Settings?.Where(setting => setting.Type != "port").ToList() ?? [];
                if (settingsToShow.Count == 0)
                {
                    continue;
                }

                // Create one visual block per module to keep related settings together.
                var border = new Border
                {
                    BackgroundColor = Color.FromArgb("#1D1D1F"),
                    Stroke = Color.FromArgb("#3A3A3C"),
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = 20
                };

                var stack = new VerticalStackLayout { Spacing = 12 };
                stack.Children.Add(new Label
                {
                    Text = $"{module.Name} Settings",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Colors.White
                });

                foreach (var setting in settingsToShow)
                {
                    // Keep the label and entry paired so saving can map them back to the source setting.
                    stack.Children.Add(new Label
                    {
                        Text = setting.Name,
                        FontSize = 12,
                        TextColor = Color.FromArgb("#8E8E93")
                    });

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


        // Saving

        /// <summary>
        /// Validates the form and saves application and module settings.
        /// </summary>
        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            // Validate both persisted port ranges before saving anything.
            if (!int.TryParse(OfficialPortEntry.Text, out var officialPort) || officialPort < 1024 || officialPort > 65000)
            {
                ShowPortError("Official port must be between 1024 and 65000.");
                return;
            }

            if (!int.TryParse(ThirdPartyPortEntry.Text, out var thirdPartyPort) || thirdPartyPort < 1024 || thirdPartyPort > 64000)
            {
                ShowPortError("Third-party port must be between 1024 and 64000.");
                return;
            }

            // The official 100-port range and third-party 1000-port range must stay separate.
            var officialPortEnd = officialPort + 100;
            var thirdPartyPortEnd = thirdPartyPort + 1000;
            if (officialPort < thirdPartyPortEnd && thirdPartyPort < officialPortEnd)
            {
                ShowPortError($"Port ranges overlap. Official {officialPort}-{officialPortEnd - 1} conflicts with Third-party {thirdPartyPort}-{thirdPartyPortEnd - 1}.");
                return;
            }

            PortErrorLabel.IsVisible = false;

            // Persist shared application settings first.
            _appData.Data.User.Name = UsernameEntry.Text?.Trim() ?? string.Empty;
            _appData.Data.Ports.OfficialStart = officialPort;
            _appData.Data.Ports.ThirdPartyStart = thirdPartyPort;
            await _appData.SaveAsync();

            // Persist only the modules whose settings actually changed.
            var touchedModules = new HashSet<ModuleConfig>();
            foreach (var mapping in _settingMappings)
            {
                var newValue = mapping.Entry.Text;
                if (mapping.Setting.Value?.ToString() == newValue)
                {
                    continue;
                }

                mapping.Setting.Value = mapping.Setting.ParseUserInput(newValue);
                touchedModules.Add(mapping.Module);
            }

            foreach (var module in touchedModules)
            {
                _moduleInstaller.SaveModuleConfig(module);
            }

            // Use the current window page to show the completion message from inside the shell.
            if (Application.Current?.Windows.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlertAsync(
                    "Success",
                    "Settings saved. Changes will apply on next module start.",
                    "OK");
            }
        }


        // Validation UI

        /// <summary>
        /// Shows the current port validation error.
        /// </summary>
        private void ShowPortError(string message)
        {
            PortErrorLabel.Text = message;
            PortErrorLabel.IsVisible = true;
        }
    }
}
