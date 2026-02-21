using System.Text;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// UI page for discovering and setting up modules.
    /// Follows the same pattern as <see cref="ModelSetupPage"/> and EngineSetupPage:
    /// discover → show pending → install (firstRun) → continue.
    /// </summary>
    public partial class ModuleSetupPage : ContentPage
    {
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly EngineInstaller _engineInstaller;
        private readonly ModelInstaller _modelInstaller;

        private readonly StringBuilder _logBuffer = new();
        private List<ModuleConfig> _pendingModules = [];
        private CancellationTokenSource? _cts;

        public ModuleSetupPage(
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            EngineInstaller engineInstaller,
            ModelInstaller modelInstaller)
        {
            InitializeComponent();
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _engineInstaller = engineInstaller;
            _modelInstaller = modelInstaller;

            Loaded += async (_, _) => await LoadModulesAsync();
        }

        /// <summary>
        /// Discovers modules and populates the pending list (modules needing firstRun).
        /// </summary>
        private async Task LoadModulesAsync()
        {
            try
            {
                var modules = await _moduleInstaller.DiscoverModulesAsync();
                _pendingModules = modules
                    .Where(m => !m.Status.FirstRunCompleted)
                    .ToList();

                if (_pendingModules.Count == 0)
                {
                    ModuleInfoLabel.Text = "All modules are set up.";
                    InstallButton.IsEnabled = false;
                    AddLog("✓ No modules require setup.");
                    ShowContinueButton();
                    return;
                }

                var names = string.Join(", ", _pendingModules.Select(m => m.Name));
                ModuleInfoLabel.Text = $"Modules to set up: {names}";
                AddLog($"Found {_pendingModules.Count} module(s) needing setup:");

                // Prefetch installed components to avoid repeated disk scans
                var installedModels = await _modelInstaller.DiscoverModelsAsync();
                var installedEngines = _engineInstaller.DiscoverEngines();

                foreach (var module in _pendingModules)
                {
                    AddLog($"  • {module.Name} v{module.Version}");
                    AddLog($"    FirstRun commands: {module.Commands.FirstRun.Count}");
                    
                    // Check dependencies
                    ValidateDependencies(module, installedModels, installedEngines);
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error discovering modules: {ex.Message}");
            }
        }

        // --- Event Handlers --------------------------------------------------

        private async void OnInstallClicked(object? sender, EventArgs e)
        {
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _cts = new CancellationTokenSource();

            var logProgress = new Progress<string>(AddLog);

            int completed = 0;
            int total = _pendingModules.Count;

            foreach (var module in _pendingModules)
            {
                if (_cts.Token.IsCancellationRequested)
                    break;

                try
                {
                    AddLog($"Setting up {module.Name}...");

                    // 1. Download source from GitHub
                    var downloaded = await Task.Run(() =>
                        _moduleInstaller.DownloadSourceAsync(module, logProgress, null, _cts.Token),
                        _cts.Token);

                    if (!downloaded)
                    {
                        AddLog($"✗ Source download failed for {module.Name}");
                        continue;
                    }

                    // 2. Install deps + run firstRun
                    var success = await Task.Run(() =>
                        _moduleRunner.ExecuteFirstRunAsync(module, logProgress, _cts.Token),
                        _cts.Token);

                    if (success)
                    {
                        // Save status
                        await Task.Run(() => _moduleInstaller.SaveConfigAsync(module));
                        completed++;
                        await InstallProgress.ProgressTo((double)completed / total, 250, Easing.CubicOut);
                        ProgressLabel.Text = $"Set up {completed}/{total}";
                    }
                    else
                    {
                        AddLog($"✗ Setup failed for {module.Name}");
                    }
                }
                catch (OperationCanceledException)
                {
                    AddLog("Setup cancelled by user.");
                    break;
                }
                catch (Exception ex)
                {
                    AddLog($"✗ Error setting up {module.Name}: {ex.Message}");
                }
            }

            CancelButton.IsEnabled = false;
            _cts.Dispose();
            _cts = null;

            if (completed == total)
            {
                ProgressLabel.Text = "All modules set up!";
                AddLog("=== All modules set up successfully ===");
                ShowContinueButton();
            }
            else
            {
                InstallButton.IsEnabled = true;
                InstallButton.Text = "Retry";
                ProgressLabel.Text = "Setup incomplete";
            }
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private async void OnContinueClicked(object? sender, EventArgs e)
        {
            await NavigateToMainAsync();
        }

        // --- Logic -----------------------------------------------------------

        private void ValidateDependencies(
            ModuleConfig module,
            List<ModelConfig> installedModels,
            List<EngineConfig> installedEngines)
        {
            foreach (var reqEngine in module.Dependencies.Engines)
            {
                var exists = installedEngines.Any(e => e.Id == reqEngine.Id && e.Status.Installed);
                if (!exists)
                    AddLog($"    ⚠ Missing Engine: {reqEngine.Id}");
                else
                    AddLog($"    ✓ Engine: {reqEngine.Id}");
            }

            foreach (var reqCategory in module.Dependencies.Models)
            {
                var exists = installedModels.Any(m =>
                    m.Category.Equals(reqCategory, StringComparison.OrdinalIgnoreCase) &&
                    m.Status.Installed);

                if (!exists)
                    AddLog($"    ⚠ Missing Model: {reqCategory}");
                else
                    AddLog($"    ✓ Model: {reqCategory}");
            }
        }

        // --- UI Helpers ------------------------------------------------------

        private void ShowContinueButton()
        {
            InstallButton.Text = "Continue";
            InstallButton.IsEnabled = true;
            InstallButton.Clicked -= OnInstallClicked;
            InstallButton.Clicked += OnContinueClicked;
            CancelButton.IsVisible = false;
        }

        private void AddLog(string message)
        {
            _logBuffer.AppendLine(message);
            LogEditor.Text = _logBuffer.ToString();
            LogEditor.CursorPosition = LogEditor.Text.Length;
        }

        private void UpdateDownloadProgress(DownloadProgress dp)
        {
            DownloadProgressPanel.IsVisible = true;
            DownloadProgressBar.Progress = dp.Fraction;

            var downloadedMb = dp.DownloadedBytes / 1024.0 / 1024.0;
            var totalMb = dp.TotalBytes / 1024.0 / 1024.0;
            var pct = (int)(dp.Fraction * 100);
            DownloadInfoLabel.Text = $"{pct}%  —  {downloadedMb:F1} MB / {totalMb:F1} MB";
        }

        private void HideDownloadProgress()
        {
            DownloadProgressPanel.IsVisible = false;
            DownloadProgressBar.Progress = 0;
            DownloadInfoLabel.Text = "";
        }

        private async Task NavigateToMainAsync()
        {
            if (Application.Current?.Windows.FirstOrDefault() is Window window)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var newPage = (Application.Current as App)?.CreateStartupPage();
                    if (newPage != null)
                        window.Page = newPage;
                });
            }
        }
    }
}
