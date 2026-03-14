using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Module management view that displays module cards with launch, stop, and restart controls.
    /// </summary>
    public partial class ModuleManagementView : ContentView, INotifyPropertyChanged
    {
        private AppShellPage? _shell;
        private const double MinCardWidth = 400;

        /// <summary>
        /// Collection of view models for the dashboard.
        /// </summary>
        public ObservableCollection<ModuleViewModel> Modules { get; } = new();

        private int _gridSpan = 1;
        /// <summary>
        /// Gets or sets the column span for the responsive grid layout.
        /// </summary>
        public int GridSpan
        {
            get => _gridSpan;
            set
            {
                if (_gridSpan != value)
                {
                    _gridSpan = value;
                    OnPropertyChanged();
                }
            }
        }

        public ModuleManagementView()
        {
            InitializeComponent();
            BindingContext = this;
            SizeChanged += OnSizeChanged;
        }

        /// <inheritdoc />
        public new event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Invokes the PropertyChanged event.</summary>
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Called by AppShellPage to initialize with module data.
        /// </summary>
        internal void Initialize(AppShellPage shell, List<ModuleConfig> modules,
            ModuleInstaller installer, ModuleRunner runner)
        {
            _shell = shell;
            PopulateModules(modules, installer, runner);
        }

        /// <summary>
        /// Called by AppShellPage to refresh module data.
        /// </summary>
        internal void RefreshModules(List<ModuleConfig> modules,
            ModuleInstaller installer, ModuleRunner runner)
        {
            PopulateModules(modules, installer, runner);
        }

        private void PopulateModules(List<ModuleConfig> modules,
            ModuleInstaller installer, ModuleRunner runner)
        {
            Modules.Clear();
            foreach (var module in modules)
            {
                Modules.Add(new ModuleViewModel(module, installer, runner, OnModuleStateChanged));
            }
        }

        private void OnModuleStateChanged()
        {
            _shell?.OnModuleStateChanged();
        }

        private void OnSizeChanged(object? sender, EventArgs e)
        {
            RecalculateGridSpan();
        }

        private void RecalculateGridSpan()
        {
            var availableWidth = Width - 60;
            if (availableWidth > 0)
            {
                var newSpan = Math.Max(1, (int)(availableWidth / MinCardWidth));
                GridSpan = newSpan;
            }
        }
    }

    /// <summary>
    /// ViewModel wrapper for ModuleConfig to handle UI state and commands.
    /// </summary>
    public class ModuleViewModel : INotifyPropertyChanged
    {
        private readonly ModuleConfig _config;
        private readonly ModuleInstaller _installer;
        private readonly ModuleRunner _runner;
        private readonly Action _onStateChanged;

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="ModuleViewModel"/> class.
        /// </summary>
        public ModuleViewModel(ModuleConfig config, ModuleInstaller installer, ModuleRunner runner, Action onStateChanged)
        {
            _config = config;
            _installer = installer;
            _runner = runner;
            _onStateChanged = onStateChanged;

            LaunchCommand = new Command(OnLaunch);
            StopCommand = new Command(OnStop);
            RestartCommand = new Command(OnRestart);
        }

        /// <summary>Module Name.</summary>
        public string Name => _config.Name;
        /// <summary>Formatted Version.</summary>
        public string VersionString => $"v{_config.Version}";
        /// <summary>Icon path.</summary>
        public string? IconFullPath => _config.IconFullPath;
        /// <summary>Whether the module has an icon.</summary>
        public bool HasIcon => !string.IsNullOrEmpty(_config.IconFullPath);

        /// <summary>Is the module currently enabled/running.</summary>
        public bool IsRunning => _config.Status.Enabled;
        /// <summary>Is the module currently stopped.</summary>
        public bool IsStopped => !_config.Status.Enabled;

        private bool _isRestarting;
        /// <summary>Is the module currently restarting.</summary>
        public bool IsRestarting
        {
            get => _isRestarting;
            set
            {
                if (_isRestarting != value)
                {
                    _isRestarting = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNotRestarting));
                }
            }
        }
        /// <summary>Is the module not currently restarting.</summary>
        public bool IsNotRestarting => !IsRestarting;

        /// <summary>Command to launch the module.</summary>
        public ICommand LaunchCommand { get; }
        /// <summary>Command to stop the module.</summary>
        public ICommand StopCommand { get; }
        /// <summary>Command to restart the module.</summary>
        public ICommand RestartCommand { get; }

        private async void OnLaunch()
        {
            // Reload from disk to get latest user-saved settings
            var freshConfig = await _installer.LoadModuleConfig(_config.SourcePath);
            if (freshConfig != null)
            {
                _config.Settings = freshConfig.Settings;
                _config.Commands = freshConfig.Commands;
            }

            if (!_config.Status.FirstRunCompleted)
            {
                var logProgressSetup = new Progress<string>(msg =>
                    Debug.WriteLine($"[Setup] {msg}"));
                
                bool setupSuccess = await Task.Run(() => 
                    _runner.ExecuteFirstRunAsync(_config, logProgressSetup, CancellationToken.None));

                if (setupSuccess)
                {
                    _config.Status.FirstRunCompleted = true;
                    _installer.SaveModuleConfig(_config);
                }
                else
                {
                    Debug.WriteLine("Setup failed, cannot launch.");
                    return;
                }
            }

            _config.Status.Enabled = true;
            _installer.SaveModuleConfig(_config);
            NotifyStateChanged();
            _onStateChanged?.Invoke();

            if (_config.Commands.Run.Count > 0)
            {
                var logProgress = new Progress<string>(msg =>
                    Debug.WriteLine($"[Launch] {msg}"));
                
                _ = Task.Run(() =>
                    _runner.ExecuteRunAsync(_config, logProgress, CancellationToken.None));
            }
        }

        private async void OnStop()
        {
            await _runner.StopModuleAsync(_config.SourcePath);

            _config.Status.Enabled = false;
            _installer.SaveModuleConfig(_config);
            NotifyStateChanged();
            _onStateChanged?.Invoke();
        }

        private async void OnRestart()
        {
            // Reload from disk to get latest user-saved settings
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

                var logProgress = new Progress<string>(msg =>
                    Debug.WriteLine($"[Restart] {msg}"));
                _ = Task.Run(() =>
                    _runner.ExecuteRunAsync(_config, logProgress, CancellationToken.None));
            }
            finally
            {
                IsRestarting = false;
            }
        }

        private void NotifyStateChanged()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(IsStopped));
        }

        /// <summary>Invokes the PropertyChanged event.</summary>
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
