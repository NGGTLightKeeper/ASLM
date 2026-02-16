using System.Text;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// First-run setup wizard. Collects username, configures port ranges,
    /// lets the user choose modules to install, then runs the full install pipeline.
    /// </summary>
    public partial class SetupWizardPage : ContentPage
    {
        private readonly AppDataService _appData;
        private readonly EngineInstaller _engineInstaller;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly ModuleRunner _moduleRunner;
        private readonly IServiceProvider _services;

        private int _currentStep = 1;
        private const int TotalSteps = 3;

        // Module checkboxes for step 3
        private readonly List<(ModuleConfig Module, CheckBox Check)> _moduleChecks = [];
        private readonly StringBuilder _logBuffer = new();
        private CancellationTokenSource? _cts;
        private bool _logVisible;

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

            // Pre-fill from existing data
            UsernameEntry.Text = _appData.Data.User.Name;
            OfficialPortEntry.Text = _appData.Data.Ports.OfficialStart.ToString();
            ThirdPartyPortEntry.Text = _appData.Data.Ports.ThirdPartyStart.ToString();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            PopulateModuleList();
        }

        // --- Module Discovery ------------------------------------------------

        private void PopulateModuleList()
        {
            ModuleList.Children.Clear();
            _moduleChecks.Clear();

            var modules = _moduleInstaller.DiscoverModules();

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
                    TextColor = Color.FromArgb("#888"),
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
                    TextColor = Color.FromArgb("#888")
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
                // Validate current step
                if (_currentStep == 1 && string.IsNullOrWhiteSpace(UsernameEntry.Text))
                {
                    await DisplayAlertAsync("Error", "Please enter a display name.", "OK");
                    return;
                }

                if (_currentStep == 2)
                {
                    if (!int.TryParse(OfficialPortEntry.Text, out var op) || op < 1024 || op > 65000)
                    {
                        await DisplayAlertAsync("Error", "Official port must be between 1024 and 65000.", "OK");
                        return;
                    }
                    if (!int.TryParse(ThirdPartyPortEntry.Text, out var tp) || tp < 1024 || tp > 64000)
                    {
                        await DisplayAlertAsync("Error", "Third-party port must be between 1024 and 64000.", "OK");
                        return;
                    }
                }

                _currentStep++;
                UpdateStepUI();
            }
            else
            {
                // Final step — start installation
                await StartInstallAsync();
            }
        }

        private void UpdateStepUI()
        {
            Step1Panel.IsVisible = _currentStep == 1;
            Step2Panel.IsVisible = _currentStep == 2;
            Step3Panel.IsVisible = _currentStep == 3;

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
            _appData.Data.Ports.OfficialStart = int.TryParse(OfficialPortEntry.Text, out var op) ? op : 8000;
            _appData.Data.Ports.ThirdPartyStart = int.TryParse(ThirdPartyPortEntry.Text, out var tp) ? tp : 9000;
            await _appData.SaveAsync();

            // Gather selected modules
            var selectedModules = _moduleChecks
                .Where(mc => mc.Check.IsChecked)
                .Select(mc => mc.Module)
                .ToList();

            if (selectedModules.Count == 0)
            {
                // No modules selected — just finish setup
                await FinishSetupAsync();
                return;
            }

            // Switch UI to install mode — hide buttons and module list, show progress
            ButtonPanel.IsVisible = false;
            ModuleListScroll.IsVisible = false;
            InstallPanel.IsVisible = true;
            StepLabel.Text = "Installing...";

            _cts = new CancellationTokenSource();
            var logProgress = new Progress<string>(AddLog);

            // Count total work steps for progress bar
            var totalSteps = 0;
            var completedSteps = 0;

            // Count engines to install
            var requiredEngineIds = selectedModules
                .SelectMany(m => m.Dependencies.Engines)
                .Select(e => e.Id)
                .Distinct()
                .ToList();
            var allEngines = _engineInstaller.DiscoverEngines();
            totalSteps += requiredEngineIds.Count;
            // Each module = download + firstRun = 2 steps
            totalSteps += selectedModules.Count * 2;

            var downloadProgress = new Progress<DownloadProgress>(dp =>
            {
                if (dp.TotalBytes > 0)
                {
                    // Show download progress within current step
                    var stepFraction = (double)dp.DownloadedBytes / dp.TotalBytes;
                    var overall = (completedSteps + stepFraction * 0.9) / totalSteps;
                    MainThread.BeginInvokeOnMainThread(() =>
                        InstallProgress.Progress = overall);
                }
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
                        UpdateProgress(completedSteps, totalSteps);
                        continue;
                    }

                    UpdateInstallStatus($"Installing engine: {engine.Name}...");
                    await Task.Run(() =>
                        _engineInstaller.InstallAsync(engine, logProgress, downloadProgress, _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateProgress(completedSteps, totalSteps);
                }

                // 2. Install each selected module
                foreach (var module in selectedModules)
                {
                    // Download source
                    UpdateInstallStatus($"Downloading {module.Name}...");
                    var downloaded = await Task.Run(() =>
                        _moduleInstaller.DownloadSourceAsync(module, logProgress, downloadProgress, _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateProgress(completedSteps, totalSteps);

                    if (!downloaded)
                    {
                        AddLog($"✗ Source download failed for {module.Name}");
                        completedSteps++; // skip firstRun step
                        UpdateProgress(completedSteps, totalSteps);
                        continue;
                    }

                    // Install deps + firstRun
                    UpdateInstallStatus($"Setting up {module.Name}...");
                    var success = await Task.Run(() =>
                        _moduleRunner.ExecuteFirstRunAsync(module, logProgress, _cts.Token),
                        _cts.Token);
                    completedSteps++;
                    UpdateProgress(completedSteps, totalSteps);

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

        private void UpdateInstallStatus(string message)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                InstallStatusLabel.Text = message);
        }

        private void UpdateProgress(int completed, int total)
        {
            if (total <= 0) return;
            MainThread.BeginInvokeOnMainThread(() =>
                InstallProgress.Progress = (double)completed / total);
        }

        private async Task FinishSetupAsync()
        {
            _appData.Data.FirstRunCompleted = true;
            await _appData.SaveAsync();

            // Navigate to MainPage
            if (Application.Current?.Windows.Count > 0)
            {
                var mainPage = _services.GetRequiredService<MainPage>();
                Application.Current.Windows[0].Page = mainPage;
            }
        }

        // --- Logging ---------------------------------------------------------

        private void AddLog(string message)
        {
            _logBuffer.AppendLine(message);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                LogEditor.Text = _logBuffer.ToString();
                // Auto-scroll to bottom
                await LogScroll.ScrollToAsync(0, LogScroll.ContentSize.Height, false);
            });
        }
    }
}
