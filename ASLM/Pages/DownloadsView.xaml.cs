// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    // Download overlay view
    // Drive the shared download catalog dialog
    public partial class DownloadsView : ContentView, INotifyPropertyChanged
    {
        private const double DialogWidthFactor = 0.88;
        private const double DialogHeightFactor = 0.84;
        private const double MinDialogWidth = 1080;
        private const double MinDialogHeight = 620;
        private const double MaxDialogWidth = 1520;
        private const double MaxDialogHeight = 920;

        // Download item colors read from the active theme palette so they update with theme changes.
        private static Color ActiveSurfaceColor => GetColorResource("DownloadActiveSurface", Color.FromArgb("#202733"));
        private static Color ActiveBorderColor => GetColorResource("DownloadActiveBorder", Color.FromArgb("#2C7CF6"));
        private static Color PassiveSurfaceColor => GetColorResource("DownloadPassiveSurface", Color.FromArgb("#1F1F22"));
        private static Color PassiveListSurfaceColor => GetColorResource("DownloadPassiveListSurface", Color.FromArgb("#1B1B1E"));
        private static Color PassiveBorderColor => GetColorResource("DownloadPassiveBorder", Color.FromArgb("#343438"));
        private static Color ActiveTextColor => GetColorResource("LabelPrimary", Colors.White);
        private static Color InactiveTextColor => GetColorResource("LabelPrimary", Color.FromArgb("#E6E6EA"));
        private static Color SecondaryTextColor => GetColorResource("LabelSecondary", Color.FromArgb("#99EBEBF5"));
        private static Color ActiveSubtitleColor => GetColorResource("DownloadActiveSubtitle", Color.FromArgb("#E7F1FF"));
        private readonly DownloadCatalog _catalog;
        private readonly DownloadInstaller _installer;
        private CancellationTokenSource? _catalogRefreshCts;
        private CancellationTokenSource? _detailRefreshCts;
        private CancellationTokenSource? _searchDebounceCts;

        private bool _hasLoaded;
        private bool _isBusy;
        private bool _isInstalling;
        private string _statusText = "Ready.";
        private string _itemListEmptyMessage = "No grouped downloads are available for this category.";
        private string _detailEmptyMessage = "Choose a resource family to inspect its variants and details.";
        private string _searchText = string.Empty;
        private readonly HashSet<string> _selectedFilterKeys = new(StringComparer.OrdinalIgnoreCase);
        private string _lastCatalogSignature = string.Empty;
        private string _lastDetailSignature = string.Empty;
        private List<DownloadCatalogItem> _categoryItems = [];
        private DownloadCategoryViewModel? _activeCategory;
        private DownloadListItemViewModel? _selectedItem;
        private DownloadCatalogItemDetail? _selectedItemDetail;
        private DownloadVariantViewModel? _selectedVariant;
        private DownloadInfoBlockViewModel? _selectedInfoBlock;
        private WebViewSource? _selectedInfoBlockSource;
        private double _selectedInfoBlockWebHeight = 720;
#if WINDOWS
        private Microsoft.UI.Xaml.Controls.WebView2? _nativeInfoBlockWebView;
        private Microsoft.UI.Xaml.Controls.ScrollViewer? _nativeDetailScrollViewer;
        private double _detailScrollTargetY;
#endif

        // Initialization
        // Build the download overlay and wire its core events
        public DownloadsView(DownloadCatalog catalog, DownloadInstaller installer)
        {
            _catalog = catalog;
            _installer = installer;

            InitializeComponent();
            BindingContext = this;

            RefreshCommand = new Command(async () => await ManualRefreshAsync());
            InstallCommand = new Command(async () => await InstallSelectedVariantAsync());
            OpenVariantCommand = new Command(async () => await OpenSelectedVariantAsync());

            DetailScrollView.HandlerChanged += OnDetailScrollViewHandlerChanged;
            Loaded += OnLoaded;
            SizeChanged += (_, _) => UpdateDialogSize();
        }

        public event EventHandler? CloseRequested;
        public new event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<DownloadCategoryViewModel> Categories { get; } = new();
        public ObservableCollection<DownloadFilterViewModel> Filters { get; } = new();
        public ObservableCollection<DownloadListItemViewModel> CurrentItems { get; } = new();
        public ObservableCollection<DownloadVariantViewModel> Variants { get; } = new();
        public ObservableCollection<DownloadInfoBlockViewModel> InfoBlocks { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand InstallCommand { get; }
        public ICommand OpenVariantCommand { get; }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        public bool IsInstalling
        {
            get => _isInstalling;
            private set
            {
                if (_isInstalling == value) return;
                _isInstalling = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowInstallButton));
                OnPropertyChanged(nameof(ShowDeleteButton));
                OnPropertyChanged(nameof(ShowSelectedVariantInstalledBadge));
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (string.Equals(_statusText, value, StringComparison.Ordinal)) return;
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public string CategoryCountLabel => Categories.Count == 0 ? "No categories" : $"{Categories.Count} categor{(Categories.Count == 1 ? "y" : "ies")}";
        public string ActiveCategoryTitle => _activeCategory?.Title ?? "Catalog";
        public string ActiveCategoryDescription => _activeCategory?.Description ?? string.Empty;
        public bool HasActiveCategoryDescription => !string.IsNullOrWhiteSpace(ActiveCategoryDescription);
        public string ActiveCategoryItemCountLabel => _activeCategory == null ? "No items" : $"{CurrentItems.Count} famil{(CurrentItems.Count == 1 ? "y" : "ies")}";
        public bool HasFilters => Filters.Count > 0;
        public bool HasCurrentItems => CurrentItems.Count > 0;
        public bool IsItemListEmptyVisible => Categories.Count > 0 && !HasCurrentItems && !IsBusy;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (string.Equals(_searchText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string ItemListEmptyMessage
        {
            get => _itemListEmptyMessage;
            private set
            {
                if (string.Equals(_itemListEmptyMessage, value, StringComparison.Ordinal)) return;
                _itemListEmptyMessage = value;
                OnPropertyChanged();
            }
        }

        public string DetailHeaderTitle => "Download Catalog";
        public string DetailHeaderSubtitle => "Choose a family, then the exact variant you want.";
        public bool HasSelectedItem => _selectedItem != null;
        public bool IsDetailEmptyVisible => !HasSelectedItem && !IsBusy;

        public string DetailEmptyMessage
        {
            get => _detailEmptyMessage;
            private set
            {
                if (string.Equals(_detailEmptyMessage, value, StringComparison.Ordinal)) return;
                _detailEmptyMessage = value;
                OnPropertyChanged();
            }
        }

        public string SelectedItemTitle => _selectedItemDetail?.Title ?? _selectedItem?.Title ?? string.Empty;
        public string SelectedItemSummary => _selectedItemDetail?.Summary ?? _selectedItem?.Summary ?? string.Empty;
        public bool HasSelectedItemSummary => !string.IsNullOrWhiteSpace(SelectedItemSummary);
        public string SelectedItemMetadataLine => BuildItemMetadataLine();
        public bool HasSelectedItemMetadata => !string.IsNullOrWhiteSpace(SelectedItemMetadataLine);
        public string SelectedItemTagsLine => BuildItemTagsLine();
        public bool HasSelectedItemTags => !string.IsNullOrWhiteSpace(SelectedItemTagsLine);
        public bool HasVariants => Variants.Count > 0;
        public bool HasSelectedVariant => _selectedVariant != null;
        public string SelectedVariantTitle => _selectedVariant?.Title ?? string.Empty;
        public string SelectedVariantSummary => _selectedVariant?.Summary ?? string.Empty;
        public bool HasSelectedVariantSummary => !string.IsNullOrWhiteSpace(SelectedVariantSummary);
        public string SelectedVariantMetadataLine => _selectedVariant?.MetadataLine ?? string.Empty;
        public bool HasSelectedVariantMetadata => !string.IsNullOrWhiteSpace(SelectedVariantMetadataLine);
        public string SelectedVariantTagsLine => _selectedVariant?.TagsLine ?? string.Empty;
        public bool HasSelectedVariantTags => !string.IsNullOrWhiteSpace(SelectedVariantTagsLine);
        public bool ShowInstallButton => _selectedVariant != null && !_selectedVariant.Variant.Installed && !IsInstalling;
        public bool ShowDeleteButton => _selectedVariant?.Variant.Installed == true && !IsInstalling;
        public bool ShowSelectedVariantInstalledBadge => _selectedVariant?.Variant.Installed == true && !IsInstalling;
        public string SelectedVariantInstalledLabel => _selectedVariant?.InstalledLabel ?? "Installed";
        public bool ShowOpenVariantButton => !string.IsNullOrWhiteSpace(GetSelectedVariantHomepageUrl());
        public bool HasInfoBlocks => InfoBlocks.Count > 0;
        public bool HasMultipleInfoBlocks => InfoBlocks.Count > 1;
        public bool HasSelectedInfoBlock => _selectedInfoBlock != null;
        public string SelectedInfoBlockTitle => _selectedInfoBlock?.Title ?? "Details";
        public string SelectedInfoBlockText => _selectedInfoBlock?.RenderedContent ?? string.Empty;
        public bool HasSelectedInfoBlockText => !string.IsNullOrWhiteSpace(SelectedInfoBlockText);
        public bool HasSelectedInfoBlockWebContent => SelectedInfoBlockSource != null;
        public WebViewSource? SelectedInfoBlockSource
        {
            get => _selectedInfoBlockSource;
            private set
            {
                if (ReferenceEquals(_selectedInfoBlockSource, value)) return;
                _selectedInfoBlockSource = value;
                OnPropertyChanged();
            }
        }

        public double SelectedInfoBlockWebHeight
        {
            get => _selectedInfoBlockWebHeight;
            private set
            {
                var clamped = Math.Max(520, value);
                if (Math.Abs(_selectedInfoBlockWebHeight - clamped) < 0.5)
                {
                    return;
                }

                _selectedInfoBlockWebHeight = clamped;
                OnPropertyChanged();
            }
        }

        public double VariantSelectorHeight
        {
            get
            {
                var visibleRows = Math.Clamp(Variants.Count, 1, 5);
                return 12 + (visibleRows * 52);
            }
        }

        // Property notifications
        // Raise the local property change event
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Overlay opening
        // Open the dialog with cached data first, then refresh in the background
        public async Task OpenAsync()
        {
            UpdateDialogSize();
            await LoadCatalogAsync(preferCached: true, forceRefresh: false, preserveSelection: true, showBusyIndicator: true, silentRefresh: false);
            _ = LoadCatalogAsync(preferCached: true, forceRefresh: true, preserveSelection: true, showBusyIndicator: false, silentRefresh: true);
        }

        // Initial layout setup
        // Initialize size and native scroll bindings once
        private void OnLoaded(object? sender, EventArgs e)
        {
            if (_hasLoaded) return;
            _hasLoaded = true;
            UpdateDialogSize();
            OnDetailScrollViewHandlerChanged(DetailScrollView, EventArgs.Empty);
        }

        // Catalog refresh
        // Run a user-triggered catalog refresh
        private async Task ManualRefreshAsync()
        {
            await LoadCatalogAsync(preferCached: true, forceRefresh: true, preserveSelection: true, showBusyIndicator: true, silentRefresh: false);
        }

        // Load catalog snapshot
        // Refresh the current catalog query and preserve selection when possible
        private async Task LoadCatalogAsync(
            bool preferCached,
            bool forceRefresh,
            bool preserveSelection,
            bool showBusyIndicator,
            bool silentRefresh)
        {
            // Replace the previous refresh token so stale requests stop updating the UI
            var catalogCts = ReplaceCancellationTokenSource(ref _catalogRefreshCts);
            var ct = catalogCts.Token;

            var previousCategoryKey = preserveSelection ? _activeCategory?.Category.GroupKey : null;
            var previousItemKey = preserveSelection ? _selectedItem?.Item.ResourceKey : null;
            var previousVariantKey = preserveSelection ? _selectedVariant?.Variant.ResourceKey : null;

            if (showBusyIndicator)
            {
                IsBusy = true;
            }

            if (!silentRefresh)
            {
                StatusText = forceRefresh ? "Refreshing shared download catalog..." : "Loading shared download catalog...";
            }

            try
            {
                // Query the aggregated catalog with the current search and filter state
                var queryText = SearchText;
                var selectedFilters = GetSelectedFilterKeys();
                var snapshot = await Task.Run(
                    () => _catalog.LoadCatalogAsync(
                        queryText: queryText,
                        filters: selectedFilters,
                        preferCached: preferCached,
                        forceRefresh: forceRefresh,
                        ct: ct),
                    ct);
                if (ct.IsCancellationRequested) return;

                var snapshotSignature = ComputeCatalogSignature(snapshot);
                if (silentRefresh && string.Equals(_lastCatalogSignature, snapshotSignature, StringComparison.Ordinal))
                {
                    return;
                }

                // Silent refreshes only update the UI when the snapshot actually changed
                ApplySnapshot(snapshot, previousCategoryKey, previousItemKey);
                _lastCatalogSignature = snapshotSignature;

                ItemListEmptyMessage = snapshot.Warnings.Count > 0
                    ? string.Join(" ", snapshot.Warnings)
                    : BuildEmptyItemListMessage();
                DetailEmptyMessage = snapshot.Categories.Count == 0
                    ? "No installed module currently exposes a downloads bridge."
                    : "Choose a resource family to inspect its variants and details.";

                if (_selectedItem != null)
                {
                    await LoadSelectedItemDetailAsync(preferCached, forceRefresh, previousVariantKey, silentRefresh);
                }
                else
                {
                    ClearDetail();
                }

                if (!silentRefresh)
                {
                    StatusText = snapshot.Categories.Count == 0
                        ? "No shared downloads were published by the current modules."
                        : forceRefresh
                            ? $"Catalog updated. {snapshot.Categories.Count} categor{(snapshot.Categories.Count == 1 ? "y" : "ies")} loaded."
                            : $"Loaded {snapshot.Categories.Count} categor{(snapshot.Categories.Count == 1 ? "y" : "ies")}.";
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!silentRefresh)
                {
                    StatusText = $"Download catalog refresh failed: {ex.Message}";
                    ItemListEmptyMessage = "The shared catalog could not be loaded from the installed module bridges.";
                }
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                {
                    if (showBusyIndicator)
                    {
                        IsBusy = false;
                    }

                    RaiseLayoutProperties();
                }
            }
        }

        // Apply catalog snapshot
        // Rebuild categories and reactivate the closest previous selection
        private void ApplySnapshot(DownloadCatalogSnapshot snapshot, string? selectedCategoryKey, string? selectedItemKey)
        {
            Categories.Clear();
            foreach (var category in snapshot.Categories)
            {
                Categories.Add(new DownloadCategoryViewModel(category, SelectCategoryAsync));
            }

            var targetCategory = Categories.FirstOrDefault(category => string.Equals(category.Category.GroupKey, selectedCategoryKey, StringComparison.OrdinalIgnoreCase))
                ?? Categories.FirstOrDefault();

            ActivateCategory(targetCategory, selectedItemKey);
            RaiseLayoutProperties();
        }

        // Activate category
        // Swap the active category and rebuild the list panel
        private void ActivateCategory(DownloadCategoryViewModel? category, string? selectedItemKey)
        {
            _activeCategory = category;
            foreach (var viewModel in Categories)
            {
                viewModel.IsSelected = ReferenceEquals(viewModel, category);
            }

            _categoryItems = category?.Category.Items.ToList() ?? [];
            BuildAvailableFilters();
            ApplyCurrentItemFilters(selectedItemKey);
            RaiseCategoryProperties();
        }

        // Build provider filters
        // Rehydrate filter state from the active category payload
        private void BuildAvailableFilters()
        {
            var availableFilters = _activeCategory?.Category.Filters
                .Where(static filter => !string.IsNullOrWhiteSpace(filter.Key))
                .OrderBy(filter => filter.SortOrder)
                .ThenBy(filter => filter.Title, StringComparer.OrdinalIgnoreCase)
                .ToList()
                ?? [];

            var availableKeys = availableFilters
                .Select(filter => filter.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _selectedFilterKeys.RemoveWhere(key => !availableKeys.Contains(key));

            if (_selectedFilterKeys.Count == 0)
            {
                foreach (var filter in availableFilters.Where(filter => filter.Selected))
                {
                    _selectedFilterKeys.Add(filter.Key);
                }
            }

            Filters.Clear();
            foreach (var filter in availableFilters)
            {
                Filters.Add(new DownloadFilterViewModel(filter.Key, filter.Title, filter.Kind, ToggleFilter)
                {
                    IsSelected = _selectedFilterKeys.Contains(filter.Key)
                });
            }

            OnPropertyChanged(nameof(HasFilters));
        }

        // Apply item list state
        // Rebuild the visible item list for the current category
        private void ApplyCurrentItemFilters(string? selectedItemKey)
        {
            var items = _categoryItems
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            CurrentItems.Clear();
            foreach (var item in items)
            {
                CurrentItems.Add(new DownloadListItemViewModel(item, SelectItemAsync));
            }

            var targetItem = CurrentItems.FirstOrDefault(item => string.Equals(item.Item.ResourceKey, selectedItemKey, StringComparison.OrdinalIgnoreCase))
                ?? CurrentItems.FirstOrDefault();

            SetSelectedItem(targetItem);
            RaiseCategoryProperties();
        }

        // Toggle provider filter
        // Keep sort filters single-select and capability filters multi-select
        private async Task ToggleFilter(DownloadFilterViewModel filter)
        {
            if (string.Equals(filter.Kind, "sort", StringComparison.OrdinalIgnoreCase))
            {
                _selectedFilterKeys.RemoveWhere(key =>
                    Filters.Any(viewModel =>
                        string.Equals(viewModel.Kind, "sort", StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(viewModel.Key, key, StringComparison.OrdinalIgnoreCase)));

                _selectedFilterKeys.Add(filter.Key);
            }
            else if (_selectedFilterKeys.Contains(filter.Key))
            {
                _selectedFilterKeys.Remove(filter.Key);
            }
            else
            {
                _selectedFilterKeys.Add(filter.Key);
            }

            UpdateFilterSelectionStates();
            await RefreshCurrentQueryAsync();
        }

        // Refresh filter selection state
        // Mirror the backing filter set into the view models
        private void UpdateFilterSelectionStates()
        {
            foreach (var viewModel in Filters)
            {
                viewModel.IsSelected = _selectedFilterKeys.Contains(viewModel.Key);
            }
        }

        // Collect active filters
        // Return a stable list of selected filter keys
        private IReadOnlyCollection<string> GetSelectedFilterKeys()
        {
            return _selectedFilterKeys
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Build empty list message
        // Return the most useful empty-state message for the current query
        private string BuildEmptyItemListMessage()
        {
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                return $"No results matched \"{SearchText.Trim()}\" on the provider side.";
            }

            if (HasExplicitProviderFilterSelection())
            {
                return "No grouped downloads matched the current provider filters.";
            }

            return "No grouped downloads are available for this category.";
        }

        // Detect explicit filters
        // Ignore the default sort-only state when building empty-state text
        private bool HasExplicitProviderFilterSelection()
        {
            return _selectedFilterKeys.Any(key => !string.Equals(key, "sort:popular", StringComparison.OrdinalIgnoreCase));
        }

        // Refresh current query
        // Re-run the active provider query without showing a busy overlay
        private async Task RefreshCurrentQueryAsync()
        {
            await LoadCatalogAsync(
                preferCached: false,
                forceRefresh: true,
                preserveSelection: true,
                showBusyIndicator: false,
                silentRefresh: true);
        }

        // Debounce remote search
        // Delay provider-side search refresh until the user pauses typing
        private void ScheduleRemoteSearchRefresh()
        {
            var debounceCts = ReplaceCancellationTokenSource(ref _searchDebounceCts);
            var ct = debounceCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(350, ct);
                    ct.ThrowIfCancellationRequested();

                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        await RefreshCurrentQueryAsync();
                    });
                }
                catch (OperationCanceledException)
                {
                }
            }, ct);
        }

        // Select category
        // Activate the category and refresh its selected item details
        private async Task SelectCategoryAsync(DownloadCategoryViewModel category)
        {
            if (ReferenceEquals(_activeCategory, category)) return;

            ActivateCategory(category, selectedItemKey: null);

            if (_selectedItem != null)
            {
                await LoadSelectedItemDetailAsync(preferCached: true, forceRefresh: false, selectedVariantKey: null, silentRefresh: false);
                _ = LoadSelectedItemDetailAsync(preferCached: true, forceRefresh: true, selectedVariantKey: _selectedVariant?.Variant.ResourceKey, silentRefresh: true);
            }
            else
            {
                ClearDetail();
            }
        }

        // Select item
        // Load cached details first, then refresh them in the background
        private async Task SelectItemAsync(DownloadListItemViewModel item)
        {
            if (ReferenceEquals(_selectedItem, item) && _selectedItemDetail != null) return;

            SetSelectedItem(item);
            await LoadSelectedItemDetailAsync(preferCached: true, forceRefresh: false, selectedVariantKey: null, silentRefresh: false);
            _ = LoadSelectedItemDetailAsync(preferCached: true, forceRefresh: true, selectedVariantKey: _selectedVariant?.Variant.ResourceKey, silentRefresh: true);
        }

        // Load item detail
        // Refresh the selected item detail while guarding against stale responses
        private async Task LoadSelectedItemDetailAsync(bool preferCached, bool forceRefresh, string? selectedVariantKey, bool silentRefresh)
        {
            if (_selectedItem == null)
            {
                ClearDetail();
                return;
            }

            var item = _selectedItem.Item;
            var itemResourceKey = item.ResourceKey;
            var detailCts = ReplaceCancellationTokenSource(ref _detailRefreshCts);
            var ct = detailCts.Token;

            if (forceRefresh && !silentRefresh)
            {
                StatusText = $"Refreshing details for {item.Title}...";
            }

            try
            {
                // Ignore late responses when the user has already moved to another item
                var detail = await Task.Run(
                    () => _catalog.GetItemDetailAsync(item, preferCached, forceRefresh, ct),
                    ct);
                if (ct.IsCancellationRequested || _selectedItem?.Item.ResourceKey != itemResourceKey) return;

                var detailSignature = ComputeDetailSignature(detail);
                if (silentRefresh && string.Equals(_lastDetailSignature, detailSignature, StringComparison.Ordinal))
                {
                    return;
                }

                ApplyDetail(detail, selectedVariantKey);
                _lastDetailSignature = detailSignature;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested && !silentRefresh)
                {
                    StatusText = $"Details could not be loaded for {item.Title}: {ex.Message}";
                }
            }
        }

        // Apply item detail
        // Rebuild variants and info blocks for the selected item
        private void ApplyDetail(DownloadCatalogItemDetail detail, string? selectedVariantKey)
        {
            _selectedItemDetail = detail;
            var previousBlockKey = _selectedInfoBlock?.Block.Id;

            Variants.Clear();
            foreach (var variant in detail.Variants)
            {
                Variants.Add(new DownloadVariantViewModel(variant, SelectVariant));
            }

            InfoBlocks.Clear();
            foreach (var block in detail.Blocks)
            {
                InfoBlocks.Add(new DownloadInfoBlockViewModel(block, detail.HomepageUrl, SelectInfoBlock));
            }

            var targetVariant = Variants.FirstOrDefault(variant => string.Equals(variant.Variant.ResourceKey, selectedVariantKey, StringComparison.OrdinalIgnoreCase))
                ?? Variants.FirstOrDefault(variant => string.Equals(variant.Variant.ResourceKey, detail.DefaultVariantResourceKey, StringComparison.OrdinalIgnoreCase))
                ?? Variants.FirstOrDefault();

            SetSelectedVariant(targetVariant);
            var targetBlock = InfoBlocks.FirstOrDefault(block => string.Equals(block.Block.Id, previousBlockKey, StringComparison.OrdinalIgnoreCase))
                ?? InfoBlocks.FirstOrDefault();
            SetSelectedInfoBlock(targetBlock);
            RaiseDetailProperties();
        }

        // Clear item detail
        // Reset all detail-side state when no item is selected
        private void ClearDetail()
        {
            _selectedItemDetail = null;
            _lastDetailSignature = string.Empty;
            Variants.Clear();
            InfoBlocks.Clear();
            SetSelectedVariant(null);
            SetSelectedInfoBlock(null);
            RaiseDetailProperties();
        }

        // Set selected item
        // Update list selection and preserve current detail when the resource did not change
        private void SetSelectedItem(DownloadListItemViewModel? item)
        {
            var preserveExistingDetail = item != null &&
                _selectedItemDetail != null &&
                string.Equals(_selectedItem?.Item.ResourceKey, item.Item.ResourceKey, StringComparison.OrdinalIgnoreCase);

            _selectedItem = item;
            foreach (var viewModel in CurrentItems)
            {
                viewModel.IsSelected = ReferenceEquals(viewModel, item);
            }

            if (item == null)
            {
                ClearDetail();
                return;
            }

            if (preserveExistingDetail)
            {
                RaiseDetailProperties();
                return;
            }

            _selectedItemDetail = null;
            Variants.Clear();
            InfoBlocks.Clear();
            SetSelectedVariant(null);
            SetSelectedInfoBlock(null);
            RaiseDetailProperties();
        }

        // Select variant
        // Route selector clicks into the shared variant setter
        private void SelectVariant(DownloadVariantViewModel? variant)
        {
            SetSelectedVariant(variant);
        }

        // Set selected variant
        // Update variant selection and dependent property state
        private void SetSelectedVariant(DownloadVariantViewModel? variant)
        {
            _selectedVariant = variant;
            foreach (var viewModel in Variants)
            {
                viewModel.IsSelected = ReferenceEquals(viewModel, variant);
            }

            RaiseVariantProperties();
        }

        // Select info block
        // Route tab clicks into the shared info block setter
        private Task SelectInfoBlock(DownloadInfoBlockViewModel? block)
        {
            SetSelectedInfoBlock(block);
            return Task.CompletedTask;
        }

        // Set selected info block
        // Rebuild the preview source and reset its measured height
        private void SetSelectedInfoBlock(DownloadInfoBlockViewModel? block)
        {
            _selectedInfoBlock = block;
            foreach (var viewModel in InfoBlocks)
            {
                viewModel.IsSelected = ReferenceEquals(viewModel, block);
            }

            SelectedInfoBlockWebHeight = 720;
            SelectedInfoBlockSource = BuildInfoBlockSource(block);
#if WINDOWS
            _detailScrollTargetY = DetailScrollView?.ScrollY ?? 0;
#endif
            RaiseInfoBlockProperties();
        }

        // Install selected variant
        // Run the install flow and then refresh the catalog state
        private async Task InstallSelectedVariantAsync()
        {
            if (_selectedItem == null || _selectedVariant == null || IsInstalling) return;

            IsInstalling = true;
            StatusText = $"Installing {_selectedVariant.Title}...";

            var progress = new Progress<string>(message => MainThread.BeginInvokeOnMainThread(() => StatusText = message));

            try
            {
                var item = _selectedItem.Item;
                var variant = _selectedVariant.Variant;
                var result = await Task.Run(() => _installer.InstallAsync(item, variant, progress));
                StatusText = result.Message;
                if (result.Success)
                {
                    await LoadCatalogAsync(preferCached: true, forceRefresh: false, preserveSelection: true, showBusyIndicator: false, silentRefresh: false);
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = $"Installation canceled for {_selectedVariant.Title}.";
            }
            catch (Exception ex)
            {
                StatusText = $"Installation failed for {_selectedVariant.Title}: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }

        // Remove selected variant
        // Run the uninstall flow and then refresh the catalog state
        private async Task DeleteSelectedVariantAsync()
        {
            if (_selectedItem == null || _selectedVariant == null || IsInstalling) return;

            IsInstalling = true;
            StatusText = $"Removing {_selectedVariant.Title}...";

            var progress = new Progress<string>(message => MainThread.BeginInvokeOnMainThread(() => StatusText = message));

            try
            {
                var item = _selectedItem.Item;
                var variant = _selectedVariant.Variant;
                var result = await Task.Run(() => _installer.UninstallAsync(item, variant, progress));
                StatusText = result.Message;
                if (result.Success)
                {
                    await LoadCatalogAsync(preferCached: true, forceRefresh: false, preserveSelection: true, showBusyIndicator: false, silentRefresh: false);
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = $"Removal canceled for {_selectedVariant.Title}.";
            }
            catch (Exception ex)
            {
                StatusText = $"Removal failed for {_selectedVariant.Title}: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }

        // Open selected variant
        // Launch the selected variant or item page in the system browser
        private async Task OpenSelectedVariantAsync()
        {
            var homepageUrl = GetSelectedVariantHomepageUrl();
            if (string.IsNullOrWhiteSpace(homepageUrl))
            {
                StatusText = "The selected variant does not expose a page to open.";
                return;
            }

            if (!Uri.TryCreate(homepageUrl, UriKind.Absolute, out var homepageUri))
            {
                StatusText = $"Open failed: invalid URL '{homepageUrl}'.";
                return;
            }

            try
            {
                await Launcher.Default.OpenAsync(homepageUri);
                StatusText = $"Opened {homepageUri.Host}{homepageUri.AbsolutePath}.";
            }
            catch (Exception ex)
            {
                StatusText = $"Could not open the selected page: {ex.Message}";
            }
        }

        // Resolve variant URL
        // Prefer the selected variant page and fall back to item-level pages
        private string GetSelectedVariantHomepageUrl()
        {
            return _selectedVariant?.Variant.HomepageUrl
                ?? _selectedItemDetail?.HomepageUrl
                ?? _selectedItem?.Item.HomepageUrl
                ?? string.Empty;
        }

        // Build info block source
        // Resolve local or remote HTML content into a WebView source
        private static WebViewSource? BuildInfoBlockSource(DownloadInfoBlockViewModel? block)
        {
            if (block == null || !block.HasContentUrl)
            {
                return null;
            }

            var contentUrl = block.ContentUrl;
            if (Uri.TryCreate(contentUrl, UriKind.Absolute, out var absoluteUri))
            {
                return new UrlWebViewSource { Url = absoluteUri.AbsoluteUri };
            }

            if (Path.IsPathRooted(contentUrl) && File.Exists(contentUrl))
            {
                return new UrlWebViewSource { Url = new Uri(contentUrl).AbsoluteUri };
            }

            if (Uri.TryCreate(block.SourceUrl, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, contentUrl, out var combinedUri))
            {
                return new UrlWebViewSource { Url = combinedUri.AbsoluteUri };
            }

            return null;
        }

        // Build item metadata line
        // Compose the compact provider and version line for the detail header
        private string BuildItemMetadataLine()
        {
            var segments = new List<string>();
            var provider = _selectedItemDetail?.Provider ?? _selectedItem?.Item.Provider;
            var version = _selectedItemDetail?.Version ?? _selectedItem?.Item.Version;
            var detail = _selectedItemDetail?.Detail ?? _selectedItem?.Item.Detail;

            if (!string.IsNullOrWhiteSpace(provider)) segments.Add(provider);
            if (!string.IsNullOrWhiteSpace(version)) segments.Add(version);
            if (!string.IsNullOrWhiteSpace(detail)) segments.Add(detail);

            return string.Join(" | ", segments);
        }

        // Build item tags line
        // Join item tags into a compact display string
        private string BuildItemTagsLine()
        {
            var tags = _selectedItemDetail?.Tags ?? _selectedItem?.Item.Tags ?? [];
            return tags.Count == 0 ? string.Empty : string.Join(" | ", tags);
        }

        // Overlay tap handling
        // Close the dialog when the backdrop is tapped
        private void OnBackgroundTapped(object? sender, EventArgs e)
        {
            RequestClose();
        }

        // Swallow dialog taps
        // Prevent backdrop close when the dialog surface is tapped
        private void OnDialogTapped(object? sender, EventArgs e)
        {
            // Swallow taps inside the dialog so they do not propagate to the tinted backdrop.
        }

        // Search entry styling
        // Match the Windows textbox chrome to the dialog design
        private void OnSearchEntryHandlerChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            if (sender is Entry entry && entry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox nativeTextBox)
            {
                nativeTextBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                nativeTextBox.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
#endif
        }

        // Refresh button handler
        // Forward the click into the async refresh flow
        private async void OnRefreshClicked(object? sender, EventArgs e)
        {
            await ManualRefreshAsync();
        }

        // Install button handler
        // Forward the click into the async install flow
        private async void OnInstallClicked(object? sender, EventArgs e)
        {
            await InstallSelectedVariantAsync();
        }

        // Delete button handler
        // Forward the click into the async uninstall flow
        private async void OnDeleteClicked(object? sender, EventArgs e)
        {
            await DeleteSelectedVariantAsync();
        }

        // Open button handler
        // Forward the click into the browser launch flow
        private async void OnOpenClicked(object? sender, EventArgs e)
        {
            await OpenSelectedVariantAsync();
        }

        // Search text handler
        // Update the local query text and debounce the remote refresh
        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            SearchText = e.NewTextValue ?? string.Empty;
            ScheduleRemoteSearchRefresh();
        }

        // Native scroll binding
        // Cache the platform scroll viewer used by the detail panel
        private void OnDetailScrollViewHandlerChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            if (sender is ScrollView scrollView && scrollView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ScrollViewer nativeScrollViewer)
            {
                _nativeDetailScrollViewer = nativeScrollViewer;
            }
#endif
        }

        // WebView binding
        // Hook native wheel handling whenever the preview WebView is recreated
        private void OnInfoBlockWebViewHandlerChanged(object? sender, EventArgs e)
        {
#if WINDOWS
            if (_nativeInfoBlockWebView != null)
            {
                _nativeInfoBlockWebView.PointerWheelChanged -= OnInfoBlockWebViewPointerWheelChanged;
                _nativeInfoBlockWebView = null;
            }

            if (sender is WebView webView && webView.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 nativeWebView)
            {
                _nativeInfoBlockWebView = nativeWebView;
                _nativeInfoBlockWebView.PointerWheelChanged += OnInfoBlockWebViewPointerWheelChanged;
            }
#endif
        }

        // WebView navigation
        // Keep external links out of the embedded preview
        private async void OnInfoBlockWebViewNavigating(object? sender, WebNavigatingEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Url))
            {
                return;
            }

            if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri))
            {
                return;
            }

            if (string.Equals(uri.Scheme, "file", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, "about", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                e.Cancel = true;

                try
                {
                    await Launcher.Default.OpenAsync(uri);
                }
                catch
                {
                }
            }
        }

#if WINDOWS
        // Native wheel forwarding
        // Redirect wheel scrolling from WebView2 into the outer detail surface
        private void OnInfoBlockWebViewPointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is not Microsoft.UI.Xaml.Controls.WebView2 nativeWebView)
            {
                return;
            }

            var wheelDelta = e.GetCurrentPoint(nativeWebView).Properties.MouseWheelDelta;
            if (wheelDelta == 0)
            {
                return;
            }

            e.Handled = true;

            var currentOffset = GetCurrentDetailScrollY();
            var currentBase = Math.Abs(_detailScrollTargetY - currentOffset) > 1
                ? _detailScrollTargetY
                : currentOffset;
            _detailScrollTargetY = Math.Max(0, currentBase - wheelDelta);

            ScrollDetailSurfaceTo(_detailScrollTargetY);
        }

        // Read current scroll position
        // Prefer the native scroll viewer offset when it is available
        private double GetCurrentDetailScrollY()
        {
            return _nativeDetailScrollViewer?.VerticalOffset ?? DetailScrollView.ScrollY;
        }

        // Scroll detail surface
        // Use the native viewer when available and fall back to the MAUI scroll view
        private void ScrollDetailSurfaceTo(double targetY)
        {
            if (_nativeDetailScrollViewer != null)
            {
                var clampedTarget = Math.Max(0, Math.Min(targetY, _nativeDetailScrollViewer.ScrollableHeight));
                _detailScrollTargetY = clampedTarget;
                _nativeDetailScrollViewer.ChangeView(null, clampedTarget, null, false);
                return;
            }

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DetailScrollView.ScrollToAsync(0, targetY, true);
            });
        }
#endif

        // WebView height measurement
        // Resize the preview to the document height after navigation completes
        private async void OnInfoBlockWebViewNavigated(object? sender, WebNavigatedEventArgs e)
        {
            if (e.Result != WebNavigationResult.Success || sender is not WebView webView)
            {
                return;
            }

            try
            {
                var result = await webView.EvaluateJavaScriptAsync("Math.max(document.body.scrollHeight, document.documentElement.scrollHeight).toString()");
                if (string.IsNullOrWhiteSpace(result))
                {
                    return;
                }

                var normalized = result.Trim().Trim('"');
                if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var contentHeight))
                {
                    SelectedInfoBlockWebHeight = contentHeight + 24;
                }
            }
            catch
            {
            }
        }

        // Close button handler
        // Request dialog close from the top-right close button
        private void OnCloseClicked(object? sender, EventArgs e)
        {
            RequestClose();
        }

        // Close request
        // Cancel all active refresh work before closing the dialog
        private void RequestClose()
        {
            _catalogRefreshCts?.Cancel();
            _detailRefreshCts?.Cancel();
            _searchDebounceCts?.Cancel();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        // Snapshot signatures
        // Compute a compact signature for catalog-level change detection
        private static string ComputeCatalogSignature(DownloadCatalogSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.Append(snapshot.Categories.Count).Append(';');

            foreach (var category in snapshot.Categories)
            {
                AppendSignatureSegment(builder, category.GroupKey);
                AppendSignatureSegment(builder, category.Title);
                builder.Append(category.Filters.Count).Append(';');

                foreach (var filter in category.Filters)
                {
                    AppendSignatureSegment(builder, filter.Key);
                    AppendSignatureSegment(builder, filter.Kind);
                    builder.Append(filter.Selected ? '1' : '0').Append(';');
                    builder.Append(filter.SortOrder).Append(';');
                }

                builder.Append(category.Items.Count).Append(';');
                foreach (var item in category.Items)
                {
                    AppendSignatureSegment(builder, item.ResourceKey);
                    builder.Append(item.Installed ? '1' : '0').Append(';');
                    AppendSignatureSegment(builder, item.InstalledVersion);
                    builder.Append(item.VariantCount).Append(';');
                    builder.Append(item.SortOrder).Append(';');
                }
            }

            return builder.ToString();
        }

        // Compute a compact signature for detail-level change detection
        private static string ComputeDetailSignature(DownloadCatalogItemDetail detail)
        {
            var builder = new StringBuilder();
            AppendSignatureSegment(builder, detail.ResourceKey);
            AppendSignatureSegment(builder, detail.DefaultVariantResourceKey);
            builder.Append(detail.Variants.Count).Append(';');

            foreach (var variant in detail.Variants)
            {
                AppendSignatureSegment(builder, variant.ResourceKey);
                builder.Append(variant.Installed ? '1' : '0').Append(';');
                AppendSignatureSegment(builder, variant.InstalledVersion);
                AppendSignatureSegment(builder, variant.Detail);
            }

            builder.Append(detail.Blocks.Count).Append(';');
            foreach (var block in detail.Blocks)
            {
                AppendSignatureSegment(builder, block.Id);
                AppendSignatureSegment(builder, block.Format);
                AppendSignatureSegment(builder, block.Content);
                AppendSignatureSegment(builder, block.ContentUrl);
                AppendSignatureSegment(builder, block.SourceUrl);
            }

            return builder.ToString();
        }

        private static void AppendSignatureSegment(StringBuilder builder, string? value)
        {
            if (value == null)
            {
                builder.Append("-1:");
                return;
            }

            builder.Append(value.Length).Append(':').Append(value).Append(';');
        }

        // Dialog sizing
        // Resize the dialog within its supported min and max bounds
        private void UpdateDialogSize()
        {
            if (Width <= 0 || Height <= 0) return;

            DownloadDialog.WidthRequest = ClampDialogSize(Math.Floor(Width * DialogWidthFactor), MinDialogWidth, MaxDialogWidth);
            DownloadDialog.HeightRequest = ClampDialogSize(Math.Floor(Height * DialogHeightFactor), MinDialogHeight, MaxDialogHeight);
        }

        // Clamp dialog dimension
        // Keep one dialog dimension within the configured bounds
        private static double ClampDialogSize(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

        // Replace cancellation token source
        // Cancel and dispose the previous token source before creating a new one
        private static CancellationTokenSource ReplaceCancellationTokenSource(ref CancellationTokenSource? current)
        {
            current?.Cancel();
            current?.Dispose();
            current = new CancellationTokenSource();
            return current;
        }

        // Property refresh helpers
        // Raise layout-level state used by the list and detail panes
        private void RaiseLayoutProperties()
        {
            OnPropertyChanged(nameof(CategoryCountLabel));
            OnPropertyChanged(nameof(HasCurrentItems));
            OnPropertyChanged(nameof(IsItemListEmptyVisible));
            OnPropertyChanged(nameof(HasSelectedItem));
            OnPropertyChanged(nameof(IsDetailEmptyVisible));
        }

        // Raise category-level properties
        private void RaiseCategoryProperties()
        {
            OnPropertyChanged(nameof(ActiveCategoryTitle));
            OnPropertyChanged(nameof(ActiveCategoryDescription));
            OnPropertyChanged(nameof(HasActiveCategoryDescription));
            OnPropertyChanged(nameof(ActiveCategoryItemCountLabel));
            RaiseLayoutProperties();
        }

        // Raise detail-level properties
        private void RaiseDetailProperties()
        {
            OnPropertyChanged(nameof(HasSelectedItem));
            OnPropertyChanged(nameof(IsDetailEmptyVisible));
            OnPropertyChanged(nameof(SelectedItemTitle));
            OnPropertyChanged(nameof(SelectedItemSummary));
            OnPropertyChanged(nameof(HasSelectedItemSummary));
            OnPropertyChanged(nameof(SelectedItemMetadataLine));
            OnPropertyChanged(nameof(HasSelectedItemMetadata));
            OnPropertyChanged(nameof(SelectedItemTagsLine));
            OnPropertyChanged(nameof(HasSelectedItemTags));
            OnPropertyChanged(nameof(HasVariants));
            OnPropertyChanged(nameof(HasInfoBlocks));
            OnPropertyChanged(nameof(VariantSelectorHeight));
            RaiseInfoBlockProperties();
            RaiseVariantProperties();
        }

        // Raise variant-level properties
        private void RaiseVariantProperties()
        {
            OnPropertyChanged(nameof(HasSelectedVariant));
            OnPropertyChanged(nameof(SelectedVariantTitle));
            OnPropertyChanged(nameof(SelectedVariantSummary));
            OnPropertyChanged(nameof(HasSelectedVariantSummary));
            OnPropertyChanged(nameof(SelectedVariantMetadataLine));
            OnPropertyChanged(nameof(HasSelectedVariantMetadata));
            OnPropertyChanged(nameof(SelectedVariantTagsLine));
            OnPropertyChanged(nameof(HasSelectedVariantTags));
            OnPropertyChanged(nameof(ShowInstallButton));
            OnPropertyChanged(nameof(ShowDeleteButton));
            OnPropertyChanged(nameof(ShowSelectedVariantInstalledBadge));
            OnPropertyChanged(nameof(SelectedVariantInstalledLabel));
            OnPropertyChanged(nameof(ShowOpenVariantButton));
        }

        // Raise info block properties
        private void RaiseInfoBlockProperties()
        {
            OnPropertyChanged(nameof(HasInfoBlocks));
            OnPropertyChanged(nameof(HasMultipleInfoBlocks));
            OnPropertyChanged(nameof(HasSelectedInfoBlock));
            OnPropertyChanged(nameof(SelectedInfoBlockTitle));
            OnPropertyChanged(nameof(SelectedInfoBlockText));
            OnPropertyChanged(nameof(HasSelectedInfoBlockText));
            OnPropertyChanged(nameof(HasSelectedInfoBlockWebContent));
            OnPropertyChanged(nameof(SelectedInfoBlockWebHeight));
        }

        // Category view model
        // Represent one selectable category in the sidebar
        public sealed class DownloadCategoryViewModel : INotifyPropertyChanged
        {
            private readonly Func<DownloadCategoryViewModel, Task> _selectAction;
            private bool _isSelected;

            // View model initialization
            // Bind one category to its select action
            public DownloadCategoryViewModel(DownloadCatalogCategory category, Func<DownloadCategoryViewModel, Task> selectAction)
            {
                Category = category;
                _selectAction = selectAction;
                SelectCommand = new Command(async () => await _selectAction(this));
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public DownloadCatalogCategory Category { get; }
            public string Title => Category.Title;
            public string Description => Category.Description;
            public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
            public string ItemCountLabel => Category.Items.Count == 0 ? "0" : Category.Items.Count.ToString();
            public ICommand SelectCommand { get; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BackgroundColor));
                    OnPropertyChanged(nameof(StrokeColor));
                    OnPropertyChanged(nameof(TitleColor));
                    OnPropertyChanged(nameof(SubtitleColor));
                }
            }

            public Color BackgroundColor => IsSelected ? ActiveSurfaceColor : PassiveSurfaceColor;
            public Color StrokeColor => IsSelected ? ActiveBorderColor : PassiveBorderColor;
            public Color TitleColor => IsSelected ? ActiveTextColor : InactiveTextColor;
            public Color SubtitleColor => IsSelected ? ActiveSubtitleColor : SecondaryTextColor;

            // Property notifications
            // Raise the local property change event
            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Filter view model
        // Represent one selectable provider filter
        public sealed class DownloadFilterViewModel : INotifyPropertyChanged
        {
            private readonly Func<DownloadFilterViewModel, Task> _toggleAction;
            private bool _isSelected;

            // View model initialization
            // Bind one filter to its toggle action
            public DownloadFilterViewModel(string key, string title, string kind, Func<DownloadFilterViewModel, Task> toggleAction)
            {
                Key = key;
                Title = title;
                Kind = kind;
                _toggleAction = toggleAction;
                ToggleCommand = new Command(async () => await _toggleAction(this));
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public string Key { get; }
            public string Title { get; }
            public string Kind { get; }
            public ICommand ToggleCommand { get; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BackgroundColor));
                    OnPropertyChanged(nameof(StrokeColor));
                    OnPropertyChanged(nameof(TitleColor));
                }
            }

            public Color BackgroundColor => IsSelected ? ActiveSurfaceColor : PassiveListSurfaceColor;
            public Color StrokeColor => IsSelected ? ActiveBorderColor : PassiveBorderColor;
            public Color TitleColor => IsSelected ? ActiveTextColor : InactiveTextColor;

            // Property notifications
            // Raise the local property change event
            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // List item view model
        // Represent one grouped item card in the center list
        public sealed class DownloadListItemViewModel : INotifyPropertyChanged
        {
            private readonly Func<DownloadListItemViewModel, Task> _selectAction;
            private bool _isSelected;

            // View model initialization
            // Bind one grouped item to its select action
            public DownloadListItemViewModel(DownloadCatalogItem item, Func<DownloadListItemViewModel, Task> selectAction)
            {
                Item = item;
                _selectAction = selectAction;
                SelectCommand = new Command(async () => await _selectAction(this));
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public DownloadCatalogItem Item { get; }
            public string Title => Item.Title;
            public string Summary => Item.Summary;
            public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
            public ICommand SelectCommand { get; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BackgroundColor));
                    OnPropertyChanged(nameof(StrokeColor));
                    OnPropertyChanged(nameof(TitleColor));
                    OnPropertyChanged(nameof(SubtitleColor));
                }
            }

            public string MetadataLine
            {
                get
                {
                    var segments = new List<string>();
                    if (!string.IsNullOrWhiteSpace(Item.Provider)) segments.Add(Item.Provider);
                    if (!string.IsNullOrWhiteSpace(Item.Detail)) segments.Add(Item.Detail);
                    else if (!string.IsNullOrWhiteSpace(Item.Version)) segments.Add(Item.Version);
                    return string.Join(" | ", segments);
                }
            }

            public bool HasMetadata => !string.IsNullOrWhiteSpace(MetadataLine);
            public string VariantCountLabel => Item.VariantCount <= 1 ? "Single variant" : $"{Item.VariantCount} variants";
            public bool HasVariantCount => !string.IsNullOrWhiteSpace(VariantCountLabel);
            public Color BackgroundColor => IsSelected ? ActiveSurfaceColor : PassiveListSurfaceColor;
            public Color StrokeColor => IsSelected ? ActiveBorderColor : PassiveBorderColor;
            public Color TitleColor => IsSelected ? ActiveTextColor : InactiveTextColor;
            public Color SubtitleColor => IsSelected ? ActiveSubtitleColor : SecondaryTextColor;

            // Property notifications
            // Raise the local property change event
            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Variant view model
        // Represent one installable variant in the selector list
        public sealed class DownloadVariantViewModel : INotifyPropertyChanged
        {
            private readonly Action<DownloadVariantViewModel?> _selectAction;
            private bool _isSelected;

            // View model initialization
            // Bind one variant to its select action
            public DownloadVariantViewModel(DownloadCatalogVariant variant, Action<DownloadVariantViewModel?> selectAction)
            {
                Variant = variant;
                _selectAction = selectAction;
                SelectCommand = new Command(() => _selectAction(this));
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public DownloadCatalogVariant Variant { get; }
            public string Title => Variant.Title;
            public string Summary => Variant.Summary;
            public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
            public ICommand SelectCommand { get; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BackgroundColor));
                    OnPropertyChanged(nameof(StrokeColor));
                    OnPropertyChanged(nameof(TitleColor));
                }
            }

            public string MetadataLine
            {
                get
                {
                    var segments = new List<string>();
                    if (!string.IsNullOrWhiteSpace(Variant.Version)) segments.Add(Variant.Version);
                    if (!string.IsNullOrWhiteSpace(Variant.Detail)) segments.Add(Variant.Detail);
                    return string.Join(" | ", segments);
                }
            }

            public bool HasMetadata => !string.IsNullOrWhiteSpace(MetadataLine);
            public string TagsLine => Variant.Tags.Count == 0 ? string.Empty : string.Join(" | ", Variant.Tags);
            public bool HasTags => Variant.Tags.Count > 0;
            public bool ShowInstalledBadge => Variant.Installed;
            public string InstalledLabel => string.IsNullOrWhiteSpace(Variant.InstalledVersion) ? "Installed" : $"Installed | {Variant.InstalledVersion}";
            public string SelectorDetailLine => BuildSelectorDetailLine();
            public bool HasSelectorDetailLine => !string.IsNullOrWhiteSpace(SelectorDetailLine);
            public string SizeLabel => ResolveSizeLabel();
            public bool HasSizeLabel => !string.IsNullOrWhiteSpace(SizeLabel);
            public Color BackgroundColor => IsSelected ? ActiveSurfaceColor : Colors.Transparent;
            public Color StrokeColor => IsSelected ? ActiveBorderColor : PassiveBorderColor;
            public Color TitleColor => IsSelected ? ActiveTextColor : InactiveTextColor;

            // Selector detail
            // Build the short secondary line shown in the compact selector row
            private string BuildSelectorDetailLine()
            {
                var segments = new List<string>();

                // Prefer the explicit summary, then fall back to the raw detail line.
                if (!string.IsNullOrWhiteSpace(Summary))
                {
                    segments.Add(Summary);
                }
                else if (!string.IsNullOrWhiteSpace(Variant.Detail))
                {
                    segments.Add(Variant.Detail);
                }

                foreach (var tag in Variant.Tags.Where(tag => !string.Equals(tag, SizeLabel, StringComparison.OrdinalIgnoreCase)))
                {
                    if (segments.Count >= 2)
                    {
                        break;
                    }

                    segments.Add(tag);
                }

                if (segments.Count == 0 && !string.IsNullOrWhiteSpace(Variant.Version))
                {
                    segments.Add(Variant.Version);
                }

                return string.Join(" | ", segments);
            }

            // Size extraction
            // Prefer an explicit size tag and then fall back to the first detail segment
            private string ResolveSizeLabel()
            {
                foreach (var tag in Variant.Tags)
                {
                    if (tag.Contains("GB", StringComparison.OrdinalIgnoreCase) ||
                        tag.Contains("MB", StringComparison.OrdinalIgnoreCase) ||
                        tag.Contains("KB", StringComparison.OrdinalIgnoreCase))
                    {
                        return tag;
                    }
                }

                var detailHead = Variant.Detail
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .FirstOrDefault();

                return detailHead ?? string.Empty;
            }

            // Property notifications
            // Raise the local property change event
            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Info block view model
        // Represent one details tab in the right panel
        public sealed class DownloadInfoBlockViewModel : INotifyPropertyChanged
        {
            private readonly Func<DownloadInfoBlockViewModel, Task> _selectAction;
            private bool _isSelected;

            // View model initialization
            // Bind one info block to its select action and resolve preview text
            public DownloadInfoBlockViewModel(DownloadCatalogInfoBlock block, string? baseUrl, Func<DownloadInfoBlockViewModel, Task> selectAction)
            {
                Block = block;
                _selectAction = selectAction;
                RenderedContent = ResolveRenderedContent(block, baseUrl);
                SelectCommand = new Command(async () => await _selectAction(this));
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            public DownloadCatalogInfoBlock Block { get; }
            public string Title => string.IsNullOrWhiteSpace(Block.Title) ? "Details" : Block.Title;
            public string RenderedContent { get; }
            public string ContentUrl => Block.ContentUrl;
            public string SourceUrl => Block.SourceUrl;
            public bool HasTextContent => !string.IsNullOrWhiteSpace(RenderedContent);
            public bool HasContentUrl => !string.IsNullOrWhiteSpace(ContentUrl);
            public ICommand SelectCommand { get; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected == value) return;
                    _isSelected = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BackgroundColor));
                    OnPropertyChanged(nameof(StrokeColor));
                    OnPropertyChanged(nameof(TitleColor));
                }
            }

            public Color BackgroundColor => IsSelected ? ActiveSurfaceColor : PassiveListSurfaceColor;
            public Color StrokeColor => IsSelected ? ActiveBorderColor : PassiveBorderColor;
            public Color TitleColor => IsSelected ? ActiveTextColor : InactiveTextColor;

            // Text fallback
            // Skip text rendering when the block points at dedicated HTML content
            private static string ResolveRenderedContent(DownloadCatalogInfoBlock block, string? baseUrl)
            {
                if (!string.IsNullOrWhiteSpace(block.ContentUrl))
                {
                    var format = (block.Format ?? string.Empty).Trim().ToLowerInvariant();
                    if (format is "html-file" or "html-url" or "web")
                    {
                        return string.Empty;
                    }
                }

                return InfoBlockTextFormatter.Render(block, baseUrl);
            }

            // Property notifications
            // Raise the local property change event
            private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        // Info block text formatter
        // Convert text-based blocks into a readable plain-text fallback
        private static class InfoBlockTextFormatter
        {
            private static readonly Regex ScriptRegex = new(
                "<script\\b[^<]*(?:(?!</script>)<[^<]*)*</script>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

            private static readonly Regex FencedCodeRegex = new(
                "```(?<lang>[a-zA-Z0-9_+-]*)\\s*\\n(?<code>.*?)```",
                RegexOptions.Singleline | RegexOptions.Compiled);

            private static readonly Regex HeadingRegex = new(
                "^(#{1,6})\\s+(.*)$",
                RegexOptions.Compiled);

            private static readonly Regex UnorderedListRegex = new(
                "^\\s*[-*+]\\s+(.*)$",
                RegexOptions.Compiled);

            private static readonly Regex OrderedListRegex = new(
                "^\\s*\\d+\\.\\s+(.*)$",
                RegexOptions.Compiled);

            private static readonly Regex BlockquoteRegex = new(
                "^\\s*>\\s?(.*)$",
                RegexOptions.Compiled);

            private static readonly Regex HorizontalRuleRegex = new(
                "^\\s*([-*_]){3,}\\s*$",
                RegexOptions.Compiled);

            // Render text block
            // Dispatch block rendering based on the declared format
            public static string Render(DownloadCatalogInfoBlock block, string? baseUrl)
            {
                if (string.IsNullOrWhiteSpace(block.Content))
                {
                    return string.Empty;
                }

                var format = (block.Format ?? "text").Trim().ToLowerInvariant();

                try
                {
                    return format switch
                    {
                        "html" => ConvertHtmlToText(block.Content, baseUrl),
                        "markdown" or "md" => ConvertMarkdownToText(block.Content, baseUrl),
                        _ => NormalizePlainText(block.Content)
                    };
                }
                catch
                {
                    return NormalizePlainText(block.Content);
                }
            }

            // Normalize text
            // Normalize line endings and trim extra outer whitespace
            private static string NormalizePlainText(string text)
            {
                return (text ?? string.Empty)
                    .Replace("\r\n", "\n", StringComparison.Ordinal)
                    .Replace("\r", "\n", StringComparison.Ordinal)
                    .Trim();
            }

            // Convert markdown
            // Reduce markdown into a readable plain-text representation
            private static string ConvertMarkdownToText(string markdown, string? baseUrl)
            {
                var normalized = NormalizePlainText(markdown);
                var builder = new StringBuilder();
                var paragraphLines = new List<string>();
                var codeLines = new List<string>();
                var inCodeBlock = false;

                foreach (var rawLine in normalized.Split('\n'))
                {
                    var line = rawLine.TrimEnd();

                    // Track fenced code blocks as raw text sections.
                    if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                    {
                        FlushParagraph(builder, paragraphLines);

                        if (inCodeBlock)
                        {
                            foreach (var codeLine in codeLines)
                            {
                                AppendLine(builder, codeLine);
                            }

                            codeLines.Clear();
                            AppendBlankLine(builder);
                            inCodeBlock = false;
                        }
                        else
                        {
                            inCodeBlock = true;
                        }

                        continue;
                    }

                    if (inCodeBlock)
                    {
                        codeLines.Add(line);
                        continue;
                    }

                    // Treat blank lines as paragraph boundaries.
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        FlushParagraph(builder, paragraphLines);
                        AppendBlankLine(builder);
                        continue;
                    }

                    // Preserve common markdown block markers in a simplified text form.
                    var headingMatch = HeadingRegex.Match(line);
                    if (headingMatch.Success)
                    {
                        FlushParagraph(builder, paragraphLines);
                        AppendLine(builder, headingMatch.Groups[2].Value.Trim().ToUpperInvariant());
                        AppendBlankLine(builder);
                        continue;
                    }

                    if (HorizontalRuleRegex.IsMatch(line))
                    {
                        FlushParagraph(builder, paragraphLines);
                        AppendLine(builder, "--------------------------------");
                        AppendBlankLine(builder);
                        continue;
                    }

                    var quoteMatch = BlockquoteRegex.Match(line);
                    if (quoteMatch.Success)
                    {
                        FlushParagraph(builder, paragraphLines);
                        AppendLine(builder, $"> {ApplyInlineMarkdownToText(quoteMatch.Groups[1].Value.Trim(), baseUrl)}");
                        continue;
                    }

                    var unorderedMatch = UnorderedListRegex.Match(line);
                    if (unorderedMatch.Success)
                    {
                        FlushParagraph(builder, paragraphLines);
                        AppendLine(builder, $"- {ApplyInlineMarkdownToText(unorderedMatch.Groups[1].Value.Trim(), baseUrl)}");
                        continue;
                    }

                    var orderedMatch = OrderedListRegex.Match(line);
                    if (orderedMatch.Success)
                    {
                        FlushParagraph(builder, paragraphLines);
                        AppendLine(builder, $"1. {ApplyInlineMarkdownToText(orderedMatch.Groups[1].Value.Trim(), baseUrl)}");
                        continue;
                    }

                    paragraphLines.Add(line.Trim());
                }

                FlushParagraph(builder, paragraphLines);

                if (codeLines.Count > 0)
                {
                    foreach (var codeLine in codeLines)
                    {
                        AppendLine(builder, codeLine);
                    }
                }

                return builder.ToString().Trim();
            }

            // Convert HTML
            // Reduce lightweight HTML into a readable plain-text representation
            private static string ConvertHtmlToText(string html, string? baseUrl)
            {
                var sanitized = ScriptRegex.Replace(html ?? string.Empty, string.Empty);
                sanitized = Regex.Replace(sanitized, "(?i)<br\\s*/?>", "\n");
                sanitized = Regex.Replace(sanitized, "(?i)</p\\s*>", "\n\n");
                sanitized = Regex.Replace(sanitized, "(?i)</div\\s*>", "\n");
                sanitized = Regex.Replace(sanitized, "(?i)</li\\s*>", "\n");
                sanitized = Regex.Replace(sanitized, "(?i)<li\\b[^>]*>", "- ");
                sanitized = Regex.Replace(sanitized, "(?i)</h[1-6]\\s*>", "\n\n");
                sanitized = Regex.Replace(sanitized, "(?i)<h[1-6]\\b[^>]*>", string.Empty);
                sanitized = Regex.Replace(sanitized, "(?i)<pre\\b[^>]*>", "\n");
                sanitized = Regex.Replace(sanitized, "(?i)</pre\\s*>", "\n");
                sanitized = Regex.Replace(sanitized, "(?i)<code\\b[^>]*>", "`");
                sanitized = Regex.Replace(sanitized, "(?i)</code\\s*>", "`");
                sanitized = Regex.Replace(sanitized, "(?i)<img\\b[^>]*alt\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>", "[Image: $1]");
                sanitized = Regex.Replace(sanitized, "(?i)<img\\b[^>]*src\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>", match =>
                {
                    var url = AbsolutizeUrl(match.Groups[1].Value, baseUrl);
                    return string.IsNullOrWhiteSpace(url) ? "[Image]" : $"[Image: {url}]";
                });

                sanitized = Regex.Replace(sanitized, "(?i)<a\\b[^>]*href\\s*=\\s*['\"]([^'\"]*)['\"][^>]*>(.*?)</a>", match =>
                {
                    var href = AbsolutizeUrl(WebUtility.HtmlDecode(match.Groups[1].Value), baseUrl);
                    var label = ConvertHtmlToText(match.Groups[2].Value, baseUrl);
                    return string.IsNullOrWhiteSpace(href) ? label : $"{label} ({href})";
                }, RegexOptions.Singleline);

                sanitized = Regex.Replace(sanitized, "<[^>]+>", string.Empty);
                sanitized = WebUtility.HtmlDecode(sanitized);
                sanitized = Regex.Replace(sanitized, "[ \t]+\n", "\n");
                sanitized = Regex.Replace(sanitized, "\n{3,}", "\n\n");
                return sanitized.Trim();
            }

            // Paragraph helpers
            // Flush the current paragraph into the output builder
            private static void FlushParagraph(StringBuilder builder, List<string> paragraphLines)
            {
                if (paragraphLines.Count == 0)
                {
                    return;
                }

                var paragraph = string.Join(" ", paragraphLines).Trim();
                if (!string.IsNullOrWhiteSpace(paragraph))
                {
                    AppendLine(builder, ApplyInlineMarkdownToText(paragraph, null));
                    AppendBlankLine(builder);
                }

                paragraphLines.Clear();
            }

            // Append one line to the output builder
            private static void AppendLine(StringBuilder builder, string value)
            {
                builder.AppendLine(value);
            }

            // Append one blank line when the builder is not already separated
            private static void AppendBlankLine(StringBuilder builder)
            {
                if (builder.Length > 0 && CountTrailingLineFeeds(builder) < 2)
                {
                    builder.AppendLine();
                }
            }

            private static int CountTrailingLineFeeds(StringBuilder builder)
            {
                var count = 0;
                for (var index = builder.Length - 1; index >= 0 && count < 2; index--)
                {
                    var current = builder[index];
                    if (current == '\n')
                    {
                        count++;
                        continue;
                    }

                    if (current != '\r')
                    {
                        break;
                    }
                }

                return count;
            }

            // Inline markdown helpers
            // Reduce inline markdown and links into readable text
            private static string ApplyInlineMarkdownToText(string value, string? baseUrl)
            {
                var text = value ?? string.Empty;

                text = Regex.Replace(
                    text,
                    "!\\[(.*?)\\]\\((.*?)\\)",
                    match =>
                    {
                        var alt = match.Groups[1].Value.Trim();
                        var url = AbsolutizeUrl(match.Groups[2].Value.Trim(), baseUrl);
                        return string.IsNullOrWhiteSpace(url)
                            ? $"[Image: {alt}]"
                            : $"[Image: {alt}] {url}";
                    });

                text = Regex.Replace(
                    text,
                    "\\[(.*?)\\]\\((.*?)\\)",
                    match =>
                    {
                        var label = match.Groups[1].Value.Trim();
                        var url = AbsolutizeUrl(match.Groups[2].Value.Trim(), baseUrl);
                        return string.IsNullOrWhiteSpace(url) ? label : $"{label} ({url})";
                    });

                text = Regex.Replace(text, "\\*\\*(.+?)\\*\\*", "$1");
                text = Regex.Replace(text, "__(.+?)__", "$1");
                text = Regex.Replace(text, "(?<!\\*)\\*(?!\\s)(.+?)(?<!\\s)\\*(?!\\*)", "$1");
                text = Regex.Replace(text, "(?<!_)_(?!\\s)(.+?)(?<!\\s)_(?!_)", "$1");
                text = Regex.Replace(text, "`([^`]+)`", "$1");
                return WebUtility.HtmlDecode(text).Trim();
            }

            // URL helpers
            // Resolve a relative URL against the block base URL
            private static string AbsolutizeUrl(string? candidate, string? baseUrl)
            {
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    return string.Empty;
                }

                if (Uri.TryCreate(candidate, UriKind.Absolute, out var absolute))
                {
                    return absolute.ToString();
                }

                if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
                    Uri.TryCreate(baseUri, candidate, out var combined))
                {
                    return combined.ToString();
                }

                return candidate ?? string.Empty;
            }
        }

        /// <summary>
        /// Finds a named color resource with a defensive fallback when the key is absent.
        /// </summary>
        private static Color GetColorResource(string key, Color fallback) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color c
                ? c
                : fallback;
    }
}
