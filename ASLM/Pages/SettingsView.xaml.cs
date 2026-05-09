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
        private static readonly TimeSpan OllamaSignInPollInterval = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan OllamaSignInPollDuration = TimeSpan.FromMinutes(5);

        private const string FooterButtonStyleKey = "SettingsFooterButtonStyle";
        private const string FooterPrimaryButtonStyleKey = "SettingsFooterPrimaryButtonStyle";
        private const string FooterDangerButtonStyleKey = "SettingsFooterDangerButtonStyle";
        private const string SelectorHeaderLabelStyleKey = "SettingsSelectorHeaderLabelStyle";
        private const string SelectorButtonBorderStyleKey = "SettingsSelectorButtonBorderStyle";
        private const string SelectorButtonLabelStyleKey = "SettingsSelectorButtonLabelStyle";
        private const string TransparentBorderStyleKey = "SettingsTransparentBorderStyle";
        private const string FieldBorderStyleKey = "SettingsFieldBorderStyle";
        private const string TextEntryStyleKey = "SettingsTextEntryStyle";
        private const string PickerStyleKey = "SettingsPickerStyle";
        private const string SubGroupHeaderLabelStyleKey = "SettingsSubGroupHeaderLabelStyle";
        private const string CardTitleLabelStyleKey = "SettingsCardTitleLabelStyle";
        private const string CardDescriptionLabelStyleKey = "SettingsCardDescriptionLabelStyle";
        private const string SecondaryLabelStyleKey = "SettingsSecondaryLabelStyle";
        private const string InlineActionButtonStyleKey = "SettingsInlineActionButtonStyle";
        private const string PasswordToggleImageStyleKey = "SettingsPasswordToggleImageStyle";

        private const double TitleDescriptionSpacing = 8;

        private readonly AppDataStore _appData;
        private readonly SettingsService _settingsService;
        private readonly OllamaSettingsStore _ollamaSettings;
        private readonly UpdateManager _updateManager;
        private readonly AslmApiServer _apiServer;
        private readonly NotificationCenter _notifications;
        private readonly List<SettingControlMapping> _settingMappings = [];
        private readonly Dictionary<string, SettingBaseline> _settingBaselines = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Border> _categoryButtons = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _runtimeLoadedModuleIds = new(StringComparer.OrdinalIgnoreCase);
        private List<ModuleConfig> _loadedModules = [];
        private List<SettingsCategory> _categories = [];
        private SettingsCategory? _activeCategory;
        private AslmBaseline _aslmBaseline = new(string.Empty, string.Empty, string.Empty, true);
        private ConsoleBaseline _consoleBaseline = new(true, true, true);
        private UpdateBaseline _updateBaseline = new(true, false, "24", "release", "release", "release");
        private ConsoleBaseline _consoleDraft = new(true, true, true);
        private UpdateBaseline _updateDraft = new(true, false, "24", "release", "release", "release");
        private OllamaPersistentSettings _ollamaDraft = new();
        private string _userNameDraft = string.Empty;
        private string _officialPortDraft = string.Empty;
        private string _thirdPartyPortDraft = string.Empty;
        private bool _apiServerEnabledDraft = true;
        private bool _hasLoaded;
        private bool _isRefreshingVisibility;
        private bool _isSwitchingCategory;
        private bool _isSaving;
        private bool _isOllamaAccountActionRunning;
        private bool _isOllamaMetadataRefreshRunning;
        private int _actionButtonUpdateQueued;
        private string _ollamaAccountAction = string.Empty;
        private Button? _ollamaAccountButton;
        private Label? _ollamaAccountStatusLabel;
        private CompactToggle? _checkUpdatesToggle;
        private CompactToggle? _autoUpdatesToggle;
        private Entry? _updatePeriodEntry;
        private Picker? _appUpdateChannelPicker;
        private Picker? _moduleUpdateModePicker;
        private Picker? _moduleUpdateChannelPicker;
        private Label? _updateStatusLabel;
        private Button? _prepareAppUpdateButton;
        private Button? _restartAppUpdateButton;
        private UpdateCandidate? _pendingAppUpdateCandidate;
        private CompactToggle? _apiServerToggle;
        private CompactToggle? _consoleSidebarToggle;
        private CompactToggle? _consoleCompletedToggle;
        private CompactToggle? _consoleIndividualToggle;
        private CancellationTokenSource? _ollamaMetadataRefreshCts;
        private CancellationTokenSource? _ollamaStatusPollingCts;

        /// <summary>
        /// Raised when the user asks to close the settings overlay.
        /// </summary>
        public event EventHandler? CloseRequested;

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

        // Initialization

        /// <summary>
        /// Creates the settings view and hooks the first-load handler.
        /// </summary>
        public SettingsView(
            AppDataStore appData,
            SettingsService settingsService,
            OllamaSettingsStore ollamaSettings,
            UpdateManager updateManager,
            AslmApiServer apiServer,
            NotificationCenter notifications)
        {
            _appData = appData;
            _settingsService = settingsService;
            _ollamaSettings = ollamaSettings;
            _updateManager = updateManager;
            _apiServer = apiServer;
            _notifications = notifications;
            InitializeComponent();
            ApplyFlatEntryStyle(UsernameEntry);
            ApplyFlatEntryStyle(OfficialPortEntry);
            ApplyFlatEntryStyle(ThirdPartyPortEntry);
            ApplyScrollViewChrome(CategoryScroll, isSidebar: true);
            ApplyScrollViewChrome(SettingsScroll, isSidebar: false);
            UsernameEntry.TextChanged += (_, _) => QueueActionButtonUpdate();
            OfficialPortEntry.TextChanged += (_, _) => QueueActionButtonUpdate();
            ThirdPartyPortEntry.TextChanged += (_, _) => QueueActionButtonUpdate();
            SizeChanged += OnViewSizeChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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

            StopOllamaStatusPolling();
            StopOllamaMetadataRefresh();
            _ollamaSettings.StopManagedRuntime();
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
            await Task.Run(LoadOllamaDraftsFromService);
            await LoadModuleDraftsAsync(reloadModules: true, reloadRuntimeValues: false);

            _categories = _settingsService.CreateOrderedCategories(_loadedModules);

            var targetCategory = ResolveCategory(previousCategoryId) ?? _categories.FirstOrDefault();
            if (targetCategory == null)
            {
                _activeCategory = null;
                ShowEmptyCategory("No settings are available.");
                UpdateActionButtons();
                return;
            }

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
        /// Stops background Ollama status polling when the settings view leaves the visual tree.
        /// </summary>
        private void OnUnloaded(object? sender, EventArgs e)
        {
            StopOllamaStatusPolling();
            StopOllamaMetadataRefresh();
            _ollamaSettings.StopManagedRuntime();
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
            var snapshot = SettingsService.BuildAslmDraftSnapshot(_appData, _apiServer.IsEnabled);
            _userNameDraft = snapshot.UserName;
            _officialPortDraft = snapshot.OfficialPort;
            _thirdPartyPortDraft = snapshot.ThirdPartyPort;
            _apiServerEnabledDraft = snapshot.ApiServerEnabled;
            _aslmBaseline = new AslmBaseline(_userNameDraft, _officialPortDraft, _thirdPartyPortDraft, _apiServerEnabledDraft);
            _consoleBaseline = snapshot.ConsoleBaseline;
            _consoleDraft = _consoleBaseline;
            _updateBaseline = snapshot.UpdateBaseline;
            _updateDraft = _updateBaseline;

            ApplyAslmDraftsToControls();
            PortErrorLabel.IsVisible = false;
        }

        /// <summary>
        /// Copies the persisted Ollama settings into the editable page draft.
        /// </summary>
        private void LoadOllamaDraftsFromService()
        {
            try
            {
                _ollamaDraft = _ollamaSettings.LoadSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load Ollama settings: {ex.Message}");
                _ollamaDraft = new OllamaPersistentSettings();
            }
        }

        /// <summary>
        /// Reloads module settings and refreshes the baseline snapshots used by save detection.
        /// </summary>
        private async Task LoadModuleDraftsAsync(bool reloadModules, bool reloadRuntimeValues)
        {
            if (reloadModules || _loadedModules.Count == 0)
            {
                _loadedModules = await _settingsService.DiscoverModulesAsync();
                _runtimeLoadedModuleIds.Clear();
                _settingBaselines.Clear();
            }

            if (reloadRuntimeValues)
            {
                _runtimeLoadedModuleIds.Clear();
                _settingBaselines.Clear();
            }

            foreach (var module in _loadedModules)
            {
                await _settingsService.LoadModuleDraftAsync(module, reloadRuntimeValues, _settingBaselines);
                if (reloadRuntimeValues)
                {
                    _runtimeLoadedModuleIds.Add(SettingsService.GetModuleRuntimeKey(module));
                }
            }
        }

        // Categories

        /// <summary>
        /// Rebuilds the unified category selector sidebar.
        /// </summary>
        private void BuildCategorySelectors()
        {
            _categoryButtons.Clear();
            CategoryPanel.Children.Clear();

            // ASLM Group
            CategoryPanel.Children.Add(CreateSelectorHeader("ASLM"));
            foreach (var category in _categories.Where(c => SettingsService.GetGroupForCategory(c) == SettingsCategoryGroup.Aslm))
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
            var modCategories = _categories.Where(c => SettingsService.GetGroupForCategory(c) == SettingsCategoryGroup.Modules).ToList();
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
                Style = GetStyleResource(SelectorHeaderLabelStyleKey)
            };

        /// <summary>
        /// Creates one sidebar selector button for a specific settings category.
        /// </summary>
        private static Border CreateSelectorButton(string text)
        {
            var border = new Border
            {
                Style = GetStyleResource(SelectorButtonBorderStyleKey)
            };

            var label = new Label
            {
                Text = text,
                Style = GetStyleResource(SelectorButtonLabelStyleKey)
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

        /// <summary>
        /// Switches to the requested category after preserving or discarding pending edits.
        /// </summary>
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
                SyncDraftValuesFromControls();

                var resolvedCategory = ResolveCategory(category.Id);
                if (resolvedCategory == null)
                {
                    return;
                }

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
                case SettingsCategoryKind.Aslm:
                    RenderAslmCategory();
                    break;
                case SettingsCategoryKind.AslmProfile:
                    RenderAccountCategory();
                    break;
                case SettingsCategoryKind.Updates:
                    RenderUpdatesCategory();
                    break;
                case SettingsCategoryKind.Ollama:
                    RenderOllamaCategory();
                    break;
                case SettingsCategoryKind.Module:
                    RenderModuleCategory(category.Module!);
                    _ = RefreshActiveModuleRuntimeValuesAsync(category);
                    break;
            }

            UpdateSelectorButtonStates();
            UpdateActionButtons();
        }

        /// <summary>
        /// Loads live runtime values only for the currently visible module settings page.
        /// </summary>
        private async Task RefreshActiveModuleRuntimeValuesAsync(SettingsCategory category)
        {
            if (category.Kind != SettingsCategoryKind.Module || category.Module == null)
            {
                return;
            }

            var module = category.Module;
            var runtimeKey = SettingsService.GetModuleRuntimeKey(module);
            if (_runtimeLoadedModuleIds.Contains(runtimeKey))
            {
                return;
            }

            try
            {
                var settings = module.Settings?.Where(SettingsService.ShouldDisplaySetting).ToList() ?? [];
                if (settings.Count == 0)
                {
                    _runtimeLoadedModuleIds.Add(runtimeKey);
                    return;
                }

                var loaded = await Task.WhenAll(settings.Select(setting => _settingsService.LoadSettingValueAsync(module, setting)));

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var stillActive =
                        _activeCategory?.Kind == SettingsCategoryKind.Module &&
                        _activeCategory.Module != null &&
                        string.Equals(_activeCategory.Module.SourcePath, module.SourcePath, StringComparison.OrdinalIgnoreCase);

                    if (stillActive && HasUnsavedChanges())
                    {
                        return;
                    }

                    _settingsService.UpdateSettingBaselines(module, loaded, _settingBaselines);
                    foreach (var item in loaded)
                    {
                        if (!item.Setting.IsAutomaticallyManaged || item.Setting.UseCustomValue)
                        {
                            item.Setting.Value = item.Value;
                        }
                    }

                    _runtimeLoadedModuleIds.Add(runtimeKey);

                    if (stillActive)
                    {
                        RenderModuleCategory(module);
                        UpdateActionButtons();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to refresh runtime settings for module '{module.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Forces live runtime values for one module after settings are saved.
        /// </summary>
        private async Task ReloadModuleRuntimeValuesAsync(ModuleConfig module)
        {
            var key = SettingsService.GetModuleRuntimeKey(module);
            _runtimeLoadedModuleIds.Remove(key);
            await _settingsService.LoadModuleDraftAsync(module, reloadRuntimeValues: true, _settingBaselines);
            _runtimeLoadedModuleIds.Add(key);
        }

        /// <summary>
        /// Returns the category that matches the stored category identifier, if it still exists.
        /// </summary>
        private SettingsCategory? ResolveCategory(string? categoryId) =>
            string.IsNullOrWhiteSpace(categoryId)
                ? null
                : _categories.FirstOrDefault(category => category.Id.Equals(categoryId, StringComparison.OrdinalIgnoreCase));

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
                label.TextColor = isActive
                    ? Colors.White
                    : GetColorResource("LabelSecondary", Color.FromArgb("#99EBEBF5"));
                label.FontAttributes = FontAttributes.None;
            }

            button.BackgroundColor = isActive
                ? GetColorResource("ActionBlue", Color.FromArgb("#0A84FF"))
                : Colors.Transparent;
            button.Opacity = isActive ? 1.0 : 0.92;
        }

        /// <summary>
        /// Coalesces rapid editor changes before recomputing save/reset button state.
        /// </summary>
        private void QueueActionButtonUpdate()
        {
            if (Dispatcher == null)
            {
                UpdateActionButtons();
                return;
            }

            if (Interlocked.Exchange(ref _actionButtonUpdateQueued, 1) == 1)
            {
                return;
            }

            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(50), () =>
            {
                Interlocked.Exchange(ref _actionButtonUpdateQueued, 0);
                UpdateActionButtons();
            });
        }

        /// <summary>
        /// Updates the footer action buttons to match the currently visible category.
        /// </summary>
        private void UpdateActionButtons()
        {
            var canInteract = !_isSaving && _activeCategory != null;
            var hasChanges = canInteract && HasAnyUnsavedChanges();
            var canReset = _activeCategory is { Kind: not SettingsCategoryKind.Ollama };
            DefaultButton.IsVisible = canReset;
            DefaultButton.IsEnabled = canInteract && canReset;
            DiscardButton.IsVisible = canReset && hasChanges;
            DiscardButton.IsEnabled = canInteract && hasChanges;
            SaveButton.IsEnabled = canInteract;
            SaveButton.Text = _isSaving ? "Saving..." : "Save";
            ApplyActionButtonState(DefaultButton, false);
            ApplyActionButtonState(DiscardButton, isPrimary: false, isDanger: true);

            if (_activeCategory == null)
            {
                DefaultButton.IsVisible = false;
                DiscardButton.IsVisible = false;
                DiscardButton.IsEnabled = false;
                SaveAndRestartButton.IsEnabled = false;
                SaveAndRestartButton.IsVisible = false;
                SaveButton.IsVisible = false;
                ApplyActionButtonState(SaveButton, false);
                SaveAndRestartButton.Text = "Save and restart";
                return;
            }

            var canShowRestart = hasChanges && HasPendingRestartChanges();

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
        /// Checks whether any pending edit has a restart path, regardless of the visible category.
        /// </summary>
        private bool HasPendingRestartChanges() =>
            HasUnsavedAslmSettingsChanges() ||
            GetModulesWithUnsavedChanges().Any(CanRestartModule);

        /// <summary>
        /// Returns modules with unsaved settings, including the currently edited module controls.
        /// </summary>
        private List<ModuleConfig> GetModulesWithUnsavedChanges()
        {
            var result = new List<ModuleConfig>();
            foreach (var module in _loadedModules)
            {
                var isActiveModule =
                    _activeCategory?.Kind == SettingsCategoryKind.Module &&
                    _activeCategory.Module != null &&
                    string.Equals(_activeCategory.Module.SourcePath, module.SourcePath, StringComparison.OrdinalIgnoreCase);

                var hasChanges = isActiveModule && _settingMappings.Count > 0
                    ? HasUnsavedModuleChanges()
                    : _settingsService.ModuleHasChangesComparedToBaseline(module, _settingBaselines);

                if (hasChanges)
                {
                    result.Add(module);
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether one changed module can be restarted from settings.
        /// </summary>
        private static bool CanRestartModule(ModuleConfig module) =>
            module.Status.Enabled && module.Commands.Run.Count > 0;

        /// <summary>
        /// Applies the passive or emphasized visual state to one footer action button.
        /// </summary>
        private static void ApplyActionButtonState(Button button, bool isPrimary, bool isDanger = false)
        {
            button.BorderWidth = 0;
            button.Style = GetStyleResource(isDanger
                ? FooterDangerButtonStyleKey
                : isPrimary
                    ? FooterPrimaryButtonStyleKey
                    : FooterButtonStyleKey);
        }

        // Rendering

        /// <summary>
        /// Shows the combined ASLM settings category while hiding module-specific content.
        /// </summary>
        private void RenderAslmCategory()
        {
            ApplyAslmDraftsToControls();
            _settingMappings.Clear();
            ResetRenderedControlReferences();
            PrepareCategorySurface(showAslmContainer: true, showModuleContainer: true, showEmptyState: false);
            UserProfileSection.IsVisible = false;
            PortsSection.IsVisible = true;

            var section = CreateModuleSectionBorder();
            var content = new VerticalStackLayout { Spacing = 8 };

            content.Children.Add(CreateSubGroupHeader("API"));
            AddAslmApiSettings(content);

            content.Children.Add(CreateSubGroupHeader("Consoles"));
            AddConsoleSettings(content);

            section.Content = content;
            ModuleSettingsContainer.Children.Add(section);
        }

        /// <summary>
        /// Shows the account category while hiding module-specific content.
        /// </summary>
        private void RenderAccountCategory()
        {
            ApplyAslmDraftsToControls();
            _settingMappings.Clear();
            ResetRenderedControlReferences();
            PrepareCategorySurface(showAslmContainer: true, showModuleContainer: false, showEmptyState: false);
            UserProfileSection.IsVisible = true;
            PortsSection.IsVisible = false;
        }

        /// <summary>
        /// Shows the dedicated updates category.
        /// </summary>
        private void RenderUpdatesCategory()
        {
            _settingMappings.Clear();
            ResetRenderedControlReferences();
            PrepareCategorySurface(showAslmContainer: false, showModuleContainer: true, showEmptyState: false);

            var section = CreateModuleSectionBorder();
            var content = new VerticalStackLayout { Spacing = 8 };
            AddUpdateSettings(content);

            section.Content = content;
            ModuleSettingsContainer.Children.Add(section);
        }

        /// <summary>
        /// Adds the local ASLM API setting rows to the combined ASLM category.
        /// </summary>
        private void AddAslmApiSettings(Layout content)
        {
            _apiServerToggle = CreateCompactToggle(_apiServerEnabledDraft);
            _apiServerToggle.Toggled += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "API server",
                "Start the local mirror server with ASLM.",
                _apiServerToggle.View));
        }

        /// <summary>
        /// Adds the console page setting rows to the combined ASLM category.
        /// </summary>
        private void AddConsoleSettings(Layout content)
        {
            _consoleSidebarToggle = CreateCompactToggle(_consoleDraft.SidebarVisible);
            _consoleSidebarToggle.Toggled += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "Consoles page",
                "Show the built-in consoles page in the sidebar.",
                _consoleSidebarToggle.View));

            _consoleIndividualToggle = CreateCompactToggle(_consoleDraft.ShowIndividualModuleConsoles);
            _consoleIndividualToggle.Toggled += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "Individual consoles",
                "Show per-process consoles alongside unified module output.",
                _consoleIndividualToggle.View));

            _consoleCompletedToggle = CreateCompactToggle(_consoleDraft.ShowCompletedProcesses);
            _consoleCompletedToggle.Toggled += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "Completed process consoles",
                "Keep finished process consoles visible when individual consoles are enabled.",
                _consoleCompletedToggle.View));
        }

        /// <summary>
        /// Adds update setting rows to the combined ASLM category.
        /// </summary>
        private void AddUpdateSettings(Layout content)
        {
            _checkUpdatesToggle = CreateCompactToggle(_updateDraft.CheckEnabled);
            _checkUpdatesToggle.Toggled += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "Check for updates",
                "Allow ASLM to query configured GitHub repositories.",
                _checkUpdatesToggle.View));

            _autoUpdatesToggle = CreateCompactToggle(_updateDraft.AutoUpdateEnabled);
            _autoUpdatesToggle.Toggled += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "Install updates automatically",
                "Automatically apply module updates and prepare ASLM builds for the next restart.",
                _autoUpdatesToggle.View));

            _updatePeriodEntry = CreateTextEntry(_updateDraft.AutoCheckPeriodHours);
            _updatePeriodEntry.Keyboard = Keyboard.Numeric;
            _updatePeriodEntry.TextChanged += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "Auto-check period",
                "Hours between background update checks.",
                CreateFieldContainer(_updatePeriodEntry)));

            _appUpdateChannelPicker = CreatePicker(null, 13);
            _appUpdateChannelPicker.ItemsSource = new List<string> { "release", "pre-release" };
            _appUpdateChannelPicker.SelectedItem = _updateDraft.AppChannel;
            _appUpdateChannelPicker.SelectedIndexChanged += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "ASLM release channel",
                "Release uses stable GitHub releases only. Pre-release also accepts preview releases.",
                CreateUpdatePickerContainer(_appUpdateChannelPicker)));

            _moduleUpdateModePicker = CreatePicker(null, 13);
            _moduleUpdateModePicker.ItemsSource = new List<string> { "release", "branch" };
            _moduleUpdateModePicker.SelectedItem = _updateDraft.ModuleDefaultMode;
            _moduleUpdateModePicker.SelectedIndexChanged += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "Default module update mode",
                "New module preferences start from this mode; individual modules can override it.",
                CreateUpdatePickerContainer(_moduleUpdateModePicker)));

            _moduleUpdateChannelPicker = CreatePicker(null, 13);
            _moduleUpdateChannelPicker.ItemsSource = new List<string> { "release", "pre-release" };
            _moduleUpdateChannelPicker.SelectedItem = _updateDraft.ModuleDefaultChannel;
            _moduleUpdateChannelPicker.SelectedIndexChanged += (_, _) => QueueActionButtonUpdate();
            content.Children.Add(CreateUpdateCard(
                "Default module release channel",
                "Used by modules that follow GitHub releases.",
                CreateUpdatePickerContainer(_moduleUpdateChannelPicker)));

            content.Children.Add(CreateManualUpdateCard());
        }

        /// <summary>
        /// Rebuilds the Ollama settings page using the current persistent draft values.
        /// </summary>
        private void RenderOllamaCategory()
        {
            _settingMappings.Clear();
            ResetRenderedControlReferences();
            PrepareCategorySurface(showAslmContainer: false, showModuleContainer: true, showEmptyState: false);

            var section = CreateModuleSectionBorder();
            var content = new VerticalStackLayout { Spacing = 8 };

            content.Children.Add(CreateOllamaAccountCard());

            section.Content = content;
            ModuleSettingsContainer.Children.Add(section);

            UpdateOllamaAccountActionControls();
            StartOllamaMetadataRefresh();
        }

        /// <summary>
        /// Creates one update setting card with a title, description, and trailing control.
        /// </summary>
        private static Border CreateUpdateCard(string title, string description, View control)
        {
            var card = CreateSettingCardBorder();
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8,
                MinimumHeightRequest = 32
            };

            var text = new VerticalStackLayout { Spacing = TitleDescriptionSpacing };
            text.Children.Add(CreateCardTitle(title));
            text.Children.Add(CreateCardDescription(description));

            grid.Children.Add(text);
            Grid.SetColumn(text, 0);

            control.HorizontalOptions = LayoutOptions.End;
            control.VerticalOptions = LayoutOptions.Center;
            grid.Children.Add(control);
            Grid.SetColumn(control, 1);

            card.Content = grid;
            return card;
        }

        /// <summary>
        /// Creates the manual update action card.
        /// </summary>
        private Border CreateManualUpdateCard()
        {
            var card = CreateSettingCardBorder();
            var content = new VerticalStackLayout { Spacing = TitleDescriptionSpacing };

            content.Children.Add(CreateCardTitle("Manual check"));
            content.Children.Add(CreateCardDescription($"Current ASLM version: {_updateManager.CurrentAppVersion}"));

            _updateStatusLabel = CreateSecondaryLabel(_updateManager.HasPendingAppUpdate
                ? "ASLM update is prepared and will be applied on restart."
                : "No update check has been run in this session.");

            var actions = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
            var checkButton = CreateInlineActionButton("Check now", OnCheckUpdatesNowClicked);
            _prepareAppUpdateButton = CreateInlineActionButton("Prepare ASLM update", OnPrepareAppUpdateClicked);
            _prepareAppUpdateButton.IsVisible = false;
            _restartAppUpdateButton = CreateInlineActionButton("Restart now", OnRestartNowClicked);
            _restartAppUpdateButton.IsVisible = _updateManager.HasPendingAppUpdate;
            actions.Children.Add(checkButton);
            actions.Children.Add(_prepareAppUpdateButton);
            actions.Children.Add(_restartAppUpdateButton);

            content.Children.Add(actions);
            content.Children.Add(_updateStatusLabel);
            card.Content = content;
            return card;
        }

        /// <summary>
        /// Runs a manual update check and exposes app self-update preparation when available.
        /// </summary>
        private async void OnCheckUpdatesNowClicked(object? sender, EventArgs e)
        {
            if (sender is Button button)
            {
                button.IsEnabled = false;
            }

            try
            {
                _pendingAppUpdateCandidate = null;
                if (_prepareAppUpdateButton != null)
                {
                    _prepareAppUpdateButton.IsVisible = false;
                }

                if (_restartAppUpdateButton != null)
                {
                    _restartAppUpdateButton.IsVisible = _updateManager.HasPendingAppUpdate;
                }

                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.Text = "Checking GitHub repositories...";
                }

                var updates = await Task.Run(() => _updateManager.CheckAllUpdatesAsync());
                _pendingAppUpdateCandidate = updates.FirstOrDefault(update =>
                    string.Equals(update.TargetKind, "app", StringComparison.OrdinalIgnoreCase));

                if (_prepareAppUpdateButton != null)
                {
                    _prepareAppUpdateButton.IsVisible = _pendingAppUpdateCandidate != null && !_updateManager.HasPendingAppUpdate;
                }

                if (_restartAppUpdateButton != null)
                {
                    _restartAppUpdateButton.IsVisible = _updateManager.HasPendingAppUpdate;
                }

                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.Text = updates.Count == 0
                        ? "Everything is up to date."
                        : $"{updates.Count} update(s) available. Module updates can be applied from the Modules page.";
                }
            }
            catch (Exception ex)
            {
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.Text = $"Update check failed: {ex.Message}";
                }
            }
            finally
            {
                if (sender is Button senderButton)
                {
                    senderButton.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Downloads an available ASLM build and writes the pending update manifest for the patcher.
        /// </summary>
        private async void OnPrepareAppUpdateClicked(object? sender, EventArgs e)
        {
            if (_pendingAppUpdateCandidate == null)
            {
                return;
            }

            if (sender is Button prepareButton)
            {
                prepareButton.IsEnabled = false;
            }

            try
            {
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.Text = "Downloading ASLM build...";
                }

                var log = new Progress<string>(message =>
                {
                    if (_updateStatusLabel != null)
                    {
                        MainThread.BeginInvokeOnMainThread(() => _updateStatusLabel.Text = message);
                    }
                });

                var success = await Task.Run(() => _updateManager.PrepareAppUpdateAsync(_pendingAppUpdateCandidate, log));
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.Text = success
                        ? "ASLM update prepared. Restart ASLM to apply it."
                        : "ASLM update could not be prepared.";
                }

                if (_prepareAppUpdateButton != null)
                {
                    _prepareAppUpdateButton.IsVisible = !success;
                }

                if (_restartAppUpdateButton != null)
                {
                    _restartAppUpdateButton.IsVisible = success || _updateManager.HasPendingAppUpdate;
                }
            }
            finally
            {
                if (sender is Button senderButton)
                {
                    senderButton.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Restarts through the launcher so the prepared ASLM update can be applied by the patcher.
        /// </summary>
        private async void OnRestartNowClicked(object? sender, EventArgs e)
        {
            if (sender is Button restartButton)
            {
                restartButton.IsEnabled = false;
            }

            try
            {
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.Text = "Restarting ASLM...";
                }

                await _settingsService.StopAllModulesAsync();
                await Task.Run(SettingsService.StartLauncherForSelfUpdate);
                Application.Current?.Quit();
            }
            catch (Exception ex)
            {
                if (_updateStatusLabel != null)
                {
                    _updateStatusLabel.Text = $"Restart failed: {ex.Message}";
                }

                if (sender is Button failedButton)
                {
                    failedButton.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Builds the account card shown at the top of the Ollama category.
        /// </summary>
        private Border CreateOllamaAccountCard()
        {
            var card = CreateSettingCardBorder();
            var row = new Grid
            {
                ColumnSpacing = 16,
                VerticalOptions = LayoutOptions.Center,
                MinimumHeightRequest = 44
            };
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var text = new VerticalStackLayout
            {
                Spacing = TitleDescriptionSpacing,
                VerticalOptions = LayoutOptions.Center
            };

            var title = CreateCardTitle("Ollama Account");
            title.VerticalOptions = LayoutOptions.Center;
            text.Children.Add(title);

            _ollamaAccountStatusLabel = CreateSecondaryLabel("Not signed in");
            _ollamaAccountStatusLabel.LineBreakMode = LineBreakMode.TailTruncation;
            _ollamaAccountStatusLabel.MaxLines = 1;
            text.Children.Add(_ollamaAccountStatusLabel);

            row.Children.Add(text);
            Grid.SetColumn(text, 0);

            _ollamaAccountButton = CreateInlineActionButton("Sign In", OnOllamaAccountButtonClicked);
            row.Children.Add(_ollamaAccountButton);
            Grid.SetColumn(_ollamaAccountButton, 1);

            card.Content = row;
            return card;
        }

        /// <summary>
        /// Rebuilds the currently visible module settings page from the in-memory draft state.
        /// </summary>
        private void RenderModuleCategory(ModuleConfig module)
        {
            _settingMappings.Clear();
            ResetRenderedControlReferences();
            PrepareCategorySurface(showAslmContainer: false, showModuleContainer: true, showEmptyState: false);

            var settings = module.Settings?.Where(SettingsService.ShouldDisplaySetting).ToList() ?? [];
            var loadedSettings = settings
                .Select(setting => new LoadedSetting(setting, _settingsService.GetCurrentSettingValue(module, setting)))
                .ToList();

            var valuesByKey = loadedSettings.ToDictionary(item => item.Setting.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            var visibleSettings = loadedSettings
                .Where(item => SettingsService.ShouldRenderSetting(item.Setting, settings, valuesByKey))
                .ToList();

            if (visibleSettings.Count == 0)
            {
                ShowEmptyCategory("This module has no settings in the current state.");
                return;
            }

            EmptyCategoryState.IsVisible = false;

            var section = CreateModuleSectionBorder();
            var content = new VerticalStackLayout { Spacing = 8 };

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
            ResetRenderedControlReferences();
            PrepareCategorySurface(showAslmContainer: false, showModuleContainer: false, showEmptyState: true);
            EmptyCategoryLabel.Text = message;
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
            SyncBuiltInDraftValuesFromControls();
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
        /// Copies visible built-in ASLM controls into the cross-category draft values.
        /// </summary>
        private void SyncBuiltInDraftValuesFromControls()
        {
            if (_apiServerToggle != null)
            {
                _apiServerEnabledDraft = _apiServerToggle.IsToggled;
            }

            _consoleDraft = GetCurrentConsoleDraft();
            _updateDraft = GetCurrentUpdateDraft();
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
            SyncDraftValuesFromControls();

            if (_activeCategory == null || !HasAnyUnsavedChanges())
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
            LoadOllamaDraftsFromService();
            await LoadModuleDraftsAsync(reloadModules: false, reloadRuntimeValues: true);
            _categories = _settingsService.CreateOrderedCategories(_loadedModules);

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
                SettingsCategoryKind.Aslm => HasUnsavedAslmSettingsChanges(),
                SettingsCategoryKind.AslmProfile => HasUnsavedAccountChanges(),
                SettingsCategoryKind.Updates => HasUnsavedUpdateChanges(),
                SettingsCategoryKind.Ollama => false,
                SettingsCategoryKind.Module => HasUnsavedModuleChanges(),
                _ => false
            };
        }

        /// <summary>
        /// Determines whether any loaded settings category has pending unsaved edits.
        /// </summary>
        private bool HasAnyUnsavedChanges() =>
            HasUnsavedAccountChanges() ||
            HasUnsavedAslmSettingsChanges() ||
            _loadedModules.Any(m => _settingsService.ModuleHasChangesComparedToBaseline(m, _settingBaselines));

        /// <summary>
        /// Determines whether the account display name differs from the saved baseline.
        /// </summary>
        private bool HasUnsavedAccountChanges()
        {
            var userName = UserProfileSection.IsVisible
                ? UsernameEntry.Text?.Trim() ?? string.Empty
                : _userNameDraft;
            return SettingsService.HasUnsavedAccountChanges(userName, _aslmBaseline);
        }

        /// <summary>
        /// Determines whether any combined ASLM setting differs from the saved baseline.
        /// </summary>
        private bool HasUnsavedAslmSettingsChanges() =>
            SettingsService.HasUnsavedAslmSettingsChanges(
                GetCurrentOfficialPortDraft(),
                GetCurrentThirdPartyPortDraft(),
                _apiServerToggle?.IsToggled ?? _apiServerEnabledDraft,
                GetCurrentConsoleDraft(),
                GetCurrentUpdateDraft(),
                _aslmBaseline,
                _consoleBaseline,
                _updateBaseline);

        private string GetCurrentOfficialPortDraft() =>
            PortsSection.IsVisible
                ? OfficialPortEntry.Text?.Trim() ?? string.Empty
                : _officialPortDraft;

        private string GetCurrentThirdPartyPortDraft() =>
            PortsSection.IsVisible
                ? ThirdPartyPortEntry.Text?.Trim() ?? string.Empty
                : _thirdPartyPortDraft;

        /// <summary>
        /// Determines whether the port draft differs from the saved baseline.
        /// </summary>
        private bool HasUnsavedPortChanges()
        {
            return SettingsService.HasUnsavedPortChanges(
                GetCurrentOfficialPortDraft(),
                GetCurrentThirdPartyPortDraft(),
                _aslmBaseline);
        }

        /// <summary>
        /// Determines whether the ASLM API setting differs from the last saved baseline.
        /// </summary>
        private bool HasUnsavedAslmApiChanges() =>
            SettingsService.HasUnsavedApiServerChanges(_apiServerToggle?.IsToggled ?? _apiServerEnabledDraft, _aslmBaseline);

        /// <summary>
        /// Determines whether console preferences differ from the last saved baseline.
        /// </summary>
        private bool HasUnsavedConsoleChanges() =>
            SettingsService.HasUnsavedConsoleChanges(GetCurrentConsoleDraft(), _consoleBaseline);

        /// <summary>
        /// Determines whether the visible module editors differ from the last saved baseline.
        /// </summary>
        private bool HasUnsavedModuleChanges()
        {
            foreach (var mapping in _settingMappings)
            {
                var useCustom = mapping.ReadCustomValue?.Invoke() ?? mapping.Setting.UseCustomValue;
                var currentValue = mapping.Setting.IsAutomaticallyManaged && !useCustom
                    ? _settingsService.GetResolvedSettingValue(mapping.Module, mapping.Setting)
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
        /// Determines whether update controls differ from the last saved baseline.
        /// </summary>
        private bool HasUnsavedUpdateChanges()
        {
            var updateDraft = GetCurrentUpdateDraft();
            return SettingsService.HasUnsavedUpdateChanges(updateDraft, _updateBaseline);
        }

        /// <summary>
        /// Gets the latest console draft, reading visible controls when present.
        /// </summary>
        private ConsoleBaseline GetCurrentConsoleDraft() =>
            _consoleSidebarToggle != null &&
            _consoleCompletedToggle != null &&
            _consoleIndividualToggle != null
                ? new ConsoleBaseline(
                    _consoleSidebarToggle.IsToggled,
                    _consoleCompletedToggle.IsToggled,
                    _consoleIndividualToggle.IsToggled)
                : _consoleDraft;

        /// <summary>
        /// Gets the latest update draft, reading visible controls when present.
        /// </summary>
        private UpdateBaseline GetCurrentUpdateDraft() =>
            _checkUpdatesToggle != null &&
            _autoUpdatesToggle != null &&
            _updatePeriodEntry != null &&
            _appUpdateChannelPicker != null &&
            _moduleUpdateModePicker != null &&
            _moduleUpdateChannelPicker != null
                ? new UpdateBaseline(
                    _checkUpdatesToggle.IsToggled,
                    _autoUpdatesToggle.IsToggled,
                    _updatePeriodEntry.Text?.Trim() ?? string.Empty,
                    _appUpdateChannelPicker.SelectedItem?.ToString() ?? "release",
                    _moduleUpdateModePicker.SelectedItem?.ToString() ?? "release",
                    _moduleUpdateChannelPicker.SelectedItem?.ToString() ?? "release")
                : _updateDraft;

        /// <summary>
        /// Pushes default update preferences into the visible update controls.
        /// </summary>
        private void ApplyUpdateDefaultsToControls()
        {
            _updateDraft = SettingsService.BuildDefaultUpdateBaseline();

            if (_checkUpdatesToggle != null)
            {
                _checkUpdatesToggle.IsToggled = _updateDraft.CheckEnabled;
            }

            if (_autoUpdatesToggle != null)
            {
                _autoUpdatesToggle.IsToggled = _updateDraft.AutoUpdateEnabled;
            }

            if (_updatePeriodEntry != null)
            {
                _updatePeriodEntry.Text = _updateDraft.AutoCheckPeriodHours;
            }

            if (_appUpdateChannelPicker != null)
            {
                _appUpdateChannelPicker.SelectedItem = _updateDraft.AppChannel;
            }

            if (_moduleUpdateModePicker != null)
            {
                _moduleUpdateModePicker.SelectedItem = _updateDraft.ModuleDefaultMode;
            }

            if (_moduleUpdateChannelPicker != null)
            {
                _moduleUpdateChannelPicker.SelectedItem = _updateDraft.ModuleDefaultChannel;
            }
        }

        /// <summary>
        /// Shows a confirmation dialog on the current shell page.
        /// </summary>
        private static Task<bool> ShowAlertAsync(string title, string message, string accept, string cancel) =>
            Application.Current?.Windows.Count > 0
                ? Application.Current.Windows[0].Page!.DisplayAlertAsync(title, message, accept, cancel)
                : Task.FromResult(false);

        /// <summary>
        /// Finds one of the keyed XAML styles used by dynamically rendered settings controls.
        /// </summary>
        private static Style? GetStyleResource(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Style style
                ? style
                : null;

        /// <summary>
        /// Finds one of the shared color resources with a defensive fallback.
        /// </summary>
        private static Color GetColorResource(string key, Color fallback) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
                ? color
                : fallback;

        /// <summary>
        /// Shows a simple informational dialog on the current shell page.
        /// </summary>
        private Task ShowSuccessAsync(string message)
        {
            _notifications.PublishSystemToast(
                "Settings saved",
                message,
                "Saved",
                "settings-save");
            return Task.CompletedTask;
        }

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
                case SettingsCategoryKind.Aslm:
                    var aslmDefaults = SettingsService.BuildDefaultAslmDrafts();
                    _officialPortDraft = aslmDefaults.OfficialPort;
                    _thirdPartyPortDraft = aslmDefaults.ThirdPartyPort;
                    _apiServerEnabledDraft = aslmDefaults.ApiServerEnabled;
                    _consoleDraft = aslmDefaults.ConsoleDefaults;
                    PortErrorLabel.IsVisible = false;
                    RenderAslmCategory();
                    break;
                case SettingsCategoryKind.AslmProfile:
                    _userNameDraft = Environment.UserName;
                    RenderAccountCategory();
                    break;
                case SettingsCategoryKind.Updates:
                    ApplyUpdateDefaultsToControls();
                    RenderUpdatesCategory();
                    break;
                case SettingsCategoryKind.Ollama:
                    break;
                case SettingsCategoryKind.Module:
                    SettingsService.ResetModuleToDefaults(_activeCategory.Module!);
                    RenderModuleCategory(_activeCategory.Module!);
                    break;
            }

            SyncDraftValuesFromControls();

            await SettingsScroll.ScrollToAsync(0, 0, false);
            UpdateActionButtons();
        }

        /// <summary>
        /// Reverts all pending settings drafts back to the last persisted values.
        /// </summary>
        private async void OnDiscardChangesClicked(object? sender, EventArgs e)
        {
            if (_activeCategory == null || _isSaving)
            {
                return;
            }

            await DiscardUnsavedChangesAsync();
            await SettingsScroll.ScrollToAsync(0, 0, false);
        }

        /// <summary>
        /// Reloads saved values and rebuilds the active category after an explicit discard.
        /// </summary>
        private async Task DiscardUnsavedChangesAsync()
        {
            var activeCategoryId = _activeCategory?.Id;

            LoadAslmDraftsFromAppData();
            LoadOllamaDraftsFromService();
            await LoadModuleDraftsAsync(reloadModules: false, reloadRuntimeValues: true);

            _categories = _settingsService.CreateOrderedCategories(_loadedModules);
            BuildCategorySelectors();

            var targetCategory = ResolveCategory(activeCategoryId) ?? _categories.FirstOrDefault();
            if (targetCategory == null)
            {
                _activeCategory = null;
                ShowEmptyCategory("No settings are available.");
                UpdateActionButtons();
                return;
            }

            ActivateCategory(targetCategory);
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
                SyncDraftValuesFromControls();
                UpdateSelectorButtonStates();
                UpdateActionButtons();

                if (!SettingsService.TryValidateDisplayName(_userNameDraft, out var validatedUserName, out var displayNameErrorMessage))
                {
                    await ShowErrorAsync(displayNameErrorMessage);
                    return;
                }
                _userNameDraft = validatedUserName;

                var portResult = SettingsService.TryParsePorts(_officialPortDraft, _thirdPartyPortDraft);
                if (!portResult.Success)
                {
                    if (_activeCategory.Kind == SettingsCategoryKind.Aslm)
                    {
                        ShowPortError(portResult.ErrorMessage);
                    }
                    else
                    {
                        await ShowErrorAsync(portResult.ErrorMessage);
                    }

                    return;
                }

                _updateDraft = GetCurrentUpdateDraft();
                if (!SettingsService.TryValidateAndBuildUpdateSettings(_updateDraft, out var nextSettings, out var updateErrorMessage))
                {
                    await ShowErrorAsync(updateErrorMessage);
                    return;
                }

                foreach (var module in _loadedModules)
                {
                    if (!_settingsService.TryValidateModuleSettings(module, out var moduleErrorMessage))
                    {
                        await ShowErrorAsync(moduleErrorMessage);
                        return;
                    }
                }

                var hadAppRestartChanges = HasUnsavedAslmSettingsChanges();
                var hadAslmChanges = HasUnsavedAccountChanges() || hadAppRestartChanges;
                var modulesWithChanges = GetModulesWithUnsavedChanges();

                SettingsService.ApplyDraftsToAppData(
                    _appData,
                    _userNameDraft,
                    portResult.OfficialPort,
                    portResult.ThirdPartyPort,
                    _consoleDraft,
                    nextSettings);
                await _appData.SaveAsync();

                if (_apiServerEnabledDraft != _aslmBaseline.ApiServerEnabled)
                {
                    await _apiServer.SetEnabledAsync(_apiServerEnabledDraft);
                }

                _aslmBaseline = new AslmBaseline(
                    _userNameDraft,
                    _officialPortDraft,
                    _thirdPartyPortDraft,
                    _apiServer.IsEnabled);
                _apiServerEnabledDraft = _apiServer.IsEnabled;
                _consoleBaseline = _consoleDraft;
                _updateBaseline = SettingsService.BuildAslmDraftSnapshot(_appData, _apiServer.IsEnabled).UpdateBaseline;
                _updateDraft = _updateBaseline;
                PortErrorLabel.IsVisible = false;

                var touchedModules = new HashSet<ModuleConfig>();
                var deferredSettings = new List<string>();
                foreach (var module in modulesWithChanges)
                {
                    var moduleSaveResult = await _settingsService.SaveActiveModuleAsync(module, _settingBaselines);
                    touchedModules.UnionWith(moduleSaveResult.TouchedModules);
                    deferredSettings.AddRange(moduleSaveResult.DeferredSettings);
                }

                foreach (var module in touchedModules)
                {
                    await ReloadModuleRuntimeValuesAsync(module);
                }

                var activeCategoryId = _activeCategory.Id;
                _categories = _settingsService.CreateOrderedCategories(_loadedModules);
                BuildCategorySelectors();
                var resolvedCategory = ResolveCategory(activeCategoryId);
                if (resolvedCategory != null)
                {
                    ActivateCategory(resolvedCategory);
                }

                var successMessage = SettingsService.BuildSaveMessage(hadAslmChanges, touchedModules.Count > 0, deferredSettings);
                if (restartAfterSave)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        await ShowSuccessAsync(successMessage);
                    });
                }
                else
                {
                    await ShowSuccessAsync(successMessage);
                }

                if (restartAfterSave && await RestartChangedTargetsAsync(hadAppRestartChanges, touchedModules))
                {
                    return;
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
        /// Restarts the changed app-level target or changed module targets when supported.
        /// </summary>
        private async Task<bool> RestartChangedTargetsAsync(bool restartApp, IEnumerable<ModuleConfig> changedModules)
        {
            if (restartApp)
            {
                await RestartApplicationAsync();
                return true;
            }

            foreach (var module in changedModules.Where(CanRestartModule))
            {
                await _settingsService.RestartModuleAsync(module);
            }

            return false;
        }

        /// <summary>
        /// Restarts the application startup chain so app-level changes take effect.
        /// </summary>
        private async Task RestartApplicationAsync()
        {
            await _settingsService.StopAllModulesAsync();

            if (Application.Current is not App application || application.Windows.Count == 0)
            {
                return;
            }

            var newPage = application.CreateStartupPage();
            var window = application.Windows[0];
            window.Page = newPage;
        }

        /// <summary>
        /// Handles the single Ollama account action button click.
        /// </summary>
        private async void OnOllamaAccountButtonClicked(object? sender, EventArgs e)
        {
            await ExecuteOllamaAccountActionAsync(signIn: !IsOllamaSignedIn());
        }

        /// <summary>
        /// Runs one Ollama account action and refreshes the compact account button state.
        /// </summary>
        private async Task ExecuteOllamaAccountActionAsync(bool signIn)
        {
            if (_isOllamaAccountActionRunning)
            {
                return;
            }

            StopOllamaStatusPolling();

            if (!signIn)
            {
                var confirmed = await ShowAlertAsync(
                    "Sign out from Ollama",
                    "This removes the current ollama.com session for this Windows profile. Continue?",
                    "Sign out",
                    "Cancel");

                if (!confirmed)
                {
                    return;
                }
            }

            try
            {
                _isOllamaAccountActionRunning = true;
                _ollamaAccountAction = signIn ? "signin" : "signout";
                UpdateOllamaAccountActionControls();

                var result = signIn
                    ? await _ollamaSettings.SignInAsync()
                    : await _ollamaSettings.SignOutAsync();

                await RefreshOllamaRuntimeMetadataAsync(queryLiveStatus: signIn);
                UpdateOllamaAccountActionControls();

                if (!result.Success)
                {
                    await ShowErrorAsync(result.Message);
                    return;
                }

                if (signIn && result.IsPendingVerification && !IsOllamaSignedIn())
                {
                    StartOllamaStatusPolling();
                }
            }
            finally
            {
                _isOllamaAccountActionRunning = false;
                _ollamaAccountAction = string.Empty;
                UpdateOllamaAccountActionControls();
            }
        }

        /// <summary>
        /// Refreshes the non-editable Ollama metadata without overwriting unsaved field edits.
        /// </summary>
        private async Task RefreshOllamaRuntimeMetadataAsync(bool queryLiveStatus, CancellationToken ct = default)
        {
            try
            {
                var refreshed = queryLiveStatus
                    ? await Task.Run(() => _ollamaSettings.RefreshSettingsAsync(ct), ct)
                    : await Task.Run(() => _ollamaSettings.LoadSettings(), ct);
                ApplyOllamaRuntimeMetadata(refreshed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to refresh Ollama settings: {ex.Message}");
                ApplyOllamaRuntimeMetadata(new OllamaPersistentSettings());
            }
        }

        /// <summary>
        /// Copies the latest Ollama metadata into the visible UI draft.
        /// </summary>
        private void ApplyOllamaRuntimeMetadata(OllamaPersistentSettings refreshed)
        {
            _ollamaDraft.IsCliAvailable = refreshed.IsCliAvailable;
            _ollamaDraft.IsSignedIn = refreshed.IsSignedIn;
            _ollamaDraft.UserName = refreshed.UserName;
        }

        /// <summary>
        /// Updates the current account status labels and action buttons when the Ollama card is visible.
        /// </summary>
        private void UpdateOllamaAccountActionControls()
        {
            if (_ollamaAccountStatusLabel != null)
            {
                _ollamaAccountStatusLabel.Text = BuildOllamaAccountStatusText();
            }

            if (_ollamaAccountButton != null)
            {
                var isSignedIn = IsOllamaSignedIn();
                _ollamaAccountButton.Text =
                    _isOllamaAccountActionRunning && string.Equals(_ollamaAccountAction, "signin", StringComparison.Ordinal) ? "Signing In..." :
                    _isOllamaAccountActionRunning && string.Equals(_ollamaAccountAction, "signout", StringComparison.Ordinal) ? "Signing Out..." :
                    isSignedIn ? "Sign Out" : "Sign In";
                _ollamaAccountButton.IsEnabled = _ollamaDraft.IsCliAvailable &&
                    !_isOllamaAccountActionRunning &&
                    !_isOllamaMetadataRefreshRunning;
                ApplyOllamaAccountButtonState(_ollamaAccountButton, isSignedIn);
            }
        }

        /// <summary>
        /// Starts a background refresh for the live Ollama account state when the account page is visible.
        /// </summary>
        private void StartOllamaMetadataRefresh()
        {
            if (_activeCategory?.Kind != SettingsCategoryKind.Ollama)
            {
                return;
            }

            StopOllamaMetadataRefresh();

            if (!_ollamaDraft.IsCliAvailable)
            {
                UpdateOllamaAccountActionControls();
                return;
            }

            var refreshCts = new CancellationTokenSource();
            _ollamaMetadataRefreshCts = refreshCts;
            _isOllamaMetadataRefreshRunning = true;
            UpdateOllamaAccountActionControls();

            _ = RefreshOllamaMetadataAsync(refreshCts);
        }

        /// <summary>
        /// Stops the in-flight live Ollama metadata refresh, if any.
        /// </summary>
        private void StopOllamaMetadataRefresh()
        {
            var refreshCts = _ollamaMetadataRefreshCts;
            _ollamaMetadataRefreshCts = null;
            _isOllamaMetadataRefreshRunning = false;
            refreshCts?.Cancel();
            refreshCts?.Dispose();
            UpdateOllamaAccountActionControls();
        }

        /// <summary>
        /// Refreshes the live Ollama metadata without blocking the initial settings page render.
        /// </summary>
        private async Task RefreshOllamaMetadataAsync(CancellationTokenSource refreshCts)
        {
            try
            {
                await RefreshOllamaRuntimeMetadataAsync(queryLiveStatus: true, refreshCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!ReferenceEquals(_ollamaMetadataRefreshCts, refreshCts))
                    {
                        return;
                    }

                    refreshCts.Dispose();
                    _ollamaMetadataRefreshCts = null;
                    _isOllamaMetadataRefreshRunning = false;
                    UpdateOllamaAccountActionControls();
                });
            }
        }

        /// <summary>
        /// Clears the current references to dynamically created Ollama controls before the page is rebuilt.
        /// </summary>
        private void ResetOllamaControlReferences()
        {
            _ollamaAccountButton = null;
            _ollamaAccountStatusLabel = null;
        }

        /// <summary>
        /// Clears all dynamic control references before rebuilding one category UI tree.
        /// </summary>
        private void ResetRenderedControlReferences()
        {
            ResetOllamaControlReferences();
            ResetUpdateControlReferences();
            ResetAslmApiControlReferences();
        }

        /// <summary>
        /// Applies baseline visibility and cleanup for one category render pass.
        /// </summary>
        private void PrepareCategorySurface(bool showAslmContainer, bool showModuleContainer, bool showEmptyState)
        {
            AslmSettingsContainer.IsVisible = showAslmContainer;
            ModuleSettingsContainer.IsVisible = showModuleContainer;
            EmptyCategoryState.IsVisible = showEmptyState;
            ModuleSettingsContainer.Children.Clear();
        }

        /// <summary>
        /// Clears references to dynamically created update controls before the page is rebuilt.
        /// </summary>
        private void ResetUpdateControlReferences()
        {
            _checkUpdatesToggle = null;
            _autoUpdatesToggle = null;
            _updatePeriodEntry = null;
            _appUpdateChannelPicker = null;
            _moduleUpdateModePicker = null;
            _moduleUpdateChannelPicker = null;
            _updateStatusLabel = null;
            _prepareAppUpdateButton = null;
            _restartAppUpdateButton = null;
            _pendingAppUpdateCandidate = null;
        }

        /// <summary>
        /// Clears the dynamically created built-in ASLM setting controls before the page is rebuilt.
        /// </summary>
        private void ResetAslmApiControlReferences()
        {
            _apiServerToggle = null;
            _consoleSidebarToggle = null;
            _consoleCompletedToggle = null;
            _consoleIndividualToggle = null;
        }

        /// <summary>
        /// Determines whether the current Ollama account state should be treated as signed in.
        /// </summary>
        private bool IsOllamaSignedIn() => _ollamaDraft.IsSignedIn;

        /// <summary>
        /// Starts a short-lived background poll that waits for the browser sign-in flow to complete.
        /// </summary>
        private void StartOllamaStatusPolling()
        {
            StopOllamaStatusPolling();

            var pollingCts = new CancellationTokenSource();
            _ollamaStatusPollingCts = pollingCts;
            UpdateOllamaAccountActionControls();

            _ = PollOllamaStatusAsync(pollingCts);
        }

        /// <summary>
        /// Cancels the active background sign-in status poll, if any.
        /// </summary>
        private void StopOllamaStatusPolling()
        {
            var pollingCts = _ollamaStatusPollingCts;
            _ollamaStatusPollingCts = null;
            pollingCts?.Cancel();
            pollingCts?.Dispose();
            UpdateOllamaAccountActionControls();
        }

        /// <summary>
        /// Polls the local Ollama API until sign-in completes or the timeout window expires.
        /// </summary>
        private async Task PollOllamaStatusAsync(CancellationTokenSource pollingCts)
        {
            var ct = pollingCts.Token;
            var deadline = DateTime.UtcNow + OllamaSignInPollDuration;

            try
            {
                while (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
                {
                    await RefreshOllamaRuntimeMetadataAsync(queryLiveStatus: true, ct);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        UpdateOllamaAccountActionControls();
                    });

                    if (IsOllamaSignedIn())
                    {
                        return;
                    }

                    await Task.Delay(OllamaSignInPollInterval, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!ReferenceEquals(_ollamaStatusPollingCts, pollingCts))
                    {
                        return;
                    }

                    pollingCts.Dispose();
                    _ollamaStatusPollingCts = null;
                    UpdateOllamaAccountActionControls();
                });
            }
        }

        /// <summary>
        /// Returns the compact status line shown under the Ollama account title.
        /// </summary>
        private string BuildOllamaAccountStatusText()
        {
            if (!_ollamaDraft.IsCliAvailable)
            {
                return "Internal Ollama is not installed";
            }

            if (_isOllamaAccountActionRunning && string.Equals(_ollamaAccountAction, "signin", StringComparison.Ordinal))
            {
                return "Waiting for browser sign-in";
            }

            if (_isOllamaAccountActionRunning && string.Equals(_ollamaAccountAction, "signout", StringComparison.Ordinal))
            {
                return "Signing out";
            }

            if (_isOllamaMetadataRefreshRunning)
            {
                return "Checking account";
            }

            if (_ollamaStatusPollingCts != null && !_ollamaDraft.IsSignedIn)
            {
                return "Waiting for browser sign-in";
            }

            if (_ollamaDraft.IsSignedIn)
            {
                return string.IsNullOrWhiteSpace(_ollamaDraft.UserName)
                    ? "Signed in"
                    : $"Signed in as {_ollamaDraft.UserName}";
            }

            return "Not signed in";
        }

        /// <summary>
        /// Displays the current port validation error.
        /// </summary>
        private void ShowPortError(string message)
        {
            PortErrorLabel.Text = message;
            PortErrorLabel.IsVisible = true;
        }

        // Editor creation

        /// <summary>
        /// Builds one setting card and its control mapping when the setting is editable.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateSettingCard(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var card = CreateSettingCardBorder();
            var description = SettingsService.BuildSettingDescription(setting);

            if (_settingsService.IsAutoDetectedAslmEngine(setting))
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                var text = new VerticalStackLayout { Spacing = TitleDescriptionSpacing };
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
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

                var text = new VerticalStackLayout { Spacing = TitleDescriptionSpacing };
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

            var content = new VerticalStackLayout { Spacing = TitleDescriptionSpacing };
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

            if (SettingsService.IsActiveEngineSelector(setting))
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
            var baseline = _settingsService.GetSettingBaseline(module, setting, value, _settingBaselines);
            var toggle = CreateCompactToggle(value is bool boolValue
                ? boolValue
                : bool.TryParse(setting.FormatValueForDisplay(value), out var parsedBool) && parsedBool);
            toggle.View.HorizontalOptions = LayoutOptions.End;

            if (SettingsService.HasDependentSettings(module, setting))
            {
                toggle.Toggled += (_, _) =>
                {
                    setting.Value = toggle.IsToggled;
                    _ = RefreshDynamicVisibilityAsync();
                };
            }

            toggle.Toggled += (_, _) => QueueActionButtonUpdate();

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
            var baseline = _settingsService.GetSettingBaseline(module, setting, value, _settingBaselines);
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
            picker.SelectedIndexChanged += (_, _) => QueueActionButtonUpdate();

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
            var baseline = _settingsService.GetSettingBaseline(module, setting, value, _settingBaselines);
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
            picker.SelectedIndexChanged += (_, _) => QueueActionButtonUpdate();

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
            var baseline = _settingsService.GetSettingBaseline(module, setting, value, _settingBaselines);
            var (field, entry) = CreateTextField(setting.FormatValueForDisplay(value));
            entry.Keyboard = Keyboard.Numeric;
            entry.TextChanged += (_, _) => QueueActionButtonUpdate();

            return (field, new SettingControlMapping(module, setting, () => entry.Text, null, baseline.DisplayValue, baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates a plain text entry for free-form string settings.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateTextEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = _settingsService.GetSettingBaseline(module, setting, value, _settingBaselines);
            var (field, entry) = CreateTextField(setting.FormatValueForDisplay(value));
            entry.TextChanged += (_, _) => QueueActionButtonUpdate();
            return (field, new SettingControlMapping(module, setting, () => entry.Text, null, baseline.DisplayValue, baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates a password entry and its mapping while preserving the shared baseline snapshot.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreatePasswordEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var baseline = _settingsService.GetSettingBaseline(module, setting, value, _settingBaselines);
            var (field, entry) = CreatePasswordField(setting.FormatValueForDisplay(value));
            entry.TextChanged += (_, _) => QueueActionButtonUpdate();
            return (field, new SettingControlMapping(module, setting, () => entry.Text, null, baseline.DisplayValue, baseline.UseCustomValue));
        }

        /// <summary>
        /// Creates the editor used for ASLM-managed settings that can optionally switch to custom values.
        /// </summary>
        private (View Control, SettingControlMapping? Mapping) CreateManagedEditor(ModuleConfig module, ModuleSetting setting, object? value)
        {
            var autoValue = _settingsService.GetResolvedSettingValue(module, setting);
            var initialDisplayValue = setting.UseCustomValue
                ? setting.FormatValueForDisplay(value)
                : setting.FormatValueForDisplay(autoValue);

            var isPasswordSetting = string.Equals(setting.NormalizedType, "password", StringComparison.OrdinalIgnoreCase);
            var entryView = isPasswordSetting
                ? CreatePasswordField(initialDisplayValue)
                : CreateTextField(initialDisplayValue);
            var baseline = _settingsService.GetSettingBaseline(module, setting, initialDisplayValue, _settingBaselines);
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

                QueueActionButtonUpdate();
            };

            customToggle.Toggled += (_, args) =>
            {
                entry.Text = args.Value
                    ? (string.IsNullOrWhiteSpace(lastCustomValue) ? setting.FormatValueForDisplay(setting.Value ?? value) : lastCustomValue)
                    : setting.FormatValueForDisplay(_settingsService.GetResolvedSettingValue(module, setting));

                ApplyTextEntryState(entry, !args.Value);
                QueueActionButtonUpdate();
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
                Style = GetStyleResource(TransparentBorderStyleKey)
            };

            return border;
        }

        /// <summary>
        /// Creates the card container used for one individual setting.
        /// </summary>
        private static Border CreateSettingCardBorder()
        {
            var border = new Border
            {
                Style = GetStyleResource(TransparentBorderStyleKey)
            };

            return border;
        }

        /// <summary>
        /// Creates a compact sub-group header label used to group related settings
        /// within one category (e.g. "Ports", "API", "Consoles", "Updates").
        /// </summary>
        private static Label CreateSubGroupHeader(string text)
        {
            var label = new Label
            {
                Text = text,
                Style = GetStyleResource(SubGroupHeaderLabelStyleKey)
            };
            return label;
        }

        /// <summary>
        /// Creates the primary label for a setting name.
        /// </summary>
        private static Label CreateCardTitle(string text)
        {
            var label = new Label
            {
                Text = text,
                Style = GetStyleResource(CardTitleLabelStyleKey)
            };
            return label;
        }

        /// <summary>
        /// Creates the secondary label used for setting descriptions.
        /// </summary>
        private static Label CreateCardDescription(string text)
        {
            var label = new Label
            {
                Text = text,
                Style = GetStyleResource(CardDescriptionLabelStyleKey)
            };
            return label;
        }

        /// <summary>
        /// Creates the compact secondary label used for inline helper rows.
        /// </summary>
        private static Label CreateSecondaryLabel(string text)
        {
            var label = new Label
            {
                Text = text,
                Style = GetStyleResource(SecondaryLabelStyleKey)
            };
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
                ColumnSpacing = 8,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Fill,
                MinimumHeightRequest = 22,
                Margin = new Thickness(0, 4, 0, 0)
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
        /// Creates a compact secondary action button used inside the Ollama account card.
        /// </summary>
        private static Button CreateInlineActionButton(string text, EventHandler clicked)
        {
            var button = new Button
            {
                Text = text,
                Style = GetStyleResource(InlineActionButtonStyleKey)
            };

            button.Clicked += clicked;
            return button;
        }

        /// <summary>
        /// Applies the Ollama account button color for the current sign-in state.
        /// </summary>
        private static void ApplyOllamaAccountButtonState(Button button, bool isSignedIn)
        {
            button.BackgroundColor = isSignedIn
                ? Color.FromArgb("#FF453A")
                : Color.FromArgb("#0A84FF");
            button.TextColor = Colors.White;
            button.BorderWidth = 0;
            button.Opacity = button.IsEnabled ? 1.0 : 0.55;
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
                Style = GetStyleResource(PickerStyleKey),
                HorizontalOptions = LayoutOptions.Fill
            };

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
                Style = GetStyleResource(FieldBorderStyleKey),
                Content = picker
            };

            return border;
        }

        /// <summary>
        /// Creates the compact fixed-width picker shell used by the Updates category.
        /// </summary>
        private static Border CreateUpdatePickerContainer(Picker picker)
        {
            var border = CreatePickerContainer(picker);
            border.WidthRequest = 132;
            border.MinimumWidthRequest = 132;
            return border;
        }

        /// <summary>
        /// Creates a bordered field shell shared by text inputs and password editors.
        /// </summary>
        private static Border CreateFieldContainer(View content, Thickness? padding = null)
        {
            var border = new Border
            {
                Style = GetStyleResource(FieldBorderStyleKey),
                Content = content
            };

            if (padding != null)
            {
                border.Padding = padding.Value;
            }

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

                    if (!isSidebar)
                    {
                        viewer.VerticalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Hidden;
                        viewer.HorizontalScrollBarVisibility = Microsoft.UI.Xaml.Controls.ScrollBarVisibility.Hidden;
                    }

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
            var hiddenOpacity = isSidebar ? (double?)null : 0;

            foreach (var scrollBar in FindDescendants<Microsoft.UI.Xaml.Controls.Primitives.ScrollBar>(viewer))
            {
                if (!isSidebar)
                {
                    scrollBar.Opacity = 0;
                    scrollBar.Width = 0;
                    scrollBar.MinWidth = 0;
                    scrollBar.Height = 0;
                    scrollBar.MinHeight = 0;
                }

                if (scrollBar.Orientation == Microsoft.UI.Xaml.Controls.Orientation.Vertical)
                {
                    scrollBar.HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Right;
                    scrollBar.Margin = new Microsoft.UI.Xaml.Thickness(0, 4, 0, 4);
                    scrollBar.Padding = new Microsoft.UI.Xaml.Thickness(0);
                    scrollBar.Background = transparentBrush;
                    scrollBar.Foreground = thumbBrush;
                    scrollBar.Opacity = hiddenOpacity ?? 0.18;
                }
                else
                {
                    scrollBar.Height = isSidebar ? 3 : 0;
                    scrollBar.MinHeight = isSidebar ? 3 : 0;
                    scrollBar.Background = transparentBrush;
                    scrollBar.Opacity = hiddenOpacity ?? 0.2;
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
            entry.Margin = new Thickness(12, 0, 36, 0);
            var toggleIcon = new Image
            {
                Source = PasswordIconHidden,
                Style = GetStyleResource(PasswordToggleImageStyleKey)
            };

            var toggleButton = new Border
            {
                WidthRequest = 32,
                HeightRequest = 32,
                MinimumWidthRequest = 32,
                MinimumHeightRequest = 32,
                Padding = 0,
                Margin = new Thickness(0, 0, 2, 0),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Center,
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                Content = toggleIcon
            };

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (_, _) =>
            {
                entry.IsPassword = !entry.IsPassword;
                UpdatePasswordToggleIcon(toggleIcon, entry.IsPassword);
            };
            toggleButton.GestureRecognizers.Add(tapGesture);

            var grid = new Grid { MinimumHeightRequest = 36 };
            grid.Children.Add(entry);
            grid.Children.Add(toggleButton);
            toggleButton.ZIndex = 1;
            return (CreateFieldContainer(grid, new Thickness(0)), entry);
        }

        /// <summary>
        /// Updates the password visibility icon to reflect the current hidden or visible state.
        /// </summary>
        private static void UpdatePasswordToggleIcon(Image toggleIcon, bool isPasswordHidden)
        {
            toggleIcon.Source = isPasswordHidden
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
                ClearButtonVisibility = clearButtonVisibility,
                IsPassword = isPassword,
                Style = GetStyleResource(TextEntryStyleKey)
            };

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

    }
}
