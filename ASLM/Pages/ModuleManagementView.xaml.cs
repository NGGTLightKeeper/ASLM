// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
                    OnModuleStateChanged));
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
    }


    // Module card view model

    /// <summary>
    /// Wraps one module config and exposes UI state and commands for its card.
    /// </summary>
    public class ModuleViewModel : INotifyPropertyChanged
    {
        private static readonly IReadOnlyList<string> SharedSourceModeOptions = ["release", "branch"];
        private static readonly IReadOnlyList<string> SharedChannelOptions = ["release", "pre-release"];

        private readonly ModuleConfig _config;
        private readonly ModuleInstaller _installer;
        private readonly ModuleRunner _runner;
        private readonly UpdateService _updateService;
        private readonly Action _onStateChanged;
        private readonly ObservableCollection<string> _branchOptions = [];
        private readonly Command _checkUpdateCommand;
        private readonly Command _updateCommand;

        private string _selectedSourceMode = "release";
        private string _selectedChannel = "release";
        private string _selectedBranch = "main";
        private string _updateStatus = "Not checked";
        private UpdateCandidate? _updateCandidate;
        private bool _hasUpdate;
        private bool _isCheckingUpdate;
        private bool _isUpdating;
        private bool _isRestarting;


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
            Action onStateChanged)
        {
            _config = config;
            _installer = installer;
            _runner = runner;
            _updateService = updateService;
            _onStateChanged = onStateChanged;

            // Normalize once so every picker starts from persisted data instead of hard-coded defaults.
            _config.Update.Normalize();
            _selectedSourceMode = _config.Update.Mode;
            _selectedChannel = _config.Update.Channel;
            _selectedBranch = _config.Update.Branch;
            _branchOptions.Add(_selectedBranch);

            LaunchCommand = new Command(OnLaunch);
            StopCommand = new Command(OnStop);
            RestartCommand = new Command(OnRestart);
            _checkUpdateCommand = new Command(ExecuteCheckUpdateCommand, CanCheckOrUpdate);
            _updateCommand = new Command(ExecuteApplyUpdateCommand, CanApplyUpdate);

            CheckUpdateCommand = _checkUpdateCommand;
            UpdateCommand = _updateCommand;

            _ = RefreshUpdateStateAsync(forceBranchLoad: false);
        }


        // Static card data

        /// <summary>
        /// Gets the module name shown on the card.
        /// </summary>
        public string Name => _config.Name;

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
        /// Gets selectable release channels.
        /// </summary>
        public IReadOnlyList<string> ChannelOptions => SharedChannelOptions;

        /// <summary>
        /// Gets repository branches loaded from GitHub.
        /// </summary>
        public ObservableCollection<string> BranchOptions => _branchOptions;

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
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsBranchMode));
                OnPropertyChanged(nameof(IsReleaseMode));
                _ = RefreshUpdateStateAsync(forceBranchLoad: IsBranchMode);
            }
        }

        /// <summary>
        /// Gets or sets the selected release channel.
        /// </summary>
        public string SelectedChannel
        {
            get => _selectedChannel;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "release" : value;
                if (string.Equals(_selectedChannel, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _selectedChannel = normalized;
                _config.Update.Channel = _selectedChannel;
                PersistUpdatePreferences();
                OnPropertyChanged();
                _ = RefreshUpdateStateAsync(forceBranchLoad: false);
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
                if (string.IsNullOrWhiteSpace(value) ||
                    string.Equals(_selectedBranch, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _selectedBranch = value;
                _config.Update.Branch = _selectedBranch;
                PersistUpdatePreferences();
                OnPropertyChanged();
                _ = RefreshUpdateStateAsync(forceBranchLoad: false);
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
                RefreshCommandStates();
            }
        }

        /// <summary>
        /// Gets whether an update operation can be started.
        /// </summary>
        public bool CanUpdate => HasUpdate && !IsCheckingUpdate && !IsUpdating;

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
                OnPropertyChanged(nameof(CanUpdate));
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
                OnPropertyChanged(nameof(CanUpdate));
                RefreshCommandStates();
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
        /// Gets the command that checks for module updates.
        /// </summary>
        public ICommand CheckUpdateCommand { get; }

        /// <summary>
        /// Gets the command that applies an available module update.
        /// </summary>
        public ICommand UpdateCommand { get; }


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
            return HasUpdate && CanCheckOrUpdate();
        }

        /// <summary>
        /// Refreshes command enabled states after one flag changes.
        /// </summary>
        private void RefreshCommandStates()
        {
            _checkUpdateCommand.ChangeCanExecute();
            _updateCommand.ChangeCanExecute();
        }

        /// <summary>
        /// Persists the module update preferences after one picker changes.
        /// </summary>
        private void PersistUpdatePreferences()
        {
            // Normalize once so the in-memory fields mirror the persisted representation exactly.
            _config.Update.Normalize();
            _selectedSourceMode = _config.Update.Mode;
            _selectedChannel = _config.Update.Channel;
            _selectedBranch = _config.Update.Branch;
            _updateService.SaveModuleUpdatePreferences(_config);
        }

        /// <summary>
        /// Bridges the check-update command into the asynchronous workflow.
        /// </summary>
        private async void ExecuteCheckUpdateCommand()
        {
            await RefreshUpdateStateAsync(forceBranchLoad: true);
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
        private async Task RefreshUpdateStateAsync(bool forceBranchLoad)
        {
            if (IsCheckingUpdate)
            {
                return;
            }

            IsCheckingUpdate = true;
            try
            {
                // Branch-based updates need a live branch list so the picker can expose the repository state.
                if (forceBranchLoad || (IsBranchMode && BranchOptions.Count <= 1))
                {
                    await LoadBranchesAsync();
                }

                _updateCandidate = await _updateService.CheckModuleUpdateAsync(_config);
                HasUpdate = _updateCandidate != null;
                UpdateStatus = _updateCandidate == null
                    ? "Up to date"
                    : $"Update: {_updateCandidate.RemoteVersion}";
            }
            catch (Exception ex)
            {
                HasUpdate = false;
                UpdateStatus = $"Check failed: {ex.Message}";
            }
            finally
            {
                IsCheckingUpdate = false;
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
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    BranchOptions.Clear();
                    foreach (var branch in branches)
                    {
                        BranchOptions.Add(branch.Name);
                    }

                    // Keep the persisted branch selectable even when the remote list is temporarily unavailable or changed.
                    if (!BranchOptions.Contains(_selectedBranch))
                    {
                        BranchOptions.Insert(0, _selectedBranch);
                    }
                });
            }
            catch
            {
                if (!BranchOptions.Contains(_selectedBranch))
                {
                    BranchOptions.Add(_selectedBranch);
                }
            }
        }

        /// <summary>
        /// Applies the latest available update candidate.
        /// </summary>
        private async Task ApplyUpdateAsync()
        {
            if (_updateCandidate == null || IsUpdating)
            {
                return;
            }

            IsUpdating = true;
            UpdateStatus = "Updating...";

            var log = new Progress<string>(message =>
                Debug.WriteLine($"[ModuleUpdate:{Name}] {message}"));

            try
            {
                var success = await _updateService.ApplyModuleUpdateAsync(_updateCandidate, log);
                UpdateStatus = success ? "Updated" : "Update failed";
                HasUpdate = false;
                _onStateChanged?.Invoke();
                await RefreshUpdateStateAsync(forceBranchLoad: false);
            }
            finally
            {
                IsUpdating = false;
            }
        }


        // Launch flow

        /// <summary>
        /// Loads the latest config, completes first-run setup if needed, and starts the module.
        /// </summary>
        private async void OnLaunch()
        {
            await ReloadEditableConfigAsync();

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


        // Stop flow

        /// <summary>
        /// Stops the module and persists the disabled state.
        /// </summary>
        private async void OnStop()
        {
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


        // UI state refresh

        /// <summary>
        /// Raises property changes for the running state flags.
        /// </summary>
        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsStopped));
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
