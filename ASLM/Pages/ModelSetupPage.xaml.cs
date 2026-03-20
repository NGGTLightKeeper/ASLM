// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    // Model setup page

    /// <summary>
    /// Installs missing models during startup and shows progress and logs.
    /// </summary>
    public partial class ModelSetupPage : ContentPage
    {
        private readonly ModelInstaller _installer;
        private readonly StringBuilder _logBuffer = new();
        private List<ModelConfig> _pendingModels = [];
        private CancellationTokenSource? _cts;
        private bool _hasLoaded;

        // Initialization

        /// <summary>
        /// Creates the model setup page and starts discovery after the page loads.
        /// </summary>
        public ModelSetupPage(ModelInstaller installer)
        {
            InitializeComponent();
            _installer = installer;
            Loaded += async (_, _) => await LoadModelsAsync();
        }


        // Discovery

        /// <summary>
        /// Discovers models and prepares the pending installation list.
        /// </summary>
        private async Task LoadModelsAsync()
        {
            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;

            try
            {
                var models = await _installer.DiscoverModelsAsync();
                _pendingModels = models.Where(model => !model.Status.Installed).ToList();

                if (_pendingModels.Count == 0)
                {
                    ModelInfoLabel.Text = "All models are installed.";
                    InstallButton.IsEnabled = false;
                    AddLog("[OK] No models require installation.");
                    ShowContinueButton();
                    return;
                }

                var names = string.Join(", ", _pendingModels.Select(model => model.Name));
                ModelInfoLabel.Text = $"Models to install: {names}";
                AddLog($"Found {_pendingModels.Count} model(s) to install:");

                foreach (var model in _pendingModels)
                {
                    AddLog($"  - {model.Name} ({model.Source.RepoId})");
                    AddLog($"    {model.Files.Count} files to download");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error discovering models: {ex.Message}");
            }
        }


        // Install actions

        /// <summary>
        /// Starts the model installation loop.
        /// </summary>
        private async void OnInstallClicked(object? sender, EventArgs e)
        {
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _cts = new CancellationTokenSource();

            var logProgress = new Progress<string>(AddLog);
            var downloadProgress = new Progress<DownloadProgress>(UpdateDownloadProgress);

            var completed = 0;
            var total = _pendingModels.Count;

            foreach (var model in _pendingModels)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await Task.Run(
                        () => _installer.InstallAsync(model, logProgress, downloadProgress, _cts.Token),
                        _cts.Token);

                    completed++;
                    HideDownloadProgress();
                    await InstallProgress.ProgressTo((double)completed / total, 250, Easing.CubicOut);
                    ProgressLabel.Text = $"Installed {completed}/{total}";
                }
                catch (OperationCanceledException)
                {
                    AddLog("Installation cancelled by user.");
                    break;
                }
                catch (Exception ex)
                {
                    AddLog($"[Error] Error installing {model.Name}: {ex.Message}");
                }
            }

            CancelButton.IsEnabled = false;
            _cts.Dispose();
            _cts = null;

            if (completed == total)
            {
                ProgressLabel.Text = "All models installed!";
                AddLog("=== All models installed successfully ===");
                ShowContinueButton();
            }
            else
            {
                InstallButton.IsEnabled = true;
                InstallButton.Text = "Retry";
                ProgressLabel.Text = "Installation incomplete";
            }
        }

        // Cancel action

        /// <summary>
        /// Requests cancellation for the current installation run.
        /// </summary>
        private void OnCancelClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        // Continue action

        /// <summary>
        /// Continues the startup chain after installation finishes.
        /// </summary>
        private async void OnContinueClicked(object? sender, EventArgs e)
        {
            await NavigateToMainAsync();
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
