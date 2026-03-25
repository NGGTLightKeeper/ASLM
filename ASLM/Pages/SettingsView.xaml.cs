// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;
using ASLM.Services;
using Microsoft.Maui.Controls.Shapes;

namespace ASLM.Pages
{
    // Settings view

    /// <summary>
    /// Displays shared application settings and dynamic module settings inside the shell.
    /// </summary>
    public partial class SettingsView : ContentView
    {
        private const string PasswordIconHidden = "icon_password_off.png";
        private const string PasswordIconVisible = "icon_password_on.png";

        private readonly AppDataService _appData;
        private readonly EngineInstaller _engineInstaller;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly List<SettingControlMapping> _settingMappings = [];
        private readonly Dictionary<string, SettingBaseline> _settingBaselines = new(StringComparer.OrdinalIgnoreCase);
        private List<ModuleConfig> _loadedModules = [];
        private bool _hasLoaded;
        private bool _isRefreshingVisibility;

        /// <summary>
        /// Stores how one rendered control maps back to its module setting and current draft readers.
        /// </summary>
        private record SettingControlMapping(
            ModuleConfig Module,
            ModuleSetting Setting,
            Func<object?> ReadValue,
            Func<bool>? ReadCustomValue,
            string InitialDisplayValue,
            bool InitialUseCustomValue);

        /// <summary>
        /// Couples one setting with the runtime value loaded for the current refresh pass.
        /// </summary>
        private record LoadedSetting(ModuleSetting Setting, object? Value);

        /// <summary>
        /// Captures the initial effective value used to detect real user changes across UI rebuilds.
        /// </summary>
        private record SettingBaseline(string DisplayValue, bool UseCustomValue);

        // Initialization

        /// <summary>
        /// Creates the settings view and hooks the first-load handler.
        /// </summary>
        public SettingsView(AppDataService appData, EngineInstaller engineInstaller, ModuleInstaller moduleInstaller, ModuleRunner moduleRunner)
        {
            _appData = appData;
            _engineInstaller = engineInstaller;
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // Refresh

        /// <summary>
        /// Reloads the module settings when the shell revisits this page.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (!_hasLoaded)
            {
                return;
            }

            try
            {
                await PopulateModuleSettingsAsync(reloadModules: true, reloadRuntimeValues: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to refresh settings view: {ex.Message}");
            }
        }

        // Loading

        /// <summary>
        /// Loads shared settings and discovers module settings for the current session.
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            UsernameEntry.Text = _appData.Data.User.Name;
            OfficialPortEntry.Text = _appData.Data.Ports.OfficialStart.ToString();
            ThirdPartyPortEntry.Text = _appData.Data.Ports.ThirdPartyStart.ToString();
            await PopulateModuleSettingsAsync(reloadModules: true, reloadRuntimeValues: true);
        }

        /// <summary>
        /// Initializes the settings page once after the control is first shown.
        /// </summary>
        private async void OnLoaded(object? sender, EventArgs e)
        {
            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;

            try
            {
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load settings view: {ex.Message}");
            }
        }

        // Module settings

        /// <summary>
        /// Rebuilds the dynamic module settings UI from discovered modules and their current values.
        /// </summary>
        private async Task PopulateModuleSettingsAsync(bool reloadModules, bool reloadRuntimeValues)
        {
            if (reloadModules || _loadedModules.Count == 0)
            {
                _loadedModules = await _moduleInstaller.DiscoverModulesAsync();
            }

            if (reloadRuntimeValues)
            {
                _settingBaselines.Clear();
            }

            ModuleSettingsContainer.Children.Clear();
            _settingMappings.Clear();

            foreach (var module in _loadedModules)
            {
                var settings = module.Settings?.Where(ShouldDisplaySetting).ToList() ?? [];
                if (settings.Count == 0)
                {
                    continue;
                }

                var loaded = reloadRuntimeValues
                    ? await Task.WhenAll(settings.Select(setting => LoadSettingValueAsync(module, setting)))
                    : settings.Select(setting => new LoadedSetting(setting, GetFallbackValue(module, setting))).ToArray();

                if (reloadRuntimeValues)
                {
                    UpdateSettingBaselines(module, loaded);
                }

                // Resolve visibility after reading controlling settings so dependent groups react immediately in the UI.
                var valuesByKey = loaded.ToDictionary(item => item.Setting.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
                var visible = loaded.Where(item => ShouldRenderSetting(item.Setting, settings, valuesByKey)).ToList();
                if (visible.Count == 0)
                {
                    continue;
                }

                var section = CreateModuleSectionBorder();
                var content = new VerticalStackLayout { Spacing = 14 };
                content.Children.Add(CreateSectionHeader($"{module.Name} Settings"));

                foreach (var item in visible)
                {
                    if (!item.Setting.IsAutomaticallyManaged || item.Setting.UseCustomValue)
                    {
                        item.Setting.Value = item.Value;
                    }

                    var (card, mapping) = CreateSettingCard(module, item.Setting, item.Value);
                    content.Children.Add(card);
                    if (mapping != null)
                    {
                        _settingMappings.Add(mapping);
                    }
                }

                section.Content = content;
                ModuleSettingsContainer.Children.Add(section);
            }
        }

        /// <summary>
        /// Rebuilds the visible settings list after one of the controlling toggles changes.
        /// </summary>
        private async Task RefreshDynamicVisibilityAsync()
        {
            if (_isRefreshingVisibility)
            {
                return;
            }

            try
            {
                _isRefreshingVisibility = true;
                SyncDraftValuesFromControls();
                await PopulateModuleSettingsAsync(reloadModules: false, reloadRuntimeValues: false);
            }
            finally
            {
                _isRefreshingVisibility = false;
            }
        }

        /// <summary>
        /// Captures the current control values into the in-memory module settings draft.
        /// </summary>
        private void SyncDraftValuesFromControls()
        {
            foreach (var mapping in _settingMappings)
            {
                var useCustom = mapping.ReadCustomValue?.Invoke() ?? mapping.Setting.UseCustomValue;
                mapping.Setting.UseCustomValue = useCustom;

                if (mapping.Setting.IsAutomaticallyManaged && !useCustom)
                {
                    continue;
                }

                mapping.Setting.Value = mapping.Setting.NormalizeUserValue(mapping.ReadValue());
            }
        }

        // Saving

        /// <summary>
        /// Validates and saves shared settings together with all changed module settings.
        /// </summary>
        private async void OnSaveClicked(object? sender, EventArgs e)
        {
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

            var officialPortEnd = officialPort + 100;
            var thirdPartyPortEnd = thirdPartyPort + 1000;
            if (officialPort < thirdPartyPortEnd && thirdPartyPort < officialPortEnd)
            {
                ShowPortError($"Port ranges overlap. Official {officialPort}-{officialPortEnd - 1} conflicts with Third-party {thirdPartyPort}-{thirdPartyPortEnd - 1}.");
                return;
            }

            PortErrorLabel.IsVisible = false;
            SyncDraftValuesFromControls();

            _appData.Data.User.Name = UsernameEntry.Text?.Trim() ?? string.Empty;
            _appData.Data.Ports.OfficialStart = officialPort;
            _appData.Data.Ports.ThirdPartyStart = thirdPartyPort;
            await _appData.SaveAsync();

            var touchedModules = new HashSet<ModuleConfig>();
            var deferred = new List<string>();

            foreach (var mapping in _settingMappings)
            {
                var effectiveValue = ResolveEffectiveSettingValue(mapping.Module, mapping.Setting, mapping.Setting.Value);
                var displayValue = mapping.Setting.FormatValueForDisplay(effectiveValue);
                if (mapping.InitialUseCustomValue == mapping.Setting.UseCustomValue &&
                    string.Equals(mapping.InitialDisplayValue, displayValue, StringComparison.Ordinal))
                {
                    continue;
                }

                touchedModules.Add(mapping.Module);

                if (string.IsNullOrWhiteSpace(mapping.Setting.SetExec))
                {
                    continue;
                }

                if (!File.Exists(mapping.Module.SourcePath))
                {
                    deferred.Add($"{mapping.Module.Name}: {mapping.Setting.Name}");
                    continue;
                }

                var applyResult = await _moduleRunner.ExecuteSettingCommandAsync(
                    mapping.Module,
                    mapping.Setting,
                    isSet: true,
                    newValue: displayValue,
                    CancellationToken.None);

                if (applyResult == null)
                {
                    deferred.Add($"{mapping.Module.Name}: {mapping.Setting.Name}");
                }
            }

            foreach (var module in touchedModules)
            {
                _moduleInstaller.SaveModuleConfig(module);
            }

            if (touchedModules.Count > 0)
            {
                await PopulateModuleSettingsAsync(reloadModules: false, reloadRuntimeValues: true);
            }

            if (Application.Current?.Windows.Count > 0)
            {
                await Application.Current.Windows[0].Page!.DisplayAlertAsync("Success", BuildSaveMessage(deferred), "OK");
            }
        }

        // Validation

        /// <summary>
        /// Displays the current port validation error.
        /// </summary>
        private void ShowPortError(string message)
        {
            PortErrorLabel.Text = message;
            PortErrorLabel.IsVisible = true;
        }

        /// <summary>
        /// Filters out settings that should never be shown in the UI editor.
        /// </summary>
        private static bool ShouldDisplaySetting(ModuleSetting setting) =>
            !string.Equals(setting.NormalizedType, "port", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Evaluates whether a setting should currently be visible based on its controlling toggle.
        /// </summary>
        private static bool ShouldRenderSetting(
            ModuleSetting setting,
            IReadOnlyList<ModuleSetting> allSettings,
            IReadOnlyDictionary<string, object?> valuesByKey)
        {
            var controller = FindControllingSetting(setting, allSettings, valuesByKey);
            if (controller == null || !valuesByKey.TryGetValue(controller.Key, out var value))
            {
                return true;
            }

            return value is bool enabled ? enabled : true;
        }

        /// <summary>
        /// Finds the nearest parent toggle that controls whether the current setting is displayed.
        /// </summary>
        private static ModuleSetting? FindControllingSetting(
            ModuleSetting setting,
            IReadOnlyList<ModuleSetting> allSettings,
            IReadOnlyDictionary<string, object?> valuesByKey) =>
            allSettings
                .Where(candidate =>
                    !string.Equals(candidate.Key, setting.Key, StringComparison.OrdinalIgnoreCase) &&
                    IsVisibilityToggle(candidate, valuesByKey) &&
                    IsGroupedUnder(candidate.Key, setting.Key))
                .OrderByDescending(candidate => candidate.Key.Length)
                .FirstOrDefault();

        /// <summary>
        /// Determines whether a child setting belongs to the same prefixed configuration group.
        /// </summary>
        private static bool IsGroupedUnder(string parentKey, string childKey) =>
            childKey.StartsWith(parentKey + "_", StringComparison.OrdinalIgnoreCase) ||
            childKey.StartsWith(parentKey + "-", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Detects whether a setting can act as a visibility toggle for dependent settings.
        /// </summary>
        private static bool IsVisibilityToggle(ModuleSetting setting, IReadOnlyDictionary<string, object?> valuesByKey)
        {
            if (!valuesByKey.TryGetValue(setting.Key, out var value))
            {
                return setting.NormalizedType is "bool" or "engine";
            }

            return value is bool;
        }

        /// <summary>
        /// Checks whether the current setting controls the visibility of any other setting.
        /// </summary>
        private static bool HasDependentSettings(ModuleConfig module, ModuleSetting setting) =>
            module.Settings.Any(other =>
                !string.Equals(other.Key, setting.Key, StringComparison.OrdinalIgnoreCase) &&
                IsGroupedUnder(setting.Key, other.Key) &&
                ShouldDisplaySetting(other));

        /// <summary>
        /// Reads the current runtime value for one module setting with graceful fallback behavior.
        /// </summary>
        private async Task<LoadedSetting> LoadSettingValueAsync(ModuleConfig module, ModuleSetting setting)
        {
            if (IsAutoDetectedAslmEngine(setting))
            {
                return new LoadedSetting(setting, IsAslmEngineInstalled(setting.Key));
            }

            var fallbackValue = GetFallbackValue(module, setting);
            if (setting.IsAutomaticallyManaged && !setting.UseCustomValue)
            {
                return new LoadedSetting(setting, fallbackValue);
            }

            if (string.IsNullOrWhiteSpace(setting.GetExec) || !File.Exists(module.SourcePath))
            {
                return new LoadedSetting(setting, fallbackValue);
            }

            try
            {
                var rawValue = await _moduleRunner.ExecuteSettingCommandAsync(module, setting, false, null, CancellationToken.None);
                return rawValue == null
                    ? new LoadedSetting(setting, fallbackValue)
                    : new LoadedSetting(setting, setting.ParseSerializedValue(rawValue));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read setting '{setting.Key}' for module '{module.Name}': {ex.Message}");
                return new LoadedSetting(setting, fallbackValue);
            }
        }

        /// <summary>
        /// Resolves the best available value when runtime loading is skipped or fails.
        /// </summary>
        private object? GetFallbackValue(ModuleConfig module, ModuleSetting setting) =>
            IsAutoDetectedAslmEngine(setting)
                ? IsAslmEngineInstalled(setting.Key)
                : setting.IsAutomaticallyManaged && !setting.UseCustomValue
                ? _moduleRunner.GetResolvedSettingValue(module, setting) ?? setting.Value ?? setting.Default
                : setting.Value ?? setting.Default;

        /// <summary>
        /// Refreshes the per-setting baselines used to detect changed values after UI rebuilds.
        /// </summary>
        private void UpdateSettingBaselines(ModuleConfig module, IEnumerable<LoadedSetting> loadedSettings)
        {
            foreach (var loadedSetting in loadedSettings)
            {
                var setting = loadedSetting.Setting;
                var effectiveValue = ResolveEffectiveSettingValue(module, setting, loadedSetting.Value);

                _settingBaselines[GetSettingIdentity(module, setting)] = new SettingBaseline(
                    setting.FormatValueForDisplay(effectiveValue),
                    setting.UseCustomValue);
            }
        }

        /// <summary>
        /// Returns the original effective value snapshot for one setting during the current page session.
        /// </summary>
        private SettingBaseline GetSettingBaseline(ModuleConfig module, ModuleSetting setting, object? currentValue)
        {
            if (_settingBaselines.TryGetValue(GetSettingIdentity(module, setting), out var baseline))
            {
                return baseline;
            }

            var effectiveValue = ResolveEffectiveSettingValue(module, setting, currentValue);
            return new SettingBaseline(setting.FormatValueForDisplay(effectiveValue), setting.UseCustomValue);
        }

        /// <summary>
        /// Returns the value that ASLM will effectively apply for the current setting state.
        /// </summary>
        private object? ResolveEffectiveSettingValue(ModuleConfig module, ModuleSetting setting, object? currentValue) =>
            setting.IsAutomaticallyManaged && !setting.UseCustomValue
                ? _moduleRunner.GetResolvedSettingValue(module, setting) ?? currentValue
                : currentValue;

        /// <summary>
        /// Builds a stable dictionary key for one setting within the currently loaded modules.
        /// </summary>
        private static string GetSettingIdentity(ModuleConfig module, ModuleSetting setting) =>
            $"{module.Id}::{setting.Key}";

        // Editor creation

        /// <summary>
        /// Builds one setting card and its control mapping when the setting is editable.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateSettingCard(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var card = CreateSettingCardBorder();
            var description = BuildSettingDescription(setting);

            if (IsAutoDetectedAslmEngine(setting))
            {
                var row = new Grid { ColumnSpacing = 16 };
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                var text = new VerticalStackLayout { Spacing = 4 };
                text.Children.Add(CreateCardTitle(setting.Name));
                if (!string.IsNullOrWhiteSpace(description))
                {
                    text.Children.Add(CreateCardDescription(description));
                }

                row.Children.Add(text);
                Grid.SetColumn(text, 0);

                var badge = CreateStatusBadge(value is bool installed && installed ? "Installed" : "Not installed", value is bool ready && ready);
                row.Children.Add(badge);
                Grid.SetColumn(badge, 1);

                card.Content = row;
                return (card, null);
            }

            if (setting.NormalizedType is "bool" or "engine")
            {
                var (toggleView, mapping) = CreateBooleanEditor(module, setting, value);
                var row = new Grid { ColumnSpacing = 16 };
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                var text = new VerticalStackLayout { Spacing = 4 };
                text.Children.Add(CreateCardTitle(setting.Name));
                if (!string.IsNullOrWhiteSpace(description))
                {
                    text.Children.Add(CreateCardDescription(description));
                }

                row.Children.Add(text);
                Grid.SetColumn(text, 0);
                row.Children.Add(toggleView);
                Grid.SetColumn(toggleView, 1);

                card.Content = row;
                return (card, mapping);
            }

            var content = new VerticalStackLayout { Spacing = 10 };
            content.Children.Add(CreateCardTitle(setting.Name));
            if (!string.IsNullOrWhiteSpace(description))
            {
                content.Children.Add(CreateCardDescription(description));
            }

            var (editor, mappingResult) = CreateEditor(module, setting, value);
            content.Children.Add(editor);
            card.Content = content;
            return (card, mappingResult);
        }

        /// <summary>
        /// Chooses the appropriate editor for the setting type and metadata.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            if (setting.IsAutomaticallyManaged)
            {
                return CreateManagedEditor(module, setting, value);
            }

            if (IsActiveEngineSelector(setting))
            {
                return CreateActiveEngineEditor(module, setting, value);
            }

            if (setting.AllowedValues is { Count: > 0 })
            {
                return CreatePickerEditor(module, setting, value);
            }

            return setting.NormalizedType switch
            {
                "int" or "integer" or "long" or "float" or "double" or "number" => CreateNumericEditor(module, setting, value),
                "password" => CreatePasswordEditor(module, setting, value),
                _ => CreateTextEditor(module, setting, value)
            };
        }

        /// <summary>
        /// Creates a toggle editor for boolean-style settings and hooks visibility refresh when needed.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateBooleanEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var toggle = new Switch
            {
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                IsToggled = value is bool boolValue
                    ? boolValue
                    : bool.TryParse(setting.FormatValueForDisplay(value), out var parsedBool) && parsedBool
            };

            if (HasDependentSettings(module, setting))
            {
                toggle.Toggled += (_, _) =>
                {
                    setting.Value = toggle.IsToggled;
                    _ = RefreshDynamicVisibilityAsync();
                };
            }

            return (toggle, new SettingControlMapping(
                module,
                setting,
                () => toggle.IsToggled,
                null,
                baseline.DisplayValue,
                baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates the segmented selector used by the active LLM engine setting.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateActiveEngineEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var selectedValue = setting.FormatValueForDisplay(value);
            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                selectedValue = setting.AllowedValues?.FirstOrDefault() ?? string.Empty;
            }

            var grid = new Grid { ColumnSpacing = 8 };
            var buttons = new List<(string Value, Button Button)>();

            foreach (var allowedValue in setting.AllowedValues ?? [])
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

                var button = new Button
                {
                    Text = ResolveChoiceLabel(module, allowedValue),
                    FontSize = 13,
                    Padding = new Thickness(12, 8),
                    CornerRadius = 10,
                    MinimumHeightRequest = 38
                };

                buttons.Add((allowedValue, button));
                button.Clicked += (_, _) =>
                {
                    selectedValue = allowedValue;
                    UpdateSegmentedButtons(buttons, selectedValue);
                };

                grid.Children.Add(button);
                Grid.SetColumn(button, buttons.Count - 1);
            }

            UpdateSegmentedButtons(buttons, selectedValue);

            return (grid, new SettingControlMapping(
                module,
                setting,
                () => selectedValue,
                null,
                baseline.DisplayValue,
                baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates a picker editor for settings that declare a list of allowed values.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreatePickerEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var picker = new Picker { Title = "Select value", FontSize = 14 };
            picker.SetDynamicResource(Picker.TextColorProperty, "LabelPrimary");
            picker.SetDynamicResource(Picker.TitleColorProperty, "LabelSecondary");
            picker.SetDynamicResource(VisualElement.BackgroundColorProperty, "BackgroundPrimary");

            foreach (var allowedValue in setting.AllowedValues!)
            {
                picker.Items.Add(allowedValue);
            }

            var currentValue = setting.FormatValueForDisplay(value);
            var selectedIndex = setting.AllowedValues.FindIndex(v => string.Equals(v, currentValue, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(currentValue))
            {
                picker.Items.Insert(0, currentValue);
                selectedIndex = 0;
            }

            picker.SelectedIndex = selectedIndex;

            return (picker, new SettingControlMapping(
                module,
                setting,
                () => picker.SelectedIndex >= 0 ? picker.Items[picker.SelectedIndex] : null,
                null,
                baseline.DisplayValue,
                baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates a numeric text entry for number-like settings.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateNumericEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var entry = CreateTextEntry(setting.FormatValueForDisplay(value));
            entry.Keyboard = Keyboard.Numeric;

            return (entry, new SettingControlMapping(module, setting, () => entry.Text, null, baseline.DisplayValue, baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates a plain text entry for free-form string settings.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateTextEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var entry = CreateTextEntry(setting.FormatValueForDisplay(value));
            return (entry, new SettingControlMapping(module, setting, () => entry.Text, null, baseline.DisplayValue, baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates a password entry and its mapping while preserving the shared baseline snapshot.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreatePasswordEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var (field, entry) = CreatePasswordField(setting.FormatValueForDisplay(value));
            return (field, new SettingControlMapping(module, setting, () => entry.Text, null, baseline.DisplayValue, baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates the editor used for ASLM-managed settings that can optionally switch to custom values.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateManagedEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var autoValue = _moduleRunner.GetResolvedSettingValue(module, setting);
            var initialDisplayValue = setting.UseCustomValue
                ? setting.FormatValueForDisplay(value)
                : setting.FormatValueForDisplay(autoValue);

            var isPasswordSetting = string.Equals(setting.NormalizedType, "password", StringComparison.OrdinalIgnoreCase);
            var entryView = isPasswordSetting
                ? CreatePasswordField(initialDisplayValue)
                : (Control: (View)CreateTextEntry(initialDisplayValue), Entry: (Entry?)null);
            var baseline = GetSettingBaseline(module, setting, initialDisplayValue);
            var entry = entryView.Entry ?? (Entry)entryView.Control;
            var lastCustomValue = setting.FormatValueForDisplay(setting.Value ?? value);

            var customToggle = CreateInlineToggle();
            customToggle.IsToggled = setting.UseCustomValue;

            entry.TextChanged += (_, args) =>
            {
                if (customToggle.IsToggled)
                {
                    lastCustomValue = args.NewTextValue ?? string.Empty;
                }
            };

            customToggle.Toggled += (_, args) =>
            {
                entry.Text = args.Value
                    ? (string.IsNullOrWhiteSpace(lastCustomValue) ? setting.FormatValueForDisplay(setting.Value ?? value) : lastCustomValue)
                    : setting.FormatValueForDisplay(_moduleRunner.GetResolvedSettingValue(module, setting));

                ApplyTextEntryState(entry, !args.Value);
            };

            ApplyTextEntryState(entry, !setting.UseCustomValue);

            var container = new VerticalStackLayout { Spacing = 10 };
            container.Children.Add(CreateInlineToggleRow("Use custom value", customToggle));
            container.Children.Add(entryView.Control);

            return (container, new SettingControlMapping(
                module,
                setting,
                () => entry.Text,
                () => customToggle.IsToggled,
                baseline.DisplayValue,
                baseline.UseCustomValue));
        }

        // Styling helpers

        /// <summary>
        /// Creates the outer container used for one module settings group.
        /// </summary>
        private static Border CreateModuleSectionBorder()
        {
            var border = new Border
            {
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = 20
            };

            border.SetDynamicResource(VisualElement.BackgroundColorProperty, "BackgroundSecondary");
            border.SetDynamicResource(Border.StrokeProperty, "Separator");
            return border;
        }

        /// <summary>
        /// Creates the card container used for one individual setting.
        /// </summary>
        private static Border CreateSettingCardBorder()
        {
            var border = new Border
            {
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 10 },
                Padding = new Thickness(14, 12)
            };

            border.SetDynamicResource(VisualElement.BackgroundColorProperty, "BackgroundPrimary");
            border.SetDynamicResource(Border.StrokeProperty, "Separator");
            return border;
        }

        /// <summary>
        /// Creates the title label used for each module settings section.
        /// </summary>
        private static Label CreateSectionHeader(string text)
        {
            var label = new Label { Text = text, FontSize = 18, FontAttributes = FontAttributes.Bold };
            label.SetDynamicResource(Label.TextColorProperty, "LabelPrimary");
            return label;
        }

        /// <summary>
        /// Creates the primary label for a setting name.
        /// </summary>
        private static Label CreateCardTitle(string text)
        {
            var label = new Label { Text = text, FontSize = 14 };
            label.SetDynamicResource(Label.TextColorProperty, "LabelPrimary");
            return label;
        }

        /// <summary>
        /// Creates the secondary label used for setting descriptions.
        /// </summary>
        private static Label CreateCardDescription(string text)
        {
            var label = new Label { Text = text, FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
            label.SetDynamicResource(Label.TextColorProperty, "LabelSecondary");
            return label;
        }

        /// <summary>
        /// Creates the compact secondary label used for inline helper rows.
        /// </summary>
        private static Label CreateSecondaryLabel(string text)
        {
            var label = new Label { Text = text, FontSize = 12 };
            label.SetDynamicResource(Label.TextColorProperty, "LabelSecondary");
            return label;
        }

        /// <summary>
        /// Creates a compact toggle used by inline helper rows.
        /// </summary>
        private static Switch CreateInlineToggle() =>
            new()
            {
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            };

        /// <summary>
        /// Creates a two-column row that pairs explanatory text with a trailing toggle.
        /// </summary>
        private static Grid CreateInlineToggleRow(string text, Switch toggle)
        {
            var label = CreateSecondaryLabel(text);
            label.VerticalOptions = LayoutOptions.Center;

            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            row.Children.Add(label);
            Grid.SetColumn(label, 0);
            row.Children.Add(toggle);
            Grid.SetColumn(toggle, 1);
            return row;
        }

        /// <summary>
        /// Creates a password editor that keeps the entry styling consistent with regular text fields.
        /// </summary>
        private static (View Control, Entry Entry) CreatePasswordField(string? text)
        {
            var entry = CreateTextEntry(text, isPassword: true, clearButtonVisibility: ClearButtonVisibility.Never);
            var toggleButton = new ImageButton
            {
                Source = PasswordIconHidden,
                WidthRequest = 36,
                HeightRequest = 36,
                MinimumWidthRequest = 36,
                MinimumHeightRequest = 36,
                Padding = 9,
                Margin = new Thickness(0, 0, 8, 0),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                BackgroundColor = Colors.Transparent,
                BorderWidth = 0,
                CornerRadius = 0,
                Aspect = Aspect.AspectFit
            };

            toggleButton.Background = new SolidColorBrush(Colors.Transparent);
            toggleButton.Clicked += (_, _) =>
            {
                entry.IsPassword = !entry.IsPassword;
                UpdatePasswordToggleIcon(toggleButton, entry.IsPassword);
            };

            var grid = new Grid { MinimumHeightRequest = 44 };
            grid.Children.Add(entry);
            grid.Children.Add(toggleButton);
            toggleButton.ZIndex = 1;
            return (grid, entry);
        }

        /// <summary>
        /// Updates the password visibility icon to reflect the current hidden or visible state.
        /// </summary>
        private static void UpdatePasswordToggleIcon(ImageButton toggleButton, bool isPasswordHidden)
        {
            toggleButton.Source = isPasswordHidden
                ? PasswordIconHidden
                : PasswordIconVisible;
        }

        /// <summary>
        /// Creates a standard text entry configured for the requested text behavior.
        /// </summary>
        private static Entry CreateTextEntry(string? text, bool isPassword = false, ClearButtonVisibility clearButtonVisibility = ClearButtonVisibility.WhileEditing)
        {
            var entry = new Entry
            {
                Text = text,
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Fill,
                ClearButtonVisibility = clearButtonVisibility,
                IsPassword = isPassword
            };

            entry.SetDynamicResource(Entry.TextColorProperty, "LabelPrimary");
            entry.SetDynamicResource(VisualElement.BackgroundColorProperty, "BackgroundPrimary");
            return entry;
        }

        /// <summary>
        /// Applies the read-only visual treatment used by ASLM-managed entries.
        /// </summary>
        private static void ApplyTextEntryState(Entry entry, bool isReadOnly)
        {
            entry.IsReadOnly = isReadOnly;
            entry.Opacity = isReadOnly ? 0.72 : 1.0;
        }

        // Misc helpers

        /// <summary>
        /// Refreshes the visual state of the segmented buttons for the active engine selector.
        /// </summary>
        private static void UpdateSegmentedButtons(IEnumerable<(string Value, Button Button)> buttons, string selectedValue)
        {
            foreach (var (value, button) in buttons)
            {
                var isSelected = string.Equals(value, selectedValue, StringComparison.OrdinalIgnoreCase);
                button.BorderWidth = 1;

                if (isSelected)
                {
                    button.SetDynamicResource(Button.BackgroundColorProperty, "SystemBlueOverlay");
                    button.SetDynamicResource(Button.TextColorProperty, "LabelPrimary");
                    button.SetDynamicResource(Button.BorderColorProperty, "ActionBlue");
                }
                else
                {
                    button.SetDynamicResource(Button.BackgroundColorProperty, "BackgroundPrimary");
                    button.SetDynamicResource(Button.TextColorProperty, "LabelSecondary");
                    button.SetDynamicResource(Button.BorderColorProperty, "Separator");
                }
            }
        }

        /// <summary>
        /// Creates the read-only installed-state badge shown for auto-detected ASLM engines.
        /// </summary>
        private Border CreateStatusBadge(string text, bool isPositive)
        {
            var border = new Border
            {
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                Padding = new Thickness(10, 6),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center
            };

            border.SetDynamicResource(VisualElement.BackgroundColorProperty, isPositive ? "SystemBlueOverlay" : "BackgroundPrimary");
            border.SetDynamicResource(Border.StrokeProperty, isPositive ? "ActionBlue" : "Separator");

            var label = new Label
            {
                Text = text,
                FontSize = 12,
                VerticalTextAlignment = TextAlignment.Center
            };

            label.SetDynamicResource(Label.TextColorProperty, isPositive ? "LabelPrimary" : "LabelSecondary");
            border.Content = label;
            return border;
        }

        /// <summary>
        /// Detects whether an engine-style setting maps directly to an ASLM engine installation.
        /// </summary>
        private bool IsAutoDetectedAslmEngine(ModuleSetting setting)
        {
            if (!string.Equals(setting.NormalizedType, "engine", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return _engineInstaller
                .DiscoverEngines()
                .Any(engine => engine.Id.Equals(setting.Key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks whether the specified ASLM engine is currently installed on the system.
        /// </summary>
        private bool IsAslmEngineInstalled(string engineId) =>
            _engineInstaller.GetEngineConfig(engineId) != null;

        /// <summary>
        /// Returns the trimmed description text shown under a setting title.
        /// </summary>
        private static string BuildSettingDescription(ModuleSetting setting) => setting.Description?.Trim() ?? string.Empty;

        /// <summary>
        /// Determines whether a setting should use the segmented active-engine selector.
        /// </summary>
        private static bool IsActiveEngineSelector(ModuleSetting setting) =>
            string.Equals(setting.Key, "llm-engine", StringComparison.OrdinalIgnoreCase) &&
            setting.AllowedValues is { Count: > 0 };

        /// <summary>
        /// Resolves a human-readable label for one allowed engine value using sibling name settings when available.
        /// </summary>
        private static string ResolveChoiceLabel(ModuleConfig module, string value)
        {
            var nameSetting = module.Settings.FirstOrDefault(setting =>
                setting.Key.Equals($"{value}_name", StringComparison.OrdinalIgnoreCase));

            if (nameSetting == null)
            {
                return value;
            }

            var label = nameSetting.FormatValueForDisplay(nameSetting.Value ?? nameSetting.Default);
            return string.IsNullOrWhiteSpace(label) ? value : label;
        }

        /// <summary>
        /// Builds the save confirmation message, including deferred runtime updates when present.
        /// </summary>
        private static string BuildSaveMessage(List<string> deferredSettings)
        {
            if (deferredSettings.Count == 0)
            {
                return "Settings saved and applied.";
            }

            var preview = string.Join("\n", deferredSettings.Take(5));
            return $"Settings saved. Some module settings could not be applied immediately and will be retried on next module start.\n\n{preview}";
        }
    }
}
