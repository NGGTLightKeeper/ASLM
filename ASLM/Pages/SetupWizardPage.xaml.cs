// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Diagnostics;
using System.Text;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    // Setup wizard page

    /// <summary>
    /// Guides the first-run flow from profile setup through module installation.
    /// </summary>
    public partial class SetupWizardPage : ContentPage
    {
        private const int TotalSteps = 3;

        private readonly AppDataService _appData;
        private readonly EngineInstaller _engineInstaller;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly UpdateService _updateService;
        private readonly IServiceProvider _services;

        private readonly List<(ModuleConfig Module, CheckBox Check)> _moduleChecks = [];
        private readonly StringBuilder _logBuffer = new();

        private int _currentStep;
        private CancellationTokenSource? _cts;
        private bool _logVisible;
        private bool _moduleListLoaded;
        private readonly object _logLock = new();
        private int _logFlushQueued;
        private int _logFlushRequested;

        private long _lastDownloadedBytes;
        private DateTime _lastSpeedUpdate = DateTime.UtcNow;
        private double _lastSpeed;

        // Initialization

        /// <summary>
        /// Creates the setup wizard and preloads persisted defaults into the form.
        /// </summary>
        public SetupWizardPage(
            AppDataService appData,
            EngineInstaller engineInstaller,
            ModuleInstaller moduleInstaller,
            ModuleRunner moduleRunner,
            UpdateService updateService,
            IServiceProvider services)
        {
            _appData = appData;
            _engineInstaller = engineInstaller;
            _moduleInstaller = moduleInstaller;
            _moduleRunner = moduleRunner;
            _updateService = updateService;
            _services = services;

            InitializeComponent();

            // Reuse the saved profile name when available, otherwise fall back to the Windows user name.
            var existingName = _appData.Data.User.Name;
            UsernameEntry.Text = string.IsNullOrWhiteSpace(existingName)
                ? Environment.UserName
                : existingName;

            OfficialPortEntry.Text = _appData.Data.Ports.OfficialStart.ToString();
            ThirdPartyPortEntry.Text = _appData.Data.Ports.ThirdPartyStart.ToString();

            // Loaded is more reliable than OnAppearing for this WinUI startup flow.
            Loaded += OnLoaded;
        }


        // Initial load

        /// <summary>
        /// Populates the module selection list once after the page is ready.
        /// </summary>
        private async void OnLoaded(object? sender, EventArgs e)
        {
            if (_moduleListLoaded)
            {
                return;
            }

            _moduleListLoaded = true;
            await PopulateModuleListAsync();
        }


        // Welcome actions

        /// <summary>
        /// Starts the step-by-step setup flow.
        /// </summary>
        private void OnSetupClicked(object? sender, EventArgs e)
        {
            _currentStep = 1;
            UpdateStepUI();
        }

        // Fast setup

        /// <summary>
        /// Saves default values and jumps directly to module selection.
        /// </summary>
        private async void OnFastSetupClicked(object? sender, EventArgs e)
        {
            // Fast setup uses the Windows username and the default port ranges.
            _appData.Data.User.Name = Environment.UserName;
            _appData.Data.Ports.OfficialStart = 8000;
            _appData.Data.Ports.ThirdPartyStart = 9000;
            await _appData.SaveAsync();

            _currentStep = 3;
            UpdateStepUI();
        }


        // Module list

        /// <summary>
        /// Builds the selectable module list shown on the last setup step.
        /// </summary>
        private async Task PopulateModuleListAsync()
        {
            ModuleList.Children.Clear();
            _moduleChecks.Clear();

            List<ModuleConfig> modules;
            try
            {
                modules = await Task.Run(() => _moduleInstaller.DiscoverModulesAsync());
            }
            catch (Exception ex)
            {
                ModuleList.Children.Add(new Label
                {
                    Text = $"Failed to load module list: {ex.Message}",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#FF453A")
                });
                return;
            }

            foreach (var module in modules)
            {
                var check = new CheckBox { IsChecked = true, Color = Colors.White };
                var row = new HorizontalStackLayout { Spacing = 10 };

                row.Children.Add(check);
                row.Children.Add(new Label
                {
                    Text = module.Version.All(c => char.IsDigit(c) || c == '.')
                        ? $"{module.Name} v{module.Version}"
                        : $"{module.Name} {module.Version}",
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


        // Navigation

        /// <summary>
        /// Moves one step back in the wizard when possible.
        /// </summary>
        private void OnBackClicked(object? sender, EventArgs e)
        {
            if (_currentStep <= 1)
            {
                return;
            }

            _currentStep--;
            UpdateStepUI();
        }

        // Next action

        /// <summary>
        /// Advances the wizard or starts installation on the last step.
        /// </summary>
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
                {
                    return;
                }

                _currentStep++;
                UpdateStepUI();
                return;
            }

            await StartInstallAsync();
        }

        // Step UI

        /// <summary>
        /// Switches visible panels and button states for the current wizard step.
        /// </summary>
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
            ResetNavigationButtons();

            StepLabel.Text = _currentStep switch
            {
                1 => "Step 1 of 3 - User Profile",
                2 => "Step 2 of 3 - Port Configuration",
                3 => "Step 3 of 3 - Module Selection",
                _ => string.Empty
            };
        }


        // Port validation

        /// <summary>
        /// Validates both persisted port ranges and reports overlap errors.
        /// </summary>
        private bool ValidatePorts()
        {
            PortErrorLabel.IsVisible = false;

            if (!int.TryParse(OfficialPortEntry.Text, out var officialPort) || officialPort < 1024 || officialPort > 65000)
            {
                ShowPortError("Official port must be between 1024 and 65000.");
                return false;
            }

            if (!int.TryParse(ThirdPartyPortEntry.Text, out var thirdPartyPort) || thirdPartyPort < 1024 || thirdPartyPort > 64000)
            {
                ShowPortError("Third-party port must be between 1024 and 64000.");
                return false;
            }

            var officialPortEnd = officialPort + 100;
            var thirdPartyPortEnd = thirdPartyPort + 1000;
            if (officialPort < thirdPartyPortEnd && thirdPartyPort < officialPortEnd)
            {
                ShowPortError($"Port ranges overlap. Official {officialPort}-{officialPortEnd - 1} conflicts with Third-party {thirdPartyPort}-{thirdPartyPortEnd - 1}.");
                return false;
            }

            return true;
        }

        // Port error

        /// <summary>
        /// Shows the current port validation error.
        /// </summary>
        private void ShowPortError(string message)
        {
            PortErrorLabel.Text = message;
            PortErrorLabel.IsVisible = true;
        }


        // Log toggle

        /// <summary>
        /// Shows or hides the installation log panel.
        /// </summary>
        private void OnToggleLogClicked(object? sender, EventArgs e)
        {
            _logVisible = !_logVisible;
            LogScroll.IsVisible = _logVisible;
            ToggleLogButton.Text = _logVisible ? "Hide Log" : "Show Log";
        }


        // Installation

        /// <summary>
        /// Saves the wizard data and installs the selected module stack.
        /// </summary>
        private async Task StartInstallAsync()
        {
            // Persist the profile and port values before any installation begins.
            _appData.Data.User.Name = UsernameEntry.Text?.Trim() ?? string.Empty;
            if (int.TryParse(OfficialPortEntry.Text, out var officialPort))
            {
                _appData.Data.Ports.OfficialStart = officialPort;
            }

            if (int.TryParse(ThirdPartyPortEntry.Text, out var thirdPartyPort))
            {
                _appData.Data.Ports.ThirdPartyStart = thirdPartyPort;
            }

            await _appData.SaveAsync();

            var selectedModules = _moduleChecks
                .Where(moduleCheck => moduleCheck.Check.IsChecked)
                .Select(moduleCheck => moduleCheck.Module)
                .ToList();

            if (selectedModules.Count == 0)
            {
                await FinishSetupAsync();
                return;
            }

            // Switch the final wizard step from selection mode into install mode.
            ButtonPanel.IsVisible = false;
            ModuleListScroll.IsVisible = false;
            InstallPanel.IsVisible = true;
            ToggleLogButton.IsVisible = true;
            StepLabel.Text = "Installing...";

            _cts = new CancellationTokenSource();
            var logProgress = new InlineProgress<string>(AddLog);

            var totalSteps = 0;
            var completedSteps = 0;
            var hasFailures = false;

            var requiredEngineIds = selectedModules
                .SelectMany(module => module.Dependencies.Engines)
                .Select(engine => engine.Id)
                .Distinct()
                .ToList();

            var allEngines = await Task.Run(() => _engineInstaller.DiscoverEngines(), _cts.Token);
            totalSteps += requiredEngineIds.Count;
            totalSteps += selectedModules.Sum(GetModuleInstallStepCount);

            // Reset rolling download metrics before the first transfer begins.
            ResetDownloadMetrics();

            var downloadProgress = new Progress<DownloadProgress>(progress =>
            {
                if (progress.TotalBytes <= 0)
                {
                    return;
                }

                // Combine the current file progress with the completed step count for the overall bar.
                var fileFraction = (double)progress.DownloadedBytes / progress.TotalBytes;
                var overallFraction = (completedSteps + fileFraction * 0.9) / totalSteps;

                // Refresh the transfer speed at a steady interval to avoid noisy UI updates.
                var now = DateTime.UtcNow;
                var elapsed = (now - _lastSpeedUpdate).TotalSeconds;
                if (elapsed >= 0.5)
                {
                    _lastSpeed = (progress.DownloadedBytes - _lastDownloadedBytes) / elapsed;
                    _lastDownloadedBytes = progress.DownloadedBytes;
                    _lastSpeedUpdate = now;
                }

                var detail = $"{FormatBytes(progress.DownloadedBytes)} / {FormatBytes(progress.TotalBytes)}";
                if (_lastSpeed > 0)
                {
                    detail += $" - {FormatBytes((long)_lastSpeed)}/s";
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    InstallProgress.Progress = overallFraction;
                    FileProgress.Progress = fileFraction;
                    DownloadDetailLabel.Text = detail;
                });
            });

            try
            {
                // Install each required engine before module downloads begin.
                foreach (var engineId in requiredEngineIds)
                {
                    var engine = allEngines.FirstOrDefault(candidate => candidate.Id == engineId);
                    if (engine == null)
                    {
                        AddLog($"[Missing] Engine '{engineId}' not found.");
                        hasFailures = true;
                        completedSteps++;
                        UpdateOverallProgress(completedSteps, totalSteps);
                        continue;
                    }

                    if (engine.Status.Installed)
                    {
                        AddLog($"[OK] Engine '{engine.Name}' already installed.");
                        completedSteps++;
                        UpdateOverallProgress(completedSteps, totalSteps);
                        continue;
                    }

                    UpdateInstallStatus($"Installing engine: {engine.Name}...");
                    ResetFileProgress();
                    await Task.Run(
                        () => _engineInstaller.InstallAsync(engine, logProgress, downloadProgress, _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateOverallProgress(completedSteps, totalSteps);
                    ResetFileProgress();
                }

                // Install each selected module either through the update-aware pipeline or the legacy download flow.
                foreach (var module in selectedModules)
                {
                    if (ShouldUseConfiguredUpdateInstall(module))
                    {
                        UpdateInstallStatus($"Installing {module.Name}...");
                        ResetFileProgress();
                        ResetDownloadMetrics();

                        var updateInstalled = await InstallModuleFromUpdateConfigAsync(
                            module,
                            logProgress,
                            downloadProgress,
                            _cts.Token);

                        completedSteps += GetModuleInstallStepCount(module);
                        UpdateOverallProgress(completedSteps, totalSteps);
                        ResetFileProgress();

                        AddLog(updateInstalled
                            ? $"[OK] {module.Name} installed successfully"
                            : $"[Error] Installation failed for {module.Name}");
                        hasFailures |= !updateInstalled;
                        continue;
                    }

                    UpdateInstallStatus($"Downloading {module.Name}...");
                    ResetFileProgress();
                    ResetDownloadMetrics();

                    var downloaded = await Task.Run(
                        () => _moduleInstaller.DownloadSourceAsync(
                            module,
                            logProgress,
                            downloadProgress,
                            _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateOverallProgress(completedSteps, totalSteps);
                    ResetFileProgress();

                    if (!downloaded)
                    {
                        AddLog($"[Error] Source download failed for {module.Name}");
                        hasFailures = true;
                        completedSteps++;
                        UpdateOverallProgress(completedSteps, totalSteps);
                        continue;
                    }

                    UpdateInstallStatus($"Setting up {module.Name}...");
                    var success = await Task.Run(
                        () => _moduleRunner.ExecuteFirstRunAsync(module, logProgress, _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateOverallProgress(completedSteps, totalSteps);

                    AddLog(success
                        ? $"[OK] {module.Name} installed successfully"
                        : $"[Error] Setup failed for {module.Name}");
                    hasFailures |= !success;
                }

                UpdateInstallStatus("Setup complete!");
                StepLabel.Text = "Setup complete!";
            }
            catch (OperationCanceledException)
            {
                hasFailures = true;
                UpdateInstallStatus("Installation canceled.");
                StepLabel.Text = "Installation canceled.";
            }
            catch (Exception ex)
            {
                hasFailures = true;
                UpdateInstallStatus($"Error: {ex.Message}");
                StepLabel.Text = "Installation failed.";
                AddLog($"Error: {ex.Message}");
            }

            ButtonPanel.IsVisible = true;

            if (!hasFailures)
            {
                ConfigureFinishButton();
            }
            else
            {
                ConfigureRetryAndSkipButtons();
            }
        }


        // Navigation button state

        /// <summary>
        /// Restores the default back and next button handlers.
        /// </summary>
        private void ResetNavigationButtons()
        {
            NextButton.Clicked -= OnRetryInstallClicked;
            NextButton.Clicked -= OnFinishClicked;
            NextButton.Clicked -= OnNextClicked;
            NextButton.Clicked += OnNextClicked;

            BackButton.Clicked -= OnSkipClicked;
            BackButton.Clicked -= OnBackClicked;
            BackButton.Clicked += OnBackClicked;

            NextButton.BackgroundColor = Color.FromArgb("#007AFF");
            BackButton.BackgroundColor = Color.FromArgb("#3A3A3C");
            BackButton.Text = "Back";
        }

        // Finish state

        /// <summary>
        /// Converts the main action button into the finish action.
        /// </summary>
        private void ConfigureFinishButton()
        {
            ResetNavigationButtons();
            BackButton.IsVisible = false;
            NextButton.Text = "Finish";
            NextButton.Clicked -= OnNextClicked;
            NextButton.Clicked += OnFinishClicked;
        }

        // Retry state

        /// <summary>
        /// Converts the buttons into retry and skip actions after a failed install.
        /// </summary>
        private void ConfigureRetryAndSkipButtons()
        {
            ResetNavigationButtons();

            NextButton.Text = "Retry";
            NextButton.Clicked -= OnNextClicked;
            NextButton.Clicked += OnRetryInstallClicked;

            BackButton.Text = "Skip";
            BackButton.IsVisible = true;
            BackButton.Clicked -= OnBackClicked;
            BackButton.Clicked += OnSkipClicked;
        }

        // Retry action

        /// <summary>
        /// Restarts the installation phase after a failure.
        /// </summary>
        private async void OnRetryInstallClicked(object? sender, EventArgs e)
        {
            ButtonPanel.IsVisible = false;
            await StartInstallAsync();
        }

        // Finish action

        /// <summary>
        /// Completes the wizard and opens the main application shell.
        /// </summary>
        private async void OnFinishClicked(object? sender, EventArgs e)
        {
            await FinishSetupAsync();
        }

        // Skip action

        /// <summary>
        /// Completes the wizard even when installation had failures.
        /// </summary>
        private async void OnSkipClicked(object? sender, EventArgs e)
        {
            await FinishSetupAsync();
        }


        // Progress UI

        /// <summary>
        /// Updates the current installation status text.
        /// </summary>
        private void UpdateInstallStatus(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                InstallStatusLabel.Text = message);
        }

        // Overall progress

        /// <summary>
        /// Updates the overall installation progress bar.
        /// </summary>
        private void UpdateOverallProgress(int completed, int total)
        {
            if (total <= 0)
            {
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
                InstallProgress.Progress = (double)completed / total);
        }

        // File progress

        /// <summary>
        /// Clears the per-file progress UI before the next download.
        /// </summary>
        private void ResetFileProgress()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                FileProgress.Progress = 0;
                DownloadDetailLabel.Text = string.Empty;
            });
        }

        /// <summary>
        /// Resets the rolling metrics used to calculate the download speed label.
        /// </summary>
        private void ResetDownloadMetrics()
        {
            _lastDownloadedBytes = 0;
            _lastSpeedUpdate = DateTime.UtcNow;
            _lastSpeed = 0;
        }

        /// <summary>
        /// Returns whether the module should be installed through the update-aware pipeline.
        /// </summary>
        private static bool ShouldUseConfiguredUpdateInstall(ModuleConfig module)
        {
            return module.HasDeclaredUpdateConfig &&
                   string.Equals(module.Source.Type, "github", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(module.Source.Repo);
        }

        /// <summary>
        /// Returns how many overall progress steps one module consumes during setup.
        /// </summary>
        private static int GetModuleInstallStepCount(ModuleConfig module)
        {
            return ShouldUseConfiguredUpdateInstall(module) ? 1 : 2;
        }

        /// <summary>
        /// Installs one module through the updater so setup respects the manifest update configuration.
        /// </summary>
        private async Task<bool> InstallModuleFromUpdateConfigAsync(
            ModuleConfig module,
            IProgress<string> logProgress,
            IProgress<DownloadProgress> downloadProgress,
            CancellationToken ct)
        {
            // Setup should resolve the same branch or release candidate the normal updater would use.
            var candidate = await Task.Run(
                () => _updateService.ResolveModuleInstallCandidateAsync(module, ct),
                ct);
            if (candidate == null)
            {
                logProgress.Report($"[Error] Could not resolve install source for {module.Name}");
                return false;
            }

            // Reuse the full module update pipeline so preserve rules and first-run behavior stay consistent.
            return await Task.Run(
                () => _updateService.ApplyModuleUpdateAsync(candidate, logProgress, downloadProgress, ct),
                ct);
        }

        // Byte formatting

        /// <summary>
        /// Formats byte counts into readable size labels.
        /// </summary>
        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1_073_741_824)
            {
                return $"{bytes / 1_073_741_824.0:F1} GB";
            }

            if (bytes >= 1_048_576)
            {
                return $"{bytes / 1_048_576.0:F1} MB";
            }

            if (bytes >= 1024)
            {
                return $"{bytes / 1024.0:F1} KB";
            }

            return $"{bytes} B";
        }

        // Finish flow

        /// <summary>
        /// Marks the first run as completed and opens the main shell.
        /// </summary>
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


        // Logging

        /// <summary>
        /// Appends one log line and keeps the scroll view pinned to the bottom.
        /// </summary>
        private void AddLog(string message)
        {
            lock (_logLock)
            {
                _logBuffer.AppendLine(message);
            }

            Interlocked.Exchange(ref _logFlushRequested, 1);
            if (Interlocked.Exchange(ref _logFlushQueued, 1) == 1)
            {
                return;
            }

            _ = FlushLogAsync();
        }

        /// <summary>
        /// Flushes buffered log lines at a steady cadence while installers keep streaming output.
        /// </summary>
        private async Task FlushLogAsync()
        {
            try
            {
                do
                {
                    Interlocked.Exchange(ref _logFlushRequested, 0);
                    await Task.Delay(75);

                    string text;
                    lock (_logLock)
                    {
                        text = _logBuffer.ToString();
                    }

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        LogEditor.Text = text;
                        _ = ScrollLogToEndAsync();
                    });
                }
                while (Interlocked.CompareExchange(ref _logFlushRequested, 0, 0) == 1);
            }
            finally
            {
                Interlocked.Exchange(ref _logFlushQueued, 0);
                if (Interlocked.CompareExchange(ref _logFlushRequested, 0, 0) == 1 &&
                    Interlocked.Exchange(ref _logFlushQueued, 1) == 0)
                {
                    _ = FlushLogAsync();
                }
            }
        }

        /// <summary>
        /// Scrolls the setup log without blocking the streaming flush loop.
        /// </summary>
        private async Task ScrollLogToEndAsync()
        {
            try
            {
                await LogScroll.ScrollToAsync(0, LogScroll.ContentSize.Height, false);
            }
            catch
            {
                // Best-effort scroll only; log streaming must keep flowing even if WinUI skips a scroll pass.
            }
        }

        /// <summary>
        /// Reports progress synchronously on the producer thread so UI updates can be throttled explicitly.
        /// </summary>
        private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
        {
            public void Report(T value)
            {
                handler(value);
            }
        }
    }
}
