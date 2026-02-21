using System.Text;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// UI page for discovering and installing machine learning models.
    /// Handles the installation workflow, progress tracking, and log display.
    /// </summary>
    public partial class ModelSetupPage : ContentPage
    {
        private readonly ModelInstaller _installer;
        private readonly StringBuilder _logBuffer = new();
        private List<ModelConfig> _pendingModels = [];
        private CancellationTokenSource? _cts;

        public ModelSetupPage(ModelInstaller installer)
        {
            InitializeComponent();
            _installer = installer;

            // Automatically load models when the page appears
            Loaded += async (_, _) => await LoadModelsAsync();
        }

        /// <summary>
        /// Discovers models asynchronously and updates the UI.
        /// </summary>
        private async Task LoadModelsAsync()
        {
            try
            {
                var models = await _installer.DiscoverModelsAsync();
                _pendingModels = models.Where(m => !m.Status.Installed).ToList();

                if (_pendingModels.Count == 0)
                {
                    ModelInfoLabel.Text = "All models are installed.";
                    InstallButton.IsEnabled = false;
                    AddLog("✓ No models require installation.");
                    ShowContinueButton();
                    return;
                }

                var names = string.Join(", ", _pendingModels.Select(m => m.Name));
                ModelInfoLabel.Text = $"Models to install: {names}";
                AddLog($"Found {_pendingModels.Count} model(s) to install:");

                foreach (var model in _pendingModels)
                {
                    AddLog($"  • {model.Name} ({model.Source.RepoId})");
                    AddLog($"    {model.Files.Count} files to download");
                }
            }
            catch (Exception ex)
            {
                AddLog($"Error discovering models: {ex.Message}");
            }
        }

        // --- Event Handlers --------------------------------------------------

        private async void OnInstallClicked(object? sender, EventArgs e)
        {
            InstallButton.IsEnabled = false;
            CancelButton.IsEnabled = true;
            _cts = new CancellationTokenSource();

            var logProgress = new Progress<string>(AddLog);
            var downloadProgress = new Progress<DownloadProgress>(UpdateDownloadProgress);

            int completed = 0;
            int total = _pendingModels.Count;

            foreach (var model in _pendingModels)
            {
                if (_cts.Token.IsCancellationRequested)
                    break;

                try
                {
                    await Task.Run(() => _installer.InstallAsync(
                        model, logProgress, downloadProgress, _cts.Token), _cts.Token);

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
                    AddLog($"✗ Error installing {model.Name}: {ex.Message}");
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

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private async void OnContinueClicked(object? sender, EventArgs e)
        {
            await NavigateToMainAsync();
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

        /// <summary>
        /// Navigates to the main application page or the next setup page in the chain.
        /// </summary>
        private async Task NavigateToMainAsync()
        {
            if (Application.Current?.Windows.FirstOrDefault() is Window window)
            {
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
}
