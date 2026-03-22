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
        public ModuleManagementView()
        {
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
                Modules.Add(new ModuleViewModel(module, installer, runner, OnModuleStateChanged));
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
        private readonly ModuleConfig _config;
        private readonly ModuleInstaller _installer;
        private readonly ModuleRunner _runner;
        private readonly Action _onStateChanged;
        private bool _isRestarting;
        private bool _isLaunching;
        private bool _isLogVisible;
        private string _launchLog = string.Empty;

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
            Action onStateChanged)
        {
            _config = config;
            _installer = installer;
            _runner = runner;
            _onStateChanged = onStateChanged;

            LaunchCommand = new Command(OnLaunch);
            StopCommand = new Command(OnStop);
            RestartCommand = new Command(OnRestart);
            ToggleLogCommand = new Command(() => IsLogVisible = !IsLogVisible);
        }

        // Static card data

        /// <summary>
        /// Gets the module name shown on the card.
        /// </summary>
        public string Name => _config.Name;

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

        // Launching state

        /// <summary>
        /// Gets or sets whether the module is currently being launched (first-run setup or startup).
        /// </summary>
        public bool IsLaunching
        {
            get => _isLaunching;
            set
            {
                if (_isLaunching == value)
                {
                    return;
                }

                _isLaunching = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsReadyToLaunch));
            }
        }

        // Launch-ready state

        /// <summary>
        /// Gets whether the launch button should be shown and clickable.
        /// </summary>
        public bool IsReadyToLaunch => IsStopped && !IsLaunching;

        // Log visibility

        /// <summary>
        /// Gets or sets whether the launch log panel is expanded.
        /// </summary>
        public bool IsLogVisible
        {
            get => _isLogVisible;
            set
            {
                if (_isLogVisible == value) return;
                _isLogVisible = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ToggleLogLabel));
            }
        }

        // Toggle log label

        /// <summary>
        /// Gets the label for the toggle log button.
        /// </summary>
        public string ToggleLogLabel => _isLogVisible ? "Hide log" : "Show log";

        // Has log

        /// <summary>
        /// Gets whether there is any log output to show.
        /// </summary>
        public bool HasLog => !string.IsNullOrEmpty(_launchLog);

        // Launch log

        /// <summary>
        /// Gets the accumulated launch log text.
        /// </summary>
        public string LaunchLog
        {
            get => _launchLog;
            private set
            {
                _launchLog = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasLog));
            }
        }

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

        // Toggle log command

        /// <summary>
        /// Gets the command that shows or hides the launch log panel.
        /// </summary>
        public ICommand ToggleLogCommand { get; }


        // Launch flow

        /// <summary>
        /// Loads the latest config, completes first-run setup if needed, and starts the module.
        /// </summary>
        private async void OnLaunch()
        {
            IsLaunching = true;
            LaunchLog = string.Empty;

            void AppendLog(string message)
            {
                Debug.WriteLine($"[Launch] {message}");
                LaunchLog += message + "\n";
            }

            try
            {
                // Reload the config so launch uses the latest user-edited settings.
                var freshConfig = await _installer.LoadModuleConfig(_config.SourcePath);
                if (freshConfig != null)
                {
                    _config.Settings = freshConfig.Settings;
                    _config.Commands = freshConfig.Commands;
                }

                // First-run setup must complete before the normal run commands can start.
                if (!_config.Status.FirstRunCompleted)
                {
                    var setupLog = new Progress<string>(message => AppendLog(message));
                    var setupSuccess = await Task.Run(() =>
                        _runner.ExecuteFirstRunAsync(_config, setupLog, CancellationToken.None));

                    if (setupSuccess)
                    {
                        _config.Status.FirstRunCompleted = true;
                        _installer.SaveModuleConfig(_config);
                    }
                    else
                    {
                        AppendLog("Setup failed, cannot launch.");
                        return;
                    }
                }

                _config.Status.Enabled = true;
                _installer.SaveModuleConfig(_config);
                NotifyStateChanged();
                _onStateChanged?.Invoke();

                // Start background run commands only when the module exposes them.
                if (_config.Commands.Run.Count > 0)
                {
                    var runLog = new Progress<string>(message => AppendLog(message));
                    _ = Task.Run(() => _runner.ExecuteRunAsync(_config, runLog, CancellationToken.None));
                }
            }
            finally
            {
                IsLaunching = false;
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
            // Reload the config so restart uses the latest user-edited settings.
            var freshConfig = await _installer.LoadModuleConfig(_config.SourcePath);
            if (freshConfig != null)
            {
                _config.Settings = freshConfig.Settings;
                _config.Commands = freshConfig.Commands;
            }

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


        // UI state refresh

        /// <summary>
        /// Raises property changes for the running state flags.
        /// </summary>
        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsStopped));
            OnPropertyChanged(nameof(IsReadyToLaunch));
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
