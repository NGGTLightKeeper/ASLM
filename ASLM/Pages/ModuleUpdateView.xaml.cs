// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Distinguishes the supported module update dialog modes.
    /// </summary>
    public enum ModuleUpdateMode
    {
        Configure,
        Update
    }

    // Module update overlay

    /// <summary>
    /// Displays module update configuration and installation progress inside the shell overlay.
    /// </summary>
    public partial class ModuleUpdateView : ContentView, INotifyPropertyChanged
    {
        private const double DialogWidthFactor = 0.78;
        private const double DialogHeightFactor = 0.82;
        private const double MinDialogWidth = 860;
        private const double MinDialogHeight = 560;
        private const double MaxDialogWidth = 1200;
        private const double MaxDialogHeight = 820;

        private readonly ObservableCollection<string> _emptyBranches = [];
        private readonly ObservableCollection<UpdateCandidate> _emptyReleases = [];

        private ModuleViewModel? _module;
        private ModuleUpdateMode _mode;
        private bool _isSynchronizingPickers;
        private CancellationTokenSource? _logSyncCts;
        private int _logSyncQueued;
        private int _logSyncRequested;

        /// <summary>
        /// Raised when the user asks to close the module update overlay.
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <inheritdoc />
        public new event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates the module update overlay and hooks layout updates.
        /// </summary>
        public ModuleUpdateView()
        {
            InitializeComponent();
            ApplyBorderlessPickerStyle(SourceModePicker);
            ApplyBorderlessPickerStyle(ReleasePicker);
            ApplyBorderlessPickerStyle(BranchPicker);
            BindingContext = this;
            SizeChanged += OnViewSizeChanged;
        }


        // Dialog state

        /// <summary>
        /// Gets the title shown in the dialog header.
        /// </summary>
        public string DialogTitle => _mode == ModuleUpdateMode.Update
            ? "Module Update"
            : "Configure Module Updates";

        /// <summary>
        /// Gets the subtitle shown in the dialog header.
        /// </summary>
        public string DialogSubtitle => _module == null
            ? "No module selected."
            : $"{_module.Name} - {_module.Description}";

        /// <summary>
        /// Gets the currently installed module version.
        /// </summary>
        public string CurrentVersionLabel => _module?.VersionString ?? "-";

        /// <summary>
        /// Gets the selected target label shown in the header.
        /// </summary>
        public string TargetVersionLabel => _module?.SelectedTargetLabel ?? "No version selected";

        /// <summary>
        /// Gets selectable module update source modes.
        /// </summary>
        public IReadOnlyList<string> SourceModeOptions => _module?.SourceModeOptions ?? Array.Empty<string>();

        /// <summary>
        /// Gets selectable release versions for release and pre-release modes.
        /// </summary>
        public ObservableCollection<UpdateCandidate> ReleaseOptions => _module?.ReleaseOptions ?? _emptyReleases;

        /// <summary>
        /// Gets selectable repository branches for branch-based updates.
        /// </summary>
        public ObservableCollection<string> BranchOptions => _module?.BranchOptions ?? _emptyBranches;

        /// <summary>
        /// Gets or sets the active update source mode.
        /// </summary>
        public string SelectedSourceMode
        {
            get => _module?.SelectedSourceMode ?? "release";
            set
            {
                if (_module == null)
                {
                    return;
                }

                _module.SelectedSourceMode = value;
                RaiseModuleProperties();
                _ = EnsureModeOptionsLoadedAsync(forceRefresh: false);
            }
        }

        /// <summary>
        /// Gets or sets the active release target.
        /// </summary>
        public UpdateCandidate? SelectedReleaseOption
        {
            get => _module?.SelectedReleaseOption;
            set
            {
                if (_module == null)
                {
                    return;
                }

                _module.SelectedReleaseOption = value;
                RaiseModuleProperties();
            }
        }

        /// <summary>
        /// Gets or sets the active repository branch.
        /// </summary>
        public string SelectedBranch
        {
            get => _module?.SelectedBranch ?? "main";
            set
            {
                if (_module == null)
                {
                    return;
                }

                _module.ApplyBranchSelection(value);
                RaiseModuleProperties();
            }
        }

        /// <summary>
        /// Gets whether branch-related controls should be visible.
        /// </summary>
        public bool IsBranchMode => _module?.IsBranchMode ?? false;

        /// <summary>
        /// Gets whether release-related controls should be visible.
        /// </summary>
        public bool IsReleaseMode => _module?.IsReleaseMode ?? true;

        /// <summary>
        /// Gets whether the current module has a pending update candidate.
        /// </summary>
        public bool HasUpdate => _module?.HasUpdate ?? false;

        /// <summary>
        /// Gets whether the install action is currently available.
        /// </summary>
        public bool CanInstallUpdate => _module?.CanInstallSelectedUpdate ?? false;

        /// <summary>
        /// Gets whether the check action is currently available.
        /// </summary>
        public bool CanCheckUpdates => _module?.CanCheckUpdates ?? false;

        /// <summary>
        /// Gets whether the install button should stay visible.
        /// </summary>
        public bool ShowInstallAction => _module?.ShowInstallAction ?? false;

        /// <summary>
        /// Gets whether the overlay is currently running an update-related operation.
        /// </summary>
        public bool IsBusy => _module?.IsBusy ?? false;

        /// <summary>
        /// Gets the title shown above the activity panel.
        /// </summary>
        public string ActivityTitle => _mode == ModuleUpdateMode.Update
            ? "Update Activity"
            : "Update Status";

        /// <summary>
        /// Gets the current activity status text shown in the progress area.
        /// </summary>
        public string ActivityStatus => _module?.UpdateActivityStatus ?? _module?.UpdateStatus ?? "Ready.";

        /// <summary>
        /// Gets whether the progress area should show the bars.
        /// </summary>
        public bool HasActivityProgress => _module?.HasUpdateProgress ?? IsBusy;

        /// <summary>
        /// Gets the overall update progress fraction.
        /// </summary>
        public double OverallProgress => _module?.UpdateOverallProgress ?? 0;

        /// <summary>
        /// Gets the download progress fraction for the current file transfer.
        /// </summary>
        public double FileProgress => _module?.UpdateFileProgress ?? 0;

        /// <summary>
        /// Gets the formatted per-file transfer detail text.
        /// </summary>
        public string DownloadDetail => _module?.UpdateDownloadDetail ?? string.Empty;

        /// <summary>
        /// Gets whether the download detail label should be visible.
        /// </summary>
        public bool HasDownloadDetail => _module?.HasUpdateDownloadDetail ?? false;

        /// <summary>
        /// Gets the console-like activity log text.
        /// </summary>
        public string LogText => _module?.UpdateLogText ?? string.Empty;

        /// <summary>
        /// Gets whether the activity log contains messages.
        /// </summary>
        public bool HasLog => _module?.HasUpdateLog ?? false;


        // Overlay opening

        /// <summary>
        /// Opens the overlay for the requested module and mode.
        /// </summary>
        public Task OpenAsync(ModuleViewModel module, ModuleUpdateMode mode)
        {
            AttachModule(module);
            var selectedModule = _module;
            if (selectedModule == null)
            {
                return Task.CompletedTask;
            }

            _mode = mode;
            selectedModule.ResetCompletedUpdateSession();
            UpdateDialogSize();
            RaiseDialogProperties();
            SyncPickerSelections();
            SyncLogView();
            _ = EnsureModeOptionsLoadedAsync(forceRefresh: false);
            return Task.CompletedTask;
        }


        // Overlay events

        /// <summary>
        /// Closes the overlay when the dimmed background is tapped.
        /// </summary>
        private void OnBackgroundTapped(object? sender, EventArgs e)
        {
            RequestClose();
        }

        /// <summary>
        /// Swallows taps inside the dialog so they do not close the overlay.
        /// </summary>
        private void OnDialogTapped(object? sender, EventArgs e)
        {
            // Intentionally left blank so dialog taps do not bubble to the overlay background.
        }

        /// <summary>
        /// Closes the overlay from the close button.
        /// </summary>
        private void OnCloseClicked(object? sender, EventArgs e)
        {
            RequestClose();
        }

        /// <summary>
        /// Raises the close event for the host shell.
        /// </summary>
        private void RequestClose()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            _logSyncCts?.Cancel();
            _logSyncCts = null;
            Interlocked.Exchange(ref _logSyncQueued, 0);
            Interlocked.Exchange(ref _logSyncRequested, 0);
            DetachModule();
        }


        // Layout

        /// <summary>
        /// Recalculates the dialog size when the overlay control changes size.
        /// </summary>
        private void OnViewSizeChanged(object? sender, EventArgs e)
        {
            UpdateDialogSize();
        }

        /// <summary>
        /// Applies the responsive width and height used by the overlay dialog.
        /// </summary>
        private void UpdateDialogSize()
        {
            if (Width <= 0 || Height <= 0)
            {
                return;
            }

            DialogBorder.WidthRequest = ClampDialogSize(
                Math.Floor(Width * DialogWidthFactor),
                MinDialogWidth,
                MaxDialogWidth);
            DialogBorder.HeightRequest = ClampDialogSize(
                Math.Floor(Height * DialogHeightFactor),
                MinDialogHeight,
                MaxDialogHeight);
        }

        /// <summary>
        /// Clamps one dialog dimension into the supported bounds.
        /// </summary>
        private static double ClampDialogSize(double value, double minValue, double maxValue)
        {
            return Math.Max(minValue, Math.Min(maxValue, value));
        }


        // Module attachment

        /// <summary>
        /// Subscribes the dialog to the selected module view model.
        /// </summary>
        private void AttachModule(ModuleViewModel module)
        {
            if (ReferenceEquals(_module, module))
            {
                return;
            }

            DetachModule();
            _module = module;
            _module.PropertyChanged += OnModulePropertyChanged;
        }

        /// <summary>
        /// Unsubscribes from the previously attached module view model.
        /// </summary>
        private void DetachModule()
        {
            if (_module == null)
            {
                return;
            }

            _module.PropertyChanged -= OnModulePropertyChanged;
            _module = null;
        }

        /// <summary>
        /// Refreshes dialog bindings after the module card state changes.
        /// </summary>
        private void OnModulePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RaiseModuleProperties();
            if (ShouldSyncPickerSelection(e.PropertyName))
            {
                SyncPickerSelections();
            }

            if (e.PropertyName == nameof(ModuleViewModel.UpdateLogText) ||
                e.PropertyName == nameof(ModuleViewModel.HasUpdateLog))
            {
                QueueLogViewSync();
            }
        }

        /// <summary>
        /// Returns whether a module property change should realign picker selections.
        /// </summary>
        private static bool ShouldSyncPickerSelection(string? propertyName)
        {
            return string.IsNullOrWhiteSpace(propertyName) ||
                   propertyName == nameof(ModuleViewModel.SelectedSourceMode) ||
                   propertyName == nameof(ModuleViewModel.SelectedReleaseOption) ||
                   propertyName == nameof(ModuleViewModel.SelectedBranch) ||
                   propertyName == nameof(ModuleViewModel.ReleaseOptions) ||
                   propertyName == nameof(ModuleViewModel.BranchOptions);
        }


        // Check flow

        /// <summary>
        /// Starts a manual update check from the dialog footer.
        /// </summary>
        private async void OnCheckUpdatesClicked(object? sender, EventArgs e)
        {
            await CheckForUpdatesAsync(forceOptionLoad: false, announceInLog: true);
        }

        /// <summary>
        /// Applies a source mode picker change even when MAUI skips the two-way binding update.
        /// </summary>
        private void OnSourceModeSelectionChanged(object? sender, EventArgs e)
        {
            if (_isSynchronizingPickers)
            {
                return;
            }

            if (SourceModePicker.SelectedItem is string mode)
            {
                SelectedSourceMode = mode;
            }
        }

        /// <summary>
        /// Applies a release picker change even when the selected object was refreshed in-place.
        /// </summary>
        private void OnReleaseSelectionChanged(object? sender, EventArgs e)
        {
            if (_isSynchronizingPickers)
            {
                return;
            }

            if (ReleasePicker.SelectedItem is UpdateCandidate release)
            {
                SelectedReleaseOption = release;
            }
        }

        /// <summary>
        /// Applies a branch picker change without waiting for delayed binding propagation.
        /// </summary>
        private void OnBranchSelectionChanged(object? sender, EventArgs e)
        {
            if (_isSynchronizingPickers)
            {
                return;
            }

            if (BranchPicker.SelectedItem is string branch)
            {
                _module?.ApplyBranchSelection(branch);
            }
        }

        /// <summary>
        /// Checks the selected module for updates and refreshes the dialog state.
        /// </summary>
        private async Task CheckForUpdatesAsync(bool forceOptionLoad, bool announceInLog)
        {
            if (_module == null)
            {
                return;
            }

            if (!ApplyPickerSelectionsToModule())
            {
                return;
            }

            if (announceInLog)
            {
                _module.AppendUpdateLog($"Checking updates for {_module.Name}...");
            }

            _module.SetUpdateActivityStatus("Checking for updates...");
            RaiseActivityProperties();

            try
            {
                await _module.RefreshUpdateStateAsync(forceOptionLoad);

                if (announceInLog)
                {
                    _module.AppendUpdateLog(_module.UpdateStatus);
                }
            }
            catch (Exception ex)
            {
                _module.SetUpdateActivityStatus($"Check failed: {ex.Message}");
                if (announceInLog)
                {
                    _module.AppendUpdateLog(_module.UpdateActivityStatus);
                }
            }
            finally
            {
                RaiseModuleProperties();
                RaiseActivityProperties();
            }
        }

        /// <summary>
        /// Loads the currently relevant branches or releases for the selected source mode.
        /// </summary>
        private async Task EnsureModeOptionsLoadedAsync(bool forceRefresh)
        {
            if (_module == null)
            {
                return;
            }

            try
            {
                await _module.EnsureSelectionOptionsLoadedAsync(forceRefresh);
                RaiseModuleProperties();
                SyncPickerSelections();
            }
            catch (Exception ex)
            {
                _module.SetUpdateActivityStatus($"Failed to load update options: {ex.Message}");
                RaiseActivityProperties();
            }
        }


        // Install flow

        /// <summary>
        /// Starts module update installation from the dialog footer.
        /// </summary>
        private async void OnInstallUpdateClicked(object? sender, EventArgs e)
        {
            await InstallUpdateAsync();
        }

        /// <summary>
        /// Applies the selected module update and streams progress into the dialog.
        /// </summary>
        private async Task InstallUpdateAsync()
        {
            if (_module == null)
            {
                return;
            }

            if (!ApplyPickerSelectionsToModule())
            {
                return;
            }

            if (!_module.CanInstallSelectedUpdate || _module.IsUpdating)
            {
                return;
            }

            _module.ResetUpdateSession(clearLog: true);
            _module.SetUpdateActivityStatus($"Installing update for {_module.Name}...");
            RaiseActivityProperties();
            _module.AppendUpdateLog($"Starting update for {_module.Name}.");

            var success = await _module.ApplyUpdateAsync();

            _module.SetUpdateActivityStatus(success
                ? $"{_module.Name} updated successfully."
                : $"{_module.Name} update failed.");

            _module.AppendUpdateLog(success
                ? $"Update finished. Installed {_module.VersionString}."
                : "Update did not complete successfully.");

            RaiseModuleProperties();
            RaiseActivityProperties();
        }

        /// <summary>
        /// Pushes the visible picker values into the module view model before check or install actions run.
        /// </summary>
        private bool ApplyPickerSelectionsToModule()
        {
            if (_module == null)
            {
                return false;
            }

            var selectedMode = SourceModePicker.SelectedItem as string ?? SelectedSourceMode;
            if (!string.IsNullOrWhiteSpace(selectedMode))
            {
                _module.SelectedSourceMode = selectedMode;
            }

            if (string.Equals(_module.SelectedSourceMode, "branch", StringComparison.OrdinalIgnoreCase))
            {
                var selectedBranch = ResolveSelectedBranchFromPicker();
                if (string.IsNullOrWhiteSpace(selectedBranch))
                {
                    _module.SetUpdateActivityStatus("Select a repository branch before checking updates.");
                    RaiseActivityProperties();
                    return false;
                }

                _module.ApplyBranchSelection(selectedBranch.Trim());
            }

            if (_module.IsReleaseMode)
            {
                var release = ReleasePicker.SelectedItem as UpdateCandidate ?? SelectedReleaseOption;
                if (release != null)
                {
                    _module.SelectedReleaseOption = release;
                }
            }

            RaiseModuleProperties();
            return true;
        }

        /// <summary>
        /// Resolves the visible branch picker choice without silently changing it to a default branch.
        /// </summary>
        private string? ResolveSelectedBranchFromPicker()
        {
            if (BranchPicker.SelectedItem is string selectedBranch &&
                !string.IsNullOrWhiteSpace(selectedBranch))
            {
                return selectedBranch;
            }

            if (BranchPicker.SelectedIndex >= 0 &&
                BranchPicker.SelectedIndex < BranchOptions.Count)
            {
                return BranchOptions[BranchPicker.SelectedIndex];
            }

            return BranchOptions.FirstOrDefault(branch =>
                string.Equals(branch, _module?.SelectedBranch, StringComparison.OrdinalIgnoreCase));
        }

        // Logging

        /// <summary>
        /// Reapplies the current cached log text and scrolls the console to the newest line.
        /// </summary>
        private void SyncLogView()
        {
            _logSyncCts?.Cancel();
            _logSyncCts = null;
            Interlocked.Exchange(ref _logSyncQueued, 0);
            Interlocked.Exchange(ref _logSyncRequested, 0);
            var text = LogText;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                LogEditor.Text = text;
                await Task.Yield();
                _ = ScrollLogToEndAsync();
            });
        }

        /// <summary>
        /// Coalesces rapid log updates so verbose installers do not saturate the UI thread.
        /// </summary>
        private void QueueLogViewSync()
        {
            Interlocked.Exchange(ref _logSyncRequested, 1);
            if (Interlocked.Exchange(ref _logSyncQueued, 1) == 1)
            {
                return;
            }

            _logSyncCts ??= new CancellationTokenSource();
            _ = FlushLogViewAsync(_logSyncCts.Token);
        }

        /// <summary>
        /// Flushes cached update log text regularly while update output is still arriving.
        /// </summary>
        private async Task FlushLogViewAsync(CancellationToken ct)
        {
            try
            {
                do
                {
                    Interlocked.Exchange(ref _logSyncRequested, 0);
                    await Task.Delay(75, ct);

                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (ct.IsCancellationRequested)
                        {
                            return;
                        }

                        var text = LogText;
                        LogEditor.Text = text;
                        _ = ScrollLogToEndAsync();
                    });
                }
                while (!ct.IsCancellationRequested &&
                       Interlocked.CompareExchange(ref _logSyncRequested, 0, 0) == 1);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Interlocked.Exchange(ref _logSyncQueued, 0);
                if (!ct.IsCancellationRequested &&
                    Interlocked.CompareExchange(ref _logSyncRequested, 0, 0) == 1 &&
                    Interlocked.Exchange(ref _logSyncQueued, 1) == 0)
                {
                    _ = FlushLogViewAsync(ct);
                }
            }
        }

        /// <summary>
        /// Scrolls the update log without blocking subsequent text flushes.
        /// </summary>
        private async Task ScrollLogToEndAsync()
        {
            try
            {
                await LogScroll.ScrollToAsync(0, LogScroll.ContentSize.Height, false);
            }
            catch
            {
                // Best-effort scroll only; log text must continue updating if a scroll pass fails.
            }
        }

        /// <summary>
        /// Keeps picker controls aligned with the module view model after option lists are rebuilt.
        /// </summary>
        private void SyncPickerSelections()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _isSynchronizingPickers = true;

                try
                {
                    SourceModePicker.SelectedItem = SourceModeOptions.FirstOrDefault(mode =>
                        string.Equals(mode, SelectedSourceMode, StringComparison.OrdinalIgnoreCase));

                    ReleasePicker.SelectedItem = SelectedReleaseOption;

                    var selectedBranch = BranchOptions.FirstOrDefault(branch =>
                        string.Equals(branch, SelectedBranch, StringComparison.OrdinalIgnoreCase));

                    BranchPicker.SelectedItem = selectedBranch;
                    if (selectedBranch == null)
                    {
                        BranchPicker.SelectedIndex = -1;
                    }
                }
                finally
                {
                    _isSynchronizingPickers = false;
                }
            });
        }

        /// <summary>
        /// Removes the native WinUI picker chrome while keeping the surrounding MAUI border intact.
        /// </summary>
        private static void ApplyBorderlessPickerStyle(Picker picker)
        {
            void ApplyPlatformStyle()
            {
#if WINDOWS
                if (picker.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ComboBox comboBox)
                {
                    var transparentBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                    comboBox.Background = transparentBrush;
                    comboBox.BorderBrush = transparentBrush;
                    comboBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                    comboBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
                    comboBox.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0);
                    comboBox.UseSystemFocusVisuals = false;
                }
#endif
            }

            picker.HandlerChanged += (_, _) => ApplyPlatformStyle();
            ApplyPlatformStyle();
        }

        // Property refresh

        /// <summary>
        /// Raises every dialog-level property affected by a new module or mode.
        /// </summary>
        private void RaiseDialogProperties()
        {
            OnPropertyChanged(nameof(DialogTitle));
            OnPropertyChanged(nameof(DialogSubtitle));
            RaiseModuleProperties();
            RaiseActivityProperties();
        }

        /// <summary>
        /// Raises properties mirrored from the selected module card.
        /// </summary>
        private void RaiseModuleProperties()
        {
            OnPropertyChanged(nameof(CurrentVersionLabel));
            OnPropertyChanged(nameof(TargetVersionLabel));
            OnPropertyChanged(nameof(SourceModeOptions));
            OnPropertyChanged(nameof(ReleaseOptions));
            OnPropertyChanged(nameof(BranchOptions));
            OnPropertyChanged(nameof(SelectedSourceMode));
            OnPropertyChanged(nameof(SelectedReleaseOption));
            OnPropertyChanged(nameof(SelectedBranch));
            OnPropertyChanged(nameof(IsBranchMode));
            OnPropertyChanged(nameof(IsReleaseMode));
            OnPropertyChanged(nameof(HasUpdate));
            OnPropertyChanged(nameof(CanInstallUpdate));
            OnPropertyChanged(nameof(CanCheckUpdates));
            OnPropertyChanged(nameof(ShowInstallAction));
            OnPropertyChanged(nameof(IsBusy));
            RaiseActivityProperties();
        }

        /// <summary>
        /// Raises properties shown inside the activity section.
        /// </summary>
        private void RaiseActivityProperties()
        {
            OnPropertyChanged(nameof(ActivityTitle));
            OnPropertyChanged(nameof(ActivityStatus));
            OnPropertyChanged(nameof(HasActivityProgress));
            OnPropertyChanged(nameof(OverallProgress));
            OnPropertyChanged(nameof(FileProgress));
            OnPropertyChanged(nameof(DownloadDetail));
            OnPropertyChanged(nameof(HasDownloadDetail));
            OnPropertyChanged(nameof(LogText));
            OnPropertyChanged(nameof(HasLog));
            OnPropertyChanged(nameof(CanInstallUpdate));
        }


        // Property change

        /// <summary>
        /// Raises the overlay property changed event.
        /// </summary>
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
