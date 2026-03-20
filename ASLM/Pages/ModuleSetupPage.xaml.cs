// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    // Module setup page

    /// <summary>
    /// Completes first-run setup for modules that still need initialization.
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
        private bool _hasLoaded;

        // Initialization

        /// <summary>
        /// Creates the module setup page and starts discovery after the page loads.
        /// </summary>
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


        // Discovery

        /// <summary>
        /// Discovers modules and prepares the pending first-run list.
        /// </summary>
        private async Task LoadModulesAsync()
        {
            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;

            try
            {
                var modules = await _moduleInstaller.DiscoverModulesAsync();
                _pendingModules = modules
                    .Where(module => !module.Status.FirstRunCompleted)
                    .ToList();

                if (_pendingModules.Count == 0)
                {
                    ModuleInfoLabel.Text = "All modules are set up.";
                    InstallButton.IsEnabled = false;
                    AddLog("[OK] No modules require setup.");
                    ShowContinueButton();
                    return;
                }

                var names = string.Join(", ", _pendingModules.Select(module => module.Name));
                ModuleInfoLabel.Text = $"Modules to set up: {names}";
                AddLog($"Found {_pendingModules.Count} module(s) needing setup:");

                // Prefetch installed dependencies once to avoid repeated disk scans per module.
                var installedModels = await _modelInstaller.DiscoverModelsAsync();
                var installedEngines = _engineInstaller.DiscoverEngines();

                foreach (var module in _pendingModules)
                {
                    var versionLabel = module.Version.All(c => char.IsDigit(c) || c == '.') ? $"v{module.Version}" : module.Version;
                    AddLog($"  - {module.Name} {versionLabel}");
                    AddLog($"    FirstRun commands: {module.Commands.FirstRun.Count}");

                    ValidateDependencies(module, installedModels, installedEngines);
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error discovering modules: {ex.Message}");
            }
        }


        // Install actions

        /// <summary>
        /// Starts the first-run setup loop for pending modules.
        /// </summary>
        private async void OnInstallClicked(object? sender, EventArgs e)
        {
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _cts = new CancellationTokenSource();

            var logProgress = new Progress<string>(AddLog);
            var completed = 0;
            var total = _pendingModules.Count;

            foreach (var module in _pendingModules)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    AddLog($"Setting up {module.Name}...");

                    // Download the module source before any setup commands run.
                    var downloaded = await Task.Run(
                        () => _moduleInstaller.DownloadSourceAsync(module, logProgress, null, _cts.Token),
                        _cts.Token);

                    if (!downloaded)
                    {
                        AddLog($"[Error] Source download failed for {module.Name}");
                        continue;
                    }

                    // Run dependency installation and first-run commands after the source is available.
                    var success = await Task.Run(
                        () => _moduleRunner.ExecuteFirstRunAsync(module, logProgress, _cts.Token),
                        _cts.Token);

                    if (success)
                    {
                        // Persist the updated first-run state right after a successful setup.
                        await Task.Run(() => _moduleInstaller.SaveConfigAsync(module));
                        completed++;
                        await InstallProgress.ProgressTo((double)completed / total, 250, Easing.CubicOut);
                        ProgressLabel.Text = $"Set up {completed}/{total}";
                    }
                    else
                    {
                        AddLog($"[Error] Setup failed for {module.Name}");
                    }
                }
                catch (OperationCanceledException)
                {
                    AddLog("Setup cancelled by user.");
                    break;
                }
                catch (Exception ex)
                {
                    AddLog($"[Error] Error setting up {module.Name}: {ex.Message}");
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
                InstallButton.Text = "Retry";
                InstallButton.IsEnabled = true;
                InstallButton.BackgroundColor = Color.FromArgb("#007AFF");
                ProgressLabel.Text = "Setup incomplete - press Retry to try again";
                ShowSkipButton();
            }
        }

        // Cancel action

        /// <summary>
        /// Requests cancellation for the current setup run.
        /// </summary>
        private void OnCancelClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        // Continue action

        /// <summary>
        /// Continues the startup chain after setup finishes.
        /// </summary>
        private async void OnContinueClicked(object? sender, EventArgs e)
        {
            await NavigateToMainAsync();
        }


        // Dependency checks

        /// <summary>
        /// Writes dependency status lines for one module.
        /// </summary>
        private void ValidateDependencies(
            ModuleConfig module,
            List<ModelConfig> installedModels,
            List<EngineConfig> installedEngines)
        {
            foreach (var requiredEngine in module.Dependencies.Engines)
            {
                var exists = installedEngines.Any(engine => engine.Id == requiredEngine.Id && engine.Status.Installed);
                AddLog(exists
                    ? $"    [OK] Engine: {requiredEngine.Id}"
                    : $"    [Missing] Engine: {requiredEngine.Id}");
            }

            foreach (var requiredCategory in module.Dependencies.Models)
            {
                var exists = installedModels.Any(model =>
                    model.Category.Equals(requiredCategory, StringComparison.OrdinalIgnoreCase) &&
                    model.Status.Installed);

                AddLog(exists
                    ? $"    [OK] Model: {requiredCategory}"
                    : $"    [Missing] Model: {requiredCategory}");
            }
        }


        // Button states

        /// <summary>
        /// Converts the main action button into the continue action.
        /// </summary>
        private void ShowContinueButton()
        {
            InstallButton.Text = "Continue";
            InstallButton.IsEnabled = true;
            InstallButton.Clicked -= OnContinueClicked;
            InstallButton.Clicked -= OnInstallClicked;
            InstallButton.Clicked += OnContinueClicked;
            CancelButton.IsVisible = false;
        }

        // Skip action

        /// <summary>
        /// Converts the secondary button into a skip action after a failed setup.
        /// </summary>
        private void ShowSkipButton()
        {
            CancelButton.Text = "Skip";
            CancelButton.IsEnabled = true;
            CancelButton.IsVisible = true;
            CancelButton.Clicked -= OnContinueClicked;
            CancelButton.Clicked -= OnCancelClicked;
            CancelButton.Clicked += OnContinueClicked;
        }


        // Log UI

        /// <summary>
        /// Appends one log line and refreshes the log editor.
        /// </summary>
        private void AddLog(string message)
        {
            _logBuffer.AppendLine(message);
            LogEditor.Text = _logBuffer.ToString();
            LogEditor.CursorPosition = LogEditor.Text.Length;
        }

        // Download UI

        /// <summary>
        /// Updates the per-download progress panel.
        /// </summary>
        private void UpdateDownloadProgress(DownloadProgress progress)
        {
            DownloadProgressPanel.IsVisible = true;
            DownloadProgressBar.Progress = progress.Fraction;

            var downloadedMb = progress.DownloadedBytes / 1024.0 / 1024.0;
            var totalMb = progress.TotalBytes / 1024.0 / 1024.0;
            var percent = (int)(progress.Fraction * 100);
            DownloadInfoLabel.Text = $"{percent}%  -  {downloadedMb:F1} MB / {totalMb:F1} MB";
        }

        // Download reset

        /// <summary>
        /// Hides the per-download progress panel between downloads.
        /// </summary>
        private void HideDownloadProgress()
        {
            DownloadProgressPanel.IsVisible = false;
            DownloadProgressBar.Progress = 0;
            DownloadInfoLabel.Text = string.Empty;
        }


        // Navigation

        /// <summary>
        /// Restarts the startup chain after the current step completes.
        /// </summary>
        private async Task NavigateToMainAsync()
        {
            if (Application.Current?.Windows.FirstOrDefault() is not Window window)
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                var newPage = (Application.Current as App)?.CreateStartupPage();
                if (newPage != null)
                {
                    window.Page = newPage;
                }
            });
        }
    }
}
