// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Globalization;
using Debug = System.Diagnostics.Debug;
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
        private const double DialogWidthFactor = 0.8;
        private const double DialogHeightFactor = 0.8;
        private const double MinDialogWidth = 960;
        private const double MinDialogHeight = 540;
        private const double MaxDialogWidth = 1280;
        private const double MaxDialogHeight = 720;

        private static readonly Color ActiveCategoryTextColor = Colors.White;
        private static readonly Color InactiveCategoryTextColor = Color.FromArgb("#99EBEBF5");
        private static readonly Color ActiveCategoryBackgroundColor = Color.FromArgb("#2F7BF6");
        private static readonly Color InactiveCategoryBackgroundColor = Colors.Transparent;
        private static readonly Color ActiveCategoryAccentColor = Color.FromArgb("#0A84FF");
        private static readonly Color PassiveActionBackgroundColor = Color.FromArgb("#2C2C2E");
        private static readonly Color PassiveActionTextColor = Color.FromArgb("#99EBEBF5");

        private readonly AppDataService _appData;
        private readonly EngineInstaller _engineInstaller;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly List<SettingControlMapping> _settingMappings = [];
        private readonly Dictionary<string, SettingBaseline> _settingBaselines = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Border> _categoryButtons = new(StringComparer.OrdinalIgnoreCase);
        private List<ModuleConfig> _loadedModules = [];
        private List<SettingsCategory> _categories = [];
        private SettingsCategory? _activeCategory;
        private SettingsCategoryGroup _activeGroup = SettingsCategoryGroup.Aslm;
        private AslmBaseline _aslmBaseline = new(string.Empty, string.Empty, string.Empty);
        private string _userNameDraft = string.Empty;
        private string _officialPortDraft = string.Empty;
        private string _thirdPartyPortDraft = string.Empty;
        private bool _hasLoaded;
        private bool _isRefreshingVisibility;
        private bool _isSwitchingCategory;
        private bool _isSaving;

        /// <summary>
        /// Raised when the user asks to close the settings overlay.
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <summary>
        /// Distinguishes the supported top-level settings groups.
        /// </summary>
        private enum SettingsCategoryGroup
        {
            Aslm,
            Modules
        }

        /// <summary>
        /// Distinguishes the supported settings category types in the selector.
        /// </summary>
        private enum SettingsCategoryKind
        {
            AslmProfile,
            AslmPorts,
            Module
        }

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
        /// Represents a lightweight settings toggle with a fixed visual and hitbox.
        /// </summary>
        private sealed class CompactToggle
        {
            private static readonly Color ToggleOnColor = Color.FromArgb("#0A84FF");
            private static readonly Color ToggleOffColor = Color.FromArgb("#3A3A3C");
            private static readonly Color ToggleThumbColor = Colors.White;
            private const double TrackWidth = 36;
            private const double TrackHeight = 20;
            private const double ThumbSize = 16;
            private const double ThumbInset = 2;

            private class ThumbDrawable : IDrawable
            {
                public void Draw(ICanvas canvas, RectF dirtyRect)
                {
                    canvas.Antialias = true;
                    canvas.FillColor = ToggleThumbColor;
                    canvas.FillCircle((float)ThumbSize / 2, (float)ThumbSize / 2, (float)ThumbSize / 2);
                }
            }

            private readonly AbsoluteLayout _layout;
            private readonly Border _track;
            private readonly GraphicsView _thumb;
            private bool _suppressTap;
            private double _dragStartOffset;

            /// <summary>
            /// Initializes a new compact toggle instance with the requested initial state.
            /// </summary>
            public CompactToggle(bool isToggled = false)
            {
                _track = new Border
                {
                    StrokeThickness = 0,
                    StrokeShape = new RoundRectangle { CornerRadius = 10 },
                    Padding = 0,
                    WidthRequest = TrackWidth,
                    HeightRequest = TrackHeight,
                    MinimumWidthRequest = TrackWidth,
                    MinimumHeightRequest = TrackHeight,
                    VerticalOptions = LayoutOptions.Center
                };

                _thumb = new GraphicsView
                {
                    Drawable = new ThumbDrawable(),
                    WidthRequest = ThumbSize,
                    HeightRequest = ThumbSize,
                    MinimumWidthRequest = ThumbSize,
                    MinimumHeightRequest = ThumbSize,
                    VerticalOptions = LayoutOptions.Center
                };

                _layout = new AbsoluteLayout
                {
                    WidthRequest = TrackWidth,
                    HeightRequest = TrackHeight,
                    MinimumWidthRequest = TrackWidth,
                    MinimumHeightRequest = TrackHeight,
                    HorizontalOptions = LayoutOptions.Start,
                    VerticalOptions = LayoutOptions.Center
                };

                AbsoluteLayout.SetLayoutBounds(_track, new Rect(0, 0, TrackWidth, TrackHeight));
                AbsoluteLayout.SetLayoutFlags(_track, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);
                AbsoluteLayout.SetLayoutBounds(_thumb, new Rect(ThumbInset, ThumbTopOffset, ThumbSize, ThumbSize));
                AbsoluteLayout.SetLayoutFlags(_thumb, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.None);

                _layout.Children.Add(_track);
                _layout.Children.Add(_thumb);
                _thumb.ZIndex = 1;
                View = _layout;

                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += (_, _) =>
                {
                    if (_suppressTap)
                    {
                        _suppressTap = false;
                        return;
                    }

                    IsToggled = !IsToggled;
                };
                _layout.GestureRecognizers.Add(tapGesture);

                var panGesture = new PanGestureRecognizer();
                panGesture.PanUpdated += OnPanUpdated;
                _layout.GestureRecognizers.Add(panGesture);

                _isToggled = isToggled;
                UpdateVisualState();
            }

            /// <summary>
            /// Gets the visual root used in the settings page layout.
            /// </summary>
            public View View { get; }

            /// <summary>
            /// Occurs when the toggle value changes.
            /// </summary>
            public event EventHandler<ToggledEventArgs>? Toggled;

            /// <summary>
            /// Gets or sets the current toggle state and updates the visual presentation.
            /// </summary>
            public bool IsToggled
            {
                get => _isToggled;
                set
                {
                    if (_isToggled == value)
                    {
                        return;
                    }

                    _isToggled = value;
                    UpdateVisualState();
                    Toggled?.Invoke(this, new ToggledEventArgs(value));
                }
            }

            private bool _isToggled;

            /// <summary>
            /// Applies the correct track and thumb layout for the current toggle state.
            /// </summary>
            private void UpdateVisualState()
            {
                _track.Background = new SolidColorBrush(_isToggled ? ToggleOnColor : ToggleOffColor);
                SetThumbOffset(_isToggled ? MaxThumbOffset : 0);
            }

            /// <summary>
            /// Handles horizontal dragging for the compact toggle thumb.
            /// </summary>
            private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
            {
                switch (e.StatusType)
                {
                    case GestureStatus.Started:
                        _dragStartOffset = CurrentThumbOffset;
                        break;
                    case GestureStatus.Running:
                        _suppressTap = true;
                        MainThread.BeginInvokeOnMainThread(() => SetThumbOffset(ClampOffset(_dragStartOffset + e.TotalX)));
                        break;
                    case GestureStatus.Canceled:
                    case GestureStatus.Completed:
                        var nextState = CurrentThumbOffset >= MaxThumbOffset / 2;
                        MainThread.BeginInvokeOnMainThread(() => {
                            SetThumbOffset(nextState ? MaxThumbOffset : 0);
                            IsToggled = nextState;
                        });
                        break;
                }
            }

            /// <summary>
            /// Moves the thumb to the requested horizontal offset inside the track.
            /// </summary>
            private void SetThumbOffset(double offset)
            {
                AbsoluteLayout.SetLayoutBounds(_thumb, new Rect(ThumbInset + offset, ThumbTopOffset, ThumbSize, ThumbSize));
            }

            /// <summary>
            /// Restricts thumb movement to the bounds of the compact track.
            /// </summary>
            private static double ClampOffset(double offset)
            {
                if (offset < 0)
                {
                    return 0;
                }

                return offset > MaxThumbOffset ? MaxThumbOffset : offset;
            }

            /// <summary>
            /// Gets the maximum translation distance available to the thumb inside the track.
            /// </summary>
            private static double MaxThumbOffset => TrackWidth - ThumbSize - (ThumbInset * 2);

            /// <summary>
            /// Gets the top offset that vertically centers the thumb inside the track.
            /// </summary>
            private static double ThumbTopOffset => (TrackHeight - ThumbSize) / 2;

            /// <summary>
            /// Gets the current horizontal thumb offset relative to the left inset.
            /// </summary>
            private double CurrentThumbOffset => AbsoluteLayout.GetLayoutBounds(_thumb).X - ThumbInset;
        }

        /// <summary>
        /// Couples one setting with the runtime value loaded for the current refresh pass.
        /// </summary>
        private record LoadedSetting(ModuleSetting Setting, object? Value);

        /// <summary>
        /// Captures the initial effective value used to detect real user changes across UI rebuilds.
        /// </summary>
        private record SettingBaseline(string DisplayValue, bool UseCustomValue);

        /// <summary>
        /// Stores the initial ASLM values loaded for the current page session.
        /// </summary>
        private record AslmBaseline(string UserName, string OfficialPort, string ThirdPartyPort);

        /// <summary>
        /// Describes one selectable settings category shown in the sidebar.
        /// </summary>
        private record SettingsCategory(
            string Id,
            string Title,
            string Description,
            SettingsCategoryKind Kind,
            ModuleConfig? Module,
            bool SupportsAppRestart);

        /// <summary>
        /// Summarizes the modules touched during one save operation.
        /// </summary>
        private record ModuleSaveResult(HashSet<ModuleConfig> TouchedModules, List<string> DeferredSettings);

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
            ApplyFlatEntryStyle(UsernameEntry);
            ApplyFlatEntryStyle(OfficialPortEntry);
            ApplyFlatEntryStyle(ThirdPartyPortEntry);
            ApplyScrollViewChrome(CategoryScroll, isSidebar: true);
            ApplyScrollViewChrome(SettingsScroll, isSidebar: false);
            UsernameEntry.TextChanged += (_, _) => UpdateActionButtons();
            OfficialPortEntry.TextChanged += (_, _) => UpdateActionButtons();
            ThirdPartyPortEntry.TextChanged += (_, _) => UpdateActionButtons();
            SizeChanged += OnViewSizeChanged;
            Loaded += OnLoaded;
        }

        // Refresh

        /// <summary>
        /// Reloads the settings page when the shell revisits it.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (!_hasLoaded)
            {
                return;
            }

            try
            {
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to refresh settings view: {ex.Message}");
            }
        }

        // Overlay Events

        private void OnBackgroundTapped(object? sender, EventArgs e)
        {
            RequestClose();
        }

        private void OnBorderTapped(object? sender, EventArgs e)
        {
            // Do nothing. Swallows the tap so it doesn't propagate to the background.
        }

        private void OnCloseClicked(object? sender, EventArgs e)
        {
            RequestClose();
        }

        private async void RequestClose()
        {
            if (!await ConfirmDiscardChangesIfNeededAsync())
            {
                return;
            }

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        // Loading

        /// <summary>
        /// Loads shared settings, discovers modules, and restores the active category.
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            var previousCategoryId = _activeCategory?.Id;

            LoadAslmDraftsFromAppData();
            await LoadModuleDraftsAsync(reloadModules: true, reloadRuntimeValues: true);

            _categories = CreateOrderedCategories();

            var targetCategory = ResolveCategory(previousCategoryId) ?? _categories.FirstOrDefault();
            if (targetCategory == null)
            {
                _activeCategory = null;
                ShowEmptyCategory("No settings are available.");
                UpdateActionButtons();
                return;
            }

            _activeGroup = GetGroupForCategory(targetCategory);
            BuildCategorySelectors();
            ActivateCategory(targetCategory);
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
            UpdateDialogSize();

            try
            {
                await LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load settings view: {ex.Message}");
            }
        }

        /// <summary>
        /// Keeps the dialog within the requested min/max bounds while scaling to the host size.
        /// </summary>
        private void OnViewSizeChanged(object? sender, EventArgs e)
        {
            UpdateDialogSize();
        }

        /// <summary>
        /// Applies the responsive dialog size using 80 percent of the available overlay area.
        /// </summary>
        private void UpdateDialogSize()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            SettingsDialog.WidthRequest = ClampDialogSize(Math.Floor(Width * DialogWidthFactor), MinDialogWidth, MaxDialogWidth);
            SettingsDialog.HeightRequest = ClampDialogSize(Math.Floor(Height * DialogHeightFactor), MinDialogHeight, MaxDialogHeight);
        }

        /// <summary>
        /// Restricts one calculated dialog dimension to its supported bounds.
        /// </summary>
        private static double ClampDialogSize(double value, double min, double max) =>
            Math.Max(min, Math.Min(max, value));

        // Loading helpers

        /// <summary>
        /// Copies the persisted shared settings into the editable page draft.
        /// </summary>
        private void LoadAslmDraftsFromAppData()
        {
            _userNameDraft = _appData.Data.User.Name ?? string.Empty;
            _officialPortDraft = _appData.Data.Ports.OfficialStart.ToString(CultureInfo.InvariantCulture);
            _thirdPartyPortDraft = _appData.Data.Ports.ThirdPartyStart.ToString(CultureInfo.InvariantCulture);
            _aslmBaseline = new AslmBaseline(_userNameDraft, _officialPortDraft, _thirdPartyPortDraft);

            ApplyAslmDraftsToControls();
            PortErrorLabel.IsVisible = false;
        }

        /// <summary>
        /// Reloads module settings and refreshes the baseline snapshots used by save detection.
        /// </summary>
        private async Task LoadModuleDraftsAsync(bool reloadModules, bool reloadRuntimeValues)
        {
            if (reloadModules || _loadedModules.Count == 0)
            {
                _loadedModules = await _moduleInstaller.DiscoverModulesAsync();
            }

            if (reloadRuntimeValues)
            {
                _settingBaselines.Clear();
            }

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

                foreach (var item in loaded)
                {
                    if (!item.Setting.IsAutomaticallyManaged || item.Setting.UseCustomValue)
                    {
                        item.Setting.Value = item.Value;
                    }
                }
            }
        }

        // Categories

        /// <summary>
        /// Builds the ordered category list with ASLM categories first and modules after them.
        /// </summary>
        private List<SettingsCategory> CreateOrderedCategories()
        {
            var categories = new List<SettingsCategory>
            {
                new(
                    "aslm-profile",
                    "User Profile",
                    "Display name used by ASLM and shared with modules.",
                    SettingsCategoryKind.AslmProfile,
                    null,
                    false),
                new(
                    "aslm-ports",
                    "Port Allocation",
                    "Reserved port ranges for official modules and third-party integrations.",
                    SettingsCategoryKind.AslmPorts,
                    null,
                    true)
            };

            categories.AddRange(
                _loadedModules
                    .Where(module => module.Settings.Any(ShouldDisplaySetting))
                    .OrderBy(module => module.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(module => new SettingsCategory(
                        $"module::{module.Id}",
                        module.Name,
                        string.IsNullOrWhiteSpace(module.Description) ? "Module-specific configuration." : module.Description.Trim(),
                        SettingsCategoryKind.Module,
                        module,
                        false)));

            return categories;
        }

        /// <summary>
        /// Rebuilds the unified category selector sidebar.
        /// </summary>
        private void BuildCategorySelectors()
        {
            _categoryButtons.Clear();
            CategoryPanel.Children.Clear();

            // ASLM Group
            CategoryPanel.Children.Add(CreateSelectorHeader("ASLM"));
            foreach (var category in _categories.Where(c => GetGroupForCategory(c) == SettingsCategoryGroup.Aslm))
            {
                var button = CreateSelectorButton(category.Title);
                button.BindingContext = category;
                var tapGesture = new TapGestureRecognizer();
                tapGesture.Tapped += OnCategorySelectorClicked;
                button.GestureRecognizers.Add(tapGesture);
                CategoryPanel.Children.Add(button);
                _categoryButtons[category.Id] = button;
            }

            // Modules Group
            var modCategories = _categories.Where(c => GetGroupForCategory(c) == SettingsCategoryGroup.Modules).ToList();
            if (modCategories.Count > 0)
            {
                CategoryPanel.Children.Add(new BoxView { HeightRequest = 12, Color = Colors.Transparent }); // spacing
                CategoryPanel.Children.Add(CreateSelectorHeader("MODULES"));
                foreach (var category in modCategories)
                {
                    var button = CreateSelectorButton(category.Title);
                    button.BindingContext = category;
                    var tapGesture = new TapGestureRecognizer();
                    tapGesture.Tapped += OnCategorySelectorClicked;
                    button.GestureRecognizers.Add(tapGesture);
                    CategoryPanel.Children.Add(button);
                    _categoryButtons[category.Id] = button;
                }
            }

            UpdateSelectorButtonStates();
        }

        /// <summary>
        /// Creates a category group header label for the sidebar.
        /// </summary>
        private static Label CreateSelectorHeader(string text) =>
            new()
            {
                Text = text,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#8E8E93"),
                Margin = new Thickness(6, 12, 6, 8)
            };

        /// <summary>
        /// Creates one sidebar selector button for a specific settings category.
        /// </summary>
        private static Border CreateSelectorButton(string text)
        {
            var border = new Border
            {
                Padding = new Thickness(12, 0),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(10) },
                MinimumHeightRequest = 32,
                HeightRequest = 32,
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                Margin = new Thickness(0, 0, 0, 4),
                HorizontalOptions = LayoutOptions.Fill
            };

            var label = new Label
            {
                Text = text,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            };

            border.Content = label;

            return border;
        }

        /// <summary>
        /// Handles clicks on the per-category selector buttons.
        /// </summary>
        private async void OnCategorySelectorClicked(object? sender, EventArgs e)
        {
            if (sender is Border { BindingContext: SettingsCategory category })
            {
                await TrySelectCategoryAsync(category);
            }
        }

                private async Task TrySelectCategoryAsync(SettingsCategory category)
        {
            if (_isSaving || _isSwitchingCategory)
            {
                return;
            }

            if (_activeCategory != null &&
                _activeCategory.Id.Equals(category.Id, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                _isSwitchingCategory = true;

                if (!await ConfirmDiscardChangesIfNeededAsync())
                {
                    return;
                }

                var resolvedCategory = ResolveCategory(category.Id);
                if (resolvedCategory == null)
                {
                    return;
                }

                _activeGroup = GetGroupForCategory(resolvedCategory);

                ActivateCategory(resolvedCategory);
            }
            finally
            {
                _isSwitchingCategory = false;
            }
        }

        /// <summary>
        /// Activates the selected category and rebuilds the visible settings content.
        /// </summary>
        private void ActivateCategory(SettingsCategory category)
        {
            _activeCategory = category;
            ActiveCategoryTitleLabel.Text = category.Title;

            switch (category.Kind)
            {
                case SettingsCategoryKind.AslmProfile:
                    RenderAslmCategory(showProfile: true, showPorts: false);
                    break;
                case SettingsCategoryKind.AslmPorts:
                    RenderAslmCategory(showProfile: false, showPorts: true);
                    break;
                case SettingsCategoryKind.Module:
                    RenderModuleCategory(category.Module!);
                    break;
            }

            UpdateSelectorButtonStates();
            UpdateActionButtons();
        }

        /// <summary>
        /// Returns the category that matches the stored category identifier, if it still exists.
        /// </summary>
        private SettingsCategory? ResolveCategory(string? categoryId) =>
            string.IsNullOrWhiteSpace(categoryId)
                ? null
                : _categories.FirstOrDefault(category => category.Id.Equals(categoryId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Returns the top-level group that owns the specified category.
        /// </summary>
        private static SettingsCategoryGroup GetGroupForCategory(SettingsCategory category) =>
            category.Kind == SettingsCategoryKind.Module
                ? SettingsCategoryGroup.Modules
                : SettingsCategoryGroup.Aslm;

        /// <summary>
        /// Applies active and inactive styling to the selector buttons.
        /// </summary>
        private void UpdateSelectorButtonStates()
        {
            foreach (var pair in _categoryButtons)
            {
                ApplySelectorButtonState(pair.Value, _activeCategory != null && pair.Key.Equals(_activeCategory.Id, StringComparison.OrdinalIgnoreCase));
                pair.Value.IsEnabled = !_isSaving;
            }
        }

        /// <summary>
        /// Applies the selected visual state to one selector button.
        /// </summary>
        private static void ApplySelectorButtonState(Border button, bool isActive)
        {
            if (button.Content is Label label)
            {
                label.TextColor = isActive ? ActiveCategoryTextColor : InactiveCategoryTextColor;
                label.FontAttributes = FontAttributes.None;
            }

            button.BackgroundColor = isActive ? ActiveCategoryBackgroundColor : InactiveCategoryBackgroundColor;
            button.Opacity = isActive ? 1.0 : 0.92;
        }

        /// <summary>
        /// Updates the footer action buttons to match the currently visible category.
        /// </summary>
        private void UpdateActionButtons()
        {
            var canInteract = !_isSaving && _activeCategory != null;
            var hasChanges = canInteract && HasUnsavedChanges();
            DefaultButton.IsEnabled = canInteract;
            SaveButton.IsEnabled = canInteract;
            SaveButton.Text = _isSaving ? "Saving..." : "Save";
            ApplyActionButtonState(DefaultButton, false);

            if (_activeCategory == null)
            {
                SaveAndRestartButton.IsEnabled = false;
                SaveAndRestartButton.IsVisible = false;
                SaveButton.IsVisible = false;
                ApplyActionButtonState(SaveButton, false);
                SaveAndRestartButton.Text = "Save and restart";
                return;
            }

            var canShowRestart = false;
            if (_activeCategory.Kind == SettingsCategoryKind.Module)
            {
                canShowRestart = _activeCategory.Module is { Status.Enabled: true } module &&
                    module.Commands.Run.Count > 0;
            }
            else
            {
                canShowRestart = _activeCategory.SupportsAppRestart;
            }

            SaveAndRestartButton.IsVisible = canShowRestart;
            SaveAndRestartButton.IsEnabled = canInteract && canShowRestart;
            SaveAndRestartButton.Text = "Save and restart";

            SaveButton.IsVisible = hasChanges;
            SaveAndRestartButton.IsVisible = hasChanges && canShowRestart;

            var highlightRestart = hasChanges && canShowRestart;
            var highlightSave = hasChanges && !canShowRestart;

            ApplyActionButtonState(SaveButton, highlightSave);
            ApplyActionButtonState(SaveAndRestartButton, highlightRestart);
        }

        /// <summary>
        /// Applies the passive or emphasized visual state to one footer action button.
        /// </summary>
        private static void ApplyActionButtonState(Button button, bool isPrimary)
        {
            button.BackgroundColor = isPrimary ? Color.FromArgb("#0A84FF") : PassiveActionBackgroundColor;
            button.TextColor = isPrimary ? Colors.White : PassiveActionTextColor;
            button.BorderWidth = 0;
        }

        // Rendering

        /// <summary>
        /// Shows one of the built-in ASLM categories while hiding module-specific content.
        /// </summary>
        private void RenderAslmCategory(bool showProfile, bool showPorts)
        {
            ApplyAslmDraftsToControls();
            _settingMappings.Clear();

            AslmSettingsContainer.IsVisible = true;
            UserProfileSection.IsVisible = showProfile;
            PortsSection.IsVisible = showPorts;
            ModuleSettingsContainer.IsVisible = false;
            ModuleSettingsContainer.Children.Clear();
            EmptyCategoryState.IsVisible = false;
        }

        /// <summary>
        /// Rebuilds the currently visible module settings page from the in-memory draft state.
        /// </summary>
        private void RenderModuleCategory(ModuleConfig module)
        {
            _settingMappings.Clear();
            ModuleSettingsContainer.Children.Clear();

            var settings = module.Settings?.Where(ShouldDisplaySetting).ToList() ?? [];
            var loadedSettings = settings
                .Select(setting => new LoadedSetting(setting, GetCurrentSettingValue(module, setting)))
                .ToList();

            var valuesByKey = loadedSettings.ToDictionary(item => item.Setting.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            var visibleSettings = loadedSettings
                .Where(item => ShouldRenderSetting(item.Setting, settings, valuesByKey))
                .ToList();

            AslmSettingsContainer.IsVisible = false;
            ModuleSettingsContainer.IsVisible = true;

            if (visibleSettings.Count == 0)
            {
                ShowEmptyCategory("This module has no settings in the current state.");
                return;
            }

            EmptyCategoryState.IsVisible = false;

            var section = CreateModuleSectionBorder();
            var content = new VerticalStackLayout { Spacing = 12 };

            foreach (var item in visibleSettings)
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

        /// <summary>
        /// Shows the empty-state card and hides other content containers.
        /// </summary>
        private void ShowEmptyCategory(string message)
        {
            AslmSettingsContainer.IsVisible = false;
            ModuleSettingsContainer.IsVisible = false;
            ModuleSettingsContainer.Children.Clear();
            EmptyCategoryLabel.Text = message;
            EmptyCategoryState.IsVisible = true;
        }

        /// <summary>
        /// Pushes the current ASLM draft values back into the always-created XAML controls.
        /// </summary>
        private void ApplyAslmDraftsToControls()
        {
            UsernameEntry.Text = _userNameDraft;
            OfficialPortEntry.Text = _officialPortDraft;
            ThirdPartyPortEntry.Text = _thirdPartyPortDraft;
        }

        // Draft synchronization

        /// <summary>
        /// Synchronizes the visible category controls back into the shared in-memory draft state.
        /// </summary>
        private void SyncDraftValuesFromControls()
        {
            if (_activeCategory == null)
            {
                return;
            }

            if (_activeCategory.Kind == SettingsCategoryKind.Module)
            {
                SyncModuleDraftValuesFromControls();
                return;
            }

            SyncAslmDraftValuesFromControls();
        }

        /// <summary>
        /// Copies the visible ASLM input controls into the current draft values.
        /// </summary>
        private void SyncAslmDraftValuesFromControls()
        {
            if (UserProfileSection.IsVisible)
            {
                _userNameDraft = UsernameEntry.Text?.Trim() ?? string.Empty;
            }

            if (PortsSection.IsVisible)
            {
                _officialPortDraft = OfficialPortEntry.Text?.Trim() ?? string.Empty;
                _thirdPartyPortDraft = ThirdPartyPortEntry.Text?.Trim() ?? string.Empty;
            }
        }

        /// <summary>
        /// Captures the current control values into the in-memory module settings draft.
        /// </summary>
        private void SyncModuleDraftValuesFromControls()
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

        /// <summary>
        /// Rebuilds the visible settings list after one of the controlling toggles changes.
        /// </summary>
        private async Task RefreshDynamicVisibilityAsync()
        {
            if (_isRefreshingVisibility || _activeCategory?.Kind != SettingsCategoryKind.Module || _activeCategory.Module == null)
            {
                return;
            }

            try
            {
                _isRefreshingVisibility = true;
                SyncModuleDraftValuesFromControls();
                RenderModuleCategory(_activeCategory.Module);
                await Task.CompletedTask;
            }
            finally
            {
                _isRefreshingVisibility = false;
            }
        }

        /// <summary>
        /// Prompts the user to discard unsaved changes before leaving the current category.
        /// </summary>
        private async Task<bool> ConfirmDiscardChangesIfNeededAsync()
        {
            if (_activeCategory == null || !HasUnsavedChanges())
            {
                return true;
            }

            var discardChanges = await ShowAlertAsync(
                "Unsaved changes",
                "You have unsaved changes in this section. Discard them and switch?",
                "Discard",
                "Stay");

            if (!discardChanges)
            {
                return false;
            }

            LoadAslmDraftsFromAppData();
            await LoadModuleDraftsAsync(reloadModules: false, reloadRuntimeValues: true);
            _categories = CreateOrderedCategories();

            return true;
        }

        /// <summary>
        /// Determines whether the currently visible category has unsaved changes.
        /// </summary>
        private bool HasUnsavedChanges()
        {
            if (_activeCategory == null)
            {
                return false;
            }

            return _activeCategory.Kind switch
            {
                SettingsCategoryKind.AslmProfile => !string.Equals(UsernameEntry.Text?.Trim() ?? string.Empty, _aslmBaseline.UserName, StringComparison.Ordinal),
                SettingsCategoryKind.AslmPorts => !string.Equals(OfficialPortEntry.Text?.Trim() ?? string.Empty, _aslmBaseline.OfficialPort, StringComparison.Ordinal) ||
                                                  !string.Equals(ThirdPartyPortEntry.Text?.Trim() ?? string.Empty, _aslmBaseline.ThirdPartyPort, StringComparison.Ordinal),
                SettingsCategoryKind.Module => HasUnsavedModuleChanges(),
                _ => false
            };
        }

        /// <summary>
        /// Determines whether the visible module editors differ from the last saved baseline.
        /// </summary>
        private bool HasUnsavedModuleChanges()
        {
            foreach (var mapping in _settingMappings)
            {
                var useCustom = mapping.ReadCustomValue?.Invoke() ?? mapping.Setting.UseCustomValue;
                var currentValue = mapping.Setting.IsAutomaticallyManaged && !useCustom
                    ? _moduleRunner.GetResolvedSettingValue(mapping.Module, mapping.Setting)
                    : mapping.Setting.NormalizeUserValue(mapping.ReadValue());
                var displayValue = mapping.Setting.FormatValueForDisplay(currentValue);

                if (mapping.InitialUseCustomValue != useCustom ||
                    !string.Equals(mapping.InitialDisplayValue, displayValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Shows a confirmation dialog on the current shell page.
        /// </summary>
        private static Task<bool> ShowAlertAsync(string title, string message, string accept, string cancel) =>
            Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page!.DisplayAlertAsync(title, message, accept, cancel)
                : Task.FromResult(false);

        /// <summary>
        /// Shows a simple informational dialog on the current shell page.
        /// </summary>
        private static Task ShowSuccessAsync(string message) =>
            Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page!.DisplayAlertAsync("Success", message, "OK")
                : Task.CompletedTask;

        /// <summary>
        /// Shows an error dialog on the current shell page.
        /// </summary>
        private static Task ShowErrorAsync(string message) =>
            Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page!.DisplayAlertAsync("Validation Error", message, "OK")
                : Task.CompletedTask;

        // Saving

        /// <summary>
        /// Restores the currently visible category to its default values.
        /// </summary>
        private async void OnDefaultClicked(object? sender, EventArgs e)
        {
            if (_activeCategory == null || _isSaving)
            {
                return;
            }

            SyncDraftValuesFromControls();

            switch (_activeCategory.Kind)
            {
                case SettingsCategoryKind.AslmProfile:
                    _userNameDraft = Environment.UserName;
                    RenderAslmCategory(showProfile: true, showPorts: false);
                    break;
                case SettingsCategoryKind.AslmPorts:
                    var defaultPorts = new AppPortConfig();
                    _officialPortDraft = defaultPorts.OfficialStart.ToString(CultureInfo.InvariantCulture);
                    _thirdPartyPortDraft = defaultPorts.ThirdPartyStart.ToString(CultureInfo.InvariantCulture);
                    PortErrorLabel.IsVisible = false;
                    RenderAslmCategory(showProfile: false, showPorts: true);
                    break;
                case SettingsCategoryKind.Module:
                    ResetModuleToDefaults(_activeCategory.Module!);
                    RenderModuleCategory(_activeCategory.Module!);
                    break;
            }

            await SettingsScroll.ScrollToAsync(0, 0, false);
        }

        /// <summary>
        /// Restores every editable setting in the selected module back to its manifest default.
        /// </summary>
        private static void ResetModuleToDefaults(ModuleConfig module)
        {
            foreach (var setting in module.Settings.Where(ShouldDisplaySetting))
            {
                if (setting.IsAutomaticallyManaged)
                {
                    setting.UseCustomValue = false;
                }

                setting.Value = setting.NormalizeUserValue(setting.Default);
            }
        }

        /// <summary>
        /// Saves the current settings without restarting anything.
        /// </summary>
        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            await SaveAsync(restartAfterSave: false);
        }

        /// <summary>
        /// Saves the current settings and restarts the active target when supported.
        /// </summary>
        private async void OnSaveAndRestartClicked(object? sender, EventArgs e)
        {
            await SaveAsync(restartAfterSave: true);
        }

        /// <summary>
        /// Validates, persists, and optionally restarts the active category target.
        /// </summary>
        private async Task SaveAsync(bool restartAfterSave)
        {
            if (_activeCategory == null || _isSaving)
            {
                return;
            }

            try
            {
                _isSaving = true;
                UpdateSelectorButtonStates();
                UpdateActionButtons();

                switch (_activeCategory.Kind)
                {
                    case SettingsCategoryKind.AslmProfile:
                    {
                        SyncAslmDraftValuesFromControls();
                        if (string.IsNullOrWhiteSpace(_userNameDraft))
                        {
                            RenderAslmCategory(showProfile: true, showPorts: false);
                            await ShowErrorAsync("Display name cannot be empty.");
                            return;
                        }

                        var hasChanges = !string.Equals(_aslmBaseline.UserName, _userNameDraft, StringComparison.Ordinal);
                        _appData.Data.User.Name = _userNameDraft;
                        await _appData.SaveAsync();
                        _aslmBaseline = _aslmBaseline with { UserName = _userNameDraft };
                        RenderAslmCategory(showProfile: true, showPorts: false);

                        if (restartAfterSave && _activeCategory.SupportsAppRestart && await RestartActiveTargetAsync())
                        {
                            return;
                        }

                        await ShowSuccessAsync(BuildSaveMessage(hasChanges, false, []));
                        break;
                    }

                    case SettingsCategoryKind.AslmPorts:
                    {
                        SyncAslmDraftValuesFromControls();
                        if (!TryParsePorts(out var officialPort, out var thirdPartyPort))
                        {
                            RenderAslmCategory(showProfile: false, showPorts: true);
                            return;
                        }

                        PortErrorLabel.IsVisible = false;
                        var hasChanges =
                            !string.Equals(_aslmBaseline.OfficialPort, _officialPortDraft, StringComparison.Ordinal) ||
                            !string.Equals(_aslmBaseline.ThirdPartyPort, _thirdPartyPortDraft, StringComparison.Ordinal);

                        _appData.Data.Ports.OfficialStart = officialPort;
                        _appData.Data.Ports.ThirdPartyStart = thirdPartyPort;
                        await _appData.SaveAsync();
                        _aslmBaseline = _aslmBaseline with
                        {
                            OfficialPort = _officialPortDraft,
                            ThirdPartyPort = _thirdPartyPortDraft
                        };

                        RenderAslmCategory(showProfile: false, showPorts: true);

                        if (restartAfterSave && _activeCategory.SupportsAppRestart && await RestartActiveTargetAsync())
                        {
                            return;
                        }

                        await ShowSuccessAsync(BuildSaveMessage(hasChanges, false, []));
                        break;
                    }

                    case SettingsCategoryKind.Module:
                    {
                        SyncModuleDraftValuesFromControls();
                        if (!TryValidateModuleSettings(out var errorMessage))
                        {
                            await ShowErrorAsync(errorMessage);
                            return;
                        }

                        var moduleSaveResult = await SaveActiveModuleAsync(_activeCategory.Module!);
                        if (moduleSaveResult.TouchedModules.Count > 0)
                        {
                            await LoadModuleDraftsAsync(reloadModules: false, reloadRuntimeValues: true);
                        }

                        var activeCategoryId = _activeCategory.Id;
                        _categories = CreateOrderedCategories();
                        BuildCategorySelectors();
                        var resolvedCategory = ResolveCategory(activeCategoryId);
                        if (resolvedCategory != null)
                        {
                            ActivateCategory(resolvedCategory);
                        }

                        if (restartAfterSave)
                        {
                            await RestartActiveTargetAsync();
                        }

                        await ShowSuccessAsync(BuildSaveMessage(false, moduleSaveResult.TouchedModules.Count > 0, moduleSaveResult.DeferredSettings));
                        break;
                    }
                }
            }
            finally
            {
                _isSaving = false;
                UpdateSelectorButtonStates();
                UpdateActionButtons();
            }
        }

        /// <summary>
        /// Persists the changed settings for the active module and applies runtime updates where possible.
        /// </summary>
        private async Task<ModuleSaveResult> SaveActiveModuleAsync(ModuleConfig module)
        {
            var touchedModules = new HashSet<ModuleConfig>();
            var deferredSettings = new List<string>();

            foreach (var setting in module.Settings.Where(ShouldDisplaySetting))
            {
                if (IsAutoDetectedAslmEngine(setting))
                {
                    continue;
                }

                var currentValue = GetCurrentSettingValue(module, setting);
                var baseline = GetSettingBaseline(module, setting, currentValue);
                var effectiveValue = ResolveEffectiveSettingValue(module, setting, currentValue);
                var displayValue = setting.FormatValueForDisplay(effectiveValue);

                if (baseline.UseCustomValue == setting.UseCustomValue &&
                    string.Equals(baseline.DisplayValue, displayValue, StringComparison.Ordinal))
                {
                    continue;
                }

                touchedModules.Add(module);

                if (string.IsNullOrWhiteSpace(setting.SetExec))
                {
                    continue;
                }

                if (!File.Exists(module.SourcePath))
                {
                    deferredSettings.Add($"{module.Name}: {setting.Name}");
                    continue;
                }

                try
                {
                    var applyResult = await _moduleRunner.ExecuteSettingCommandAsync(
                        module,
                        setting,
                        isSet: true,
                        newValue: displayValue,
                        CancellationToken.None);

                    if (applyResult == null)
                    {
                        deferredSettings.Add($"{module.Name}: {setting.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to apply setting '{setting.Key}' for module '{module.Name}': {ex.Message}");
                    deferredSettings.Add($"{module.Name}: {setting.Name}");
                }
            }

            foreach (var touchedModule in touchedModules)
            {
                _moduleInstaller.SaveModuleConfig(touchedModule);
            }

            return new ModuleSaveResult(touchedModules, deferredSettings);
        }

        /// <summary>
        /// Restarts the target represented by the active category when restart is supported.
        /// </summary>
        private async Task<bool> RestartActiveTargetAsync()
        {
            if (_activeCategory == null)
            {
                return false;
            }

            if (_activeCategory.Kind == SettingsCategoryKind.Module && _activeCategory.Module != null)
            {
                if (!_activeCategory.Module.Status.Enabled || _activeCategory.Module.Commands.Run.Count == 0)
                {
                    return false;
                }

                await RestartModuleAsync(_activeCategory.Module);
                return false;
            }

            if (_activeCategory.SupportsAppRestart)
            {
                await RestartApplicationAsync();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Restarts one module using the same flow as the module management page.
        /// </summary>
        private async Task RestartModuleAsync(ModuleConfig module)
        {
            var freshConfig = await _moduleInstaller.LoadModuleConfig(module.SourcePath);
            if (freshConfig != null)
            {
                module.Settings = freshConfig.Settings;
                module.Commands = freshConfig.Commands;
            }

            await _moduleRunner.StopModuleAsync(module.SourcePath);
            await Task.Delay(1000);

            var restartLog = new Progress<string>(message => Debug.WriteLine($"[Restart] {message}"));
            _ = Task.Run(() => _moduleRunner.ExecuteRunAsync(module, restartLog, CancellationToken.None));
        }

        /// <summary>
        /// Restarts the application startup chain so app-level changes take effect.
        /// </summary>
        private async Task RestartApplicationAsync()
        {
            await _moduleRunner.StopAllModulesAsync();

            if (Application.Current is not App application || application.Windows.Count == 0)
            {
                return;
            }

            var newPage = application.CreateStartupPage();
            var window = application.Windows[0];
            window.Page = newPage;
        }

        // Validation

        /// <summary>
        /// Validates the current port draft values and returns parsed integers when valid.
        /// </summary>
        private bool TryParsePorts(out int officialPort, out int thirdPartyPort)
        {
            officialPort = 0;
            thirdPartyPort = 0;

            if (!int.TryParse(_officialPortDraft, NumberStyles.Integer, CultureInfo.InvariantCulture, out officialPort) ||
                officialPort < 1024 ||
                officialPort > 65000)
            {
                ShowPortError("Official port must be between 1024 and 65000.");
                return false;
            }

            if (!int.TryParse(_thirdPartyPortDraft, NumberStyles.Integer, CultureInfo.InvariantCulture, out thirdPartyPort) ||
                thirdPartyPort < 1024 ||
                thirdPartyPort > 64000)
            {
                ShowPortError("Third-party port must be between 1024 and 64000.");
                return false;
            }

            var officialPortEnd = officialPort + 100;
            var thirdPartyPortEnd = thirdPartyPort + 1000;
            if (officialPort < thirdPartyPortEnd && thirdPartyPort < officialPortEnd)
            {
                ShowPortError($"Port ranges overlap. Official {officialPort}-{officialPortEnd - 1} conflicts with Third-party {thirdPartyPort}-{thirdPartyPortEnd - 1}.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Validates the current drafted module settings to ensure type correctness.
        /// </summary>
        private bool TryValidateModuleSettings(out string errorMessage)
        {
            errorMessage = string.Empty;

            foreach (var mapping in _settingMappings)
            {
                var useCustom = mapping.ReadCustomValue?.Invoke() ?? mapping.Setting.UseCustomValue;
                if (mapping.Setting.IsAutomaticallyManaged && !useCustom)
                {
                    continue;
                }

                var rawValueObj = mapping.ReadValue();
                var rawValue = rawValueObj?.ToString();
                
                if (rawValueObj is bool)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rawValue))
                {
                    continue;
                }

                var type = mapping.Setting.NormalizedType;
                
                if (type is "int" or "integer" or "port")
                {
                    if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        errorMessage = $"Invalid integer numeric value for '{mapping.Setting.Name}'.";
                        return false;
                    }
                }
                else if (type is "long")
                {
                    if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                    {
                        errorMessage = $"Invalid long integer numeric value for '{mapping.Setting.Name}'.";
                        return false;
                    }
                }
                else if (type is "float" or "double" or "number")
                {
                    if (!double.TryParse(rawValue, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out _))
                    {
                        errorMessage = $"Invalid numeric value for '{mapping.Setting.Name}'.";
                        return false;
                    }
                }
                else if (type is "bool" or "engine")
                {
                    if (!bool.TryParse(rawValue, out _) && !string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase) && !string.Equals(rawValue, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        // Some text fields might be used for booleans, allow true/false strings.
                        errorMessage = $"Invalid boolean value for '{mapping.Setting.Name}'.";
                        return false;
                    }
                }
                else if (type is "json" or "object" or "array")
                {
                    var trimmed = rawValue.Trim();
                    if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
                    {
                        try
                        {
                            using var jsonDocument = System.Text.Json.JsonDocument.Parse(trimmed);
                        }
                        catch
                        {
                            errorMessage = $"Invalid JSON payload for '{mapping.Setting.Name}'.";
                            return false;
                        }
                    }
                }
            }

            return true;
        }

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
                Debug.WriteLine($"Failed to read setting '{setting.Key}' for module '{module.Name}': {ex.Message}");
                return new LoadedSetting(setting, fallbackValue);
            }
        }

        /// <summary>
        /// Resolves the current draft value used for rendering and save comparison.
        /// </summary>
        private object? GetCurrentSettingValue(ModuleConfig module, ModuleSetting setting) =>
            IsAutoDetectedAslmEngine(setting)
                ? IsAslmEngineInstalled(setting.Key)
                : setting.IsAutomaticallyManaged && !setting.UseCustomValue
                    ? _moduleRunner.GetResolvedSettingValue(module, setting) ?? setting.Value ?? setting.Default
                    : setting.Value ?? setting.Default;

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

                var text = new VerticalStackLayout { Spacing = 5 };
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

                var text = new VerticalStackLayout { Spacing = 5 };
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

            var content = new VerticalStackLayout { Spacing = 9 };
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
            var toggle = CreateCompactToggle(value is bool boolValue
                ? boolValue
                : bool.TryParse(setting.FormatValueForDisplay(value), out var parsedBool) && parsedBool);
            toggle.View.HorizontalOptions = LayoutOptions.End;

            if (HasDependentSettings(module, setting))
            {
                toggle.Toggled += (_, _) =>
                {
                    setting.Value = toggle.IsToggled;
                    _ = RefreshDynamicVisibilityAsync();
                };
            }

            toggle.Toggled += (_, _) => UpdateActionButtons();

            return (toggle.View, new SettingControlMapping(
                module,
                setting,
                () => toggle.IsToggled,
                null,
                baseline.DisplayValue,
                baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates the dropdown selector used by the active LLM engine setting.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateActiveEngineEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var allowedValues = setting.AllowedValues ?? [];
            var picker = CreatePicker(null, 13);
            var pickerContainer = CreatePickerContainer(picker);

            var selectedValue = setting.FormatValueForDisplay(value);
            if (string.IsNullOrWhiteSpace(selectedValue))
            {
                selectedValue = allowedValues.FirstOrDefault() ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(selectedValue) &&
                allowedValues.All(option => !string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase)))
            {
                allowedValues = [selectedValue, .. allowedValues];
            }

            picker.ItemsSource = allowedValues;

            var selectedIndex = allowedValues.FindIndex(option =>
                string.Equals(option, selectedValue, StringComparison.OrdinalIgnoreCase));
            picker.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (allowedValues.Count > 0 ? 0 : -1);
            picker.SelectedIndexChanged += (_, _) => UpdateActionButtons();

            return (pickerContainer, new SettingControlMapping(
                module,
                setting,
                () => picker.SelectedIndex >= 0 && picker.SelectedIndex < allowedValues.Count
                    ? allowedValues[picker.SelectedIndex]
                    : null,
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
            var picker = CreatePicker(null, 14);
            var pickerContainer = CreatePickerContainer(picker);
            var allowedValues = setting.AllowedValues!.ToList();
            picker.ItemsSource = allowedValues;

            var currentValue = setting.FormatValueForDisplay(value);
            if (string.IsNullOrWhiteSpace(currentValue))
            {
                currentValue = setting.FormatValueForDisplay(setting.Default);
            }

            var selectedIndex = allowedValues.FindIndex(option =>
                string.Equals(option, currentValue, StringComparison.OrdinalIgnoreCase));
            if (selectedIndex < 0 && !string.IsNullOrWhiteSpace(currentValue))
            {
                allowedValues.Insert(0, currentValue);
                selectedIndex = 0;
            }

            picker.SelectedIndex = selectedIndex;
            picker.SelectedIndexChanged += (_, _) => UpdateActionButtons();

            return (pickerContainer, new SettingControlMapping(
                module,
                setting,
                () => picker.SelectedIndex >= 0 && picker.SelectedIndex < allowedValues.Count
                    ? allowedValues[picker.SelectedIndex]
                    : null,
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
            var (field, entry) = CreateTextField(setting.FormatValueForDisplay(value));
            entry.Keyboard = Keyboard.Numeric;
            entry.TextChanged += (_, _) => UpdateActionButtons();

            return (field, new SettingControlMapping(module, setting, () => entry.Text, null, baseline.DisplayValue, baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates a plain text entry for free-form string settings.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateTextEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var (field, entry) = CreateTextField(setting.FormatValueForDisplay(value));
            entry.TextChanged += (_, _) => UpdateActionButtons();
            return (field, new SettingControlMapping(module, setting, () => entry.Text, null, baseline.DisplayValue, baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates a password entry and its mapping while preserving the shared baseline snapshot.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreatePasswordEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = GetSettingBaseline(module, setting, value);
            var (field, entry) = CreatePasswordField(setting.FormatValueForDisplay(value));
            entry.TextChanged += (_, _) => UpdateActionButtons();
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
                : CreateTextField(initialDisplayValue);
            var baseline = GetSettingBaseline(module, setting, initialDisplayValue);
            var entry = entryView.Entry;
            var lastCustomValue = setting.FormatValueForDisplay(setting.Value ?? value);

            var customToggle = CreateInlineToggle();
            customToggle.IsToggled = setting.UseCustomValue;

            entry.TextChanged += (_, args) =>
            {
                if (customToggle.IsToggled)
                {
                    lastCustomValue = args.NewTextValue ?? string.Empty;
                }

                UpdateActionButtons();
            };

            customToggle.Toggled += (_, args) =>
            {
                entry.Text = args.Value
                    ? (string.IsNullOrWhiteSpace(lastCustomValue) ? setting.FormatValueForDisplay(setting.Value ?? value) : lastCustomValue)
                    : setting.FormatValueForDisplay(_moduleRunner.GetResolvedSettingValue(module, setting));

                ApplyTextEntryState(entry, !args.Value);
                UpdateActionButtons();
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
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 0 },
                Padding = 0
            };

            border.BackgroundColor = Colors.Transparent;
            return border;
        }

        /// <summary>
        /// Creates the card container used for one individual setting.
        /// </summary>
        private static Border CreateSettingCardBorder()
        {
            var border = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 0 },
                Padding = 0
            };

            border.BackgroundColor = Colors.Transparent;
            return border;
        }

        /// <summary>
        /// Creates the title label used for each module settings section.
        /// </summary>
        private static Label CreateSectionHeader(string text)
        {
            var label = new Label { Text = text, FontSize = 16, FontAttributes = FontAttributes.Bold };
            label.SetDynamicResource(Label.TextColorProperty, "LabelPrimary");
            return label;
        }

        /// <summary>
        /// Creates the primary label for a setting name.
        /// </summary>
        private static Label CreateCardTitle(string text)
        {
            var label = new Label { Text = text, FontSize = 15, FontAttributes = FontAttributes.Bold };
            label.SetDynamicResource(Label.TextColorProperty, "LabelPrimary");
            return label;
        }

        /// <summary>
        /// Creates the secondary label used for setting descriptions.
        /// </summary>
        private static Label CreateCardDescription(string text)
        {
            var label = new Label { Text = text, FontSize = 12, LineBreakMode = LineBreakMode.WordWrap, MaxLines = 3 };
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
        private static CompactToggle CreateInlineToggle() =>
            CreateCompactToggle(false);

        /// <summary>
        /// Creates a compact helper row that keeps the custom-value toggle close to its label.
        /// </summary>
        private static Grid CreateInlineToggleRow(string text, CompactToggle toggle)
        {
            var label = CreateSecondaryLabel(text);
            label.VerticalOptions = LayoutOptions.Center;
            label.LineBreakMode = LineBreakMode.NoWrap;

            var row = new Grid
            {
                ColumnSpacing = 10,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                MinimumHeightRequest = 22,
                Margin = new Thickness(0, 4, 0, 2)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            row.Children.Add(label);
            Grid.SetColumn(label, 0);

            toggle.View.HorizontalOptions = LayoutOptions.End;
            row.Children.Add(toggle.View);
            Grid.SetColumn(toggle.View, 1);

            return row;
        }

        /// <summary>
        /// Creates a compact picker that follows the shared settings page typography and colors.
        /// </summary>
        private static Picker CreatePicker(string? title, double fontSize)
        {
            var picker = new Picker
            {
                Title = title,
                FontSize = fontSize,
                HeightRequest = 46,
                MinimumHeightRequest = 46,
                HorizontalOptions = LayoutOptions.Fill
            };

            picker.SetDynamicResource(Picker.TextColorProperty, "LabelPrimary");
            picker.SetDynamicResource(Picker.TitleColorProperty, "LabelSecondary");
            picker.BackgroundColor = Colors.Transparent;
            ApplyCompactPickerStyle(picker);
            return picker;
        }

        /// <summary>
        /// Creates a subtle field container for pickers so they match the weight of text inputs.
        /// </summary>
        private static Border CreatePickerContainer(Picker picker)
        {
            var border = new Border
            {
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(12, 0),
                MinimumHeightRequest = 46,
                Content = picker
            };

            border.BackgroundColor = Color.FromArgb("#19191C");
            border.SetDynamicResource(Border.StrokeProperty, "Separator");
            return border;
        }

        /// <summary>
        /// Creates a bordered field shell shared by text inputs and password editors.
        /// </summary>
        private static Border CreateFieldContainer(View content, Thickness? padding = null)
        {
            var border = new Border
            {
                StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Padding = padding ?? new Thickness(12, 0),
                MinimumHeightRequest = 46,
                Content = content
            };

            border.BackgroundColor = Color.FromArgb("#19191C");
            border.SetDynamicResource(Border.StrokeProperty, "Separator");
            return border;
        }

        /// <summary>
        /// Creates a compact toggle with a fixed custom visual used instead of the native switch.
        /// </summary>
        private static CompactToggle CreateCompactToggle(bool isToggled) =>
            new(isToggled);

        /// <summary>
        /// Normalizes the native picker chrome so it does not render an extra WinUI border inside the card.
        /// </summary>
        private static void ApplyCompactPickerStyle(Picker picker)
        {
            void ApplyPlatformStyle()
            {
#if WINDOWS
                if (picker.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ComboBox comboBox)
                {
                    var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    comboBox.Background = transparentBrush;
                    comboBox.BorderBrush = transparentBrush;
                    comboBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    comboBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
                    comboBox.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0);
                    comboBox.UseSystemFocusVisuals = false;
                }
#endif
            }

            picker.HandlerChanged += (_, _) => ApplyPlatformStyle();
            ApplyPlatformStyle();
        }

        /// <summary>
        /// Applies a quieter WinUI scrollbar treatment so the overlay keeps its minimalist look.
        /// </summary>
        private static void ApplyScrollViewChrome(ScrollView scrollView, bool isSidebar)
        {
            void ApplyPlatformStyle()
            {
#if WINDOWS
                if (scrollView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ScrollViewer viewer)
                {
                    var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    viewer.Background = transparentBrush;
                    viewer.BorderBrush = transparentBrush;
                    viewer.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    viewer.Padding = new Microsoft.UI.Xaml.Thickness(0);

                    void Restyle()
                    {
                        StyleScrollBars(viewer, isSidebar);
                    }

                    viewer.Loaded -= OnViewerLoaded;
                    viewer.Loaded += OnViewerLoaded;
                    viewer.SizeChanged -= OnViewerSizeChanged;
                    viewer.SizeChanged += OnViewerSizeChanged;
                    viewer.DispatcherQueue.TryEnqueue(Restyle);

                    void OnViewerLoaded(object? sender, Microsoft.UI.Xaml.RoutedEventArgs e) => Restyle();
                    void OnViewerSizeChanged(object? sender, Microsoft.UI.Xaml.SizeChangedEventArgs e) => Restyle();
                }
#endif
            }

            scrollView.HandlerChanged += (_, _) => ApplyPlatformStyle();
            ApplyPlatformStyle();
        }

#if WINDOWS
        /// <summary>
        /// Restyles WinUI scrollbars to sit tighter to the edge with lower visual weight.
        /// </summary>
        private static void StyleScrollBars(Microsoft.UI.Xaml.Controls.ScrollViewer viewer, bool isSidebar)
        {
            var thumbBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Microsoft.UI.ColorHelper.FromArgb(isSidebar ? (byte)54 : (byte)92, 255, 255, 255));
            var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

            foreach (var scrollBar in FindDescendants<Microsoft.UI.Xaml.Controls.Primitives.ScrollBar>(viewer))
            {
                if (scrollBar.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical)
                {
                    scrollBar.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right;
                    scrollBar.Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 4);
                    scrollBar.Padding = new Microsoft.UI.Xaml.Thickness(0);
                    scrollBar.Background = transparentBrush;
                    scrollBar.Foreground = thumbBrush;
                    scrollBar.Opacity = isSidebar ? 0.18 : 0.34;
                }
                else
                {
                    scrollBar.Height = 3;
                    scrollBar.MinHeight = 3;
                    scrollBar.Background = transparentBrush;
                    scrollBar.Opacity = 0.2;
                }

                foreach (var repeatButton in FindDescendants<Microsoft.UI.Xaml.Controls.Primitives.RepeatButton>(scrollBar))
                {
                    repeatButton.Background = transparentBrush;
                    repeatButton.BorderBrush = transparentBrush;
                    repeatButton.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    repeatButton.Opacity = 0;
                }

                foreach (var border in FindDescendants<Microsoft.UI.Xaml.Controls.Border>(scrollBar))
                {
                    border.Background = transparentBrush;
                    border.BorderBrush = transparentBrush;
                    border.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                }

                foreach (var thumb in FindDescendants<Microsoft.UI.Xaml.Controls.Primitives.Thumb>(scrollBar))
                {
                    thumb.Background = thumbBrush;
                    thumb.BorderBrush = transparentBrush;
                    thumb.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    thumb.MinWidth = isSidebar ? 6 : 6;
                    thumb.Width = isSidebar ? 6 : 6;
                    thumb.MinHeight = 18;
                    thumb.Opacity = isSidebar ? 0.38 : 0.55;
                }
            }
        }

        /// <summary>
        /// Enumerates descendants of the requested WinUI type.
        /// </summary>
        private static IEnumerable<T> FindDescendants<T>(Microsoft.UI.Xaml.DependencyObject root) where T : Microsoft.UI.Xaml.DependencyObject
        {
            var queue = new Queue<Microsoft.UI.Xaml.DependencyObject>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                var childCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(current);

                for (var index = 0; index < childCount; index++)
                {
                    var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(current, index);
                    if (child is T typed)
                    {
                        yield return typed;
                    }

                    queue.Enqueue(child);
                }
            }
        }
#endif


        /// <summary>
        /// Creates a password editor that keeps the entry styling consistent with regular text fields.
        /// </summary>
        private static (View Control, Entry Entry) CreatePasswordField(string? text)
        {
            var entry = CreateTextEntry(text, isPassword: true, clearButtonVisibility: ClearButtonVisibility.Never);
            entry.Margin = new Thickness(12, 0, 44, 0);
            var toggleButton = new ImageButton
            {
                Source = PasswordIconHidden,
                WidthRequest = 36,
                HeightRequest = 36,
                MinimumWidthRequest = 36,
                MinimumHeightRequest = 36,
                Padding = 10,
                Margin = new Thickness(0, 0, 4, 0),
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

            var grid = new Grid { MinimumHeightRequest = 46 };
            grid.Children.Add(entry);
            grid.Children.Add(toggleButton);
            toggleButton.ZIndex = 1;
            return (CreateFieldContainer(grid, new Thickness(0)), entry);
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
        private static (View Control, Entry Entry) CreateTextField(string? text, bool isPassword = false, ClearButtonVisibility clearButtonVisibility = ClearButtonVisibility.WhileEditing)
        {
            var entry = CreateTextEntry(text, isPassword, clearButtonVisibility);
            return (CreateFieldContainer(entry), entry);
        }

        /// <summary>
        /// Creates a standard text entry configured for the requested text behavior.
        /// </summary>
        private static Entry CreateTextEntry(string? text, bool isPassword = false, ClearButtonVisibility clearButtonVisibility = ClearButtonVisibility.WhileEditing)
        {
            var entry = new Entry
            {
                Text = text,
                FontSize = 13,
                HeightRequest = 46,
                MinimumHeightRequest = 46,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Center,
                VerticalTextAlignment = TextAlignment.Center,
                ClearButtonVisibility = clearButtonVisibility,
                IsPassword = isPassword,
                BackgroundColor = Colors.Transparent
            };

            entry.SetDynamicResource(Entry.TextColorProperty, "LabelPrimary");
            ApplyFlatEntryStyle(entry);
            return entry;
        }

        /// <summary>
        /// Removes native WinUI chrome so entries match the shared field borders.
        /// </summary>
        private static void ApplyFlatEntryStyle(Entry entry)
        {
            void ApplyPlatformStyle()
            {
#if WINDOWS
                var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

                switch (entry.Handler?.PlatformView)
                {
                    case Microsoft.UI.Xaml.Controls.TextBox textBox:
                        textBox.Background = transparentBrush;
                        textBox.BorderBrush = transparentBrush;
                        textBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                        textBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
                        textBox.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0);
                        textBox.UseSystemFocusVisuals = false;
                        break;
                    case Microsoft.UI.Xaml.Controls.PasswordBox passwordBox:
                        passwordBox.Background = transparentBrush;
                        passwordBox.BorderBrush = transparentBrush;
                        passwordBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                        passwordBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
                        passwordBox.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0);
                        passwordBox.UseSystemFocusVisuals = false;
                        break;
                }
#endif
            }

            entry.HandlerChanged += (_, _) => ApplyPlatformStyle();
            ApplyPlatformStyle();
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
        /// Creates the read-only installed-state badge shown for auto-detected ASLM engines.
        /// </summary>
        private Border CreateStatusBadge(string text, bool isPositive)
        {
            var label = new Label
            {
                Text = text,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                VerticalTextAlignment = TextAlignment.Center
            };

            label.TextColor = isPositive
                ? Color.FromArgb("#D8E8FF")
                : Color.FromArgb("#99EBEBF5");

            return new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 0 },
                Padding = 0,
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                Content = label
            };
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
        /// Builds the save confirmation message, including deferred runtime updates when present.
        /// </summary>
        private static string BuildSaveMessage(bool hasAslmChanges, bool hasModuleChanges, List<string> deferredSettings)
        {
            if (!hasAslmChanges && !hasModuleChanges)
            {
                return "No changes to save.";
            }

            if (deferredSettings.Count == 0)
            {
                return "Settings saved and applied.";
            }

            var preview = string.Join("\n", deferredSettings.Take(5));
            return $"Settings saved. Some module settings could not be applied immediately and will be retried on next module start.\n\n{preview}";
        }
    }
}
