// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    // Engine setup page

    /// <summary>
    /// Installs missing engines during startup and shows progress and logs.
    /// </summary>
    public partial class EngineSetupPage : ContentPage
    {
        private readonly EngineInstaller _installer;
        private List<EngineConfig> _pendingEngines = [];
        private CancellationTokenSource? _cts;
        private bool _hasLoaded;

        // Initialization

        /// <summary>
        /// Creates the engine setup page and starts discovery after the page loads.
        /// </summary>
        public EngineSetupPage(EngineInstaller installer)
        {
            InitializeComponent();
            _installer = installer;

            // Exceptions are handled inside the async loader, so fire-and-forget is safe here.
            Loaded += async (_, _) => await LoadEnginesAsync();
        }


        // Discovery

        /// <summary>
        /// Discovers engines and prepares the pending installation list.
        /// </summary>
        private async Task LoadEnginesAsync()
        {
            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;

            try
            {
                var engines = await Task.Run(_installer.DiscoverEngines);
                _pendingEngines = engines.Where(engine => !engine.Status.Installed).ToList();

                if (_pendingEngines.Count == 0)
                {
                    EngineInfoLabel.Text = "All engines are installed.";
                    InstallButton.IsEnabled = false;
                    AddLog("[OK] No engines require installation.");
                    ShowContinueButton();
                    return;
                }

                var names = string.Join(", ", _pendingEngines.Select(engine =>
                {
                    var versionLabel = engine.Version.All(c => char.IsDigit(c) || c == '.') ? $"v{engine.Version}" : engine.Version;
                    return $"{engine.Name} {versionLabel}";
                }));

                EngineInfoLabel.Text = $"Engines to install: {names}";
                AddLog($"Found {_pendingEngines.Count} engine(s) to install:");

                foreach (var engine in _pendingEngines)
                {
                    var versionLabel = engine.Version.All(c => char.IsDigit(c) || c == '.') ? $"v{engine.Version}" : engine.Version;
                    AddLog($"  - {engine.Name} {versionLabel} ({engine.Install.Count} steps)");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error discovering engines: {ex.Message}");
            }
        }


        // Install actions

        /// <summary>
        /// Starts the engine installation loop.
        /// </summary>
        private async void OnInstallClicked(object? sender, EventArgs e)
        {
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _cts = new CancellationTokenSource();

            // Progress callbacks already return to the UI thread through Progress<T>.
            var logProgress = new Progress<string>(AddLog);
            var downloadProgress = new Progress<DownloadProgress>(UpdateDownloadProgress);

            var completed = 0;
            var total = _pendingEngines.Count;

            foreach (var engine in _pendingEngines)
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await Task.Run(
                        () => _installer.InstallAsync(engine, logProgress, downloadProgress, _cts.Token),
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
                    AddLog($"[Error] Error installing {engine.Name}: {ex.Message}");
                }
            }

            CancelButton.IsEnabled = false;
            _cts.Dispose();
            _cts = null;

            if (completed == total)
            {
                ProgressLabel.Text = "All engines installed!";
                AddLog("=== All engines installed successfully ===");
                ShowContinueButton();
            }
            else
            {
                // Keep Retry as the main action and expose Continue as the fallback path.
                InstallButton.Text = "Retry";
                InstallButton.IsEnabled = true;
                InstallButton.BackgroundColor = Color.FromArgb("#007AFF");
                ProgressLabel.Text = "Installation incomplete - press Retry to try again";
                ShowSkipButton();
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

        // Skip action

        /// <summary>
        /// Converts the secondary button into a skip action after a failed install.
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
        /// Appends one log line and keeps the editor scrolled to the end.
        /// </summary>
        private void AddLog(string message)
        {
            LogEditor.Text += message + "\n";

            // Moving the caret to the end keeps the latest lines visible.
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
                // Re-run startup so the app can choose the next required page.
                var newPage = (Application.Current as App)?.CreateStartupPage();
                if (newPage != null)
                {
                    window.Page = newPage;
                }
            });
        }
    }
}
