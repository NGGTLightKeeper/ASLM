// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Displays live module console sessions and subprocess output.
    /// </summary>
    public partial class ConsolesView : ContentView, IConsolesView
    {
        private const double CompactBreakpoint = 1180;

        private readonly ConsolesPageViewModel _viewModel = new();
        private readonly ConsolesPresenter _presenter;
        private bool _suppressSelection;

        public ConsolesView(ModuleInstaller moduleInstaller, ModuleConsoleService consoleService)
        {
            InitializeComponent();

            BindingContext = _viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            _presenter = new ConsolesPresenter(this, moduleInstaller, consoleService);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;

            UpdateResponsiveLayout();
        }

        internal Task RefreshAsync()
        {
            return _presenter.RefreshAsync();
        }

        private async void OnLoaded(object? sender, EventArgs e)
        {
            await _presenter.ActivateAsync();
        }

        private void OnUnloaded(object? sender, EventArgs e)
        {
            _presenter.Deactivate();
        }

        private void OnSizeChanged(object? sender, EventArgs e)
        {
            UpdateResponsiveLayout();
        }

        private async void OnRefreshClicked(object? sender, EventArgs e)
        {
            await _presenter.RefreshAsync();
        }

        private async void OnShowCompletedProcessesChanged(object? sender, CheckedChangedEventArgs e)
        {
            await _presenter.SetShowCompletedProcessesAsync(e.Value);
        }

        private async void OnModuleSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection)
            {
                return;
            }

            if (e.CurrentSelection.FirstOrDefault() is ConsoleModuleItemViewModel module)
            {
                await _presenter.SelectModuleAsync(module.SourcePath);
            }
        }

        private async void OnSessionSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection)
            {
                return;
            }

            if (e.CurrentSelection.FirstOrDefault() is ConsoleSessionItemViewModel session)
            {
                await _presenter.SelectSessionAsync(session.Id);
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConsolesPageViewModel.FollowOutput) && _viewModel.FollowOutput)
            {
                ScrollOutputToEnd();
            }
        }

        void IConsolesView.Render(ConsolesDashboardState state)
        {
            var previousSessionId = _viewModel.SelectedSessionId;

            _suppressSelection = true;

            _viewModel.Subtitle = state.Subtitle;
            _viewModel.ModuleListCaption = state.ModuleListCaption;
            _viewModel.SessionListCaption = state.SessionListCaption;
            _viewModel.ShowCompletedProcesses = state.ShowCompletedProcesses;
            _viewModel.SelectedSessionId = state.SelectedSessionId;
            _viewModel.SelectedSessionTitle = state.SelectedSessionTitle;
            _viewModel.SelectedSessionStatus = state.SelectedSessionStatus;
            _viewModel.SelectedSessionDescription = state.SelectedSessionDescription;
            _viewModel.SelectedSessionCommandLine = state.SelectedSessionCommandLine;
            _viewModel.SelectedSessionFooter = state.SelectedSessionFooter;
            _viewModel.SelectedSessionText = string.Join(Environment.NewLine, state.SelectedSessionLines);

            SyncModuleItems(_viewModel.Modules, state.Modules);
            SyncSessionItems(_viewModel.Sessions, state.Sessions);

            var selectedModule = _viewModel.Modules.FirstOrDefault(module => module.SourcePath == state.SelectedModuleSourcePath);
            if (!ReferenceEquals(ModulesCollection.SelectedItem, selectedModule))
            {
                ModulesCollection.SelectedItem = selectedModule;
            }

            var selectedSession = _viewModel.Sessions.FirstOrDefault(session => session.Id == state.SelectedSessionId);
            if (!ReferenceEquals(SessionsCollection.SelectedItem, selectedSession))
            {
                SessionsCollection.SelectedItem = selectedSession;
            }
            _suppressSelection = false;

            if (_viewModel.FollowOutput &&
                string.Equals(previousSessionId, state.SelectedSessionId, StringComparison.Ordinal))
            {
                ScrollOutputToEnd();
            }
        }

        private void UpdateResponsiveLayout()
        {
            var isCompact = Width > 0 && Width < CompactBreakpoint;

            WorkspaceLayout.ColumnDefinitions.Clear();
            WorkspaceLayout.RowDefinitions.Clear();

            if (isCompact)
            {
                WorkspaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
                WorkspaceLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                WorkspaceLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                WorkspaceLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(ModulesPanel, 0);
                Grid.SetColumn(ModulesPanel, 0);
                Grid.SetRow(SessionsPanel, 1);
                Grid.SetColumn(SessionsPanel, 0);
                Grid.SetRow(OutputPanel, 2);
                Grid.SetColumn(OutputPanel, 0);
            }
            else
            {
                WorkspaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = 280 });
                WorkspaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = 320 });
                WorkspaceLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                WorkspaceLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid.SetRow(ModulesPanel, 0);
                Grid.SetColumn(ModulesPanel, 0);
                Grid.SetRow(SessionsPanel, 0);
                Grid.SetColumn(SessionsPanel, 1);
                Grid.SetRow(OutputPanel, 0);
                Grid.SetColumn(OutputPanel, 2);
            }
        }

        private void ScrollOutputToEnd()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (string.IsNullOrEmpty(_viewModel.SelectedSessionText))
                {
                    return;
                }

                OutputEditor.CursorPosition = _viewModel.SelectedSessionText.Length;
                OutputEditor.SelectionLength = 0;
            });
        }

        private static void SyncModuleItems(ObservableCollection<ConsoleModuleItemViewModel> target, IReadOnlyList<ConsoleModuleItemViewModel> source)
        {
            SyncKeyedItems(
                target,
                source,
                static item => item.SourcePath,
                static (targetItem, sourceItem) => targetItem.UpdateFrom(sourceItem));
        }

        private static void SyncSessionItems(ObservableCollection<ConsoleSessionItemViewModel> target, IReadOnlyList<ConsoleSessionItemViewModel> source)
        {
            SyncKeyedItems(
                target,
                source,
                static item => item.Id,
                static (targetItem, sourceItem) => targetItem.UpdateFrom(sourceItem));
        }

        private static void SyncKeyedItems<T>(
            ObservableCollection<T> target,
            IReadOnlyList<T> source,
            Func<T, string> keySelector,
            Action<T, T> updateExisting)
            where T : class
        {
            for (var index = 0; index < source.Count; index++)
            {
                var sourceItem = source[index];

                if (index < target.Count &&
                    string.Equals(keySelector(target[index]), keySelector(sourceItem), StringComparison.Ordinal))
                {
                    updateExisting(target[index], sourceItem);
                    continue;
                }

                var existingIndex = -1;
                for (var searchIndex = index + 1; searchIndex < target.Count; searchIndex++)
                {
                    if (string.Equals(keySelector(target[searchIndex]), keySelector(sourceItem), StringComparison.Ordinal))
                    {
                        existingIndex = searchIndex;
                        break;
                    }
                }

                if (existingIndex >= 0)
                {
                    var existingItem = target[existingIndex];
                    target.Move(existingIndex, index);
                    updateExisting(existingItem, sourceItem);
                }
                else
                {
                    target.Insert(index, sourceItem);
                }
            }

            while (target.Count > source.Count)
            {
                target.RemoveAt(target.Count - 1);
            }
        }
    }

    internal interface IConsolesView
    {
        void Render(ConsolesDashboardState state);
    }

    internal sealed class ConsolesPresenter
    {
        private const string AllModulesModuleId = "__all_modules__";
        private const string GlobalUnifiedSessionId = "__all_modules_unified__";
        private const string UnifiedSessionId = "__module_unified__";

        private readonly IConsolesView _view;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleConsoleService _consoleService;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private List<ModuleConfig> _knownModules = [];
        private bool _isActive;
        private int _refreshQueued;
        private bool _showCompletedProcesses;
        private string? _selectedModuleSourcePath;
        private string? _selectedSessionId;

        public ConsolesPresenter(IConsolesView view, ModuleInstaller moduleInstaller, ModuleConsoleService consoleService)
        {
            _view = view;
            _moduleInstaller = moduleInstaller;
            _consoleService = consoleService;
        }

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

        public void Deactivate()
        {
            if (!_isActive)
            {
                return;
            }

            _consoleService.StateChanged -= OnConsoleStateChanged;
            _isActive = false;
        }

        public async Task SelectModuleAsync(string sourcePath)
        {
            if (string.Equals(_selectedModuleSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _selectedModuleSourcePath = sourcePath;
            _selectedSessionId = null;
            await RefreshAsync(forceModuleReload: false);
        }

        public async Task SetShowCompletedProcessesAsync(bool showCompletedProcesses)
        {
            if (_showCompletedProcesses == showCompletedProcesses)
            {
                return;
            }

            _showCompletedProcesses = showCompletedProcesses;
            await RefreshAsync(forceModuleReload: false);
        }

        public async Task SelectSessionAsync(string sessionId)
        {
            if (string.Equals(_selectedSessionId, sessionId, StringComparison.Ordinal))
            {
                return;
            }

            _selectedSessionId = sessionId;
            await RefreshAsync(forceModuleReload: false);
        }

        public Task RefreshAsync()
        {
            return RefreshAsync(forceModuleReload: true);
        }

        private async Task RefreshAsync(bool forceModuleReload)
        {
            await _refreshLock.WaitAsync();
            try
            {
                if (forceModuleReload || _knownModules.Count == 0)
                {
                    _knownModules = await _moduleInstaller.DiscoverModulesAsync();
                }

                _consoleService.EnsureModules(_knownModules);

                var snapshots = _consoleService.GetSnapshot();
                var state = BuildState(snapshots);

                await MainThread.InvokeOnMainThreadAsync(() => _view.Render(state));
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private void OnConsoleStateChanged(object? sender, EventArgs e)
        {
            if (!_isActive || Interlocked.Exchange(ref _refreshQueued, 1) == 1)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                Interlocked.Exchange(ref _refreshQueued, 0);
                await RefreshAsync(forceModuleReload: false);
            });
        }

        private ConsolesDashboardState BuildState(IReadOnlyList<ModuleConsoleModuleSnapshot> snapshots)
        {
            var activeModules = snapshots
                .Where(module => module.IsEnabled || module.ActiveProcessCount > 0)
                .ToList();

            var moduleItems = new List<ConsoleModuleItemViewModel>();
            if (activeModules.Count > 0)
            {
                moduleItems.Add(new ConsoleModuleItemViewModel
                {
                    SourcePath = AllModulesModuleId,
                    Name = "All Modules",
                    StatusText = $"{activeModules.Count} active modules",
                    ActivityText = "Unified console for all active modules"
                });
            }

            moduleItems.AddRange(activeModules.Select(MapModule));

            if (moduleItems.Count == 0)
            {
                _selectedModuleSourcePath = null;
            }
            else if (string.IsNullOrWhiteSpace(_selectedModuleSourcePath) ||
                     !moduleItems.Any(module => string.Equals(module.SourcePath, _selectedModuleSourcePath, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedModuleSourcePath = moduleItems[0].SourcePath;
            }

            var isGlobalModule = string.Equals(_selectedModuleSourcePath, AllModulesModuleId, StringComparison.Ordinal);
            var selectedModule = activeModules.FirstOrDefault(module => module.SourcePath == _selectedModuleSourcePath);
            var activeModulePaths = activeModules.Select(module => module.SourcePath).ToList();

            IReadOnlyList<ConsoleSessionItemViewModel> sessionItems;
            IReadOnlyList<string> selectedSessionLines;
            string selectedSessionTitle;
            string selectedSessionStatus;
            string selectedSessionDescription;
            string selectedSessionCommandLine;
            string selectedSessionFooter;

            if (isGlobalModule)
            {
                _selectedSessionId = GlobalUnifiedSessionId;
                sessionItems =
                [
                    new ConsoleSessionItemViewModel
                    {
                        Id = GlobalUnifiedSessionId,
                        ModuleSourcePath = AllModulesModuleId,
                        SessionSourceId = GlobalUnifiedSessionId,
                        Title = "Unified Console",
                        StatusText = "Merged output across all active modules",
                        Preview = "Includes unified logs and completed process history from active modules."
                    }
                ];

                selectedSessionLines = _consoleService.GetUnifiedOverviewLines(activeModulePaths);
                selectedSessionTitle = "All Modules / Unified Console";
                selectedSessionStatus = $"{activeModules.Count} active modules";
                selectedSessionDescription = "Merged output across all active modules. Completed processes remain visible here even when hidden from per-module lists.";
                selectedSessionCommandLine = string.Empty;
                selectedSessionFooter = $"{selectedSessionLines.Count} visible lines";
            }
            else if (selectedModule == null)
            {
                _selectedSessionId = null;
                sessionItems = [];
                selectedSessionLines = ["No output yet."];
                selectedSessionTitle = "No console selected";
                selectedSessionStatus = "Start or select a module to see console activity.";
                selectedSessionDescription = "Console output from module startup, setup, settings sync, services, and run subprocesses will appear here.";
                selectedSessionCommandLine = string.Empty;
                selectedSessionFooter = "Waiting for module activity.";
            }
            else
            {
                var visibleSessions = selectedModule.Sessions
                    .Where(session => !string.Equals(session.Id, "overview", StringComparison.OrdinalIgnoreCase))
                    .Where(session => _showCompletedProcesses || session.IsRunning)
                    .ToList();

                sessionItems =
                [
                    new ConsoleSessionItemViewModel
                    {
                        Id = UnifiedSessionId,
                        ModuleSourcePath = selectedModule.SourcePath,
                        SessionSourceId = UnifiedSessionId,
                        Title = "Unified Console",
                        StatusText = "Merged output for this module",
                        Preview = "Includes shared lifecycle logs and completed process history for the selected module."
                    },
                    .. visibleSessions.Select(session => MapSession(selectedModule, session))
                ];

                if (string.IsNullOrWhiteSpace(_selectedSessionId) ||
                    !sessionItems.Any(session => string.Equals(session.Id, _selectedSessionId, StringComparison.Ordinal)))
                {
                    _selectedSessionId = UnifiedSessionId;
                }

                var selectedSessionItem = sessionItems.FirstOrDefault(session => string.Equals(session.Id, _selectedSessionId, StringComparison.Ordinal));
                var selectedSession = selectedSessionItem == null ||
                                      string.Equals(selectedSessionItem.SessionSourceId, UnifiedSessionId, StringComparison.Ordinal)
                    ? null
                    : visibleSessions.FirstOrDefault(session => string.Equals(session.Id, selectedSessionItem.SessionSourceId, StringComparison.Ordinal));

                if (selectedSessionItem == null || string.Equals(_selectedSessionId, UnifiedSessionId, StringComparison.Ordinal))
                {
                    selectedSessionLines = _consoleService.GetUnifiedModuleLines(selectedModule.SourcePath);
                    selectedSessionTitle = $"{selectedModule.Name} / Unified Console";
                    selectedSessionStatus = $"{selectedModule.ActiveProcessCount} active subprocesses";
                    selectedSessionDescription = "Merged output for the selected module. Completed processes stay visible here even when hidden in the per-process list.";
                    selectedSessionCommandLine = string.Empty;
                    selectedSessionFooter = $"{selectedSessionLines.Count} visible lines";
                }
                else if (selectedSession == null)
                {
                    selectedSessionLines = ["No output yet."];
                    selectedSessionTitle = "No console selected";
                    selectedSessionStatus = "Start or select a module to see console activity.";
                    selectedSessionDescription = "Console output from module startup, setup, settings sync, services, and run subprocesses will appear here.";
                    selectedSessionCommandLine = string.Empty;
                    selectedSessionFooter = "Waiting for module activity.";
                }
                else
                {
                    selectedSessionLines = _consoleService.GetSessionLines(selectedModule.SourcePath, selectedSession.Id);
                    selectedSessionTitle = $"{selectedModule.Name} / {selectedSession.Title}";
                    selectedSessionStatus = BuildSelectedStatus(selectedSession);
                    selectedSessionDescription = selectedSession.IsObservedProcess
                        ? "Observed child process started by the module. ASLM can keep it visible as a service, but direct stdout/stderr capture may be unavailable for third-party executables."
                        : string.IsNullOrWhiteSpace(selectedSession.CommandDescription)
                            ? $"{selectedSession.Stage} session"
                            : selectedSession.CommandDescription;
                    selectedSessionCommandLine = selectedSession.CommandLine ?? string.Empty;
                    selectedSessionFooter = BuildFooter(selectedSession);
                }
            }

            var activeProcesses = activeModules.Sum(module => module.ActiveProcessCount);
            var totalSessions = activeModules.Sum(module => module.Sessions.Count);

            return new ConsolesDashboardState
            {
                Subtitle = activeModules.Count == 0
                    ? "No module consoles are available yet."
                    : $"{activeModules.Count} active modules - {activeProcesses} active subprocesses - {totalSessions} sessions",
                ModuleListCaption = activeModules.Count == 0
                    ? "No active modules"
                    : $"{activeModules.Count} active modules",
                ShowCompletedProcesses = _showCompletedProcesses,
                SessionListCaption = isGlobalModule
                    ? "Unified output across active modules."
                    : selectedModule == null
                        ? "Select a module to inspect its output."
                        : $"{sessionItems.Count} visible consoles for {selectedModule.Name}",
                Modules = moduleItems,
                SelectedModuleSourcePath = _selectedModuleSourcePath,
                Sessions = sessionItems,
                SelectedSessionId = _selectedSessionId,
                SelectedSessionTitle = selectedSessionTitle,
                SelectedSessionStatus = selectedSessionStatus,
                SelectedSessionDescription = selectedSessionDescription,
                SelectedSessionCommandLine = selectedSessionCommandLine,
                SelectedSessionLines = selectedSessionLines,
                SelectedSessionFooter = selectedSessionFooter
            };
        }

        private static ConsoleModuleItemViewModel MapModule(ModuleConsoleModuleSnapshot module)
        {
            var activityText = module.LastActivityUtc.HasValue
                ? $"Last activity {module.LastActivityUtc.Value.ToLocalTime():HH:mm:ss}"
                : "No activity yet";

            return new ConsoleModuleItemViewModel
            {
                SourcePath = module.SourcePath,
                Name = module.Name,
                StatusText = $"{(module.IsEnabled ? "Enabled" : "Disabled")} - {module.ActiveProcessCount} active - {module.Sessions.Count} sessions",
                ActivityText = activityText
            };
        }

        private static ConsoleSessionItemViewModel MapSession(ModuleConsoleModuleSnapshot module, ModuleConsoleSessionSnapshot session)
        {
            var status = session.IsObservedProcess
                ? session.IsRunning
                    ? $"Service - Observed{FormatPid(session.ProcessId)}"
                    : $"Service - Stopped{FormatPid(session.ProcessId)}"
                : session.IsRunning
                    ? $"{session.Stage} - Running{FormatPid(session.ProcessId)}"
                    : session.ExitCode.HasValue
                        ? $"{session.Stage} - Exit {session.ExitCode.Value}{FormatPid(session.ProcessId)}"
                        : $"{session.Stage} - Completed";

            return new ConsoleSessionItemViewModel
            {
                Id = $"{module.SourcePath}::{session.Id}",
                ModuleSourcePath = module.SourcePath,
                SessionSourceId = session.Id,
                Title = session.Title,
                StatusText = status,
                Preview = string.IsNullOrWhiteSpace(session.Preview)
                    ? session.IsObservedProcess
                        ? "Observed child process. Direct stdout/stderr capture may be unavailable."
                        : "No output yet."
                    : session.Preview
            };
        }

        private static string BuildSelectedStatus(ModuleConsoleSessionSnapshot session)
        {
            if (session.IsObservedProcess)
            {
                return session.IsRunning
                    ? $"Observed service is running{FormatPid(session.ProcessId)}"
                    : $"Observed service stopped{FormatPid(session.ProcessId)}";
            }

            if (session.IsRunning)
            {
                return $"{session.Stage} session is running{FormatPid(session.ProcessId)}";
            }

            if (session.ExitCode.HasValue)
            {
                return $"{session.Stage} session finished with exit code {session.ExitCode.Value}{FormatPid(session.ProcessId)}";
            }

            return $"{session.Stage} session completed";
        }

        private static string BuildFooter(ModuleConsoleSessionSnapshot session)
        {
            var started = $"Started {session.StartedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            var ended = session.EndedUtc.HasValue
                ? $" - Ended {session.EndedUtc.Value.ToLocalTime():HH:mm:ss}"
                : string.Empty;
            var observedSuffix = session.IsObservedProcess
                ? " - direct stdout/stderr capture may be unavailable"
                : string.Empty;

            return $"{session.LineCount} buffered lines - {started}{ended}{observedSuffix}";
        }

        private static string FormatPid(int? processId)
        {
            return processId.HasValue ? $" - PID {processId.Value}" : string.Empty;
        }
    }

    internal sealed class ConsolesDashboardState
    {
        public string Subtitle { get; set; } = string.Empty;
        public string ModuleListCaption { get; set; } = string.Empty;
        public string SessionListCaption { get; set; } = string.Empty;
        public bool ShowCompletedProcesses { get; set; }
        public IReadOnlyList<ConsoleModuleItemViewModel> Modules { get; set; } = [];
        public string? SelectedModuleSourcePath { get; set; }
        public IReadOnlyList<ConsoleSessionItemViewModel> Sessions { get; set; } = [];
        public string? SelectedSessionId { get; set; }
        public string SelectedSessionTitle { get; set; } = string.Empty;
        public string SelectedSessionStatus { get; set; } = string.Empty;
        public string SelectedSessionDescription { get; set; } = string.Empty;
        public string SelectedSessionCommandLine { get; set; } = string.Empty;
        public IReadOnlyList<string> SelectedSessionLines { get; set; } = [];
        public string SelectedSessionFooter { get; set; } = string.Empty;
    }

    public sealed class ConsolesPageViewModel : INotifyPropertyChanged
    {
        private bool _followOutput = true;
        private bool _showCompletedProcesses;
        private string _subtitle = string.Empty;
        private string _moduleListCaption = string.Empty;
        private string _sessionListCaption = string.Empty;
        private string? _selectedSessionId;
        private string _selectedSessionTitle = "No console selected";
        private string _selectedSessionStatus = string.Empty;
        private string _selectedSessionDescription = string.Empty;
        private string _selectedSessionCommandLine = string.Empty;
        private string _selectedSessionFooter = string.Empty;
        private string _selectedSessionText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<ConsoleModuleItemViewModel> Modules { get; } = new();
        public ObservableCollection<ConsoleSessionItemViewModel> Sessions { get; } = new();
        public bool FollowOutput
        {
            get => _followOutput;
            set => SetProperty(ref _followOutput, value);
        }

        public bool ShowCompletedProcesses
        {
            get => _showCompletedProcesses;
            set => SetProperty(ref _showCompletedProcesses, value);
        }

        public string Subtitle
        {
            get => _subtitle;
            set => SetProperty(ref _subtitle, value);
        }

        public string ModuleListCaption
        {
            get => _moduleListCaption;
            set => SetProperty(ref _moduleListCaption, value);
        }

        public string SessionListCaption
        {
            get => _sessionListCaption;
            set => SetProperty(ref _sessionListCaption, value);
        }

        public string? SelectedSessionId
        {
            get => _selectedSessionId;
            set => SetProperty(ref _selectedSessionId, value);
        }

        public string SelectedSessionTitle
        {
            get => _selectedSessionTitle;
            set => SetProperty(ref _selectedSessionTitle, value);
        }

        public string SelectedSessionStatus
        {
            get => _selectedSessionStatus;
            set => SetProperty(ref _selectedSessionStatus, value);
        }

        public string SelectedSessionDescription
        {
            get => _selectedSessionDescription;
            set => SetProperty(ref _selectedSessionDescription, value);
        }

        public string SelectedSessionCommandLine
        {
            get => _selectedSessionCommandLine;
            set => SetProperty(ref _selectedSessionCommandLine, value);
        }

        public string SelectedSessionFooter
        {
            get => _selectedSessionFooter;
            set => SetProperty(ref _selectedSessionFooter, value);
        }

        public string SelectedSessionText
        {
            get => _selectedSessionText;
            set => SetProperty(ref _selectedSessionText, value);
        }

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

    public sealed class ConsoleModuleItemViewModel : INotifyPropertyChanged
    {
        private string _sourcePath = string.Empty;
        private string _name = string.Empty;
        private string _statusText = string.Empty;
        private string _activityText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string SourcePath
        {
            get => _sourcePath;
            set => SetProperty(ref _sourcePath, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ActivityText
        {
            get => _activityText;
            set => SetProperty(ref _activityText, value);
        }

        public bool IsSameAs(ConsoleModuleItemViewModel other)
        {
            return SourcePath == other.SourcePath &&
                   Name == other.Name &&
                   StatusText == other.StatusText &&
                   ActivityText == other.ActivityText;
        }

        public void UpdateFrom(ConsoleModuleItemViewModel other)
        {
            SourcePath = other.SourcePath;
            Name = other.Name;
            StatusText = other.StatusText;
            ActivityText = other.ActivityText;
        }

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

    public sealed class ConsoleSessionItemViewModel : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _moduleSourcePath = string.Empty;
        private string _sessionSourceId = string.Empty;
        private string _title = string.Empty;
        private string _statusText = string.Empty;
        private string _preview = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string ModuleSourcePath
        {
            get => _moduleSourcePath;
            set => SetProperty(ref _moduleSourcePath, value);
        }

        public string SessionSourceId
        {
            get => _sessionSourceId;
            set => SetProperty(ref _sessionSourceId, value);
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string Preview
        {
            get => _preview;
            set => SetProperty(ref _preview, value);
        }

        public bool IsSameAs(ConsoleSessionItemViewModel other)
        {
            return Id == other.Id &&
                   ModuleSourcePath == other.ModuleSourcePath &&
                   SessionSourceId == other.SessionSourceId &&
                   Title == other.Title &&
                   StatusText == other.StatusText &&
                   Preview == other.Preview;
        }

        public void UpdateFrom(ConsoleSessionItemViewModel other)
        {
            Id = other.Id;
            ModuleSourcePath = other.ModuleSourcePath;
            SessionSourceId = other.SessionSourceId;
            Title = other.Title;
            StatusText = other.StatusText;
            Preview = other.Preview;
        }

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
}
