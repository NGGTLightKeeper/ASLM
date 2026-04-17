// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    // Home dashboard view

    /// <summary>
    /// Displays the main ASLM dashboard with live runtime metrics, module controls, and the managed process tree.
    /// </summary>
    public partial class HomeView : ContentView, IHomeDashboardView
    {
        private const double MinSummaryCardWidth = 400;
        private const double CompactBreakpoint = 1480;
        private const double MinProcessNameColumnWidth = 280;
        private const double MinProcessMetricColumnWidth = 72;
        private const double MinProcessDetailsColumnWidth = 140;
        private const double MinModulesNameColumnWidth = 220;
        private const double MinModulesActionsColumnWidth = 148;

        private readonly HomeDashboardPageViewModel _viewModel = new();
        private readonly HomeDashboardPresenter _presenter;
        private IDispatcherTimer? _refreshTimer;
        private bool _hasCustomProcessColumnWidths;
        private bool _hasCustomModulesColumnWidths;
        private string? _activeProcessResizeColumnKey;
        private double _activeProcessResizeStartWidth;
        private string? _activeModulesResizeColumnKey;
        private double _activeModulesResizeStartWidth;

        /// <summary>
        /// Creates the home dashboard and prepares the presenter-driven lifecycle.
        /// </summary>
        public HomeView(
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            ModuleConsoleService consoleService,
            ProcessSnapshotService processSnapshotService)
        {
            InitializeComponent();

            BindingContext = _viewModel;

            _presenter = new HomeDashboardPresenter(this, moduleInstaller, moduleRunner, consoleService, processSnapshotService);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;

            UpdateResponsiveLayout();
        }

        /// <summary>
        /// Attaches the shell host so the dashboard can request navigation to related pages.
        /// </summary>
        internal void Initialize(AppShellPage shell)
        {
            _presenter.AttachShell(shell);
        }

        /// <summary>
        /// Requests an immediate presenter-driven refresh of the dashboard.
        /// </summary>
        internal Task RefreshAsync()
        {
            return _presenter.RefreshAsync();
        }

        // Lifetime events

        /// <summary>
        /// Activates the presenter and starts the periodic refresh loop when the dashboard enters the visual tree.
        /// </summary>
        private async void OnLoaded(object? sender, EventArgs e)
        {
            await _presenter.ActivateAsync();
            StartRefreshTimer();
        }

        /// <summary>
        /// Deactivates the presenter and stops the periodic refresh loop when the dashboard leaves the visual tree.
        /// </summary>
        private void OnUnloaded(object? sender, EventArgs e)
        {
            StopRefreshTimer();
            _presenter.Deactivate();
        }

        // Layout events

        /// <summary>
        /// Rebuilds the responsive dashboard layout when the host size changes.
        /// </summary>
        private void OnSizeChanged(object? sender, EventArgs e)
        {
            UpdateResponsiveLayout();
        }

        // Rendering

        /// <summary>
        /// Applies the fully prepared presenter state to the bindable page model.
        /// </summary>
        void IHomeDashboardView.Render(HomeDashboardState state)
        {
            _viewModel.Subtitle = state.Subtitle;
            _viewModel.ProcessTreeCaption = state.ProcessTreeCaption;
            _viewModel.ModulesCaption = state.ModulesCaption;

            SyncKeyedItems(
                _viewModel.SummaryCards,
                state.SummaryCards,
                static item => item.Key,
                static (target, source) => target.CopyFrom(source));

            SyncKeyedItems(
                _viewModel.ProcessNodes,
                state.ProcessNodes,
                static item => item.Key,
                static (target, source) => target.CopyFrom(source));

            SyncKeyedItems(
                _viewModel.Modules,
                state.Modules,
                static item => item.SourcePath,
                static (target, source) => target.CopyFrom(source));
        }

        // Responsive layout

        /// <summary>
        /// Adapts the summary grid and main workspace to the available dashboard width.
        /// </summary>
        private void UpdateResponsiveLayout()
        {
            var availableWidth = Width > 0 ? Width - 24 : CompactBreakpoint;
            if (availableWidth > 0)
            {
                var summaryWidth = Math.Max(0, availableWidth - 8);
                _viewModel.SummaryGridSpan = Math.Min(3, Math.Max(1, (int)(summaryWidth / MinSummaryCardWidth)));
            }

            var isCompact = availableWidth < CompactBreakpoint;
            ApplyTableLayout(availableWidth, isCompact);

            WorkspaceLayout.ColumnDefinitions.Clear();
            WorkspaceLayout.RowDefinitions.Clear();

            if (isCompact)
            {
                WorkspaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                WorkspaceLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                WorkspaceLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(ProcessTreePanel, 0);
                Grid.SetColumn(ProcessTreePanel, 0);
                Grid.SetRow(ModulesPanel, 1);
                Grid.SetColumn(ModulesPanel, 0);
            }
            else
            {
                WorkspaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
                WorkspaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                WorkspaceLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(ProcessTreePanel, 0);
                Grid.SetColumn(ProcessTreePanel, 0);
                Grid.SetRow(ModulesPanel, 0);
                Grid.SetColumn(ModulesPanel, 1);
            }
        }

        /// <summary>
        /// Applies the responsive table-column presets used by the process and module panels.
        /// </summary>
        private void ApplyTableLayout(double availableWidth, bool isCompact)
        {
            _viewModel.ProcessTableColumnSpacing = 10;
            _viewModel.ModulesTableColumnSpacing = 10;

            if (!_hasCustomProcessColumnWidths)
            {
                var processNameWidth = isCompact
                    ? 340
                    : availableWidth < 1700
                        ? 420
                        : 520;

                _viewModel.ProcessNameColumnWidth = new GridLength(processNameWidth);
                _viewModel.ProcessCpuColumnWidth = new GridLength(78);
                _viewModel.ProcessGpuColumnWidth = new GridLength(78);
                _viewModel.ProcessMemoryColumnWidth = new GridLength(92);
                _viewModel.ProcessDiskColumnWidth = new GridLength(92);
                _viewModel.ProcessNetworkColumnWidth = new GridLength(80);
                _viewModel.ProcessDetailsColumnWidth = new GridLength(isCompact ? 160 : 210);
            }

            if (!_hasCustomModulesColumnWidths)
            {
                _viewModel.ModulesNameColumnWidth = new GridLength(isCompact ? 260 : 320);
                _viewModel.ModulesActionsColumnWidth = new GridLength(isCompact ? 160 : 172);
            }

            UpdateTableWidths();
        }

        /// <summary>
        /// Handles runtime resizing of process-table columns from the header drag handles.
        /// </summary>
        private void OnProcessColumnResizePanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (sender is not VisualElement { ClassId: { Length: > 0 } columnKey })
            {
                return;
            }

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _activeProcessResizeColumnKey = columnKey;
                    _activeProcessResizeStartWidth = GetProcessColumnWidth(columnKey);
                    break;

                case GestureStatus.Running when string.Equals(_activeProcessResizeColumnKey, columnKey, StringComparison.Ordinal):
                    _hasCustomProcessColumnWidths = true;
                    // `TotalX` is measured from gesture start, so we keep the initial width
                    // and apply the running delta on top to avoid cumulative drift.
                    SetProcessColumnWidth(columnKey, _activeProcessResizeStartWidth + e.TotalX);
                    UpdateTableWidths();
                    break;

                case GestureStatus.Canceled:
                case GestureStatus.Completed:
                    _activeProcessResizeColumnKey = null;
                    _activeProcessResizeStartWidth = 0;
                    break;
            }
        }

        /// <summary>
        /// Handles runtime resizing of module-table columns from the header drag handles.
        /// </summary>
        private void OnModulesColumnResizePanUpdated(object? sender, PanUpdatedEventArgs e)
        {
            if (sender is not VisualElement { ClassId: { Length: > 0 } columnKey })
            {
                return;
            }

            switch (e.StatusType)
            {
                case GestureStatus.Started:
                    _activeModulesResizeColumnKey = columnKey;
                    _activeModulesResizeStartWidth = GetModulesColumnWidth(columnKey);
                    break;

                case GestureStatus.Running when string.Equals(_activeModulesResizeColumnKey, columnKey, StringComparison.Ordinal):
                    _hasCustomModulesColumnWidths = true;
                    // Reuse the same delta-based resize behavior as the process grid so the
                    // header splitter stays predictable during long drags.
                    SetModulesColumnWidth(columnKey, _activeModulesResizeStartWidth + e.TotalX);
                    UpdateTableWidths();
                    break;

                case GestureStatus.Canceled:
                case GestureStatus.Completed:
                    _activeModulesResizeColumnKey = null;
                    _activeModulesResizeStartWidth = 0;
                    break;
            }
        }

        /// <summary>
        /// Recomputes the minimum widths required by the horizontally scrollable tables.
        /// </summary>
        private void UpdateTableWidths()
        {
            // The scrollable table uses fixed column widths plus the horizontal grid padding.
            _viewModel.ProcessTableWidth =
                24 +
                (_viewModel.ProcessTableColumnSpacing * 6) +
                _viewModel.ProcessNameColumnWidth.Value +
                _viewModel.ProcessCpuColumnWidth.Value +
                _viewModel.ProcessGpuColumnWidth.Value +
                _viewModel.ProcessMemoryColumnWidth.Value +
                _viewModel.ProcessDiskColumnWidth.Value +
                _viewModel.ProcessNetworkColumnWidth.Value +
                _viewModel.ProcessDetailsColumnWidth.Value;

            _viewModel.ModulesTableWidth =
                24 +
                _viewModel.ModulesTableColumnSpacing +
                _viewModel.ModulesNameColumnWidth.Value +
                _viewModel.ModulesActionsColumnWidth.Value;
        }

        /// <summary>
        /// Returns the current width of one process-table column.
        /// </summary>
        private double GetProcessColumnWidth(string columnKey)
        {
            return columnKey switch
            {
                "Name" => _viewModel.ProcessNameColumnWidth.Value,
                "Cpu" => _viewModel.ProcessCpuColumnWidth.Value,
                "Gpu" => _viewModel.ProcessGpuColumnWidth.Value,
                "Memory" => _viewModel.ProcessMemoryColumnWidth.Value,
                "Disk" => _viewModel.ProcessDiskColumnWidth.Value,
                "Network" => _viewModel.ProcessNetworkColumnWidth.Value,
                "Details" => _viewModel.ProcessDetailsColumnWidth.Value,
                _ => 0
            };
        }

        /// <summary>
        /// Updates one process-table column width while respecting per-column minimums.
        /// </summary>
        private void SetProcessColumnWidth(string columnKey, double width)
        {
            var clampedWidth = Math.Max(GetMinimumProcessColumnWidth(columnKey), width);

            switch (columnKey)
            {
                case "Name":
                    _viewModel.ProcessNameColumnWidth = new GridLength(clampedWidth);
                    break;
                case "Cpu":
                    _viewModel.ProcessCpuColumnWidth = new GridLength(clampedWidth);
                    break;
                case "Gpu":
                    _viewModel.ProcessGpuColumnWidth = new GridLength(clampedWidth);
                    break;
                case "Memory":
                    _viewModel.ProcessMemoryColumnWidth = new GridLength(clampedWidth);
                    break;
                case "Disk":
                    _viewModel.ProcessDiskColumnWidth = new GridLength(clampedWidth);
                    break;
                case "Network":
                    _viewModel.ProcessNetworkColumnWidth = new GridLength(clampedWidth);
                    break;
                case "Details":
                    _viewModel.ProcessDetailsColumnWidth = new GridLength(clampedWidth);
                    break;
            }
        }

        /// <summary>
        /// Returns the current width of one modules-table column.
        /// </summary>
        private double GetModulesColumnWidth(string columnKey)
        {
            return columnKey switch
            {
                "Name" => _viewModel.ModulesNameColumnWidth.Value,
                "Actions" => _viewModel.ModulesActionsColumnWidth.Value,
                _ => 0
            };
        }

        /// <summary>
        /// Updates one modules-table column width while respecting per-column minimums.
        /// </summary>
        private void SetModulesColumnWidth(string columnKey, double width)
        {
            var clampedWidth = Math.Max(
                string.Equals(columnKey, "Actions", StringComparison.Ordinal)
                    ? MinModulesActionsColumnWidth
                    : MinModulesNameColumnWidth,
                width);

            switch (columnKey)
            {
                case "Name":
                    _viewModel.ModulesNameColumnWidth = new GridLength(clampedWidth);
                    break;
                case "Actions":
                    _viewModel.ModulesActionsColumnWidth = new GridLength(clampedWidth);
                    break;
            }
        }

        /// <summary>
        /// Returns the minimum allowed width of one process-table column.
        /// </summary>
        private static double GetMinimumProcessColumnWidth(string columnKey)
        {
            return columnKey switch
            {
                "Name" => MinProcessNameColumnWidth,
                "Details" => MinProcessDetailsColumnWidth,
                _ => MinProcessMetricColumnWidth
            };
        }

        // Timed refresh

        /// <summary>
        /// Starts the periodic dashboard refresh timer.
        /// </summary>
        private void StartRefreshTimer()
        {
            if (Dispatcher == null)
            {
                return;
            }

            _refreshTimer ??= CreateRefreshTimer(Dispatcher);
            if (!_refreshTimer.IsRunning)
            {
                _refreshTimer.Start();
            }
        }

        /// <summary>
        /// Stops the periodic dashboard refresh timer.
        /// </summary>
        private void StopRefreshTimer()
        {
            _refreshTimer?.Stop();
        }

        /// <summary>
        /// Creates the repeating timer used to keep the dashboard metrics current.
        /// </summary>
        private IDispatcherTimer CreateRefreshTimer(IDispatcher dispatcher)
        {
            var timer = dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.IsRepeating = true;
            timer.Tick += OnRefreshTimerTick;
            return timer;
        }

        // Collection helpers

        /// <summary>
        /// Requests a lightweight live refresh on each timer tick.
        /// </summary>
        private async void OnRefreshTimerTick(object? sender, EventArgs e)
        {
            await _presenter.RefreshAsync(forceModuleReload: false);
        }

        /// <summary>
        /// Updates an observable collection in place by key so item instances stay stable between refreshes.
        /// </summary>
        private static void SyncKeyedItems<T>(
            ObservableCollection<T> target,
            IReadOnlyList<T> source,
            Func<T, string> keySelector,
            Action<T, T> updateExisting)
            where T : class
        {
            var indexByKey = BuildIndex(target, keySelector);

            for (var index = 0; index < source.Count; index++)
            {
                var sourceItem = source[index];
                var sourceKey = keySelector(sourceItem);

                if (index < target.Count &&
                    string.Equals(keySelector(target[index]), sourceKey, StringComparison.Ordinal))
                {
                    updateExisting(target[index], sourceItem);
                    continue;
                }

                if (indexByKey.TryGetValue(sourceKey, out var existingIndex) && existingIndex >= index)
                {
                    // Move the matching item instead of replacing it so CollectionView rows
                    // keep their instance identity and avoid full reanimation on refresh.
                    var existingItem = target[existingIndex];
                    target.Move(existingIndex, index);
                    updateExisting(existingItem, sourceItem);
                    indexByKey = BuildIndex(target, keySelector);
                }
                else
                {
                    target.Insert(index, sourceItem);
                    indexByKey = BuildIndex(target, keySelector);
                }
            }

            while (target.Count > source.Count)
            {
                target.RemoveAt(target.Count - 1);
            }
        }

        private static Dictionary<string, int> BuildIndex<T>(ObservableCollection<T> target, Func<T, string> keySelector)
            where T : class
        {
            var indexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < target.Count; index++)
            {
                indexByKey[keySelector(target[index])] = index;
            }

            return indexByKey;
        }
    }


    // View contract

    /// <summary>
    /// Defines the rendering contract used by the home dashboard presenter.
    /// </summary>
    internal interface IHomeDashboardView
    {
        /// <summary>
        /// Applies a fully prepared dashboard state to the view.
        /// </summary>
        void Render(HomeDashboardState state);
    }


    // Presenter

    /// <summary>
    /// Builds the home dashboard state, coordinates live refreshes, and handles module actions.
    /// </summary>
    internal sealed class HomeDashboardPresenter
    {
        private readonly IHomeDashboardView _view;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly ModuleConsoleService _consoleService;
        private readonly HomeDiagnosticsCollector _diagnosticsCollector;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private readonly Dictionary<string, HomeModuleOperationState> _moduleOperations =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, bool> _expandedNodes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["root:aslm"] = true
            };

        private List<ModuleConfig> _knownModules = [];
        private IReadOnlyList<ModuleConsoleModuleSnapshot> _lastConsoleSnapshots = [];
        private HomeDiagnosticsSnapshot? _lastDiagnosticsSnapshot;
        private AppShellPage? _shell;
        private bool _isActive;
        private int _forceRefreshQueued;

        /// <summary>
        /// Creates the presenter for the main home dashboard.
        /// </summary>
        public HomeDashboardPresenter(
            IHomeDashboardView view,
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            ModuleConsoleService consoleService,
            ProcessSnapshotService processSnapshotService)
        {
            _view = view;
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _consoleService = consoleService;
            _diagnosticsCollector = new HomeDiagnosticsCollector(processSnapshotService);
        }

        /// <summary>
        /// Attaches the shell host used for page navigation.
        /// </summary>
        public void AttachShell(AppShellPage shell)
        {
            _shell = shell;
        }

        /// <summary>
        /// Starts listening for console changes and renders the initial dashboard state.
        /// </summary>
        public async Task ActivateAsync()
        {
            if (_isActive)
            {
                return;
            }

            _isActive = true;
            _consoleService.StateChanged += OnConsoleStateChanged;
            await RefreshAsync(forceModuleReload: true);
        }

        /// <summary>
        /// Stops listening for console changes.
        /// </summary>
        public void Deactivate()
        {
            if (!_isActive)
            {
                return;
            }

            _consoleService.StateChanged -= OnConsoleStateChanged;
            _isActive = false;
        }

        /// <summary>
        /// Reloads modules when needed, captures diagnostics, and rerenders the dashboard.
        /// </summary>
        public Task RefreshAsync()
        {
            return RefreshAsync(forceModuleReload: true);
        }

        /// <summary>
        /// Reloads modules when needed, captures diagnostics, and rerenders the dashboard.
        /// </summary>
        public async Task RefreshAsync(bool forceModuleReload)
        {
            if (forceModuleReload)
            {
                Interlocked.Exchange(ref _forceRefreshQueued, 1);
            }

            if (!await _refreshLock.WaitAsync(0))
            {
                return;
            }

            try
            {
                var shouldForceReload = forceModuleReload || Interlocked.Exchange(ref _forceRefreshQueued, 0) == 1;

                do
                {
                    if (shouldForceReload || _knownModules.Count == 0)
                    {
                        _knownModules = await _moduleInstaller.DiscoverModulesAsync();
                    }

                    var modules = _knownModules.ToList();
                    var refreshSnapshot = await Task.Run(() =>
                    {
                        _consoleService.EnsureModules(modules);
                        var consoleSnapshots = _consoleService.GetSnapshot();
                        var diagnostics = _diagnosticsCollector.Capture(modules, consoleSnapshots);
                        var state = BuildState(modules, consoleSnapshots, diagnostics);

                        return new HomeRefreshSnapshot
                        {
                            ConsoleSnapshots = consoleSnapshots,
                            Diagnostics = diagnostics,
                            State = state
                        };
                    }).ConfigureAwait(false);

                    _lastConsoleSnapshots = refreshSnapshot.ConsoleSnapshots;
                    _lastDiagnosticsSnapshot = refreshSnapshot.Diagnostics;

                    await MainThread.InvokeOnMainThreadAsync(() => _view.Render(refreshSnapshot.State));
                    shouldForceReload = Interlocked.Exchange(ref _forceRefreshQueued, 0) == 1;
                }
                while (shouldForceReload);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// Opens the full module management page.
        /// </summary>
        public void OpenModules()
        {
            MainThread.BeginInvokeOnMainThread(() => _shell?.OpenModuleManagement());
        }

        /// <summary>
        /// Opens the consoles page.
        /// </summary>
        public void OpenConsoles()
        {
            MainThread.BeginInvokeOnMainThread(() => _shell?.OpenConsoles());
        }

        // Console refresh triggers

        /// <summary>
        /// Coalesces bursts of console-store changes into a single dashboard refresh.
        /// </summary>
        private void OnConsoleStateChanged(object? sender, EventArgs e)
        {
            // Periodic one-second refreshes already keep the dashboard current.
        }

        // State construction

        /// <summary>
        /// Builds the complete dashboard state from modules, console snapshots, and live diagnostics.
        /// </summary>
        private HomeDashboardState BuildState(
            IReadOnlyList<ModuleConfig> modules,
            IReadOnlyList<ModuleConsoleModuleSnapshot> consoleSnapshots,
            HomeDiagnosticsSnapshot diagnostics)
        {
            var activeSessionCount = consoleSnapshots.Sum(module => module.Sessions.Count(session => session.IsRunning && !session.IsObservedProcess));
            var totalSessionCount = consoleSnapshots.Sum(module => module.Sessions.Count(session => !session.IsObservedProcess));
            var runningModuleCount = modules.Count(module => module.Status.Enabled);
            var updatedAt = diagnostics.CapturedUtc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);

            return new HomeDashboardState
            {
                Subtitle = modules.Count == 0
                    ? "No modules installed yet."
                    : $"{runningModuleCount} running modules - {diagnostics.ActiveProcessCount} live subprocesses - updated {updatedAt}",
                ProcessTreeCaption = diagnostics.ActiveProcessCount == 0
                    ? "ASLM is idle. Start a module to populate the managed tree."
                    : $"{diagnostics.ActiveProcessCount} live processes across {diagnostics.ActiveModuleCount} active modules.",
                ModulesCaption = modules.Count == 0
                    ? "Install a module to begin."
                    : $"{modules.Count} installed - {runningModuleCount} enabled - {activeSessionCount} active sessions - {totalSessionCount} total sessions",
                SummaryCards = BuildSummaryCards(modules, consoleSnapshots, diagnostics),
                ProcessNodes = FlattenTree(diagnostics.RootNode),
                Modules = BuildModuleCards(modules, diagnostics)
            };
        }

        /// <summary>
        /// Builds the summary resource cards shown at the top of the dashboard.
        /// </summary>
        private IReadOnlyList<HomeSummaryCardViewModel> BuildSummaryCards(
            IReadOnlyList<ModuleConfig> modules,
            IReadOnlyList<ModuleConsoleModuleSnapshot> consoleSnapshots,
            HomeDiagnosticsSnapshot diagnostics)
        {
            var updatedAt = diagnostics.CapturedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var activeSessionCount = consoleSnapshots.Sum(module => module.Sessions.Count(session => session.IsRunning && !session.IsObservedProcess));
            var totalSessionCount = consoleSnapshots.Sum(module => module.Sessions.Count(session => !session.IsObservedProcess));
            var systemMetrics = new List<HomeSummaryMetricViewModel>
            {
                CreatePercentMetric(
                    "CPU",
                    diagnostics.SystemUsage.CpuPercent,
                    $"{Environment.ProcessorCount} logical cores")
            };

            systemMetrics.AddRange(BuildGpuMetrics(diagnostics.GpuAdapters, useRuntimeValues: false));
            systemMetrics.AddRange(
            [
                CreatePercentMetric(
                    "RAM",
                    diagnostics.SystemUsage.MemoryPercent,
                    $"{FormatBytes(diagnostics.SystemUsage.MemoryBytes)} / {FormatBytes(diagnostics.SystemUsage.MemoryTotalBytes)}"),
                CreatePercentMetric(
                    "Disk",
                    diagnostics.SystemUsage.DiskBusyPercent,
                    $"R {FormatRate(diagnostics.SystemUsage.DiskReadBytesPerSec)} - W {FormatRate(diagnostics.SystemUsage.DiskWriteBytesPerSec)}"),
                CreateInformationalMetric(
                    "Network",
                    FormatRate(diagnostics.SystemUsage.NetworkReceiveBytesPerSec + diagnostics.SystemUsage.NetworkSendBytesPerSec),
                    $"Rx {FormatRate(diagnostics.SystemUsage.NetworkReceiveBytesPerSec)} - Tx {FormatRate(diagnostics.SystemUsage.NetworkSendBytesPerSec)}",
                    Color.FromArgb("#2264D2FF"))
            ]);

            var runtimeMetrics = new List<HomeSummaryMetricViewModel>
            {
                CreatePercentMetric(
                    "CPU",
                    diagnostics.RuntimeUsage.CpuPercent,
                    "Application host and managed subprocesses")
            };

            runtimeMetrics.AddRange(BuildGpuMetrics(diagnostics.GpuAdapters, useRuntimeValues: true));
            runtimeMetrics.AddRange(
            [
                CreatePercentMetric(
                    "RAM",
                    diagnostics.RuntimeUsage.MemoryPercent,
                    $"{FormatBytes(diagnostics.RuntimeUsage.MemoryBytes)} resident"),
                CreateInformationalMetric(
                    "Disk",
                    FormatRate(diagnostics.RuntimeUsage.DiskIoBytesPerSec),
                    "Combined managed process I/O",
                    Color.FromArgb("#22FF9F0A")),
                CreateInformationalMetric(
                    "Network",
                    FormatConnectionCount(diagnostics.RuntimeUsage.ConnectionCount),
                    "Open TCP and UDP endpoints in the ASLM tree",
                    Color.FromArgb("#2264D2FF"))
            ]);

            return
            [
                new HomeSummaryCardViewModel
                {
                    Key = "system",
                    Title = "System",
                    Subtitle = "Host machine load",
                    StatusText = "Live",
                    AccentOverlayColor = Color.FromArgb("#220A84FF"),
                    FooterText = $"Sampled at {updatedAt}",
                    Metrics = systemMetrics
                },
                new HomeSummaryCardViewModel
                {
                    Key = "runtime",
                    Title = "ASLM Runtime",
                    Subtitle = "App process and managed child tree",
                    StatusText = $"PID {Environment.ProcessId}",
                    AccentOverlayColor = Color.FromArgb("#2232D74B"),
                    FooterText = diagnostics.ActiveProcessCount == 0
                        ? "No managed child processes are currently active."
                        : $"{diagnostics.ActiveProcessCount} live child processes inside the ASLM tree",
                    Metrics = runtimeMetrics
                },
                new HomeSummaryCardViewModel
                {
                    Key = "modules",
                    Title = "Managed Modules",
                    Subtitle = "Lifecycle and console activity",
                    StatusText = $"{modules.Count} total",
                    AccentOverlayColor = Color.FromArgb("#22FF9F0A"),
                    FooterText = diagnostics.LastModuleActivityUtc.HasValue
                        ? $"Last module activity {diagnostics.LastModuleActivityUtc.Value.ToLocalTime():HH:mm:ss}"
                        : "No module activity recorded yet",
                    Metrics =
                    [
                        CreateInformationalMetric(
                            "Installed",
                            modules.Count.ToString(CultureInfo.InvariantCulture),
                            "Modules discovered on disk",
                            Color.FromArgb("#2238383A")),
                        CreateInformationalMetric(
                            "Running",
                            modules.Count(module => module.Status.Enabled).ToString(CultureInfo.InvariantCulture),
                            "Modules marked enabled",
                            Color.FromArgb("#2232D74B")),
                        CreateInformationalMetric(
                            "Processes",
                            diagnostics.ActiveProcessCount.ToString(CultureInfo.InvariantCulture),
                            "Live subprocesses managed by ASLM",
                            Color.FromArgb("#220A84FF")),
                        CreateInformationalMetric(
                            "Sessions",
                            totalSessionCount.ToString(CultureInfo.InvariantCulture),
                            $"{activeSessionCount} active console sessions",
                            Color.FromArgb("#225E5CE6")),
                        CreateInformationalMetric(
                            "Active",
                            diagnostics.ActiveModuleCount.ToString(CultureInfo.InvariantCulture),
                            "Modules with live managed processes",
                            Color.FromArgb("#22FF9F0A"))
                    ]
                }
            ];
        }

        /// <summary>
        /// Builds one compact dashboard row per installed module.
        /// </summary>
        private IReadOnlyList<HomeModuleCardViewModel> BuildModuleCards(
            IReadOnlyList<ModuleConfig> modules,
            HomeDiagnosticsSnapshot diagnostics)
        {
            var rows = new List<HomeModuleCardViewModel>();

            foreach (var module in modules)
            {
                diagnostics.ModulesBySourcePath.TryGetValue(module.SourcePath, out var moduleDiagnostics);
                var operationState = GetOperationState(module.SourcePath);
                var hasConsole = (moduleDiagnostics?.ConsoleSessionCount ?? 0) > 0;

                var statusText = module.Status.Enabled
                    ? "Running"
                    : "Stopped";

                var secondaryParts = new List<string> { statusText };

                if (moduleDiagnostics?.LastActivityUtc.HasValue == true)
                {
                    secondaryParts.Add($"last {moduleDiagnostics.LastActivityUtc.Value.ToLocalTime():HH:mm:ss}");
                }
                else if (!module.Status.Enabled)
                {
                    secondaryParts.Add(BuildModuleSecondaryText(module));
                }

                rows.Add(new HomeModuleCardViewModel
                {
                    SourcePath = module.SourcePath,
                    Name = module.Name,
                    SecondaryText = string.Join(" | ", secondaryParts),
                    CanOpenConsole = module.Status.Enabled || hasConsole,
                    IsBusy = operationState.IsBusy,
                    BusyText = operationState.BusyText,
                    IsRunningAndIdle = module.Status.Enabled && !operationState.IsBusy,
                    IsStoppedAndIdle = !module.Status.Enabled && !operationState.IsBusy,
                    LaunchCommand = new Command(async () => await LaunchModuleAsync(module.SourcePath)),
                    StopCommand = new Command(async () => await StopModuleAsync(module.SourcePath)),
                    RestartCommand = new Command(async () => await RestartModuleAsync(module.SourcePath)),
                    ManageCommand = new Command(() => OpenModuleManagement(module.SourcePath)),
                    ConsoleCommand = new Command(() => OpenModuleConsole(module.SourcePath))
                });
            }

            return rows;
        }

        /// <summary>
        /// Flattens the tree snapshot into a list of visible rows while preserving expand/collapse state.
        /// </summary>
        private IReadOnlyList<HomeProcessTreeNodeViewModel> FlattenTree(HomeProcessTreeNodeSnapshot root)
        {
            var visibleNodes = new List<HomeProcessTreeNodeViewModel>();
            AppendVisibleNode(root, depth: 0, visibleNodes);
            return visibleNodes;
        }

        /// <summary>
        /// Recursively appends one visible tree node and its expanded descendants.
        /// </summary>
        private void AppendVisibleNode(
            HomeProcessTreeNodeSnapshot node,
            int depth,
            List<HomeProcessTreeNodeViewModel> visibleNodes)
        {
            var isExpanded = node.Children.Count == 0
                ? false
                : IsFixedRootNode(node.Key)
                    ? true
                    : GetExpandedState(node.Key, node.DefaultExpanded);

            visibleNodes.Add(new HomeProcessTreeNodeViewModel
            {
                Key = node.Key,
                Title = node.Title,
                TitleColor = node.TitleColor,
                TitleFontAttributes = node.TitleFontAttributes,
                CpuText = node.CpuText,
                GpuText = node.GpuText,
                MemoryText = node.MemoryText,
                DiskText = node.DiskText,
                NetworkText = node.NetworkText,
                DetailsText = node.DetailsText,
                IndentMargin = new Thickness(depth * 18, 0, 0, 0),
                CanExpand = node.Children.Count > 0 && !IsFixedRootNode(node.Key),
                ExpanderText = node.Children.Count == 0 || IsFixedRootNode(node.Key)
                    ? string.Empty
                    : isExpanded
                        ? "▾"
                        : "▸",
                ToggleCommand = new Command(() =>
                {
                    if (node.Children.Count > 0 && !IsFixedRootNode(node.Key))
                    {
                        ToggleNodeExpansion(node.Key);
                    }
                })
            });

            if (!isExpanded)
            {
                return;
            }

            foreach (var child in node.Children)
            {
                AppendVisibleNode(child, depth + 1, visibleNodes);
            }
        }

        /// <summary>
        /// Returns the persisted expansion state for one tree node, or a sensible default.
        /// </summary>
        private bool GetExpandedState(string nodeKey, bool defaultExpanded)
        {
            return _expandedNodes.TryGetValue(nodeKey, out var isExpanded)
                ? isExpanded
                : defaultExpanded;
        }

        /// <summary>
        /// Returns whether the supplied node is the fixed ASLM root.
        /// </summary>
        private static bool IsFixedRootNode(string nodeKey)
        {
            return string.Equals(nodeKey, "root:aslm", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Toggles the expansion state of one tree node and rerenders the visible tree.
        /// </summary>
        private void ToggleNodeExpansion(string nodeKey)
        {
            if (IsFixedRootNode(nodeKey))
            {
                return;
            }

            var current = GetExpandedState(nodeKey, defaultExpanded: false);
            _expandedNodes[nodeKey] = !current;
            RenderCachedState();
        }

        /// <summary>
        /// Re-renders the latest captured dashboard snapshot without sampling diagnostics again.
        /// </summary>
        private void RenderCachedState()
        {
            if (_lastDiagnosticsSnapshot == null)
            {
                return;
            }

            var state = BuildState(_knownModules, _lastConsoleSnapshots, _lastDiagnosticsSnapshot);
            MainThread.BeginInvokeOnMainThread(() => _view.Render(state));
        }

        /// <summary>
        /// Returns the shared operation state object for one module.
        /// </summary>
        private HomeModuleOperationState GetOperationState(string sourcePath)
        {
            if (!_moduleOperations.TryGetValue(sourcePath, out var operationState))
            {
                operationState = new HomeModuleOperationState();
                _moduleOperations[sourcePath] = operationState;
            }

            return operationState;
        }

        // Module actions

        /// <summary>
        /// Launches one module using the same lifecycle as the main modules page.
        /// </summary>
        private async Task LaunchModuleAsync(string sourcePath)
        {
            var operationState = GetOperationState(sourcePath);
            if (operationState.IsBusy)
            {
                return;
            }

            operationState.StartLaunching();
            await RefreshAsync(forceModuleReload: false);

            try
            {
                var module = await ReloadModuleAsync(sourcePath);
                if (module == null)
                {
                    return;
                }

                if (!module.Status.FirstRunCompleted)
                {
                    var setupLog = new Progress<string>(message => Debug.WriteLine($"[Setup:{module.Name}] {message}"));
                    var setupSuccess = await Task.Run(() =>
                        _moduleRunner.ExecuteFirstRunAsync(module, setupLog, CancellationToken.None));

                    if (!setupSuccess)
                    {
                        return;
                    }

                    module.Status.FirstRunCompleted = true;
                    _moduleInstaller.SaveModuleConfig(module);
                }

                module.Status.Enabled = true;
                _moduleInstaller.SaveModuleConfig(module);
                _consoleService.EnsureModule(module);
                _consoleService.UpdateModuleEnabledState(module.SourcePath, true);
                NotifyShellModuleStateChanged();

                if (module.Commands.Run.Count > 0)
                {
                    var launchLog = new Progress<string>(message => Debug.WriteLine($"[Launch:{module.Name}] {message}"));
                    _ = Task.Run(() => _moduleRunner.ExecuteRunAsync(module, launchLog, CancellationToken.None));
                }
            }
            finally
            {
                operationState.Clear();
                await RefreshAsync(forceModuleReload: true);
            }
        }

        /// <summary>
        /// Stops one module and persists the disabled state.
        /// </summary>
        private async Task StopModuleAsync(string sourcePath)
        {
            var operationState = GetOperationState(sourcePath);
            if (operationState.IsBusy)
            {
                return;
            }

            operationState.StartStopping();
            await RefreshAsync(forceModuleReload: false);

            try
            {
                await _moduleRunner.StopModuleAsync(sourcePath);

                var module = await ReloadModuleAsync(sourcePath);
                if (module == null)
                {
                    return;
                }

                module.Status.Enabled = false;
                _moduleInstaller.SaveModuleConfig(module);
                _consoleService.UpdateModuleEnabledState(module.SourcePath, false);
                NotifyShellModuleStateChanged();
            }
            finally
            {
                operationState.Clear();
                await RefreshAsync(forceModuleReload: true);
            }
        }

        /// <summary>
        /// Restarts one module while keeping its enabled state.
        /// </summary>
        private async Task RestartModuleAsync(string sourcePath)
        {
            var operationState = GetOperationState(sourcePath);
            if (operationState.IsBusy)
            {
                return;
            }

            operationState.StartRestarting();
            await RefreshAsync(forceModuleReload: false);

            try
            {
                var module = await ReloadModuleAsync(sourcePath);
                if (module == null)
                {
                    return;
                }

                await _moduleRunner.StopModuleAsync(sourcePath);
                await Task.Delay(1000);

                module.Status.Enabled = true;
                _moduleInstaller.SaveModuleConfig(module);
                _consoleService.EnsureModule(module);
                _consoleService.UpdateModuleEnabledState(module.SourcePath, true);

                if (module.Commands.Run.Count > 0)
                {
                    var restartLog = new Progress<string>(message => Debug.WriteLine($"[Restart:{module.Name}] {message}"));
                    _ = Task.Run(() => _moduleRunner.ExecuteRunAsync(module, restartLog, CancellationToken.None));
                }

                NotifyShellModuleStateChanged();
            }
            finally
            {
                operationState.Clear();
                await RefreshAsync(forceModuleReload: true);
            }
        }

        /// <summary>
        /// Loads the latest module manifest from disk and updates the cached module list.
        /// </summary>
        private async Task<ModuleConfig?> ReloadModuleAsync(string sourcePath)
        {
            var module = await _moduleInstaller.LoadModuleConfig(sourcePath);
            if (module == null)
            {
                return null;
            }

            var existingIndex = _knownModules.FindIndex(candidate =>
                string.Equals(candidate.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));

            if (existingIndex >= 0)
            {
                _knownModules[existingIndex] = module;
            }
            else
            {
                _knownModules.Add(module);
            }

            return module;
        }

        /// <summary>
        /// Opens the full modules page and scrolls the requested module into view.
        /// </summary>
        private void OpenModuleManagement(string sourcePath)
        {
            MainThread.BeginInvokeOnMainThread(() => _shell?.OpenModuleManagement(sourcePath));
        }

        /// <summary>
        /// Opens the consoles page focused on the requested module.
        /// </summary>
        private void OpenModuleConsole(string sourcePath)
        {
            MainThread.BeginInvokeOnMainThread(() => _shell?.OpenConsoles(sourcePath));
        }

        /// <summary>
        /// Notifies the shell that module state changed so shared navigation and related pages can refresh.
        /// </summary>
        private void NotifyShellModuleStateChanged()
        {
            MainThread.BeginInvokeOnMainThread(() => _shell?.OnModuleStateChanged());
        }

        // Formatting helpers

        /// <summary>
        /// Creates a standard percent-based summary metric row.
        /// </summary>
        private static HomeSummaryMetricViewModel CreatePercentMetric(string name, double percentValue, string detailText)
        {
            var accent = GetSeverityColor(percentValue / 100d);
            return new HomeSummaryMetricViewModel
            {
                Name = name,
                ValueText = $"{percentValue:F1}%",
                DetailText = detailText,
                HasProgress = true,
                ProgressValue = Math.Clamp(percentValue / 100d, 0, 1),
                AccentColor = accent
            };
        }

        /// <summary>
        /// Creates a summary metric row for an optional percent-based signal.
        /// </summary>
        private static HomeSummaryMetricViewModel CreateOptionalPercentMetric(string name, double? percentValue, string detailText)
        {
            if (!percentValue.HasValue)
            {
                return new HomeSummaryMetricViewModel
                {
                    Name = name,
                    ValueText = "n/a",
                    DetailText = detailText,
                    HasProgress = false,
                    ProgressValue = 0,
                    AccentColor = Color.FromArgb("#225E5CE6")
                };
            }

            return CreatePercentMetric(name, percentValue.Value, detailText);
        }

        /// <summary>
        /// Creates a summary metric row without a progress bar.
        /// </summary>
        private static HomeSummaryMetricViewModel CreateInformationalMetric(
            string name,
            string valueText,
            string detailText,
            Color accentColor)
        {
            return new HomeSummaryMetricViewModel
            {
                Name = name,
                ValueText = valueText,
                DetailText = detailText,
                HasProgress = false,
                ProgressValue = 0,
                AccentColor = accentColor
            };
        }

        /// <summary>
        /// Builds summary metrics for each detected GPU adapter.
        /// </summary>
        private static IReadOnlyList<HomeSummaryMetricViewModel> BuildGpuMetrics(
            IReadOnlyList<HomeGpuAdapterSnapshot> adapters,
            bool useRuntimeValues)
        {
            if (adapters.Count == 0)
            {
                return
                [
                    new HomeSummaryMetricViewModel
                    {
                        Name = "GPU",
                        ValueText = "n/a",
                        DetailText = "GPU counters unavailable",
                        HasProgress = false,
                        AccentColor = Color.FromArgb("#225E5CE6")
                    }
                ];
            }

            var metrics = new List<HomeSummaryMetricViewModel>();
            foreach (var adapter in adapters.OrderBy(adapter => adapter.AdapterIndex))
            {
                var value = useRuntimeValues
                    ? adapter.RuntimePercent
                    : adapter.SystemPercent;

                var label = $"GPU {adapter.AdapterIndex}";

                metrics.Add(CreatePercentMetric(label, value, adapter.Name));
            }

            return metrics;
        }

        /// <summary>
        /// Builds the compact resource badges shown on module cards and tree rows.
        /// </summary>
        internal static IReadOnlyList<HomeMetricBadgeViewModel> BuildUsageBadges(
            HomeUsageSnapshot usage,
            int processCount,
            int sessionCount)
        {
            var badges = new List<HomeMetricBadgeViewModel>
            {
                CreateBadge($"CPU {usage.CpuPercent:F1}%", Color.FromArgb("#220A84FF"))
            };

            badges.Add(usage.GpuPercent.HasValue
                ? CreateBadge($"GPU {usage.GpuPercent.Value:F1}%", Color.FromArgb("#225E5CE6"))
                : CreateBadge("GPU n/a", Color.FromArgb("#2238383A")));

            badges.Add(CreateBadge($"RAM {FormatBytes(usage.MemoryBytes)}", Color.FromArgb("#2232D74B")));
            badges.Add(CreateBadge($"Disk {FormatRate(usage.DiskIoBytesPerSec)}", Color.FromArgb("#22FF9F0A")));
            badges.Add(CreateBadge($"Net {FormatConnectionCount(usage.ConnectionCount)}", Color.FromArgb("#2264D2FF")));

            if (processCount > 0)
            {
                badges.Add(CreateBadge($"Proc {processCount}", Color.FromArgb("#220A84FF")));
            }

            if (sessionCount > 0)
            {
                badges.Add(CreateBadge($"Sess {sessionCount}", Color.FromArgb("#225E5CE6")));
            }

            return badges;
        }

        /// <summary>
        /// Creates one compact badge view model.
        /// </summary>
        private static HomeMetricBadgeViewModel CreateBadge(string text, Color backgroundColor)
        {
            return new HomeMetricBadgeViewModel
            {
                Text = text,
                BackgroundColor = backgroundColor
            };
        }

        /// <summary>
        /// Builds the secondary text shown under one module card title.
        /// </summary>
        private static string BuildModuleSecondaryText(ModuleConfig module)
        {
            if (!string.IsNullOrWhiteSpace(module.Description))
            {
                return module.Description;
            }

            return string.IsNullOrWhiteSpace(module.Author)
                ? "Installed module"
                : $"by {module.Author}";
        }

        /// <summary>
        /// Formats byte counts using a compact binary unit scale.
        /// </summary>
        internal static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double size = bytes;
            var unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return size >= 100 || unitIndex == 0
                ? $"{size:F0} {units[unitIndex]}"
                : $"{size:F1} {units[unitIndex]}";
        }

        /// <summary>
        /// Formats a byte throughput value using a compact binary unit scale.
        /// </summary>
        internal static string FormatRate(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0)
            {
                return "0 B/s";
            }

            string[] units = ["B/s", "KB/s", "MB/s", "GB/s", "TB/s"];
            var size = bytesPerSecond;
            var unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return size >= 100 || unitIndex == 0
                ? $"{size:F0} {units[unitIndex]}"
                : $"{size:F1} {units[unitIndex]}";
        }

        /// <summary>
        /// Formats a compact network-connection count.
        /// </summary>
        private static string FormatConnectionCount(int connectionCount)
        {
            return connectionCount == 1
                ? "1 conn"
                : $"{connectionCount} conn";
        }

        /// <summary>
        /// Formats a percent value using the compact dashboard style.
        /// </summary>
        internal static string FormatPercent(double value)
        {
            return $"{value:F1}%";
        }

        /// <summary>
        /// Formats an optional percent value or returns a fallback placeholder.
        /// </summary>
        internal static string FormatPercentOrNa(double? value)
        {
            return value.HasValue
                ? $"{value.Value:F1}%"
                : "n/a";
        }

        /// <summary>
        /// Formats compact integer counts for narrow table columns.
        /// </summary>
        internal static string FormatCompactCount(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns a severity color for one normalized usage value.
        /// </summary>
        private static Color GetSeverityColor(double normalizedValue)
        {
            if (normalizedValue >= 0.8)
            {
                return Color.FromArgb("#FFFF453A");
            }

            if (normalizedValue >= 0.5)
            {
                return Color.FromArgb("#FFFFD60A");
            }

            return Color.FromArgb("#FF32D74B");
        }
    }


    // Dashboard state

    /// <summary>
    /// Represents the full UI state required to render the home dashboard.
    /// </summary>
    internal sealed class HomeDashboardState
    {
        /// <summary>
        /// Gets or sets the page subtitle.
        /// </summary>
        public string Subtitle { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the caption shown above the managed process tree.
        /// </summary>
        public string ProcessTreeCaption { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the caption shown above the modules panel.
        /// </summary>
        public string ModulesCaption { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the summary cards shown above the main workspace.
        /// </summary>
        public IReadOnlyList<HomeSummaryCardViewModel> SummaryCards { get; set; } = [];

        /// <summary>
        /// Gets or sets the visible rows of the managed process tree.
        /// </summary>
        public IReadOnlyList<HomeProcessTreeNodeViewModel> ProcessNodes { get; set; } = [];

        /// <summary>
        /// Gets or sets the module dashboard rows.
        /// </summary>
        public IReadOnlyList<HomeModuleCardViewModel> Modules { get; set; } = [];
    }

    /// <summary>
    /// Stores one fully prepared refresh payload before it is rendered on the UI thread.
    /// </summary>
    internal sealed class HomeRefreshSnapshot
    {
        /// <summary>
        /// Gets or sets the latest console snapshots.
        /// </summary>
        public IReadOnlyList<ModuleConsoleModuleSnapshot> ConsoleSnapshots { get; set; } = [];

        /// <summary>
        /// Gets or sets the latest diagnostics snapshot.
        /// </summary>
        public HomeDiagnosticsSnapshot Diagnostics { get; set; } = new();

        /// <summary>
        /// Gets or sets the render-ready dashboard state.
        /// </summary>
        public HomeDashboardState State { get; set; } = new();
    }


    // Page view model

    /// <summary>
    /// Exposes bindable dashboard state for the home page shell.
    /// </summary>
    public sealed class HomeDashboardPageViewModel : INotifyPropertyChanged
    {
        private string _subtitle = string.Empty;
        private string _processTreeCaption = string.Empty;
        private string _modulesCaption = string.Empty;
        private int _summaryGridSpan = 1;
        private double _processTableColumnSpacing = 10;
        private double _processTableWidth = 920;
        private GridLength _processNameColumnWidth = new(520);
        private GridLength _processCpuColumnWidth = new(60);
        private GridLength _processGpuColumnWidth = new(60);
        private GridLength _processMemoryColumnWidth = new(86);
        private GridLength _processDiskColumnWidth = new(82);
        private GridLength _processNetworkColumnWidth = new(74);
        private GridLength _processDetailsColumnWidth = new(140);
        private double _modulesTableColumnSpacing = 8;
        private double _modulesTableWidth = 520;
        private GridLength _modulesNameColumnWidth = new(320);
        private GridLength _modulesActionsColumnWidth = new(132);

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the summary cards shown at the top of the dashboard.
        /// </summary>
        public ObservableCollection<HomeSummaryCardViewModel> SummaryCards { get; } = new();

        /// <summary>
        /// Gets the visible rows of the managed process tree.
        /// </summary>
        public ObservableCollection<HomeProcessTreeNodeViewModel> ProcessNodes { get; } = new();

        /// <summary>
        /// Gets the module rows shown in the modules panel.
        /// </summary>
        public ObservableCollection<HomeModuleCardViewModel> Modules { get; } = new();

        /// <summary>
        /// Gets or sets the page subtitle.
        /// </summary>
        public string Subtitle
        {
            get => _subtitle;
            set => SetProperty(ref _subtitle, value);
        }

        /// <summary>
        /// Gets or sets the caption shown above the managed process tree.
        /// </summary>
        public string ProcessTreeCaption
        {
            get => _processTreeCaption;
            set => SetProperty(ref _processTreeCaption, value);
        }

        /// <summary>
        /// Gets or sets the caption shown above the modules panel.
        /// </summary>
        public string ModulesCaption
        {
            get => _modulesCaption;
            set => SetProperty(ref _modulesCaption, value);
        }

        /// <summary>
        /// Gets or sets the number of columns used by the summary-card grid.
        /// </summary>
        public int SummaryGridSpan
        {
            get => _summaryGridSpan;
            set => SetProperty(ref _summaryGridSpan, value);
        }

        /// <summary>
        /// Gets or sets the spacing used by the process table.
        /// </summary>
        public double ProcessTableColumnSpacing
        {
            get => _processTableColumnSpacing;
            set => SetProperty(ref _processTableColumnSpacing, value);
        }

        /// <summary>
        /// Gets or sets the minimum width of the horizontally scrollable process table.
        /// </summary>
        public double ProcessTableWidth
        {
            get => _processTableWidth;
            set => SetProperty(ref _processTableWidth, value);
        }

        /// <summary>
        /// Gets or sets the name column width in the process table.
        /// </summary>
        public GridLength ProcessNameColumnWidth
        {
            get => _processNameColumnWidth;
            set => SetProperty(ref _processNameColumnWidth, value);
        }

        /// <summary>
        /// Gets or sets the CPU column width in the process table.
        /// </summary>
        public GridLength ProcessCpuColumnWidth
        {
            get => _processCpuColumnWidth;
            set => SetProperty(ref _processCpuColumnWidth, value);
        }

        /// <summary>
        /// Gets or sets the GPU column width in the process table.
        /// </summary>
        public GridLength ProcessGpuColumnWidth
        {
            get => _processGpuColumnWidth;
            set => SetProperty(ref _processGpuColumnWidth, value);
        }

        /// <summary>
        /// Gets or sets the memory column width in the process table.
        /// </summary>
        public GridLength ProcessMemoryColumnWidth
        {
            get => _processMemoryColumnWidth;
            set => SetProperty(ref _processMemoryColumnWidth, value);
        }

        /// <summary>
        /// Gets or sets the disk column width in the process table.
        /// </summary>
        public GridLength ProcessDiskColumnWidth
        {
            get => _processDiskColumnWidth;
            set => SetProperty(ref _processDiskColumnWidth, value);
        }

        /// <summary>
        /// Gets or sets the network column width in the process table.
        /// </summary>
        public GridLength ProcessNetworkColumnWidth
        {
            get => _processNetworkColumnWidth;
            set => SetProperty(ref _processNetworkColumnWidth, value);
        }

        /// <summary>
        /// Gets or sets the details column width in the process table.
        /// </summary>
        public GridLength ProcessDetailsColumnWidth
        {
            get => _processDetailsColumnWidth;
            set => SetProperty(ref _processDetailsColumnWidth, value);
        }

        /// <summary>
        /// Gets or sets the spacing used by the modules table.
        /// </summary>
        public double ModulesTableColumnSpacing
        {
            get => _modulesTableColumnSpacing;
            set => SetProperty(ref _modulesTableColumnSpacing, value);
        }

        /// <summary>
        /// Gets or sets the minimum width of the horizontally scrollable modules table.
        /// </summary>
        public double ModulesTableWidth
        {
            get => _modulesTableWidth;
            set => SetProperty(ref _modulesTableWidth, value);
        }

        /// <summary>
        /// Gets or sets the name column width in the modules table.
        /// </summary>
        public GridLength ModulesNameColumnWidth
        {
            get => _modulesNameColumnWidth;
            set => SetProperty(ref _modulesNameColumnWidth, value);
        }

        /// <summary>
        /// Gets or sets the actions column width in the modules table.
        /// </summary>
        public GridLength ModulesActionsColumnWidth
        {
            get => _modulesActionsColumnWidth;
            set => SetProperty(ref _modulesActionsColumnWidth, value);
        }

        /// <summary>
        /// Updates a bindable field and raises <see cref="PropertyChanged"/> when the value changes.
        /// </summary>
        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    // Bindable view models

    /// <summary>
    /// Provides a small bindable base implementation for dashboard item models.
    /// </summary>
    public abstract class HomeBindableItemViewModel : INotifyPropertyChanged
    {
        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Updates a bindable field and raises <see cref="PropertyChanged"/> when it changes.
        /// </summary>
        protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents one summary card on the home dashboard.
    /// </summary>
    public sealed class HomeSummaryCardViewModel : HomeBindableItemViewModel
    {
        private string _key = string.Empty;
        private string _title = string.Empty;
        private string _subtitle = string.Empty;
        private string _statusText = string.Empty;
        private Color _accentOverlayColor = Color.FromArgb("#220A84FF");
        private IReadOnlyList<HomeSummaryMetricViewModel> _metrics = [];
        private string _footerText = string.Empty;

        /// <summary>
        /// Gets or sets the stable card key.
        /// </summary>
        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        /// <summary>
        /// Gets or sets the card title.
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets or sets the card subtitle.
        /// </summary>
        public string Subtitle
        {
            get => _subtitle;
            set => SetProperty(ref _subtitle, value);
        }

        /// <summary>
        /// Gets or sets the status pill text shown on the card.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Gets or sets the accent overlay color used by the status pill.
        /// </summary>
        public Color AccentOverlayColor
        {
            get => _accentOverlayColor;
            set => SetProperty(ref _accentOverlayColor, value);
        }

        /// <summary>
        /// Gets or sets the summary metrics shown inside the card.
        /// </summary>
        public IReadOnlyList<HomeSummaryMetricViewModel> Metrics
        {
            get => _metrics;
            set => SetProperty(ref _metrics, value);
        }

        /// <summary>
        /// Gets or sets the footer text shown at the bottom of the card.
        /// </summary>
        public string FooterText
        {
            get => _footerText;
            set => SetProperty(ref _footerText, value);
        }

        /// <summary>
        /// Applies the values from the latest presenter snapshot.
        /// </summary>
        public void CopyFrom(HomeSummaryCardViewModel source)
        {
            Key = source.Key;
            Title = source.Title;
            Subtitle = source.Subtitle;
            StatusText = source.StatusText;
            AccentOverlayColor = source.AccentOverlayColor;
            Metrics = source.Metrics;
            FooterText = source.FooterText;
        }
    }

    /// <summary>
    /// Represents one summary metric row inside a home dashboard card.
    /// </summary>
    public sealed class HomeSummaryMetricViewModel
    {
        /// <summary>
        /// Gets or sets the metric label.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the primary metric value.
        /// </summary>
        public string ValueText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the secondary explanatory text.
        /// </summary>
        public string DetailText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether a progress bar should be rendered for this metric.
        /// </summary>
        public bool HasProgress { get; set; }

        /// <summary>
        /// Gets or sets the normalized progress value.
        /// </summary>
        public double ProgressValue { get; set; }

        /// <summary>
        /// Gets or sets the accent color of the metric.
        /// </summary>
        public Color AccentColor { get; set; } = Color.FromArgb("#FF32D74B");
    }

    /// <summary>
    /// Represents one compact metric badge.
    /// </summary>
    public sealed class HomeMetricBadgeViewModel
    {
        /// <summary>
        /// Gets or sets the badge text.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the badge background color.
        /// </summary>
        public Color BackgroundColor { get; set; } = Color.FromArgb("#2238383A");
    }

    /// <summary>
    /// Represents one module row in the home dashboard.
    /// </summary>
    public sealed class HomeModuleCardViewModel : HomeBindableItemViewModel
    {
        private string _sourcePath = string.Empty;
        private string _name = string.Empty;
        private string _secondaryText = string.Empty;
        private bool _canOpenConsole;
        private bool _isBusy;
        private string _busyText = string.Empty;
        private bool _isRunningAndIdle;
        private bool _isStoppedAndIdle;
        private ICommand _launchCommand = new Command(() => { });
        private ICommand _stopCommand = new Command(() => { });
        private ICommand _restartCommand = new Command(() => { });
        private ICommand _manageCommand = new Command(() => { });
        private ICommand _consoleCommand = new Command(() => { });

        /// <summary>
        /// Gets or sets the stable module source path.
        /// </summary>
        public string SourcePath
        {
            get => _sourcePath;
            set => SetProperty(ref _sourcePath, value);
        }

        /// <summary>
        /// Gets or sets the module name.
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Gets or sets the secondary module description line.
        /// </summary>
        public string SecondaryText
        {
            get => _secondaryText;
            set => SetProperty(ref _secondaryText, value);
        }

        /// <summary>
        /// Gets or sets whether the console button should stay enabled.
        /// </summary>
        public bool CanOpenConsole
        {
            get => _canOpenConsole;
            set => SetProperty(ref _canOpenConsole, value);
        }

        /// <summary>
        /// Gets or sets whether the row is currently showing a long-running module action.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        /// <summary>
        /// Gets or sets the busy-state text.
        /// </summary>
        public string BusyText
        {
            get => _busyText;
            set => SetProperty(ref _busyText, value);
        }

        /// <summary>
        /// Gets or sets whether the running-state button row is visible.
        /// </summary>
        public bool IsRunningAndIdle
        {
            get => _isRunningAndIdle;
            set => SetProperty(ref _isRunningAndIdle, value);
        }

        /// <summary>
        /// Gets or sets whether the stopped-state launch button is visible.
        /// </summary>
        public bool IsStoppedAndIdle
        {
            get => _isStoppedAndIdle;
            set => SetProperty(ref _isStoppedAndIdle, value);
        }

        /// <summary>
        /// Gets or sets the launch command.
        /// </summary>
        public ICommand LaunchCommand
        {
            get => _launchCommand;
            set => SetProperty(ref _launchCommand, value);
        }

        /// <summary>
        /// Gets or sets the stop command.
        /// </summary>
        public ICommand StopCommand
        {
            get => _stopCommand;
            set => SetProperty(ref _stopCommand, value);
        }

        /// <summary>
        /// Gets or sets the restart command.
        /// </summary>
        public ICommand RestartCommand
        {
            get => _restartCommand;
            set => SetProperty(ref _restartCommand, value);
        }

        /// <summary>
        /// Gets or sets the module-management navigation command.
        /// </summary>
        public ICommand ManageCommand
        {
            get => _manageCommand;
            set => SetProperty(ref _manageCommand, value);
        }

        /// <summary>
        /// Gets or sets the console-navigation command.
        /// </summary>
        public ICommand ConsoleCommand
        {
            get => _consoleCommand;
            set => SetProperty(ref _consoleCommand, value);
        }

        /// <summary>
        /// Applies the values from the latest presenter snapshot.
        /// </summary>
        public void CopyFrom(HomeModuleCardViewModel source)
        {
            SourcePath = source.SourcePath;
            Name = source.Name;
            SecondaryText = source.SecondaryText;
            CanOpenConsole = source.CanOpenConsole;
            IsBusy = source.IsBusy;
            BusyText = source.BusyText;
            IsRunningAndIdle = source.IsRunningAndIdle;
            IsStoppedAndIdle = source.IsStoppedAndIdle;
            LaunchCommand = source.LaunchCommand;
            StopCommand = source.StopCommand;
            RestartCommand = source.RestartCommand;
            ManageCommand = source.ManageCommand;
            ConsoleCommand = source.ConsoleCommand;
        }
    }

    /// <summary>
    /// Represents one visible row of the managed process tree.
    /// </summary>
    public sealed class HomeProcessTreeNodeViewModel : HomeBindableItemViewModel
    {
        private string _key = string.Empty;
        private string _title = string.Empty;
        private Color _titleColor = Color.FromArgb("#FFFFFFFF");
        private FontAttributes _titleFontAttributes = FontAttributes.None;
        private string _cpuText = string.Empty;
        private string _gpuText = string.Empty;
        private string _memoryText = string.Empty;
        private string _diskText = string.Empty;
        private string _networkText = string.Empty;
        private string _detailsText = string.Empty;
        private Thickness _indentMargin;
        private bool _canExpand;
        private string _expanderText = string.Empty;
        private ICommand _toggleCommand = new Command(() => { });

        /// <summary>
        /// Gets or sets the stable row key.
        /// </summary>
        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        /// <summary>
        /// Gets or sets the row title.
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets or sets the row title color.
        /// </summary>
        public Color TitleColor
        {
            get => _titleColor;
            set => SetProperty(ref _titleColor, value);
        }

        /// <summary>
        /// Gets or sets the title font weight.
        /// </summary>
        public FontAttributes TitleFontAttributes
        {
            get => _titleFontAttributes;
            set => SetProperty(ref _titleFontAttributes, value);
        }

        /// <summary>
        /// Gets or sets the CPU column text.
        /// </summary>
        public string CpuText
        {
            get => _cpuText;
            set => SetProperty(ref _cpuText, value);
        }

        /// <summary>
        /// Gets or sets the GPU column text.
        /// </summary>
        public string GpuText
        {
            get => _gpuText;
            set => SetProperty(ref _gpuText, value);
        }

        /// <summary>
        /// Gets or sets the memory column text.
        /// </summary>
        public string MemoryText
        {
            get => _memoryText;
            set => SetProperty(ref _memoryText, value);
        }

        /// <summary>
        /// Gets or sets the disk column text.
        /// </summary>
        public string DiskText
        {
            get => _diskText;
            set => SetProperty(ref _diskText, value);
        }

        /// <summary>
        /// Gets or sets the network column text.
        /// </summary>
        public string NetworkText
        {
            get => _networkText;
            set => SetProperty(ref _networkText, value);
        }

        /// <summary>
        /// Gets or sets the trailing details column text.
        /// </summary>
        public string DetailsText
        {
            get => _detailsText;
            set => SetProperty(ref _detailsText, value);
        }

        /// <summary>
        /// Gets or sets the left margin used to indent the row according to tree depth.
        /// </summary>
        public Thickness IndentMargin
        {
            get => _indentMargin;
            set => SetProperty(ref _indentMargin, value);
        }

        /// <summary>
        /// Gets or sets whether the row can be expanded or collapsed.
        /// </summary>
        public bool CanExpand
        {
            get => _canExpand;
            set => SetProperty(ref _canExpand, value);
        }

        /// <summary>
        /// Gets or sets the current expander button text.
        /// </summary>
        public string ExpanderText
        {
            get => _expanderText;
            set => SetProperty(ref _expanderText, value);
        }

        /// <summary>
        /// Gets or sets the command used to toggle expansion.
        /// </summary>
        public ICommand ToggleCommand
        {
            get => _toggleCommand;
            set => SetProperty(ref _toggleCommand, value);
        }

        /// <summary>
        /// Applies the values from the latest presenter snapshot.
        /// </summary>
        public void CopyFrom(HomeProcessTreeNodeViewModel source)
        {
            Key = source.Key;
            Title = source.Title;
            TitleColor = source.TitleColor;
            TitleFontAttributes = source.TitleFontAttributes;
            CpuText = source.CpuText;
            GpuText = source.GpuText;
            MemoryText = source.MemoryText;
            DiskText = source.DiskText;
            NetworkText = source.NetworkText;
            DetailsText = source.DetailsText;
            IndentMargin = source.IndentMargin;
            CanExpand = source.CanExpand;
            ExpanderText = source.ExpanderText;
            ToggleCommand = source.ToggleCommand;
        }
    }


    // Presenter support state

    /// <summary>
    /// Tracks the busy state of one module action initiated from the home dashboard.
    /// </summary>
    internal sealed class HomeModuleOperationState
    {
        private string _busyText = string.Empty;

        /// <summary>
        /// Gets whether the module is currently processing an action.
        /// </summary>
        public bool IsBusy { get; private set; }

        /// <summary>
        /// Gets the user-visible busy-state text.
        /// </summary>
        public string BusyText => _busyText;

        /// <summary>
        /// Marks the module as launching.
        /// </summary>
        public void StartLaunching()
        {
            IsBusy = true;
            _busyText = "Launching";
        }

        /// <summary>
        /// Marks the module as stopping.
        /// </summary>
        public void StartStopping()
        {
            IsBusy = true;
            _busyText = "Stopping";
        }

        /// <summary>
        /// Marks the module as restarting.
        /// </summary>
        public void StartRestarting()
        {
            IsBusy = true;
            _busyText = "Restarting";
        }

        /// <summary>
        /// Clears the busy state after the module action completes.
        /// </summary>
        public void Clear()
        {
            IsBusy = false;
            _busyText = string.Empty;
        }
    }


    // Diagnostics collector

    /// <summary>
    /// Captures live system and process diagnostics used by the home dashboard.
    /// </summary>
    internal sealed class HomeDiagnosticsCollector
    {
        private static readonly Regex GpuInstanceRegex =
            new(@"pid_(\d+).*?phys_(\d+).*?eng_(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static IReadOnlyList<string>? _cachedGpuAdapterNames;

        private readonly ProcessSnapshotService _processSnapshotService;
        private readonly Dictionary<int, HomeProcessSample> _processSamples = new();

        private HomeSystemCpuSample? _lastSystemCpuSample;
        private HomeNetworkSample? _lastNetworkSample;
        private HomeDiskStats _cachedDiskStats = new();
        private DateTimeOffset _cachedDiskStatsUtc = DateTimeOffset.MinValue;
        private HomeGpuQueryResult _cachedGpuQuery = new();
        private DateTimeOffset _cachedGpuQueryUtc = DateTimeOffset.MinValue;
        private Dictionary<int, int> _cachedConnectionCounts = new();
        private HashSet<int> _cachedConnectionProcessIds = new();
        private DateTimeOffset _cachedConnectionCountsUtc = DateTimeOffset.MinValue;

        public HomeDiagnosticsCollector(ProcessSnapshotService processSnapshotService)
        {
            _processSnapshotService = processSnapshotService;
        }

        /// <summary>
        /// Captures a complete diagnostics snapshot for the current dashboard refresh.
        /// </summary>
        public HomeDiagnosticsSnapshot Capture(
            IReadOnlyList<ModuleConfig> modules,
            IReadOnlyList<ModuleConsoleModuleSnapshot> consoleSnapshots)
        {
            var capturedUtc = DateTimeOffset.UtcNow;
            var currentProcessId = Environment.ProcessId;
            var processSnapshotEntries = _processSnapshotService.GetSnapshot(TimeSpan.FromMilliseconds(500));
            var entriesByPid = processSnapshotEntries.ToDictionary(entry => entry.ProcessId);
            var childrenByParent = processSnapshotEntries
                .GroupBy(entry => entry.ParentProcessId)
                .ToDictionary(group => group.Key, group => group.Select(entry => entry.ProcessId).ToList());

            var managedPids = CollectDescendantPids(currentProcessId, childrenByParent);
            managedPids.Add(currentProcessId);

            var memoryStatus = GetMemoryStatus();
            var systemCpu = SampleSystemCpu(capturedUtc);
            var diskStats = GetDiskStatsSample(capturedUtc);
            var networkStats = SampleNetwork(capturedUtc);
            var gpuQuery = GetGpuUsageSample(capturedUtc);
            var gpuAdapters = BuildGpuAdapterSnapshots(gpuQuery, managedPids);
            var connectionCounts = GetConnectionCountsSample(capturedUtc, managedPids);

            var liveProcesses = new Dictionary<int, HomeLiveProcessInfo>();
            foreach (var processId in managedPids.OrderBy(processId => processId))
            {
                if (TrySampleProcess(
                        processId,
                        capturedUtc,
                        entriesByPid,
                        gpuQuery.ProcessByPid,
                        connectionCounts,
                        memoryStatus.TotalPhysicalBytes,
                        out var liveProcess))
                {
                    liveProcesses[processId] = liveProcess;
                }
            }

            CleanupProcessSamples(liveProcesses.Keys);

            var moduleDiagnostics = BuildModuleDiagnostics(
                modules,
                consoleSnapshots,
                liveProcesses,
                entriesByPid,
                childrenByParent,
                currentProcessId,
                memoryStatus.TotalPhysicalBytes);

            var assignedProcessIds = moduleDiagnostics.Values
                .SelectMany(module => module.ProcessIds)
                .ToHashSet();

            var internalProcessIds = liveProcesses.Keys
                .Where(processId => processId != currentProcessId && !assignedProcessIds.Contains(processId))
                .ToHashSet();

            var runtimeUsage = AggregateUsage(liveProcesses.Values.Select(process => process.Usage), memoryStatus.TotalPhysicalBytes);
            runtimeUsage.GpuPercent = gpuAdapters.Count > 0 ? gpuAdapters.Max(adapter => adapter.RuntimePercent) : null;
            var rootNode = BuildRootNode(
                currentProcessId,
                liveProcesses,
                moduleDiagnostics.Values,
                internalProcessIds,
                childrenByParent,
                entriesByPid,
                memoryStatus.TotalPhysicalBytes);

            var lastModuleActivities = moduleDiagnostics.Values
                .Where(module => module.LastActivityUtc.HasValue)
                .Select(module => module.LastActivityUtc!.Value)
                .ToList();

            return new HomeDiagnosticsSnapshot
            {
                CapturedUtc = capturedUtc,
                SystemUsage = new HomeUsageSnapshot
                {
                    CpuPercent = systemCpu,
                    GpuPercent = gpuAdapters.Count > 0 ? gpuAdapters.Max(adapter => adapter.SystemPercent) : null,
                    MemoryBytes = memoryStatus.UsedPhysicalBytes,
                    MemoryTotalBytes = memoryStatus.TotalPhysicalBytes,
                    DiskBusyPercent = diskStats.BusyPercent,
                    DiskReadBytesPerSec = diskStats.ReadBytesPerSecond,
                    DiskWriteBytesPerSec = diskStats.WriteBytesPerSecond,
                    NetworkReceiveBytesPerSec = networkStats.ReceiveBytesPerSecond,
                    NetworkSendBytesPerSec = networkStats.SendBytesPerSecond
                },
                RuntimeUsage = runtimeUsage,
                ModulesBySourcePath = moduleDiagnostics,
                GpuAdapters = gpuAdapters,
                RootNode = rootNode,
                ActiveProcessCount = internalProcessIds.Count + moduleDiagnostics.Values.Sum(module => module.ProcessIds.Count),
                ActiveModuleCount = moduleDiagnostics.Values.Count(module => module.ProcessIds.Count > 0),
                LastModuleActivityUtc = lastModuleActivities.Count > 0
                    ? lastModuleActivities.Max()
                    : null
            };
        }

        /// <summary>
        /// Returns a cached disk sample when the previous WMI query is still fresh enough.
        /// </summary>
        private HomeDiskStats GetDiskStatsSample(DateTimeOffset capturedUtc)
        {
            if ((capturedUtc - _cachedDiskStatsUtc).TotalMilliseconds < 3000)
            {
                return _cachedDiskStats;
            }

            _cachedDiskStats = QueryDiskStats();
            _cachedDiskStatsUtc = capturedUtc;
            return _cachedDiskStats;
        }

        /// <summary>
        /// Returns a cached GPU sample when the previous WMI query is still fresh enough.
        /// </summary>
        private HomeGpuQueryResult GetGpuUsageSample(DateTimeOffset capturedUtc)
        {
            if ((capturedUtc - _cachedGpuQueryUtc).TotalMilliseconds < 3000)
            {
                return _cachedGpuQuery;
            }

            _cachedGpuQuery = QueryGpuUsage();
            _cachedGpuQueryUtc = capturedUtc;
            return _cachedGpuQuery;
        }

        /// <summary>
        /// Returns cached per-process connection counts while the managed PID set is unchanged.
        /// </summary>
        private IReadOnlyDictionary<int, int> GetConnectionCountsSample(DateTimeOffset capturedUtc, IReadOnlyCollection<int> processIds)
        {
            if ((capturedUtc - _cachedConnectionCountsUtc).TotalMilliseconds < 2000 &&
                _cachedConnectionProcessIds.SetEquals(processIds))
            {
                return _cachedConnectionCounts;
            }

            _cachedConnectionCounts = QueryProcessConnectionCounts(processIds);
            _cachedConnectionProcessIds = processIds.ToHashSet();
            _cachedConnectionCountsUtc = capturedUtc;
            return _cachedConnectionCounts;
        }

        // Module diagnostics

        /// <summary>
        /// Builds one diagnostics snapshot per module from live processes and console sessions.
        /// </summary>
        private static Dictionary<string, HomeModuleDiagnostics> BuildModuleDiagnostics(
            IReadOnlyList<ModuleConfig> modules,
            IReadOnlyList<ModuleConsoleModuleSnapshot> consoleSnapshots,
            IReadOnlyDictionary<int, HomeLiveProcessInfo> liveProcesses,
            IReadOnlyDictionary<int, ProcessSnapshotEntry> entriesByPid,
            IReadOnlyDictionary<int, List<int>> childrenByParent,
            int currentProcessId,
            long totalPhysicalBytes)
        {
            var diagnostics = new Dictionary<string, HomeModuleDiagnostics>(StringComparer.OrdinalIgnoreCase);
            var consoleByModule = consoleSnapshots.ToDictionary(snapshot => snapshot.SourcePath, StringComparer.OrdinalIgnoreCase);
            var globallyAssigned = new HashSet<int>();

            foreach (var module in modules)
            {
                consoleByModule.TryGetValue(module.SourcePath, out var consoleSnapshot);

                var sessionsByPid = (consoleSnapshot?.Sessions ?? [])
                    .Where(session => session.ProcessId.HasValue && !session.IsObservedProcess)
                    .GroupBy(session => session.ProcessId!.Value)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .OrderByDescending(session => session.IsRunning)
                            .ThenByDescending(session => session.IsTrackedProcess)
                            .First());

                var observedProcessIds = (consoleSnapshot?.Sessions ?? [])
                    .Where(session => session.IsObservedProcess &&
                                      session.ProcessId.HasValue)
                    .Select(session => session.ProcessId!.Value)
                    .ToHashSet();

                var trackedRootPids = (consoleSnapshot?.Sessions ?? [])
                    .Where(session => session.IsRunning &&
                                      session.IsTrackedProcess &&
                                      !session.IsObservedProcess &&
                                      session.ProcessId.HasValue &&
                                      liveProcesses.ContainsKey(session.ProcessId.Value))
                    .Select(session => session.ProcessId!.Value)
                    .Distinct()
                    .ToList();

                var processIds = new HashSet<int>();

                foreach (var rootProcessId in trackedRootPids)
                {
                    CollectProcessBranch(rootProcessId, childrenByParent, liveProcesses, processIds);
                }

                foreach (var processId in sessionsByPid.Keys.Where(liveProcesses.ContainsKey))
                {
                    processIds.Add(processId);
                }

                processIds.ExceptWith(observedProcessIds);

                processIds.RemoveWhere(globallyAssigned.Contains);
                globallyAssigned.UnionWith(processIds);

                var rootProcessIds = trackedRootPids
                    .Where(processIds.Contains)
                    .Distinct()
                    .ToList();

                if (rootProcessIds.Count == 0 && processIds.Count > 0)
                {
                    rootProcessIds = processIds
                        .Where(processId =>
                        {
                            if (!entriesByPid.TryGetValue(processId, out var entry))
                            {
                                return true;
                            }

                            return entry.ParentProcessId == currentProcessId || !processIds.Contains(entry.ParentProcessId);
                        })
                        .OrderBy(processId => processId)
                        .ToList();
                }

                diagnostics[module.SourcePath] = new HomeModuleDiagnostics
                {
                    Module = module,
                    ConsoleSnapshot = consoleSnapshot,
                    ProcessIds = processIds,
                    RootProcessIds = rootProcessIds,
                    SessionsByPid = sessionsByPid,
                    Usage = AggregateUsage(
                        processIds
                            .Where(liveProcesses.ContainsKey)
                            .Select(processId => liveProcesses[processId].Usage),
                        totalPhysicalBytes)
                };
            }

            return diagnostics;
        }

        /// <summary>
        /// Collects one process branch and all of its descendants into the supplied set.
        /// </summary>
        private static void CollectProcessBranch(
            int rootProcessId,
            IReadOnlyDictionary<int, List<int>> childrenByParent,
            IReadOnlyDictionary<int, HomeLiveProcessInfo> liveProcesses,
            ISet<int> results)
        {
            if (!liveProcesses.ContainsKey(rootProcessId) || !results.Add(rootProcessId))
            {
                return;
            }

            if (!childrenByParent.TryGetValue(rootProcessId, out var children))
            {
                return;
            }

            foreach (var childProcessId in children)
            {
                CollectProcessBranch(childProcessId, childrenByParent, liveProcesses, results);
            }
        }

        // Tree construction

        /// <summary>
        /// Builds the full ASLM root tree used by the dashboard process panel.
        /// </summary>
        private static HomeProcessTreeNodeSnapshot BuildRootNode(
            int currentProcessId,
            IReadOnlyDictionary<int, HomeLiveProcessInfo> liveProcesses,
            IReadOnlyCollection<HomeModuleDiagnostics> moduleDiagnostics,
            ISet<int> internalProcessIds,
            IReadOnlyDictionary<int, List<int>> childrenByParent,
            IReadOnlyDictionary<int, ProcessSnapshotEntry> entriesByPid,
            long totalPhysicalBytes)
        {
            var moduleNodes = moduleDiagnostics
                .Where(module => module.Module.Status.Enabled || module.ProcessIds.Count > 0)
                .OrderByDescending(module => module.ProcessIds.Count > 0)
                .ThenBy(module => module.Module.Name, StringComparer.OrdinalIgnoreCase)
                .Select(module => BuildModuleNode(module, liveProcesses, childrenByParent, entriesByPid))
                .ToList();

            if (internalProcessIds.Count > 0)
            {
                var internalUsage = AggregateUsage(
                    internalProcessIds
                        .Where(liveProcesses.ContainsKey)
                        .Select(processId => liveProcesses[processId].Usage),
                    totalPhysicalBytes);

                var internalRoots = internalProcessIds
                    .Where(processId =>
                    {
                        if (!entriesByPid.TryGetValue(processId, out var entry))
                        {
                            return true;
                        }

                        return entry.ParentProcessId == currentProcessId || !internalProcessIds.Contains(entry.ParentProcessId);
                    })
                    .OrderBy(processId => processId)
                    .Select(processId => BuildProcessNode(
                        processId,
                        liveProcesses,
                        childrenByParent,
                        internalProcessIds,
                        sessionsByPid: null))
                    .ToList();

                moduleNodes.Add(new HomeProcessTreeNodeSnapshot
                {
                    Key = "module:aslm_internal",
                    Title = "ASLM Internal",
                    TitleColor = Color.FromArgb("#FFFFFFFF"),
                    TitleFontAttributes = FontAttributes.Bold,
                    CpuText = HomeDashboardPresenter.FormatPercent(internalUsage.CpuPercent),
                    GpuText = HomeDashboardPresenter.FormatPercentOrNa(internalUsage.GpuPercent),
                    MemoryText = HomeDashboardPresenter.FormatBytes(internalUsage.MemoryBytes),
                    DiskText = HomeDashboardPresenter.FormatRate(internalUsage.DiskIoBytesPerSec),
                    NetworkText = HomeDashboardPresenter.FormatCompactCount(internalUsage.ConnectionCount),
                    DetailsText = internalProcessIds.Count == 1 ? "1 proc" : $"{internalProcessIds.Count} proc",
                    Children = internalRoots,
                    DefaultExpanded = false
                });
            }

            liveProcesses.TryGetValue(currentProcessId, out var currentProcess);
            var rootUsage = AggregateUsage(liveProcesses.Values.Select(process => process.Usage), totalPhysicalBytes);

            return new HomeProcessTreeNodeSnapshot
            {
                Key = "root:aslm",
                Title = "ASLM",
                TitleColor = Color.FromArgb("#FFFFFFFF"),
                TitleFontAttributes = FontAttributes.Bold,
                CpuText = HomeDashboardPresenter.FormatPercent(rootUsage.CpuPercent),
                GpuText = HomeDashboardPresenter.FormatPercentOrNa(rootUsage.GpuPercent),
                MemoryText = HomeDashboardPresenter.FormatBytes(rootUsage.MemoryBytes),
                DiskText = HomeDashboardPresenter.FormatRate(rootUsage.DiskIoBytesPerSec),
                NetworkText = HomeDashboardPresenter.FormatCompactCount(rootUsage.ConnectionCount),
                DetailsText = currentProcess == null
                    ? "Runtime"
                    : $"PID {currentProcess.ProcessId}",
                Children = moduleNodes,
                DefaultExpanded = true
            };
        }

        /// <summary>
        /// Builds one tree node for a module and all of its live processes.
        /// </summary>
        private static HomeProcessTreeNodeSnapshot BuildModuleNode(
            HomeModuleDiagnostics moduleDiagnostics,
            IReadOnlyDictionary<int, HomeLiveProcessInfo> liveProcesses,
            IReadOnlyDictionary<int, List<int>> childrenByParent,
            IReadOnlyDictionary<int, ProcessSnapshotEntry> entriesByPid)
        {
            var rootProcessIds = moduleDiagnostics.RootProcessIds
                .Where(processId => moduleDiagnostics.ProcessIds.Contains(processId))
                .Distinct()
                .ToList();

            if (rootProcessIds.Count == 0 && moduleDiagnostics.ProcessIds.Count > 0)
            {
                rootProcessIds = moduleDiagnostics.ProcessIds
                    .Where(processId =>
                    {
                        if (!entriesByPid.TryGetValue(processId, out var entry))
                        {
                            return true;
                        }

                        return !moduleDiagnostics.ProcessIds.Contains(entry.ParentProcessId);
                    })
                    .OrderBy(processId => processId)
                    .ToList();
            }

            var childNodes = rootProcessIds
                .Select(processId => BuildProcessNode(
                    processId,
                    liveProcesses,
                    childrenByParent,
                    moduleDiagnostics.ProcessIds,
                    moduleDiagnostics.SessionsByPid))
                .ToList();

            return new HomeProcessTreeNodeSnapshot
            {
                Key = $"module:{moduleDiagnostics.Module.SourcePath}",
                Title = moduleDiagnostics.Module.Name,
                TitleColor = Color.FromArgb("#FFFFFFFF"),
                TitleFontAttributes = FontAttributes.Bold,
                CpuText = HomeDashboardPresenter.FormatPercent(moduleDiagnostics.Usage.CpuPercent),
                GpuText = HomeDashboardPresenter.FormatPercentOrNa(moduleDiagnostics.Usage.GpuPercent),
                MemoryText = HomeDashboardPresenter.FormatBytes(moduleDiagnostics.Usage.MemoryBytes),
                DiskText = HomeDashboardPresenter.FormatRate(moduleDiagnostics.Usage.DiskIoBytesPerSec),
                NetworkText = HomeDashboardPresenter.FormatCompactCount(moduleDiagnostics.Usage.ConnectionCount),
                DetailsText = moduleDiagnostics.ProcessIds.Count == 0
                    ? "Module"
                    : moduleDiagnostics.ProcessIds.Count == 1
                        ? "1 proc"
                        : $"{moduleDiagnostics.ProcessIds.Count} proc",
                Children = childNodes,
                DefaultExpanded = false
            };
        }

        /// <summary>
        /// Builds one tree node for a live process and its descendants.
        /// </summary>
        private static HomeProcessTreeNodeSnapshot BuildProcessNode(
            int processId,
            IReadOnlyDictionary<int, HomeLiveProcessInfo> liveProcesses,
            IReadOnlyDictionary<int, List<int>> childrenByParent,
            ISet<int> allowedProcessIds,
            IReadOnlyDictionary<int, ModuleConsoleSessionSnapshot>? sessionsByPid)
        {
            var process = liveProcesses[processId];
            ModuleConsoleSessionSnapshot? session = null;
            sessionsByPid?.TryGetValue(processId, out session);

            var childNodes = new List<HomeProcessTreeNodeSnapshot>();
            if (childrenByParent.TryGetValue(processId, out var childProcessIds))
            {
                foreach (var childProcessId in childProcessIds
                             .Where(allowedProcessIds.Contains)
                             .OrderBy(childProcessId => childProcessId))
                {
                    childNodes.Add(BuildProcessNode(
                        childProcessId,
                        liveProcesses,
                        childrenByParent,
                        allowedProcessIds,
                        sessionsByPid));
                }
            }

            var title = !string.IsNullOrWhiteSpace(session?.Title)
                ? session.Title
                : process.ProcessName;

            var isObservedService = session?.IsObservedProcess == true;
            var detailsText = session != null && session.ProcessId.HasValue
                ? $"PID {session.ProcessId.Value}"
                : $"PID {process.ProcessId}";

            if (session?.ExitCode.HasValue == true)
            {
                detailsText += $" - exit {session.ExitCode.Value}";
            }
            else if (isObservedService)
            {
                detailsText += " - svc";
            }
            else if (session != null && !string.IsNullOrWhiteSpace(session.Stage))
            {
                detailsText += $" - {session.Stage.ToLowerInvariant()}";
            }

            return new HomeProcessTreeNodeSnapshot
            {
                Key = $"process:{process.ProcessId}",
                Title = title,
                TitleColor = isObservedService
                    ? Color.FromArgb("#FFFFD60A")
                    : Color.FromArgb("#FFEBEBF5"),
                TitleFontAttributes = FontAttributes.None,
                CpuText = HomeDashboardPresenter.FormatPercent(process.Usage.CpuPercent),
                GpuText = HomeDashboardPresenter.FormatPercentOrNa(process.Usage.GpuPercent),
                MemoryText = HomeDashboardPresenter.FormatBytes(process.Usage.MemoryBytes),
                DiskText = HomeDashboardPresenter.FormatRate(process.Usage.DiskIoBytesPerSec),
                NetworkText = HomeDashboardPresenter.FormatCompactCount(process.Usage.ConnectionCount),
                DetailsText = detailsText,
                Children = childNodes,
                DefaultExpanded = false
            };
        }

        // Process sampling

        /// <summary>
        /// Samples one live process and returns the resulting diagnostics payload.
        /// </summary>
        private bool TrySampleProcess(
            int processId,
            DateTimeOffset capturedUtc,
            IReadOnlyDictionary<int, ProcessSnapshotEntry> entriesByPid,
            IReadOnlyDictionary<int, double> gpuByPid,
            IReadOnlyDictionary<int, int> connectionCounts,
            long totalPhysicalBytes,
            out HomeLiveProcessInfo liveProcess)
        {
            liveProcess = null!;

            try
            {
                using var process = Process.GetProcessById(processId);
                process.Refresh();

                if (process.HasExited)
                {
                    return false;
                }

                var hasEntry = entriesByPid.TryGetValue(processId, out var entry);
                var parentProcessId = hasEntry
                    ? entry!.ParentProcessId
                    : 0;

                var processName = TryResolveProcessName(process, hasEntry ? entry!.ExecutableName : null);
                var workingSetBytes = process.WorkingSet64;
                var totalProcessorTime = process.TotalProcessorTime;
                var ioCounters = GetProcessIoCountersSafe(process);

                var cpuPercent = 0d;
                var diskIoBytesPerSecond = 0d;

                if (_processSamples.TryGetValue(processId, out var previousSample))
                {
                    var elapsedSeconds = (capturedUtc - previousSample.TimestampUtc).TotalSeconds;
                    if (elapsedSeconds > 0)
                    {
                        cpuPercent = Math.Clamp(
                            ((totalProcessorTime - previousSample.TotalProcessorTime).TotalSeconds /
                             (elapsedSeconds * Environment.ProcessorCount)) * 100d,
                            0,
                            100);

                        var readDelta = ioCounters.ReadTransferCount >= previousSample.ReadTransferCount
                            ? ioCounters.ReadTransferCount - previousSample.ReadTransferCount
                            : 0;

                        var writeDelta = ioCounters.WriteTransferCount >= previousSample.WriteTransferCount
                            ? ioCounters.WriteTransferCount - previousSample.WriteTransferCount
                            : 0;

                        diskIoBytesPerSecond = (readDelta + writeDelta) / elapsedSeconds;
                    }
                }

                _processSamples[processId] = new HomeProcessSample
                {
                    TimestampUtc = capturedUtc,
                    TotalProcessorTime = totalProcessorTime,
                    ReadTransferCount = ioCounters.ReadTransferCount,
                    WriteTransferCount = ioCounters.WriteTransferCount
                };

                gpuByPid.TryGetValue(processId, out var gpuPercent);
                connectionCounts.TryGetValue(processId, out var connectionCount);

                liveProcess = new HomeLiveProcessInfo
                {
                    ProcessId = processId,
                    ParentProcessId = parentProcessId,
                    ProcessName = processName,
                    Usage = new HomeUsageSnapshot
                    {
                        CpuPercent = cpuPercent,
                        GpuPercent = gpuByPid.ContainsKey(processId) ? gpuPercent : null,
                        MemoryBytes = workingSetBytes,
                        MemoryTotalBytes = totalPhysicalBytes,
                        DiskIoBytesPerSec = diskIoBytesPerSecond,
                        ConnectionCount = connectionCount
                    }
                };

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes stale process samples that no longer exist in the managed runtime tree.
        /// </summary>
        private void CleanupProcessSamples(IEnumerable<int> liveProcessIds)
        {
            var liveProcessSet = liveProcessIds.ToHashSet();
            var staleKeys = _processSamples.Keys
                .Where(processId => !liveProcessSet.Contains(processId))
                .ToList();

            foreach (var staleKey in staleKeys)
            {
                _processSamples.Remove(staleKey);
            }
        }

        /// <summary>
        /// Aggregates process usage into a single summary snapshot.
        /// </summary>
        private static HomeUsageSnapshot AggregateUsage(IEnumerable<HomeUsageSnapshot> usages, long totalPhysicalBytes)
        {
            double cpuPercent = 0;
            double gpuPercent = 0;
            var hasGpu = false;
            long memoryBytes = 0;
            double diskIoBytesPerSecond = 0;
            var connectionCount = 0;

            foreach (var usage in usages)
            {
                cpuPercent += usage.CpuPercent;

                if (usage.GpuPercent.HasValue)
                {
                    gpuPercent += usage.GpuPercent.Value;
                    hasGpu = true;
                }

                memoryBytes += usage.MemoryBytes;
                diskIoBytesPerSecond += usage.DiskIoBytesPerSec;
                connectionCount += usage.ConnectionCount;
            }

            return new HomeUsageSnapshot
            {
                CpuPercent = Math.Clamp(cpuPercent, 0, 100),
                GpuPercent = hasGpu ? Math.Clamp(gpuPercent, 0, 100) : null,
                MemoryBytes = memoryBytes,
                MemoryTotalBytes = totalPhysicalBytes,
                DiskIoBytesPerSec = diskIoBytesPerSecond,
                ConnectionCount = connectionCount
            };
        }

        // System metrics

        /// <summary>
        /// Samples total system CPU usage from kernel tick counters.
        /// </summary>
        private double SampleSystemCpu(DateTimeOffset capturedUtc)
        {
            if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
            {
                return 0;
            }

            var currentSample = new HomeSystemCpuSample
            {
                TimestampUtc = capturedUtc,
                IdleTime = ToUInt64(idleTime),
                KernelTime = ToUInt64(kernelTime),
                UserTime = ToUInt64(userTime)
            };

            if (_lastSystemCpuSample == null)
            {
                _lastSystemCpuSample = currentSample;
                return 0;
            }

            var idleDelta = currentSample.IdleTime - _lastSystemCpuSample.IdleTime;
            var kernelDelta = currentSample.KernelTime - _lastSystemCpuSample.KernelTime;
            var userDelta = currentSample.UserTime - _lastSystemCpuSample.UserTime;
            var totalDelta = kernelDelta + userDelta;

            _lastSystemCpuSample = currentSample;

            if (totalDelta == 0)
            {
                return 0;
            }

            return Math.Clamp((1d - idleDelta / (double)totalDelta) * 100d, 0, 100);
        }

        /// <summary>
        /// Samples total network throughput across active adapters.
        /// </summary>
        private HomeNetworkStats SampleNetwork(DateTimeOffset capturedUtc)
        {
            long totalReceived = 0;
            long totalSent = 0;

            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                try
                {
                    var interfaceStats = networkInterface.GetIPStatistics();
                    totalReceived += interfaceStats.BytesReceived;
                    totalSent += interfaceStats.BytesSent;
                }
                catch
                {
                    // Ignore adapters that do not expose IP statistics.
                }
            }

            var currentSample = new HomeNetworkSample
            {
                TimestampUtc = capturedUtc,
                TotalReceivedBytes = totalReceived,
                TotalSentBytes = totalSent
            };

            if (_lastNetworkSample == null)
            {
                _lastNetworkSample = currentSample;
                return new HomeNetworkStats();
            }

            var elapsedSeconds = (capturedUtc - _lastNetworkSample.TimestampUtc).TotalSeconds;
            if (elapsedSeconds <= 0)
            {
                _lastNetworkSample = currentSample;
                return new HomeNetworkStats();
            }

            var receivedDelta = Math.Max(0, currentSample.TotalReceivedBytes - _lastNetworkSample.TotalReceivedBytes);
            var sentDelta = Math.Max(0, currentSample.TotalSentBytes - _lastNetworkSample.TotalSentBytes);

            _lastNetworkSample = currentSample;

            return new HomeNetworkStats
            {
                ReceiveBytesPerSecond = receivedDelta / elapsedSeconds,
                SendBytesPerSecond = sentDelta / elapsedSeconds
            };
        }

        /// <summary>
        /// Queries total disk activity from the Windows performance WMI provider.
        /// </summary>
        private static HomeDiskStats QueryDiskStats()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT Name, PercentIdleTime, PercentDiskTime, DiskReadBytesPersec, DiskWriteBytesPersec " +
                    "FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk WHERE Name = '_Total'");

                foreach (ManagementObject result in searcher.Get())
                {
                    var idlePercent = ConvertToDouble(result["PercentIdleTime"]);
                    var busyPercent = idlePercent > 0
                        ? Math.Clamp(100d - idlePercent, 0, 100)
                        : Math.Clamp(ConvertToDouble(result["PercentDiskTime"]), 0, 100);

                    return new HomeDiskStats
                    {
                        BusyPercent = busyPercent,
                        ReadBytesPerSecond = ConvertToDouble(result["DiskReadBytesPersec"]),
                        WriteBytesPerSecond = ConvertToDouble(result["DiskWriteBytesPersec"])
                    };
                }
            }
            catch
            {
                // Best-effort sampling only.
            }

            return new HomeDiskStats();
        }

        /// <summary>
        /// Queries GPU engine utilization and groups it by process and adapter.
        /// </summary>
        private static HomeGpuQueryResult QueryGpuUsage()
        {
            var samples = new List<HomeGpuEngineSample>();
            var adapterNames = QueryGpuAdapterNames();

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT Name, UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine");

                foreach (ManagementObject result in searcher.Get())
                {
                    var instanceName = result["Name"]?.ToString();
                    if (!TryParseGpuInstance(instanceName, out var processId, out var adapterIndex, out var engineId))
                    {
                        continue;
                    }

                    var utilization = ConvertToDouble(result["UtilizationPercentage"]);
                    samples.Add(new HomeGpuEngineSample
                    {
                        ProcessId = processId,
                        AdapterIndex = adapterIndex,
                        EngineId = engineId,
                        Utilization = Math.Max(0, utilization)
                    });
                }
            }
            catch
            {
                return new HomeGpuQueryResult
                {
                    ProcessByPid = new Dictionary<int, double>(),
                    AdapterNames = adapterNames,
                    Samples = []
                };
            }

            var byProcessId = samples
                .GroupBy(sample => sample.ProcessId)
                .ToDictionary(
                    group => group.Key,
                    group => Math.Clamp(group.Sum(sample => sample.Utilization), 0, 100));

            return new HomeGpuQueryResult
            {
                ProcessByPid = byProcessId,
                AdapterNames = adapterNames,
                Samples = samples
            };
        }

        /// <summary>
        /// Builds the per-adapter GPU snapshots for system and runtime views.
        /// </summary>
        private static IReadOnlyList<HomeGpuAdapterSnapshot> BuildGpuAdapterSnapshots(
            HomeGpuQueryResult gpuQuery,
            IReadOnlyCollection<int> managedProcessIds)
        {
            var managedProcessSet = managedProcessIds.ToHashSet();
            var systemUsageByAdapter = AggregateGpuUsageByAdapter(gpuQuery.Samples);
            var runtimeUsageByAdapter = AggregateGpuUsageByAdapter(
                gpuQuery.Samples.Where(sample => managedProcessSet.Contains(sample.ProcessId)));

            var adapterIndices = systemUsageByAdapter.Keys
                .Union(runtimeUsageByAdapter.Keys)
                .Union(Enumerable.Range(0, gpuQuery.AdapterNames.Count))
                .OrderBy(index => index)
                .ToList();

            return adapterIndices
                .Select(adapterIndex => new HomeGpuAdapterSnapshot
                {
                    AdapterIndex = adapterIndex,
                    Name = adapterIndex < gpuQuery.AdapterNames.Count
                        ? gpuQuery.AdapterNames[adapterIndex]
                        : $"GPU {adapterIndex}",
                    SystemPercent = systemUsageByAdapter.TryGetValue(adapterIndex, out var systemPercent)
                        ? systemPercent
                        : 0,
                    RuntimePercent = runtimeUsageByAdapter.TryGetValue(adapterIndex, out var runtimePercent)
                        ? runtimePercent
                        : 0
                })
                .ToList();
        }

        /// <summary>
        /// Aggregates GPU engine samples into one utilization value per adapter.
        /// </summary>
        private static Dictionary<int, double> AggregateGpuUsageByAdapter(IEnumerable<HomeGpuEngineSample> samples)
        {
            return samples
                .GroupBy(sample => sample.AdapterIndex)
                .ToDictionary(
                    group => group.Key,
                    group => Math.Clamp(
                        group.GroupBy(sample => sample.EngineId)
                            .Select(engineGroup => engineGroup.Sum(sample => sample.Utilization))
                            .DefaultIfEmpty(0)
                            .Max(),
                        0,
                        100));
        }

        /// <summary>
        /// Queries installed GPU adapter names from WMI.
        /// </summary>
        private static IReadOnlyList<string> QueryGpuAdapterNames()
        {
            if (_cachedGpuAdapterNames != null)
            {
                return _cachedGpuAdapterNames;
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2",
                    "SELECT Name FROM Win32_VideoController");

                _cachedGpuAdapterNames = searcher.Get()
                    .OfType<ManagementObject>()
                    .Select(result => result["Name"]?.ToString())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return _cachedGpuAdapterNames;
            }
            catch
            {
                _cachedGpuAdapterNames = [];
                return _cachedGpuAdapterNames;
            }
        }

        /// <summary>
        /// Queries the number of open TCP and UDP endpoints per process.
        /// </summary>
        private static Dictionary<int, int> QueryProcessConnectionCounts(IReadOnlyCollection<int> processIds)
        {
            var counts = new Dictionary<int, int>();
            if (processIds.Count == 0)
            {
                return counts;
            }

            var trackedProcessIds = processIds.ToHashSet();

            IncrementCountsFromTcpTable(counts, trackedProcessIds, AddressFamily.InterNetwork);
            IncrementCountsFromTcpTable(counts, trackedProcessIds, AddressFamily.InterNetworkV6);
            IncrementCountsFromUdpTable(counts, trackedProcessIds, AddressFamily.InterNetwork);
            IncrementCountsFromUdpTable(counts, trackedProcessIds, AddressFamily.InterNetworkV6);

            return counts;
        }

        /// <summary>
        /// Appends TCP connection counts from one address family.
        /// </summary>
        private static void IncrementCountsFromTcpTable(
            IDictionary<int, int> counts,
            ISet<int> trackedProcessIds,
            AddressFamily addressFamily)
        {
            var bufferLength = 0;
            var result = GetExtendedTcpTable(
                IntPtr.Zero,
                ref bufferLength,
                order: true,
                (int)addressFamily,
                TcpTableClass.OwnerPidAll,
                0);

            if (result != ErrorInsufficientBuffer || bufferLength <= 0)
            {
                return;
            }

            var buffer = Marshal.AllocHGlobal(bufferLength);
            try
            {
                result = GetExtendedTcpTable(
                    buffer,
                    ref bufferLength,
                    order: true,
                    (int)addressFamily,
                    TcpTableClass.OwnerPidAll,
                    0);

                if (result != ErrorSuccess)
                {
                    return;
                }

                var rowCount = Marshal.ReadInt32(buffer);
                var rowPointer = IntPtr.Add(buffer, sizeof(int));

                if (addressFamily == AddressFamily.InterNetwork)
                {
                    var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
                    for (var index = 0; index < rowCount; index++)
                    {
                        var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                        var processId = unchecked((int)row.OwningPid);
                        if (trackedProcessIds.Contains(processId))
                        {
                            counts[processId] = counts.TryGetValue(processId, out var value) ? value + 1 : 1;
                        }

                        rowPointer = IntPtr.Add(rowPointer, rowSize);
                    }
                }
                else
                {
                    var rowSize = Marshal.SizeOf<MibTcp6RowOwnerPid>();
                    for (var index = 0; index < rowCount; index++)
                    {
                        var row = Marshal.PtrToStructure<MibTcp6RowOwnerPid>(rowPointer);
                        var processId = unchecked((int)row.OwningPid);
                        if (trackedProcessIds.Contains(processId))
                        {
                            counts[processId] = counts.TryGetValue(processId, out var value) ? value + 1 : 1;
                        }

                        rowPointer = IntPtr.Add(rowPointer, rowSize);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// Appends UDP endpoint counts from one address family.
        /// </summary>
        private static void IncrementCountsFromUdpTable(
            IDictionary<int, int> counts,
            ISet<int> trackedProcessIds,
            AddressFamily addressFamily)
        {
            var bufferLength = 0;
            var result = GetExtendedUdpTable(
                IntPtr.Zero,
                ref bufferLength,
                order: true,
                (int)addressFamily,
                UdpTableClass.OwnerPid,
                0);

            if (result != ErrorInsufficientBuffer || bufferLength <= 0)
            {
                return;
            }

            var buffer = Marshal.AllocHGlobal(bufferLength);
            try
            {
                result = GetExtendedUdpTable(
                    buffer,
                    ref bufferLength,
                    order: true,
                    (int)addressFamily,
                    UdpTableClass.OwnerPid,
                    0);

                if (result != ErrorSuccess)
                {
                    return;
                }

                var rowCount = Marshal.ReadInt32(buffer);
                var rowPointer = IntPtr.Add(buffer, sizeof(int));

                if (addressFamily == AddressFamily.InterNetwork)
                {
                    var rowSize = Marshal.SizeOf<MibUdpRowOwnerPid>();
                    for (var index = 0; index < rowCount; index++)
                    {
                        var row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(rowPointer);
                        var processId = unchecked((int)row.OwningPid);
                        if (trackedProcessIds.Contains(processId))
                        {
                            counts[processId] = counts.TryGetValue(processId, out var value) ? value + 1 : 1;
                        }

                        rowPointer = IntPtr.Add(rowPointer, rowSize);
                    }
                }
                else
                {
                    var rowSize = Marshal.SizeOf<MibUdp6RowOwnerPid>();
                    for (var index = 0; index < rowCount; index++)
                    {
                        var row = Marshal.PtrToStructure<MibUdp6RowOwnerPid>(rowPointer);
                        var processId = unchecked((int)row.OwningPid);
                        if (trackedProcessIds.Contains(processId))
                        {
                            counts[processId] = counts.TryGetValue(processId, out var value) ? value + 1 : 1;
                        }

                        rowPointer = IntPtr.Add(rowPointer, rowSize);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // Process-tree helpers

        /// <summary>
        /// Returns the complete set of descendant process identifiers rooted at one parent process.
        /// </summary>
        private static HashSet<int> CollectDescendantPids(
            int rootProcessId,
            IReadOnlyDictionary<int, List<int>> childrenByParent)
        {
            var descendants = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(rootProcessId);

            while (queue.Count > 0)
            {
                var parentProcessId = queue.Dequeue();
                if (!childrenByParent.TryGetValue(parentProcessId, out var childProcessIds))
                {
                    continue;
                }

                foreach (var childProcessId in childProcessIds)
                {
                    if (!descendants.Add(childProcessId))
                    {
                        continue;
                    }

                    queue.Enqueue(childProcessId);
                }
            }

            return descendants;
        }

        // Native helpers

        /// <summary>
        /// Returns the current physical-memory totals for the host system.
        /// </summary>
        private static HomeMemoryStatus GetMemoryStatus()
        {
            var status = new MemoryStatusEx();
            if (!GlobalMemoryStatusEx(status))
            {
                return new HomeMemoryStatus();
            }

            return new HomeMemoryStatus
            {
                TotalPhysicalBytes = unchecked((long)status.TotalPhys),
                UsedPhysicalBytes = unchecked((long)(status.TotalPhys - status.AvailPhys))
            };
        }

        /// <summary>
        /// Returns process I/O counters, or a zeroed structure when the native query fails.
        /// </summary>
        private static IoCounters GetProcessIoCountersSafe(Process process)
        {
            return GetProcessIoCounters(process.Handle, out var ioCounters)
                ? ioCounters
                : default;
        }

        /// <summary>
        /// Resolves a stable display name for one process.
        /// </summary>
        private static string TryResolveProcessName(Process process, string? executableName)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(process.ProcessName))
                {
                    return process.ProcessName;
                }
            }
            catch
            {
                // Fall back to the snapshot executable name.
            }

            var snapshotName = Path.GetFileNameWithoutExtension(executableName);
            return string.IsNullOrWhiteSpace(snapshotName)
                ? $"Process {process.Id}"
                : snapshotName;
        }

        /// <summary>
        /// Parses one GPU engine instance name and extracts the owning process identifier.
        /// </summary>
        private static bool TryParseGpuInstance(
            string? instanceName,
            out int processId,
            out int adapterIndex,
            out string engineId)
        {
            processId = 0;
            adapterIndex = 0;
            engineId = string.Empty;

            if (string.IsNullOrWhiteSpace(instanceName))
            {
                return false;
            }

            var match = GpuInstanceRegex.Match(instanceName);
            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out processId) ||
                !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out adapterIndex))
            {
                return false;
            }

            engineId = match.Groups[3].Value;
            return true;
        }

        /// <summary>
        /// Converts one WMI numeric value to <see cref="double"/>.
        /// </summary>
        private static double ConvertToDouble(object? value)
        {
            if (value == null)
            {
                return 0;
            }

            return value switch
            {
                byte byteValue => byteValue,
                short shortValue => shortValue,
                int intValue => intValue,
                long longValue => longValue,
                float floatValue => floatValue,
                double doubleValue => doubleValue,
                decimal decimalValue => (double)decimalValue,
                _ when double.TryParse(
                    Convert.ToString(value, CultureInfo.InvariantCulture),
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out var parsedValue) => parsedValue,
                _ => 0
            };
        }

        /// <summary>
        /// Converts a native <see cref="FileTime"/> value to an unsigned 64-bit tick count.
        /// </summary>
        private static ulong ToUInt64(FileTime fileTime)
        {
            return ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;
        }

        // Native interop

        private const uint ErrorSuccess = 0;
        private const uint ErrorInsufficientBuffer = 122;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(
            out FileTime idleTime,
            out FileTime kernelTime,
            out FileTime userTime);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatusEx memoryStatus);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr hProcess, out IoCounters ioCounters);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr tcpTable,
            ref int outBufferLength,
            bool order,
            int ipVersion,
            TcpTableClass tableClass,
            uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedUdpTable(
            IntPtr udpTable,
            ref int outBufferLength,
            bool order,
            int ipVersion,
            UdpTableClass tableClass,
            uint reserved);

        private enum TcpTableClass
        {
            OwnerPidAll = 5
        }

        private enum UdpTableClass
        {
            OwnerPid = 1
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class MemoryStatusEx
        {
            public uint Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MibTcpRowOwnerPid
        {
            public uint State;
            public uint LocalAddress;
            public uint LocalPort;
            public uint RemoteAddress;
            public uint RemotePort;
            public uint OwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MibTcp6RowOwnerPid
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] LocalAddress;
            public uint LocalScopeId;
            public uint LocalPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] RemoteAddress;
            public uint RemoteScopeId;
            public uint RemotePort;
            public uint State;
            public uint OwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MibUdpRowOwnerPid
        {
            public uint LocalAddress;
            public uint LocalPort;
            public uint OwningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MibUdp6RowOwnerPid
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] LocalAddress;
            public uint LocalScopeId;
            public uint LocalPort;
            public uint OwningPid;
        }

    }


    // Diagnostics models

    /// <summary>
    /// Represents one complete diagnostics capture used to render the home dashboard.
    /// </summary>
    internal sealed class HomeDiagnosticsSnapshot
    {
        /// <summary>
        /// Gets or sets the capture timestamp.
        /// </summary>
        public DateTimeOffset CapturedUtc { get; set; }

        /// <summary>
        /// Gets or sets the host-system resource usage snapshot.
        /// </summary>
        public HomeUsageSnapshot SystemUsage { get; set; } = new();

        /// <summary>
        /// Gets or sets the ASLM runtime resource usage snapshot.
        /// </summary>
        public HomeUsageSnapshot RuntimeUsage { get; set; } = new();

        /// <summary>
        /// Gets or sets the per-module diagnostics snapshots keyed by source path.
        /// </summary>
        public IReadOnlyDictionary<string, HomeModuleDiagnostics> ModulesBySourcePath { get; set; } =
            new Dictionary<string, HomeModuleDiagnostics>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the GPU adapter utilization snapshots.
        /// </summary>
        public IReadOnlyList<HomeGpuAdapterSnapshot> GpuAdapters { get; set; } = [];

        /// <summary>
        /// Gets or sets the root node of the managed process tree.
        /// </summary>
        public HomeProcessTreeNodeSnapshot RootNode { get; set; } = new();

        /// <summary>
        /// Gets or sets the number of live managed child processes.
        /// </summary>
        public int ActiveProcessCount { get; set; }

        /// <summary>
        /// Gets or sets the number of modules with live managed processes.
        /// </summary>
        public int ActiveModuleCount { get; set; }

        /// <summary>
        /// Gets or sets the latest module activity timestamp.
        /// </summary>
        public DateTimeOffset? LastModuleActivityUtc { get; set; }
    }

    /// <summary>
    /// Represents one aggregate usage snapshot.
    /// </summary>
    internal sealed class HomeUsageSnapshot
    {
        /// <summary>
        /// Gets or sets CPU utilization in percent.
        /// </summary>
        public double CpuPercent { get; set; }

        /// <summary>
        /// Gets or sets GPU utilization in percent when available.
        /// </summary>
        public double? GpuPercent { get; set; }

        /// <summary>
        /// Gets or sets resident memory in bytes.
        /// </summary>
        public long MemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets total physical memory in bytes.
        /// </summary>
        public long MemoryTotalBytes { get; set; }

        /// <summary>
        /// Gets the normalized memory usage in percent.
        /// </summary>
        public double MemoryPercent => MemoryTotalBytes <= 0
            ? 0
            : Math.Clamp(MemoryBytes / (double)MemoryTotalBytes * 100d, 0, 100);

        /// <summary>
        /// Gets or sets system disk busy percentage.
        /// </summary>
        public double DiskBusyPercent { get; set; }

        /// <summary>
        /// Gets or sets system disk read throughput in bytes per second.
        /// </summary>
        public double DiskReadBytesPerSec { get; set; }

        /// <summary>
        /// Gets or sets system disk write throughput in bytes per second.
        /// </summary>
        public double DiskWriteBytesPerSec { get; set; }

        /// <summary>
        /// Gets or sets aggregate process I/O throughput in bytes per second.
        /// </summary>
        public double DiskIoBytesPerSec { get; set; }

        /// <summary>
        /// Gets or sets system receive throughput in bytes per second.
        /// </summary>
        public double NetworkReceiveBytesPerSec { get; set; }

        /// <summary>
        /// Gets or sets system send throughput in bytes per second.
        /// </summary>
        public double NetworkSendBytesPerSec { get; set; }

        /// <summary>
        /// Gets or sets the number of open network endpoints.
        /// </summary>
        public int ConnectionCount { get; set; }
    }

    /// <summary>
    /// Represents the diagnostics for one installed module.
    /// </summary>
    internal sealed class HomeModuleDiagnostics
    {
        /// <summary>
        /// Gets or sets the module configuration.
        /// </summary>
        public ModuleConfig Module { get; set; } = new();

        /// <summary>
        /// Gets or sets the latest console snapshot for the module.
        /// </summary>
        public ModuleConsoleModuleSnapshot? ConsoleSnapshot { get; set; }

        /// <summary>
        /// Gets or sets the live process identifiers assigned to the module.
        /// </summary>
        public ISet<int> ProcessIds { get; set; } = new HashSet<int>();

        /// <summary>
        /// Gets or sets the root process identifiers assigned to the module.
        /// </summary>
        public IReadOnlyList<int> RootProcessIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the console sessions indexed by process identifier.
        /// </summary>
        public IReadOnlyDictionary<int, ModuleConsoleSessionSnapshot> SessionsByPid { get; set; } =
            new Dictionary<int, ModuleConsoleSessionSnapshot>();

        /// <summary>
        /// Gets or sets the aggregate module usage.
        /// </summary>
        public HomeUsageSnapshot Usage { get; set; } = new();

        /// <summary>
        /// Gets the latest module activity timestamp.
        /// </summary>
        public DateTimeOffset? LastActivityUtc => ConsoleSnapshot?.LastActivityUtc;

        /// <summary>
        /// Gets the total number of console sessions for the module.
        /// </summary>
        public int ConsoleSessionCount => ConsoleSnapshot?.Sessions.Count ?? 0;
    }

    /// <summary>
    /// Represents one live process inside the managed runtime tree.
    /// </summary>
    internal sealed class HomeLiveProcessInfo
    {
        /// <summary>
        /// Gets or sets the process identifier.
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the parent process identifier.
        /// </summary>
        public int ParentProcessId { get; set; }

        /// <summary>
        /// Gets or sets the display name of the process.
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sampled process usage.
        /// </summary>
        public HomeUsageSnapshot Usage { get; set; } = new();
    }

    /// <summary>
    /// Represents one GPU adapter with both system-wide and ASLM-runtime utilization.
    /// </summary>
    internal sealed class HomeGpuAdapterSnapshot
    {
        /// <summary>
        /// Gets or sets the adapter index reported by the GPU counters.
        /// </summary>
        public int AdapterIndex { get; set; }

        /// <summary>
        /// Gets or sets the user-visible adapter name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the system-wide utilization percentage.
        /// </summary>
        public double SystemPercent { get; set; }

        /// <summary>
        /// Gets or sets the utilization percentage attributed to the ASLM runtime tree.
        /// </summary>
        public double RuntimePercent { get; set; }
    }

    /// <summary>
    /// Stores raw GPU engine samples captured during one refresh.
    /// </summary>
    internal sealed class HomeGpuEngineSample
    {
        /// <summary>
        /// Gets or sets the owning process identifier.
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the adapter index.
        /// </summary>
        public int AdapterIndex { get; set; }

        /// <summary>
        /// Gets or sets the engine identifier reported by Windows.
        /// </summary>
        public string EngineId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the sampled utilization percentage.
        /// </summary>
        public double Utilization { get; set; }
    }

    /// <summary>
    /// Stores the GPU query results used for per-process and per-adapter aggregation.
    /// </summary>
    internal sealed class HomeGpuQueryResult
    {
        /// <summary>
        /// Gets or sets process utilization keyed by process identifier.
        /// </summary>
        public IReadOnlyDictionary<int, double> ProcessByPid { get; set; } = new Dictionary<int, double>();

        /// <summary>
        /// Gets or sets the ordered adapter-name list.
        /// </summary>
        public IReadOnlyList<string> AdapterNames { get; set; } = [];

        /// <summary>
        /// Gets or sets the raw engine samples returned by the query.
        /// </summary>
        public IReadOnlyList<HomeGpuEngineSample> Samples { get; set; } = [];
    }

    /// <summary>
    /// Represents one tree node before it is flattened for display.
    /// </summary>
    internal sealed class HomeProcessTreeNodeSnapshot
    {
        /// <summary>
        /// Gets or sets the stable node key.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the row title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the row title color.
        /// </summary>
        public Color TitleColor { get; set; } = Color.FromArgb("#FFFFFFFF");

        /// <summary>
        /// Gets or sets the title font weight.
        /// </summary>
        public FontAttributes TitleFontAttributes { get; set; } = FontAttributes.None;

        /// <summary>
        /// Gets or sets the CPU column text.
        /// </summary>
        public string CpuText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the GPU column text.
        /// </summary>
        public string GpuText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the memory column text.
        /// </summary>
        public string MemoryText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the disk column text.
        /// </summary>
        public string DiskText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the network column text.
        /// </summary>
        public string NetworkText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the trailing details text.
        /// </summary>
        public string DetailsText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the child nodes.
        /// </summary>
        public IReadOnlyList<HomeProcessTreeNodeSnapshot> Children { get; set; } = [];

        /// <summary>
        /// Gets or sets whether the node should default to expanded when first shown.
        /// </summary>
        public bool DefaultExpanded { get; set; }
    }

    /// <summary>
    /// Stores one raw per-process sample used to calculate CPU and I/O deltas.
    /// </summary>
    internal sealed class HomeProcessSample
    {
        /// <summary>
        /// Gets or sets the sample timestamp.
        /// </summary>
        public DateTimeOffset TimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets the accumulated processor time for the process.
        /// </summary>
        public TimeSpan TotalProcessorTime { get; set; }

        /// <summary>
        /// Gets or sets the total bytes read by the process.
        /// </summary>
        public ulong ReadTransferCount { get; set; }

        /// <summary>
        /// Gets or sets the total bytes written by the process.
        /// </summary>
        public ulong WriteTransferCount { get; set; }
    }

    /// <summary>
    /// Stores one raw system CPU sample used for delta-based utilization calculation.
    /// </summary>
    internal sealed class HomeSystemCpuSample
    {
        /// <summary>
        /// Gets or sets the sample timestamp.
        /// </summary>
        public DateTimeOffset TimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets the cumulative system idle time.
        /// </summary>
        public ulong IdleTime { get; set; }

        /// <summary>
        /// Gets or sets the cumulative system kernel time.
        /// </summary>
        public ulong KernelTime { get; set; }

        /// <summary>
        /// Gets or sets the cumulative system user time.
        /// </summary>
        public ulong UserTime { get; set; }
    }

    /// <summary>
    /// Stores one raw network-interface sample used to calculate transfer rates.
    /// </summary>
    internal sealed class HomeNetworkSample
    {
        /// <summary>
        /// Gets or sets the sample timestamp.
        /// </summary>
        public DateTimeOffset TimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets the total received bytes across active interfaces.
        /// </summary>
        public long TotalReceivedBytes { get; set; }

        /// <summary>
        /// Gets or sets the total sent bytes across active interfaces.
        /// </summary>
        public long TotalSentBytes { get; set; }
    }

    /// <summary>
    /// Stores one host-memory snapshot used to build memory metrics.
    /// </summary>
    internal sealed class HomeMemoryStatus
    {
        /// <summary>
        /// Gets or sets the total physical memory in bytes.
        /// </summary>
        public long TotalPhysicalBytes { get; set; }

        /// <summary>
        /// Gets or sets the used physical memory in bytes.
        /// </summary>
        public long UsedPhysicalBytes { get; set; }
    }

    /// <summary>
    /// Stores one normalized disk-usage snapshot.
    /// </summary>
    internal sealed class HomeDiskStats
    {
        /// <summary>
        /// Gets or sets the disk busy percentage.
        /// </summary>
        public double BusyPercent { get; set; }

        /// <summary>
        /// Gets or sets read throughput in bytes per second.
        /// </summary>
        public double ReadBytesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets write throughput in bytes per second.
        /// </summary>
        public double WriteBytesPerSecond { get; set; }
    }

    /// <summary>
    /// Stores one normalized network-throughput snapshot.
    /// </summary>
    internal sealed class HomeNetworkStats
    {
        /// <summary>
        /// Gets or sets receive throughput in bytes per second.
        /// </summary>
        public double ReceiveBytesPerSecond { get; set; }

        /// <summary>
        /// Gets or sets send throughput in bytes per second.
        /// </summary>
        public double SendBytesPerSecond { get; set; }
    }

}
