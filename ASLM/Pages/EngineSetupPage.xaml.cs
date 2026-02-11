using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Displayed at startup when one or more engines need installation.
    /// Shows a real-time log, per-download progress bar, and an overall progress indicator.
    /// After all engines are installed, offers a Continue button to navigate to <see cref="MainPage"/>.
    /// </summary>
    public partial class EngineSetupPage : ContentPage
    {
        private readonly EngineInstaller _installer;
        private List<EngineConfig> _pendingEngines = [];
        private CancellationTokenSource? _cts;

        public EngineSetupPage(EngineInstaller installer)
        {
            InitializeComponent();
            _installer = installer;

            // Fire-and-forget is safe here: exceptions are caught inside LoadEnginesAsync.
            Loaded += async (_, _) => await LoadEnginesAsync();
        }

        /// <summary>
        /// Discovers engines and populates the pending list.
        /// Runs on the <see cref="Page.Loaded"/> event so the UI is fully initialized.
        /// </summary>
        private async Task LoadEnginesAsync()
        {
            try
            {
                var engines = await Task.Run(() => _installer.DiscoverEngines());
                _pendingEngines = engines.Where(e => !e.Status.Installed).ToList();

                if (_pendingEngines.Count == 0)
                {
                    EngineInfoLabel.Text = "All engines are installed.";
                    InstallButton.IsEnabled = false;
                    AddLog("✓ No engines require installation.");
                    ShowContinueButton();
                    return;
                }

                var names = string.Join(", ", _pendingEngines.Select(e => $"{e.Name} v{e.Version}"));
                EngineInfoLabel.Text = $"Engines to install: {names}";
                AddLog($"Found {_pendingEngines.Count} engine(s) to install:");

                foreach (var engine in _pendingEngines)
                    AddLog($"  • {engine.Name} v{engine.Version} ({engine.Install.Count} steps)");
            }
            catch (Exception ex)
            {
                AddLog($"Error discovering engines: {ex.Message}");
            }
        }

        // --- Event Handlers --------------------------------------------------

        private async void OnInstallClicked(object? sender, EventArgs e)
        {
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _cts = new CancellationTokenSource();

            // Progress<T> captures the SynchronizationContext, so callbacks
            // already run on the UI thread — no need for extra dispatching.
            var logProgress = new Progress<string>(AddLog);
            var downloadProgress = new Progress<DownloadProgress>(UpdateDownloadProgress);

            int completed = 0;
            int total = _pendingEngines.Count;

            foreach (var engine in _pendingEngines)
            {
                if (_cts.Token.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Run(() => _installer.InstallAsync(
                        engine, logProgress, downloadProgress, _cts.Token), _cts.Token);

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
                    AddLog($"✗ Error installing {engine.Name}: {ex.Message}");
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
                InstallButton.IsEnabled = true;
                InstallButton.Text = "Retry";
                ProgressLabel.Text = "Installation incomplete";
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

        // --- UI Helpers ------------------------------------------------------

        /// <summary>
        /// Replaces the Install button with a Continue button that navigates to MainPage.
        /// </summary>
        private void ShowContinueButton()
        {
            InstallButton.Text = "Continue";
            InstallButton.IsEnabled = true;
            InstallButton.Clicked -= OnInstallClicked;
            InstallButton.Clicked += OnContinueClicked;
            CancelButton.IsVisible = false;
        }

        /// <summary>Appends a log line and auto-scrolls the editor to the bottom.</summary>
        private void AddLog(string message)
        {
            LogEditor.Text += message + "\n";

            // Auto-scroll to bottom by setting cursor to end.
            LogEditor.CursorPosition = LogEditor.Text.Length;
        }

        /// <summary>Shows and updates the green download progress bar.</summary>
        private void UpdateDownloadProgress(DownloadProgress dp)
        {
            DownloadProgressPanel.IsVisible = true;
            DownloadProgressBar.Progress = dp.Fraction;

            var downloadedMb = dp.DownloadedBytes / 1024.0 / 1024.0;
            var totalMb = dp.TotalBytes / 1024.0 / 1024.0;
            var pct = (int)(dp.Fraction * 100);
            DownloadInfoLabel.Text = $"{pct}%  —  {downloadedMb:F1} MB / {totalMb:F1} MB";
        }

        /// <summary>Hides the download progress bar between downloads.</summary>
        private void HideDownloadProgress()
        {
            DownloadProgressPanel.IsVisible = false;
            DownloadProgressBar.Progress = 0;
            DownloadInfoLabel.Text = "";
        }

        /// <summary>Swaps the current page to <see cref="MainPage"/>.</summary>
        private async Task NavigateToMainAsync()
        {
            if (Application.Current?.Windows.FirstOrDefault() is Window window)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    var mainPage = Handler?.MauiContext?.Services.GetService<MainPage>();
                    if (mainPage != null)
                        window.Page = mainPage;
                });
            }
        }
    }
}
