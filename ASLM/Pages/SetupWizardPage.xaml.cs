using System.Diagnostics;
using System.Text;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// First-run setup wizard.
    /// Step 0: Welcome (Setup / Fast Setup).
    /// Step 1: Display name.
    /// Step 2: Port allocation with overlap validation.
    /// Step 3: Module selection and installation with dual progress bars.
    /// </summary>
    public partial class SetupWizardPage : ContentPage
    {
        private readonly AppDataService _appData;
        private readonly EngineInstaller _engineInstaller;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly IServiceProvider _services;

        private int _currentStep;
        private const int TotalSteps = 3;

        private readonly List<(ModuleConfig Module, CheckBox Check)> _moduleChecks = [];
        private readonly StringBuilder _logBuffer = new();
        private CancellationTokenSource? _cts;
        private bool _logVisible;

        // For speed calculation
        private long _lastDownloadedBytes;
        private DateTime _lastSpeedUpdate = DateTime.UtcNow;
        private double _lastSpeed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SetupWizardPage"/> class.
        /// </summary>
        public SetupWizardPage(
            AppDataService appData,
            EngineInstaller engineInstaller,
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            IServiceProvider services)
        {
            _appData = appData;
            _engineInstaller = engineInstaller;
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _services = services;
            InitializeComponent();

            // Auto-fill username from Windows or existing data
            var existingName = _appData.Data.User.Name;
            UsernameEntry.Text = string.IsNullOrWhiteSpace(existingName)
                ? Environment.UserName
                : existingName;

            OfficialPortEntry.Text = _appData.Data.Ports.OfficialStart.ToString();
            ThirdPartyPortEntry.Text = _appData.Data.Ports.ThirdPartyStart.ToString();

            // Use Loaded instead of OnAppearing — OnAppearing doesn't fire on WinUI
            Loaded += (_, _) => PopulateModuleList();
        }

        // --- Welcome Screen --------------------------------------------------

        private void OnSetupClicked(object? sender, EventArgs e)
        {
            _currentStep = 1;
            UpdateStepUI();
        }

        private async void OnFastSetupClicked(object? sender, EventArgs e)
        {
            // Auto-configure: Windows username + default ports
            _appData.Data.User.Name = Environment.UserName;
            _appData.Data.Ports.OfficialStart = 8000;
            _appData.Data.Ports.ThirdPartyStart = 9000;
            await _appData.SaveAsync();

            // Skip to module selection
            _currentStep = 3;
            UpdateStepUI();
        }

        // --- Module Discovery ------------------------------------------------

        private async void PopulateModuleList()
        {
            ModuleList.Children.Clear();
            _moduleChecks.Clear();

            var modules = await _moduleInstaller.DiscoverModulesAsync();

            foreach (var module in modules)
            {
                var check = new CheckBox { IsChecked = true, Color = Colors.White };
                var row = new HorizontalStackLayout { Spacing = 10 };
                row.Children.Add(check);
                row.Children.Add(new Label
                {
                    Text = $"{module.Name} v{module.Version}",
                    FontSize = 14,
                    TextColor = Colors.White,
                    VerticalOptions = LayoutOptions.Center
                });
                row.Children.Add(new Label
                {
                    Text = module.Description,
                    FontSize = 12,
                    TextColor = Color.FromArgb("#8E8E93"),
                    VerticalOptions = LayoutOptions.Center
                });

                ModuleList.Children.Add(row);
                _moduleChecks.Add((module, check));
            }

            if (modules.Count == 0)
            {
                ModuleList.Children.Add(new Label
                {
                    Text = "No modules found in Modules/ directory.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#8E8E93")
                });
            }
        }

        // --- Navigation ------------------------------------------------------

        private void OnBackClicked(object? sender, EventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepUI();
            }
        }

        private async void OnNextClicked(object? sender, EventArgs e)
        {
            if (_currentStep < TotalSteps)
            {
                if (_currentStep == 1 && string.IsNullOrWhiteSpace(UsernameEntry.Text))
                {
                    await DisplayAlertAsync("Error", "Please enter a display name.", "OK");
                    return;
                }

                if (_currentStep == 2 && !ValidatePorts())
                    return;

                _currentStep++;
                UpdateStepUI();
            }
            else
            {
                await StartInstallAsync();
            }
        }

        private void UpdateStepUI()
        {
            Step0Panel.IsVisible = _currentStep == 0;
            Step1Panel.IsVisible = _currentStep == 1;
            Step2Panel.IsVisible = _currentStep == 2;
            Step3Panel.IsVisible = _currentStep == 3;

            HeaderRow.IsVisible = _currentStep > 0;
            ButtonPanel.IsVisible = _currentStep > 0;
            BackButton.IsVisible = _currentStep > 1;
            NextButton.Text = _currentStep == TotalSteps ? "Install" : "Next";

            StepLabel.Text = _currentStep switch
            {
                1 => "Step 1 of 3 — User Profile",
                2 => "Step 2 of 3 — Port Configuration",
                3 => "Step 3 of 3 — Module Selection",
                _ => ""
            };
        }

        // --- Port Validation -------------------------------------------------

        private bool ValidatePorts()
        {
            PortErrorLabel.IsVisible = false;

            if (!int.TryParse(OfficialPortEntry.Text, out var op) || op < 1024 || op > 65000)
            {
                ShowPortError("Official port must be between 1024 and 65000.");
                return false;
            }
            if (!int.TryParse(ThirdPartyPortEntry.Text, out var tp) || tp < 1024 || tp > 64000)
            {
                ShowPortError("Third-party port must be between 1024 and 64000.");
                return false;
            }

            int opEnd = op + 100;
            int tpEnd = tp + 1000;
            if (op < tpEnd && tp < opEnd)
            {
                ShowPortError($"Port ranges overlap! Official {op}–{opEnd - 1} conflicts with Third-party {tp}–{tpEnd - 1}.");
                return false;
            }

            return true;
        }

        private void ShowPortError(string message)
        {
            PortErrorLabel.Text = message;
            PortErrorLabel.IsVisible = true;
        }

        // --- Log Toggle ------------------------------------------------------

        private void OnToggleLogClicked(object? sender, EventArgs e)
        {
            _logVisible = !_logVisible;
            LogScroll.IsVisible = _logVisible;
            ToggleLogButton.Text = _logVisible ? "Hide Log" : "Show Log";
        }

        // --- Installation ----------------------------------------------------

        private async Task StartInstallAsync()
        {
            // Save user data first
            _appData.Data.User.Name = UsernameEntry.Text?.Trim() ?? "";
            if (int.TryParse(OfficialPortEntry.Text, out var op))
                _appData.Data.Ports.OfficialStart = op;
            if (int.TryParse(ThirdPartyPortEntry.Text, out var tp))
                _appData.Data.Ports.ThirdPartyStart = tp;
            await _appData.SaveAsync();

            var selectedModules = _moduleChecks
                .Where(mc => mc.Check.IsChecked)
                .Select(mc => mc.Module)
                .ToList();

            if (selectedModules.Count == 0)
            {
                await FinishSetupAsync();
                return;
            }

            // Switch UI to install mode
            ButtonPanel.IsVisible = false;
            ModuleListScroll.IsVisible = false;
            InstallPanel.IsVisible = true;
            ToggleLogButton.IsVisible = true;
            StepLabel.Text = "Installing...";

            _cts = new CancellationTokenSource();
            var logProgress = new Progress<string>(AddLog);

            var totalSteps = 0;
            var completedSteps = 0;

            var requiredEngineIds = selectedModules
                .SelectMany(m => m.Dependencies.Engines)
                .Select(e => e.Id)
                .Distinct()
                .ToList();
            var allEngines = _engineInstaller.DiscoverEngines();
            totalSteps += requiredEngineIds.Count;
            totalSteps += selectedModules.Count * 2;

            // Download progress: updates BOTH bars
            _lastDownloadedBytes = 0;
            _lastSpeedUpdate = DateTime.UtcNow;
            _lastSpeed = 0;

            var downloadProgress = new Progress<DownloadProgress>(dp =>
            {
                if (dp.TotalBytes <= 0) return;

                var fileFraction = (double)dp.DownloadedBytes / dp.TotalBytes;
                var overallFraction = (completedSteps + fileFraction * 0.9) / totalSteps;

                // Calculate speed
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastSpeedUpdate).TotalSeconds;
                if (elapsed >= 0.5)
                {
                    _lastSpeed = (dp.DownloadedBytes - _lastDownloadedBytes) / elapsed;
                    _lastDownloadedBytes = dp.DownloadedBytes;
                    _lastSpeedUpdate = now;
                }

                var detail = $"{FormatBytes(dp.DownloadedBytes)} / {FormatBytes(dp.TotalBytes)}";
                if (_lastSpeed > 0)
                    detail += $" — {FormatBytes((long)_lastSpeed)}/s";

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    InstallProgress.Progress = overallFraction;
                    FileProgress.Progress = fileFraction;
                    DownloadDetailLabel.Text = detail;
                });
            });

            try
            {
                // 1. Install required engines
                foreach (var engineId in requiredEngineIds)
                {
                    var engine = allEngines.FirstOrDefault(e => e.Id == engineId);
                    if (engine == null)
                    {
                        AddLog($"⚠ Engine '{engineId}' not found, skipping.");
                        completedSteps++;
                        continue;
                    }
                    if (engine.Status.Installed)
                    {
                        AddLog($"✓ Engine '{engine.Name}' already installed.");
                        completedSteps++;
                        UpdateOverallProgress(completedSteps, totalSteps);
                        continue;
                    }

                    UpdateInstallStatus($"Installing engine: {engine.Name}...");
                    ResetFileProgress();
                    await Task.Run(() =>
                        _engineInstaller.InstallAsync(engine, logProgress, downloadProgress, _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateOverallProgress(completedSteps, totalSteps);
                    ResetFileProgress();
                }

                // 2. Install each selected module
                foreach (var module in selectedModules)
                {
                    UpdateInstallStatus($"Downloading {module.Name}...");
                    ResetFileProgress();
                    _lastDownloadedBytes = 0;
                    _lastSpeedUpdate = DateTime.UtcNow;
                    _lastSpeed = 0;

                    var downloaded = await Task.Run(() =>
                        _moduleInstaller.DownloadSourceAsync(module, logProgress, downloadProgress, _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateOverallProgress(completedSteps, totalSteps);
                    ResetFileProgress();

                    if (!downloaded)
                    {
                        AddLog($"✗ Source download failed for {module.Name}");
                        completedSteps++;
                        UpdateOverallProgress(completedSteps, totalSteps);
                        continue;
                    }

                    UpdateInstallStatus($"Setting up {module.Name}...");
                    var success = await Task.Run(() =>
                        _moduleRunner.ExecuteFirstRunAsync(module, logProgress, _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateOverallProgress(completedSteps, totalSteps);

                    AddLog(success
                        ? $"✓ {module.Name} installed successfully"
                        : $"✗ Setup failed for {module.Name}");
                }

                UpdateInstallStatus("Setup complete!");
                StepLabel.Text = "Setup complete!";
            }
            catch (OperationCanceledException)
            {
                UpdateInstallStatus("Installation canceled.");
                StepLabel.Text = "Installation canceled.";
            }
            catch (Exception ex)
            {
                UpdateInstallStatus($"Error: {ex.Message}");
                StepLabel.Text = "Installation failed.";
                AddLog($"Error: {ex.Message}");
            }

            // Show finish button
            ButtonPanel.IsVisible = true;
            BackButton.IsVisible = false;
            NextButton.Text = "Finish";
            NextButton.Clicked -= OnNextClicked;
            NextButton.Clicked += async (s, e) => await FinishSetupAsync();
        }

        // --- Helpers ---------------------------------------------------------

        private void UpdateInstallStatus(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                InstallStatusLabel.Text = message);
        }

        private void UpdateOverallProgress(int completed, int total)
        {
            if (total <= 0) return;
            MainThread.BeginInvokeOnMainThread(() =>
                InstallProgress.Progress = (double)completed / total);
        }

        private void ResetFileProgress()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FileProgress.Progress = 0;
                DownloadDetailLabel.Text = "";
            });
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        private async Task FinishSetupAsync()
        {
            try
            {
                _appData.Data.FirstRunCompleted = true;
                await _appData.SaveAsync();

                if (Application.Current?.Windows.Count > 0)
                {
                    var mainPage = _services.GetRequiredService<AppShellPage>();
                    Application.Current.Windows[0].Page = mainPage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SetupWizard] FinishSetupAsync error: {ex}");
                await DisplayAlertAsync("Error", $"Failed to navigate: {ex.Message}", "OK");
            }
        }

        // --- Logging ---------------------------------------------------------

        private void AddLog(string message)
        {
            _logBuffer.AppendLine(message);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                LogEditor.Text = _logBuffer.ToString();
                await LogScroll.ScrollToAsync(0, LogScroll.ContentSize.Height, false);
            });
        }
    }
}
