// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    // Module management view

    /// <summary>
    /// Displays module cards and keeps the grid responsive inside the shell.
    /// </summary>
    public partial class ModuleManagementView : ContentView, INotifyPropertyChanged
    {
        private const double MinCardWidth = 400;

        private readonly UpdateService _updateService;
        private AppShellPage? _shell;
        private int _gridSpan = 1;


        // View data

        /// <summary>
        /// Stores the module cards shown in the dashboard.
        /// </summary>
        public ObservableCollection<ModuleViewModel> Modules { get; } = new();


        // Layout state

        /// <summary>
        /// Gets or sets the number of columns used by the responsive grid.
        /// </summary>
        public int GridSpan
        {
            get => _gridSpan;
            set
            {
                if (_gridSpan == value)
                {
                    return;
                }

                _gridSpan = value;
                OnPropertyChanged();
            }
        }


        // Initialization

        /// <summary>
        /// Creates the module management view and hooks the resize handler.
        /// </summary>
        public ModuleManagementView(UpdateService updateService)
        {
            _updateService = updateService;
            InitializeComponent();
            BindingContext = this;
            SizeChanged += OnSizeChanged;
        }


        // Notifications

        /// <inheritdoc />
        public new event PropertyChangedEventHandler? PropertyChanged;


        // Property change

        /// <summary>
        /// Raises the view-level property changed event.
        /// </summary>
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        // Data binding

        /// <summary>
        /// Initializes the dashboard with the current module list and services.
        /// </summary>
        internal void Initialize(
            AppShellPage shell,
            List<ModuleConfig> modules,
            ModuleInstaller installer,
            ModuleRunner runner)
        {
            _shell = shell;
            PopulateModules(modules, installer, runner);
        }


        // Refresh

        /// <summary>
        /// Rebuilds the dashboard from the latest module list.
        /// </summary>
        internal void RefreshModules(
            List<ModuleConfig> modules,
            ModuleInstaller installer,
            ModuleRunner runner)
        {
            PopulateModules(modules, installer, runner);
        }

        /// <summary>
        /// Scrolls the requested module card into view.
        /// </summary>
        internal void FocusModule(string sourcePath)
        {
            var module = Modules.FirstOrDefault(candidate =>
                string.Equals(candidate.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));

            if (module == null || Dispatcher == null)
            {
                return;
            }

            Dispatcher.Dispatch(() =>
                DashboardView.ScrollTo(module, position: ScrollToPosition.MakeVisible, animate: true));
        }


        // Module population

        /// <summary>
        /// Recreates the module card view models from the current config list.
        /// </summary>
        private void PopulateModules(
            List<ModuleConfig> modules,
            ModuleInstaller installer,
            ModuleRunner runner)
        {
            Modules.Clear();

            foreach (var module in modules)
            {
                Modules.Add(new ModuleViewModel(
                    module,
                    installer,
                    runner,
                    _updateService,
                    OnModuleStateChanged,
                    OnMenuToggleRequested,
                    OpenConfigureUpdates,
                    OpenUpdateDialog));
            }
        }


        // State callback

        /// <summary>
        /// Forwards module state changes back to the shell.
        /// </summary>
        private void OnModuleStateChanged()
        {
            _shell?.OnModuleStateChanged();
        }


        // Menu flow

        /// <summary>
        /// Opens or closes the mini action menu for the selected module card.
        /// </summary>
        private void OnMenuToggleRequested(ModuleViewModel module)
        {
            foreach (var candidate in Modules)
            {
                var shouldStayOpen = ReferenceEquals(candidate, module) && !candidate.IsMenuOpen;
                candidate.SetMenuOpen(shouldStayOpen);
            }
        }

        /// <summary>
        /// Opens the module update configuration overlay for the selected card.
        /// </summary>
        private void OpenConfigureUpdates(ModuleViewModel module)
        {
            CloseAllMenus();
            _shell?.OpenModuleUpdateOverlay(module, ModuleUpdateDialogMode.Configure);
        }

        /// <summary>
        /// Opens the module update details overlay for the selected card.
        /// </summary>
        private void OpenUpdateDialog(ModuleViewModel module)
        {
            CloseAllMenus();
            _shell?.OpenModuleUpdateOverlay(module, ModuleUpdateDialogMode.Update);
        }

        /// <summary>
        /// Closes every module mini menu in the dashboard.
        /// </summary>
        private void CloseAllMenus()
        {
            foreach (var module in Modules)
            {
                module.SetMenuOpen(false);
            }
        }


        // Layout events

        /// <summary>
        /// Recalculates the grid layout when the view size changes.
        /// </summary>
        private void OnSizeChanged(object? sender, EventArgs e)
        {
            RecalculateGridSpan();
        }


        // Layout calculation

        /// <summary>
        /// Computes the number of columns that fit into the available width.
        /// </summary>
        private void RecalculateGridSpan()
        {
            var availableWidth = Width - 60;
            if (availableWidth > 0)
            {
                GridSpan = Math.Max(1, (int)(availableWidth / MinCardWidth));
            }
        }

        /// <summary>
        /// Closes any open module menu when the user taps outside the popup actions.
        /// </summary>
        private void OnBackgroundTapped(object? sender, TappedEventArgs e)
        {
            CloseAllMenus();
        }
    }


    // Module card view model

    /// <summary>
    /// Wraps one module config and exposes UI state and commands for its card.
    /// </summary>
    public class ModuleViewModel : INotifyPropertyChanged
    {
        private static readonly IReadOnlyList<string> SharedSourceModeOptions = ["release", "pre-release", "branch"];

        private readonly ModuleConfig _config;
        private readonly ModuleInstaller _installer;
        private readonly ModuleRunner _runner;
        private readonly UpdateService _updateService;
        private readonly Action _onStateChanged;
        private readonly Action<ModuleViewModel> _onMenuToggleRequested;
        private readonly Action<ModuleViewModel> _onConfigureUpdatesRequested;
        private readonly Action<ModuleViewModel> _onUpdateDialogRequested;
        private readonly ObservableCollection<string> _branchOptions = [];
        private readonly ObservableCollection<UpdateCandidate> _releaseOptions = [];
        private readonly Command _launchCommand;
        private readonly Command _stopCommand;
        private readonly Command _restartCommand;
        private readonly Command _checkUpdateCommand;
        private readonly Command _updateCommand;
        private readonly Command _closeMenuCommand;

        private string _selectedSourceMode = "release";
        private string _selectedBranch = "main";
        private string _updateStatus = "Ready to check.";
        private UpdateCandidate? _updateCandidate;
        private UpdateCandidate? _selectedReleaseOption;
        private bool _hasUpdate;
        private bool _isCheckingUpdate;
        private bool _isUpdating;
        private bool _isRestarting;
        private bool _isStarting;
        private bool _isMenuOpen;
        private bool _hasLoadedBranchOptions;
        private bool _hasLoadedReleaseOptions;
        private bool _isRefreshingBranchOptions;
        private bool _isRefreshingReleaseOptions;
        private string _loadedReleaseMode = string.Empty;
        private readonly StringBuilder _updateLogBuffer = new();
        private string _updateActivityStatus = "Ready to check.";
        private string _updateDownloadDetail = string.Empty;
        private double _updateOverallProgress;
        private double _updateFileProgress;
        private bool _hasUpdateProgress;
        private long _lastDownloadedBytes;
        private DateTime _lastSpeedUpdate = DateTime.UtcNow;
        private double _lastSpeed;


        // Notifications

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;


        // Initialization

        /// <summary>
        /// Creates the card view model for one module.
        /// </summary>
        public ModuleViewModel(
            ModuleConfig config,
            ModuleInstaller installer,
            ModuleRunner runner,
            UpdateService updateService,
            Action onStateChanged,
            Action<ModuleViewModel> onMenuToggleRequested,
            Action<ModuleViewModel> onConfigureUpdatesRequested,
            Action<ModuleViewModel> onUpdateDialogRequested)
        {
            _config = config;
            _installer = installer;
            _runner = runner;
            _updateService = updateService;
            _onStateChanged = onStateChanged;
            _onMenuToggleRequested = onMenuToggleRequested;
            _onConfigureUpdatesRequested = onConfigureUpdatesRequested;
            _onUpdateDialogRequested = onUpdateDialogRequested;

            // Normalize once so every card starts from the persisted update preferences.
            _config.Normalize();
            _selectedSourceMode = _config.Update.Mode;
            _selectedBranch = _config.Update.Branch;
            _branchOptions.Add(_selectedBranch);
            
            _launchCommand = new Command(OnLaunch, CanLaunch);
            _stopCommand = new Command(OnStop, CanStop);
            _restartCommand = new Command(OnRestart, CanRestart);
            ToggleMenuCommand = new Command(ExecuteToggleMenuCommand);
            _closeMenuCommand = new Command(ExecuteCloseMenuCommand);
            OpenConfigureUpdatesCommand = new Command(ExecuteOpenConfigureUpdatesCommand);
            OpenUpdateDialogCommand = new Command(ExecuteOpenUpdateDialogCommand);
            _checkUpdateCommand = new Command(ExecuteCheckUpdateCommand, CanCheckOrUpdate);
            _updateCommand = new Command(ExecuteApplyUpdateCommand, CanApplyUpdate);

            LaunchCommand = _launchCommand;
            StopCommand = _stopCommand;
            RestartCommand = _restartCommand;
            CheckUpdateCommand = _checkUpdateCommand;
            UpdateCommand = _updateCommand;
            CloseMenuCommand = _closeMenuCommand;
        }


        // Static card data

        /// <summary>
        /// Gets the module name shown on the card.
        /// </summary>
        public string Name => _config.Name;

        /// <summary>
        /// Gets the short module description shown on the card.
        /// </summary>
        public string Description => _config.Description;

        /// <summary>
        /// Gets the stable source path of the module manifest.
        /// </summary>
        public string SourcePath => _config.SourcePath;


        // Version label

        /// <summary>
        /// Gets the formatted version label shown on the card.
        /// </summary>
        public string VersionString => $"v{_config.Version}";


        // Icon path

        /// <summary>
        /// Gets the resolved module icon path.
        /// </summary>
        public string? IconFullPath => _config.IconFullPath;


        // Icon flag

        /// <summary>
        /// Gets whether the module has an icon to render.
        /// </summary>
        public bool HasIcon => !string.IsNullOrEmpty(_config.IconFullPath);


        // Running state

        /// <summary>
        /// Gets whether the module is currently enabled.
        /// </summary>
        public bool IsRunning => _config.Status.Enabled;


        // Stopped state

        /// <summary>
        /// Gets whether the module is currently stopped.
        /// </summary>
        public bool IsStopped => !_config.Status.Enabled;


        // Restart state

        /// <summary>
        /// Gets or sets whether the module is currently restarting.
        /// </summary>
        public bool IsRestarting
        {
            get => _isRestarting;
            set
            {
                if (_isRestarting == value)
                {
                    return;
                }

                _isRestarting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNotRestarting));
                OnPropertyChanged(nameof(ShowRunningActions));
                RefreshCommandStates();
            }
        }


        // Restart inverse

        /// <summary>
        /// Gets whether the restart overlay should stay hidden.
        /// </summary>
        public bool IsNotRestarting => !IsRestarting;


        // Update options

        /// <summary>
        /// Gets selectable module update source modes.
        /// </summary>
        public IReadOnlyList<string> SourceModeOptions => SharedSourceModeOptions;

        /// <summary>
        /// Gets repository branches loaded from GitHub.
        /// </summary>
        public ObservableCollection<string> BranchOptions => _branchOptions;

        /// <summary>
        /// Gets selectable release versions loaded from GitHub.
        /// </summary>
        public ObservableCollection<UpdateCandidate> ReleaseOptions => _releaseOptions;

        /// <summary>
        /// Gets or sets the selected module update mode.
        /// </summary>
        public string SelectedSourceMode
        {
            get => _selectedSourceMode;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "release" : value;
                if (string.Equals(_selectedSourceMode, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _selectedSourceMode = normalized;
                _config.Update.Mode = _selectedSourceMode;
                PersistUpdatePreferences();
                _updateCandidate = null;
                HasUpdate = false;
                UpdateStatus = "Ready to check.";
                SetUpdateActivityStatus(UpdateStatus);
                OnPropertyChanged();
                OnPropertyChanged(nameof(UpdateCandidate));
                OnPropertyChanged(nameof(AvailableUpdateLabel));
                OnPropertyChanged(nameof(IsBranchMode));
                OnPropertyChanged(nameof(IsReleaseMode));
                OnPropertyChanged(nameof(UpdateTrackingSummary));
                OnPropertyChanged(nameof(SelectedTargetLabel));
                OnPropertyChanged(nameof(CanInstallSelectedUpdate));
                OnPropertyChanged(nameof(ShowInstallAction));
            }
        }

        /// <summary>
        /// Gets or sets the selected branch.
        /// </summary>
        public string SelectedBranch
        {
            get => _selectedBranch;
            set
            {
                if (_isRefreshingBranchOptions)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(value) ||
                    string.Equals(_selectedBranch, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _selectedBranch = value;
                _config.Update.Branch = _selectedBranch;
                PersistUpdatePreferences();
                _updateCandidate = null;
                HasUpdate = false;
                UpdateStatus = "Ready to check.";
                SetUpdateActivityStatus(UpdateStatus);
                OnPropertyChanged();
                OnPropertyChanged(nameof(UpdateCandidate));
                OnPropertyChanged(nameof(AvailableUpdateLabel));
                OnPropertyChanged(nameof(UpdateTrackingSummary));
            }
        }

        /// <summary>
        /// Gets or sets the selected release target.
        /// </summary>
        public UpdateCandidate? SelectedReleaseOption
        {
            get => _selectedReleaseOption;
            set
            {
                if (_isRefreshingReleaseOptions)
                {
                    return;
                }

                var selectedTag = value?.ReleaseTag;
                if (string.Equals(_selectedReleaseOption?.ReleaseTag, selectedTag, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _selectedReleaseOption = value;
                _config.Update.SelectedReleaseTag = string.IsNullOrWhiteSpace(selectedTag) ? null : selectedTag;
                PersistUpdatePreferences();

                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTargetLabel));
                OnPropertyChanged(nameof(UpdateTrackingSummary));
                OnPropertyChanged(nameof(CanInstallSelectedUpdate));
                OnPropertyChanged(nameof(ShowInstallAction));
                RefreshCommandStates();
            }
        }

        /// <summary>
        /// Gets whether branch update controls should be visible.
        /// </summary>
        public bool IsBranchMode => string.Equals(SelectedSourceMode, "branch", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether release channel controls should be visible.
        /// </summary>
        public bool IsReleaseMode => !IsBranchMode;

        /// <summary>
        /// Gets a short text description of the active update tracking mode.
        /// </summary>
        public string UpdateTrackingSummary => IsBranchMode
            ? $"Tracking branch '{SelectedBranch}'"
            : $"Selected {SelectedSourceMode} version: {SelectedTargetLabel}";


        // Update state

        /// <summary>
        /// Gets update status text shown on the module card.
        /// </summary>
        public string UpdateStatus
        {
            get => _updateStatus;
            private set
            {
                if (_updateStatus == value)
                {
                    return;
                }

                _updateStatus = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the current update candidate loaded for the card.
        /// </summary>
        public UpdateCandidate? UpdateCandidate => _updateCandidate;

        /// <summary>
        /// Gets whether an update candidate is available.
        /// </summary>
        public bool HasUpdate
        {
            get => _hasUpdate;
            private set
            {
                if (_hasUpdate == value)
                {
                    return;
                }

                _hasUpdate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(AvailableUpdateLabel));
                OnPropertyChanged(nameof(ShowInstallAction));
                OnPropertyChanged(nameof(ShowCardUpdateAction));
                RefreshCommandStates();
            }
        }

        /// <summary>
        /// Gets a short label for the currently available update.
        /// </summary>
        public string AvailableUpdateLabel => _updateCandidate == null
            ? "No update detected"
            : _updateCandidate.RemoteVersion;

        /// <summary>
        /// Gets the currently selected install target for the dialog header.
        /// </summary>
        public string SelectedTargetLabel => IsBranchMode
            ? SelectedBranch
            : _selectedReleaseOption?.DisplayName
                ?? _selectedReleaseOption?.RemoteVersion
                ?? "No version selected";

        /// <summary>
        /// Gets whether an update operation can be started.
        /// </summary>
        public bool CanUpdate => CanInstallSelectedUpdate;

        /// <summary>
        /// Gets whether the current selection can be installed.
        /// </summary>
        public bool CanInstallSelectedUpdate => ResolveSelectedInstallCandidate() != null && !IsBusy;

        /// <summary>
        /// Gets whether update checking is in progress.
        /// </summary>
        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            private set
            {
                if (_isCheckingUpdate == value)
                {
                    return;
                }

                _isCheckingUpdate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(CanCheckUpdates));
                OnPropertyChanged(nameof(HasUpdateProgress));
                OnPropertyChanged(nameof(ShowInstallAction));
                OnPropertyChanged(nameof(ShowHeaderBusyIndicator));
                RefreshCommandStates();
            }
        }

        /// <summary>
        /// Gets whether update installation is in progress.
        /// </summary>
        public bool IsUpdating
        {
            get => _isUpdating;
            private set
            {
                if (_isUpdating == value)
                {
                    return;
                }

                _isUpdating = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanUpdate));
                OnPropertyChanged(nameof(CanCheckUpdates));
                OnPropertyChanged(nameof(HasUpdateProgress));
                OnPropertyChanged(nameof(ShowInstallAction));
                OnPropertyChanged(nameof(ShowUpdatingStatus));
                OnPropertyChanged(nameof(CanShowLaunchAction));
                OnPropertyChanged(nameof(ShowCardUpdateAction));
                OnPropertyChanged(nameof(ShowHeaderBusyIndicator));
                RefreshCommandStates();
            }
        }

        /// <summary>
        /// Gets whether any update-related work is currently running.
        /// </summary>
        public bool IsBusy => IsCheckingUpdate || IsUpdating;

        /// <summary>
        /// Gets whether the update check action should be enabled.
        /// </summary>
        public bool CanCheckUpdates => !IsBusy;

        /// <summary>
        /// Gets whether the install action should stay visible for the current target selection.
        /// </summary>
        public bool ShowInstallAction => CanInstallSelectedUpdate && !IsUpdating;

        /// <summary>
        /// Gets the activity status cached for the module update dialog.
        /// </summary>
        public string UpdateActivityStatus => string.IsNullOrWhiteSpace(_updateActivityStatus)
            ? UpdateStatus
            : _updateActivityStatus;

        /// <summary>
        /// Gets whether the update dialog should show progress bars.
        /// </summary>
        public bool HasUpdateProgress => _hasUpdateProgress || IsBusy;

        /// <summary>
        /// Gets the cached overall update progress fraction.
        /// </summary>
        public double UpdateOverallProgress => _updateOverallProgress;

        /// <summary>
        /// Gets the cached file download progress fraction.
        /// </summary>
        public double UpdateFileProgress => _updateFileProgress;

        /// <summary>
        /// Gets the cached transfer detail line.
        /// </summary>
        public string UpdateDownloadDetail => _updateDownloadDetail;

        /// <summary>
        /// Gets whether the cached transfer detail line should be shown.
        /// </summary>
        public bool HasUpdateDownloadDetail => !string.IsNullOrWhiteSpace(UpdateDownloadDetail);

        /// <summary>
        /// Gets the cached update console text.
        /// </summary>
        public string UpdateLogText => _updateLogBuffer.ToString();

        /// <summary>
        /// Gets whether the update console already contains lines.
        /// </summary>
        public bool HasUpdateLog => _updateLogBuffer.Length > 0;

        /// <summary>
        /// Gets whether the header should show the lightweight check spinner.
        /// </summary>
        public bool ShowHeaderBusyIndicator => IsCheckingUpdate;

        /// <summary>
        /// Gets whether the compact update action should stay visible in the card header.
        /// </summary>
        public bool ShowCardUpdateAction => HasUpdate && !IsUpdating;

        /// <summary>
        /// Gets or sets whether the module is currently starting.
        /// </summary>
        public bool IsStarting
        {
            get => _isStarting;
            private set
            {
                if (_isStarting == value)
                {
                    return;
                }

                _isStarting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanShowLaunchAction));
                OnPropertyChanged(nameof(ShowStartingStatus));
                OnPropertyChanged(nameof(ShowRunningActions));
                RefreshCommandStates();
            }
        }

        /// <summary>
        /// Gets whether the launch button should stay visible.
        /// </summary>
        public bool CanShowLaunchAction => IsStopped && !IsUpdating && !IsStarting;

        /// <summary>
        /// Gets whether the running action buttons should stay visible.
        /// </summary>
        public bool ShowRunningActions => IsRunning && !IsRestarting && !IsStarting;

        /// <summary>
        /// Gets whether the updating status pill should be visible.
        /// </summary>
        public bool ShowUpdatingStatus => IsUpdating && IsStopped;

        /// <summary>
        /// Gets whether the starting status pill should be visible.
        /// </summary>
        public bool ShowStartingStatus => IsStarting;


        // Card menu

        /// <summary>
        /// Gets whether the mini actions menu is open for this card.
        /// </summary>
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            private set
            {
                if (_isMenuOpen == value)
                {
                    return;
                }

                _isMenuOpen = value;
                OnPropertyChanged();
            }
        }


        // Launch command

        /// <summary>
        /// Gets the command that launches the module.
        /// </summary>
        public ICommand LaunchCommand { get; }


        // Stop command

        /// <summary>
        /// Gets the command that stops the module.
        /// </summary>
        public ICommand StopCommand { get; }


        // Restart command

        /// <summary>
        /// Gets the command that restarts the module.
        /// </summary>
        public ICommand RestartCommand { get; }

        /// <summary>
        /// Gets the command that toggles the mini actions menu.
        /// </summary>
        public ICommand ToggleMenuCommand { get; }

        /// <summary>
        /// Gets the command that closes the mini actions menu.
        /// </summary>
        public ICommand CloseMenuCommand { get; }

        /// <summary>
        /// Gets the command that checks for module updates.
        /// </summary>
        public ICommand CheckUpdateCommand { get; }

        /// <summary>
        /// Gets the command that opens update configuration for the module.
        /// </summary>
        public ICommand OpenConfigureUpdatesCommand { get; }

        /// <summary>
        /// Gets the command that opens the update details dialog.
        /// </summary>
        public ICommand OpenUpdateDialogCommand { get; }

        /// <summary>
        /// Gets the command that applies an available module update.
        /// </summary>
        public ICommand UpdateCommand { get; }


        // Menu helpers

        /// <summary>
        /// Opens or closes the card mini menu through the parent dashboard.
        /// </summary>
        private void ExecuteToggleMenuCommand()
        {
            _onMenuToggleRequested(this);
        }

        /// <summary>
        /// Closes the mini menu without running an action.
        /// </summary>
        private void ExecuteCloseMenuCommand()
        {
            SetMenuOpen(false);
        }

        /// <summary>
        /// Closes the menu and opens the update configuration dialog.
        /// </summary>
        private void ExecuteOpenConfigureUpdatesCommand()
        {
            SetMenuOpen(false);
            _onConfigureUpdatesRequested(this);
        }

        /// <summary>
        /// Opens the update details dialog when an update exists.
        /// </summary>
        private void ExecuteOpenUpdateDialogCommand()
        {
            if (!HasUpdate)
            {
                return;
            }

            SetMenuOpen(false);
            _onUpdateDialogRequested(this);
        }

        /// <summary>
        /// Sets the visible state of the mini menu.
        /// </summary>
        internal void SetMenuOpen(bool isOpen)
        {
            IsMenuOpen = isOpen;
        }


        // Update command helpers

        /// <summary>
        /// Returns whether update-related commands may run.
        /// </summary>
        private bool CanCheckOrUpdate()
        {
            return !IsCheckingUpdate && !IsUpdating;
        }

        /// <summary>
        /// Returns whether the apply-update command may run.
        /// </summary>
        private bool CanApplyUpdate()
        {
            return CanInstallSelectedUpdate && CanCheckOrUpdate();
        }

        /// <summary>
        /// Returns whether the launch command may run.
        /// </summary>
        private bool CanLaunch()
        {
            return IsStopped && !IsStarting && !IsUpdating;
        }

        /// <summary>
        /// Returns whether the stop command may run.
        /// </summary>
        private bool CanStop()
        {
            return IsRunning && !IsRestarting && !IsStarting && !IsUpdating;
        }

        /// <summary>
        /// Returns whether the restart command may run.
        /// </summary>
        private bool CanRestart()
        {
            return IsRunning && !IsRestarting && !IsStarting && !IsUpdating;
        }

        /// <summary>
        /// Refreshes command enabled states after one flag changes.
        /// </summary>
        private void RefreshCommandStates()
        {
            _launchCommand.ChangeCanExecute();
            _stopCommand.ChangeCanExecute();
            _restartCommand.ChangeCanExecute();
            _checkUpdateCommand.ChangeCanExecute();
            _updateCommand.ChangeCanExecute();
        }

        /// <summary>
        /// Persists the module update preferences after one setting changes.
        /// </summary>
        private void PersistUpdatePreferences()
        {
            // Normalize once so the in-memory fields mirror the persisted representation exactly.
            _config.Update.Normalize();
            _selectedSourceMode = _config.Update.Mode;
            _selectedBranch = _config.Update.Branch;
            _config.Update.SelectedReleaseTag = string.IsNullOrWhiteSpace(_config.Update.SelectedReleaseTag)
                ? null
                : _config.Update.SelectedReleaseTag;
            _updateService.SaveModuleUpdatePreferences(_config);
        }

        /// <summary>
        /// Bridges the check-update command into the asynchronous workflow.
        /// </summary>
        private async void ExecuteCheckUpdateCommand()
        {
            SetMenuOpen(false);
            await RefreshUpdateStateAsync(forceOptionLoad: false);
        }

        /// <summary>
        /// Bridges the apply-update command into the asynchronous workflow.
        /// </summary>
        private async void ExecuteApplyUpdateCommand()
        {
            await ApplyUpdateAsync();
        }

        /// <summary>
        /// Refreshes the update candidate state for this module.
        /// </summary>
        internal async Task RefreshUpdateStateAsync(bool forceOptionLoad)
        {
            if (IsCheckingUpdate)
            {
                return;
            }

            IsCheckingUpdate = true;
            UpdateStatus = "Checking for updates...";
            SetUpdateActivityStatus(UpdateStatus);

            try
            {
                await EnsureSelectionOptionsLoadedAsync(forceOptionLoad);

                _updateCandidate = await _updateService.CheckModuleUpdateAsync(_config);
                HasUpdate = _updateCandidate != null;
                OnPropertyChanged(nameof(UpdateCandidate));
                OnPropertyChanged(nameof(AvailableUpdateLabel));
                ApplyCheckResultSelection();

                UpdateStatus = _updateCandidate == null
                    ? "Up to date"
                    : $"Update available: {_updateCandidate.RemoteVersion}";
                SetUpdateActivityStatus(UpdateStatus);
            }
            catch (Exception ex)
            {
                _updateCandidate = null;
                HasUpdate = false;
                OnPropertyChanged(nameof(UpdateCandidate));
                OnPropertyChanged(nameof(AvailableUpdateLabel));
                UpdateStatus = $"Check failed: {ex.Message}";
                SetUpdateActivityStatus(UpdateStatus);
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        /// <summary>
        /// Ensures the dialog pickers have the data required by the currently selected update mode.
        /// </summary>
        internal async Task EnsureSelectionOptionsLoadedAsync(bool forceRefresh)
        {
            if (IsBranchMode)
            {
                if (forceRefresh || !_hasLoadedBranchOptions || BranchOptions.Count == 0)
                {
                    await LoadBranchesAsync();
                }

                return;
            }

            if (forceRefresh ||
                !_hasLoadedReleaseOptions ||
                ReleaseOptions.Count == 0 ||
                !string.Equals(_loadedReleaseMode, _selectedSourceMode, StringComparison.OrdinalIgnoreCase))
            {
                await LoadReleaseOptionsAsync();
            }
        }

        /// <summary>
        /// Loads branch names for the configured GitHub repository.
        /// </summary>
        private async Task LoadBranchesAsync()
        {
            try
            {
                var branches = await _updateService.GetModuleBranchesAsync(_config);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var selectedBranch = _selectedBranch;
                    _isRefreshingBranchOptions = true;
                    BranchOptions.Clear();
                    foreach (var branch in branches)
                    {
                        BranchOptions.Add(branch.Name);
                    }

                    // Keep the persisted branch selectable even when the remote list changed or failed before.
                    if (!BranchOptions.Contains(selectedBranch))
                    {
                        BranchOptions.Insert(0, selectedBranch);
                    }

                    _hasLoadedBranchOptions = true;
                    _isRefreshingBranchOptions = false;
                    OnPropertyChanged(nameof(SelectedBranch));
                    OnPropertyChanged(nameof(BranchOptions));
                });
            }
            catch
            {
                if (!BranchOptions.Contains(_selectedBranch))
                {
                    BranchOptions.Add(_selectedBranch);
                }

                _hasLoadedBranchOptions = true;
                OnPropertyChanged(nameof(SelectedBranch));
                OnPropertyChanged(nameof(BranchOptions));
            }
        }

        /// <summary>
        /// Loads release versions for the current release stream without forcing an update check.
        /// </summary>
        private async Task LoadReleaseOptionsAsync()
        {
            var releases = await _updateService.GetModuleReleaseCandidatesAsync(_config);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var preferredTag = ResolvePreferredReleaseTag(releases);
                _isRefreshingReleaseOptions = true;
                ReleaseOptions.Clear();

                foreach (var release in releases)
                {
                    ReleaseOptions.Add(release);
                }

                _selectedReleaseOption = ReleaseOptions.FirstOrDefault(candidate =>
                    string.Equals(candidate.ReleaseTag, preferredTag, StringComparison.OrdinalIgnoreCase));

                _hasLoadedReleaseOptions = true;
                _loadedReleaseMode = _selectedSourceMode;
                _isRefreshingReleaseOptions = false;
                OnPropertyChanged(nameof(ReleaseOptions));
                OnPropertyChanged(nameof(SelectedReleaseOption));
                OnPropertyChanged(nameof(SelectedTargetLabel));
                OnPropertyChanged(nameof(UpdateTrackingSummary));
                OnPropertyChanged(nameof(CanInstallSelectedUpdate));
                OnPropertyChanged(nameof(ShowInstallAction));
                RefreshCommandStates();
            });
        }

        /// <summary>
        /// Picks the release tag that should stay selected after a version list refresh.
        /// </summary>
        private string? ResolvePreferredReleaseTag(IEnumerable<UpdateCandidate> releases)
        {
            if (!string.IsNullOrWhiteSpace(_config.Update.SelectedReleaseTag) &&
                releases.Any(candidate =>
                    string.Equals(candidate.ReleaseTag, _config.Update.SelectedReleaseTag, StringComparison.OrdinalIgnoreCase)))
            {
                return _config.Update.SelectedReleaseTag;
            }

            if (!string.IsNullOrWhiteSpace(_config.Update.InstalledReleaseTag) &&
                releases.Any(candidate =>
                    string.Equals(candidate.ReleaseTag, _config.Update.InstalledReleaseTag, StringComparison.OrdinalIgnoreCase)))
            {
                return _config.Update.InstalledReleaseTag;
            }

            return releases.FirstOrDefault()?.ReleaseTag;
        }

        /// <summary>
        /// Aligns the selected release target with the newest update when the user has not pinned a version.
        /// </summary>
        private void ApplyCheckResultSelection()
        {
            if (IsBranchMode || _updateCandidate == null || !string.IsNullOrWhiteSpace(_config.Update.SelectedReleaseTag))
            {
                OnPropertyChanged(nameof(CanInstallSelectedUpdate));
                OnPropertyChanged(nameof(ShowInstallAction));
                RefreshCommandStates();
                return;
            }

            var matchingRelease = ReleaseOptions.FirstOrDefault(candidate =>
                string.Equals(candidate.ReleaseTag, _updateCandidate.ReleaseTag, StringComparison.OrdinalIgnoreCase));
            if (matchingRelease != null)
            {
                _selectedReleaseOption = matchingRelease;
                OnPropertyChanged(nameof(SelectedReleaseOption));
                OnPropertyChanged(nameof(SelectedTargetLabel));
                OnPropertyChanged(nameof(UpdateTrackingSummary));
            }

            OnPropertyChanged(nameof(CanInstallSelectedUpdate));
            OnPropertyChanged(nameof(ShowInstallAction));
            RefreshCommandStates();
        }

        /// <summary>
        /// Applies the latest available update candidate.
        /// </summary>
        internal async Task<bool> ApplyUpdateAsync(
            IProgress<string>? log = null,
            IProgress<DownloadProgress>? progress = null)
        {
            var installCandidate = ResolveSelectedInstallCandidate();
            if (installCandidate == null || IsUpdating)
            {
                return false;
            }

            IsUpdating = true;
            UpdateStatus = "Updating...";
            SetUpdateActivityStatus(UpdateStatus);

            // Reflect the stopped state immediately so the card does not offer launch actions mid-update.
            if (_config.Status.Enabled)
            {
                _config.Status.Enabled = false;
                NotifyStateChanged();
            }

            IProgress<string> debugLog = new Progress<string>(message =>
                Debug.WriteLine($"[ModuleUpdate:{Name}] {message}"));

            try
            {
                var logSink = log;
                var progressSink = progress;

                // Forward messages into both the debug output and the optional dialog log.
                var combinedLog = new Progress<string>(message =>
                {
                    debugLog.Report(message);
                    AppendUpdateLog(message);
                    AdvanceProgressFromLog(message);
                    SetUpdateActivityStatus(message);
                    logSink?.Report(message);
                });

                var combinedProgress = new Progress<DownloadProgress>(download =>
                {
                    UpdateCachedDownloadProgress(download);
                    progressSink?.Report(download);
                });

                var success = await _updateService.ApplyModuleUpdateAsync(
                    installCandidate,
                    combinedLog,
                    combinedProgress,
                    CancellationToken.None);

                if (success)
                {
                    await ReloadInstalledConfigAsync();
                }

                UpdateStatus = success ? "Updated" : "Update failed";
                SetUpdateActivityStatus(UpdateStatus);
                SetOverallProgress(success ? 1.0 : Math.Max(_updateOverallProgress, 0.15));
                HasUpdate = false;
                _updateCandidate = null;
                OnPropertyChanged(nameof(UpdateCandidate));
                OnPropertyChanged(nameof(AvailableUpdateLabel));
                _onStateChanged?.Invoke();
                await EnsureSelectionOptionsLoadedAsync(forceRefresh: false);
                await RefreshUpdateStateAsync(forceOptionLoad: false);
                return success;
            }
            finally
            {
                IsUpdating = false;
                OnPropertyChanged(nameof(ShowUpdatingStatus));
                OnPropertyChanged(nameof(CanShowLaunchAction));
            }
        }


        // Launch flow

        /// <summary>
        /// Loads the latest config, completes first-run setup if needed, and starts the module.
        /// </summary>
        private async void OnLaunch()
        {
            if (!CanLaunch())
            {
                return;
            }

            IsStarting = true;

            await ReloadEditableConfigAsync();

            try
            {
                // First-run setup must complete before the normal run commands can start.
                if (!_config.Status.FirstRunCompleted)
                {
                    var setupLog = new Progress<string>(message => Debug.WriteLine($"[Setup] {message}"));
                    var setupSuccess = await Task.Run(() =>
                        _runner.ExecuteFirstRunAsync(_config, setupLog, CancellationToken.None));

                    if (!setupSuccess)
                    {
                        Debug.WriteLine("Setup failed, cannot launch.");
                        return;
                    }

                    _config.Status.FirstRunCompleted = true;
                    _installer.SaveModuleConfig(_config);
                }

                _config.Status.Enabled = true;
                _installer.SaveModuleConfig(_config);
                NotifyStateChanged();
                _onStateChanged?.Invoke();

                // Start background run commands only when the module exposes them.
                if (_config.Commands.Run.Count > 0)
                {
                    var launchLog = new Progress<string>(message => Debug.WriteLine($"[Launch] {message}"));
                    _ = Task.Run(() => _runner.ExecuteRunAsync(_config, launchLog, CancellationToken.None));
                }
            }
            finally
            {
                IsStarting = false;
            }
        }


        // Stop flow

        /// <summary>
        /// Stops the module and persists the disabled state.
        /// </summary>
        private async void OnStop()
        {
            if (!CanStop())
            {
                return;
            }

            await _runner.StopModuleAsync(_config.SourcePath);

            _config.Status.Enabled = false;
            _installer.SaveModuleConfig(_config);
            NotifyStateChanged();
            _onStateChanged?.Invoke();
        }


        // Restart flow

        /// <summary>
        /// Stops and starts the module again while showing the restart state.
        /// </summary>
        private async void OnRestart()
        {
            if (!CanRestart())
            {
                return;
            }

            await ReloadEditableConfigAsync();

            IsRestarting = true;

            try
            {
                await _runner.StopModuleAsync(_config.SourcePath);
                await Task.Delay(1000);

                var restartLog = new Progress<string>(message => Debug.WriteLine($"[Restart] {message}"));
                _ = Task.Run(() => _runner.ExecuteRunAsync(_config, restartLog, CancellationToken.None));
            }
            finally
            {
                IsRestarting = false;
            }
        }


        // Config refresh

        /// <summary>
        /// Reloads module settings and commands from disk before launch or restart.
        /// </summary>
        private async Task ReloadEditableConfigAsync()
        {
            // Launch and restart should respect the latest settings saved in the overlay,
            // so the card refreshes the mutable sections from disk just before execution.
            var freshConfig = await _installer.LoadModuleConfig(_config.SourcePath);
            if (freshConfig == null)
            {
                return;
            }

            _config.Settings = freshConfig.Settings;
            _config.Commands = freshConfig.Commands;
        }

        /// <summary>
        /// Reloads the installed manifest after a module update changed files on disk.
        /// </summary>
        private async Task ReloadInstalledConfigAsync()
        {
            var freshConfig = await _installer.LoadModuleConfig(_config.SourcePath);
            if (freshConfig == null)
            {
                return;
            }

            ApplyReloadedConfig(freshConfig);
        }

        /// <summary>
        /// Copies the refreshed manifest data back into the live card view model.
        /// </summary>
        private void ApplyReloadedConfig(ModuleConfig freshConfig)
        {
            // The card keeps the same backing object reference, so we copy new manifest data into it.
            _config.FileVersion = freshConfig.FileVersion;
            _config.Id = freshConfig.Id;
            _config.Name = freshConfig.Name;
            _config.Description = freshConfig.Description;
            _config.Version = freshConfig.Version;
            _config.Author = freshConfig.Author;
            _config.Type = freshConfig.Type;
            _config.Source = freshConfig.Source;
            _config.Dependencies = freshConfig.Dependencies;
            _config.Commands = freshConfig.Commands;
            _config.HasPage = freshConfig.HasPage;
            _config.Icon = freshConfig.Icon;
            _config.SidebarIcon = freshConfig.SidebarIcon;
            _config.Settings = freshConfig.Settings;
            _config.DownloadsBridge = freshConfig.DownloadsBridge;
            _config.Update = freshConfig.Update;
            _config.Status = freshConfig.Status;
            _config.SourcePath = freshConfig.SourcePath;

            _config.Update.Normalize();
            _selectedSourceMode = _config.Update.Mode;
            _selectedBranch = _config.Update.Branch;
            _selectedReleaseOption = null;
            _hasLoadedReleaseOptions = false;
            _loadedReleaseMode = string.Empty;

            // Rebuild branches so the picker keeps the persisted value after a release-based update.
            BranchOptions.Clear();
            BranchOptions.Add(_selectedBranch);
            _hasLoadedBranchOptions = false;

            NotifyModuleMetadataChanged();
            NotifyStateChanged();
        }


        // UI state refresh

        /// <summary>
        /// Raises property changes for the running state flags.
        /// </summary>
        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsStopped));
            OnPropertyChanged(nameof(CanShowLaunchAction));
            OnPropertyChanged(nameof(ShowRunningActions));
            OnPropertyChanged(nameof(ShowUpdatingStatus));
            RefreshCommandStates();
        }

        /// <summary>
        /// Raises property changes for module metadata that may change after an update.
        /// </summary>
        private void NotifyModuleMetadataChanged()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(VersionString));
            OnPropertyChanged(nameof(IconFullPath));
            OnPropertyChanged(nameof(HasIcon));
            OnPropertyChanged(nameof(SourceModeOptions));
            OnPropertyChanged(nameof(ReleaseOptions));
            OnPropertyChanged(nameof(BranchOptions));
            OnPropertyChanged(nameof(SelectedSourceMode));
            OnPropertyChanged(nameof(SelectedReleaseOption));
            OnPropertyChanged(nameof(SelectedBranch));
            OnPropertyChanged(nameof(IsBranchMode));
            OnPropertyChanged(nameof(IsReleaseMode));
            OnPropertyChanged(nameof(SelectedTargetLabel));
            OnPropertyChanged(nameof(UpdateTrackingSummary));
            OnPropertyChanged(nameof(CanInstallSelectedUpdate));
            OnPropertyChanged(nameof(ShowInstallAction));
            OnPropertyChanged(nameof(ShowCardUpdateAction));
            OnPropertyChanged(nameof(ShowHeaderBusyIndicator));
        }

        /// <summary>
        /// Returns the update candidate represented by the current UI selection.
        /// </summary>
        private UpdateCandidate? ResolveSelectedInstallCandidate()
        {
            if (IsBranchMode)
            {
                return _updateCandidate;
            }

            if (_selectedReleaseOption == null || string.IsNullOrWhiteSpace(_selectedReleaseOption.ReleaseTag))
            {
                return null;
            }

            var currentTag = _config.Update.InstalledReleaseTag ?? _config.Status.InstalledVersion ?? _config.Version;
            return string.Equals(currentTag, _selectedReleaseOption.ReleaseTag, StringComparison.OrdinalIgnoreCase)
                ? null
                : _selectedReleaseOption;
        }

        /// <summary>
        /// Resets the cached update session before a new dialog action begins.
        /// </summary>
        internal void ResetUpdateSession(bool clearLog)
        {
            if (clearLog)
            {
                _updateLogBuffer.Clear();
                OnPropertyChanged(nameof(UpdateLogText));
                OnPropertyChanged(nameof(HasUpdateLog));
            }

            _updateDownloadDetail = string.Empty;
            _updateOverallProgress = 0;
            _updateFileProgress = 0;
            _hasUpdateProgress = false;
            _lastDownloadedBytes = 0;
            _lastSpeedUpdate = DateTime.UtcNow;
            _lastSpeed = 0;
            _updateActivityStatus = UpdateStatus;

            OnPropertyChanged(nameof(UpdateActivityStatus));
            OnPropertyChanged(nameof(UpdateDownloadDetail));
            OnPropertyChanged(nameof(HasUpdateDownloadDetail));
            OnPropertyChanged(nameof(UpdateOverallProgress));
            OnPropertyChanged(nameof(UpdateFileProgress));
            OnPropertyChanged(nameof(HasUpdateProgress));
        }

        /// <summary>
        /// Clears finished update output before reopening the dialog while preserving active sessions.
        /// </summary>
        internal void ResetCompletedUpdateSession()
        {
            if (IsBusy)
            {
                return;
            }

            if (string.Equals(UpdateStatus, "Updated", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(UpdateStatus, "Update failed", StringComparison.OrdinalIgnoreCase))
            {
                UpdateStatus = "Ready to check.";
            }

            ResetUpdateSession(clearLog: true);
            SetUpdateActivityStatus(UpdateStatus);
        }

        /// <summary>
        /// Appends one line to the cached module update console.
        /// </summary>
        internal void AppendUpdateLog(string message)
        {
            _updateLogBuffer.AppendLine(message);
            OnPropertyChanged(nameof(UpdateLogText));
            OnPropertyChanged(nameof(HasUpdateLog));
        }

        /// <summary>
        /// Updates the cached activity status shown in the update dialog.
        /// </summary>
        internal void SetUpdateActivityStatus(string value)
        {
            if (string.Equals(_updateActivityStatus, value, StringComparison.Ordinal))
            {
                return;
            }

            _updateActivityStatus = value;
            OnPropertyChanged(nameof(UpdateActivityStatus));
        }

        /// <summary>
        /// Advances the coarse overall progress based on high-level installer log lines.
        /// </summary>
        private void AdvanceProgressFromLog(string message)
        {
            if (message.Contains("Stopping", StringComparison.OrdinalIgnoreCase))
            {
                SetOverallProgress(0.18);
            }
            else if (message.Contains("Preserved", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("Restored", StringComparison.OrdinalIgnoreCase))
            {
                SetOverallProgress(Math.Max(_updateOverallProgress, 0.62));
            }
            else if (message.Contains("Running first-run setup", StringComparison.OrdinalIgnoreCase))
            {
                SetOverallProgress(Math.Max(_updateOverallProgress, 0.84));
            }
            else if (message.Contains("updated", StringComparison.OrdinalIgnoreCase))
            {
                SetOverallProgress(1.0);
            }
        }

        /// <summary>
        /// Updates the cached transfer progress shown in the module update dialog.
        /// </summary>
        private void UpdateCachedDownloadProgress(DownloadProgress progress)
        {
            if (progress.TotalBytes <= 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var elapsed = (now - _lastSpeedUpdate).TotalSeconds;
            if (elapsed >= 0.5)
            {
                _lastSpeed = (progress.DownloadedBytes - _lastDownloadedBytes) / elapsed;
                _lastDownloadedBytes = progress.DownloadedBytes;
                _lastSpeedUpdate = now;
            }

            var detail = $"{FormatBytes(progress.DownloadedBytes)} / {FormatBytes(progress.TotalBytes)}";
            if (_lastSpeed > 0)
            {
                detail += $" - {FormatBytes((long)_lastSpeed)}/s";
            }

            SetOverallProgress(Math.Max(_updateOverallProgress, 0.12 + (progress.Fraction * 0.38)));
            SetFileProgress(progress.Fraction);
            SetDownloadDetail(detail);
        }

        /// <summary>
        /// Updates the cached overall progress fraction.
        /// </summary>
        private void SetOverallProgress(double value)
        {
            var clamped = Math.Max(0, Math.Min(1, value));
            if (Math.Abs(_updateOverallProgress - clamped) < 0.0001)
            {
                return;
            }

            _updateOverallProgress = clamped;
            _hasUpdateProgress = true;
            OnPropertyChanged(nameof(UpdateOverallProgress));
            OnPropertyChanged(nameof(HasUpdateProgress));
        }

        /// <summary>
        /// Updates the cached file progress fraction.
        /// </summary>
        private void SetFileProgress(double value)
        {
            var clamped = Math.Max(0, Math.Min(1, value));
            if (Math.Abs(_updateFileProgress - clamped) < 0.0001)
            {
                return;
            }

            _updateFileProgress = clamped;
            _hasUpdateProgress = true;
            OnPropertyChanged(nameof(UpdateFileProgress));
            OnPropertyChanged(nameof(HasUpdateProgress));
        }

        /// <summary>
        /// Updates the cached download detail line.
        /// </summary>
        private void SetDownloadDetail(string value)
        {
            if (string.Equals(_updateDownloadDetail, value, StringComparison.Ordinal))
            {
                return;
            }

            _updateDownloadDetail = value;
            OnPropertyChanged(nameof(UpdateDownloadDetail));
            OnPropertyChanged(nameof(HasUpdateDownloadDetail));
        }

        /// <summary>
        /// Formats byte counts into readable size labels.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824)
            {
                return $"{bytes / 1_073_741_824.0:F1} GB";
            }

            if (bytes >= 1_048_576)
            {
                return $"{bytes / 1_048_576.0:F1} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes} B";
        }


        // Property change

        /// <summary>
        /// Raises the view-model property changed event.
        /// </summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
