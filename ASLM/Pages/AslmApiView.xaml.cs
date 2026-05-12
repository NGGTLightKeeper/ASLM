// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using ASLM.Models;
using ASLM.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ASLM.Pages
{
    // ASLM API page

    /// <summary>
    /// Displays and controls the local ASLM API mirror server.
    /// </summary>
    public partial class AslmApiView : ContentView, INotifyPropertyChanged
    {
        private readonly AslmApiServer _apiServer;
        private readonly NotificationCenter _notifications;
        private readonly ModuleInstaller _moduleInstaller;
        private readonly Dictionary<string, AslmApiHostViewModel> _hostRows = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, AslmApiModuleDisplayState> _moduleDisplayStates = new(StringComparer.OrdinalIgnoreCase);
        private Task? _moduleDisplayStatesLoadTask;
        private int _moduleDisplayStateLoadVersion;
        private bool _moduleDisplayStatesLoaded;
        private DateTime _moduleDisplayStatesLoadedAt = DateTime.MinValue;
        private CancellationTokenSource? _refreshLoopCts;
        private bool _isVisible;
        private string _serverUrl = string.Empty;
        private bool _canOpenServer;

        private static readonly TimeSpan ModuleStateRefreshInterval = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Stores the host rows displayed by the page.
        /// </summary>
        public ObservableCollection<AslmApiHostViewModel> Hosts { get; } = new();

        // Initialization

        /// <summary>
        /// Creates the ASLM API page and hooks service state notifications.
        /// </summary>
        public AslmApiView(
            AslmApiServer apiServer,
            NotificationCenter notifications,
            ModuleInstaller moduleInstaller)
        {
            _apiServer = apiServer;
            _notifications = notifications;
            _moduleInstaller = moduleInstaller;

            InitializeComponent();
            BindingContext = this;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _apiServer.StateChanged += OnServerStateChanged;
            _moduleInstaller.ModulesChanged += OnModulesChanged;
        }


        // Notifications

        /// <inheritdoc />
        public new event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises property change notifications for binding updates.
        /// </summary>
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        // Bound state

        /// <summary>
        /// Gets the mirror server root URL.
        /// </summary>
        public string ServerUrl
        {
            get => _serverUrl;
            private set => SetProperty(ref _serverUrl, value);
        }

        /// <summary>
        /// Gets whether the server root can be opened in a browser.
        /// </summary>
        public bool CanOpenServer
        {
            get => _canOpenServer;
            private set => SetProperty(ref _canOpenServer, value);
        }


        // Lifecycle

        /// <summary>
        /// Starts the lightweight host refresh loop when the page becomes visible.
        /// </summary>
        private void OnLoaded(object? sender, EventArgs e)
        {
            _isVisible = true;
            _ = RefreshAsync();
            StartRefreshLoop();
        }

        /// <summary>
        /// Stops periodic host refresh when the page leaves the visual tree.
        /// </summary>
        private void OnUnloaded(object? sender, EventArgs e)
        {
            _isVisible = false;
            StopRefreshLoop();
        }


        // Actions

        /// <summary>
        /// Opens the ASLM API mirror root in the system browser.
        /// </summary>
        private async void OnOpenServerClicked(object? sender, EventArgs e)
        {
            if (!CanOpenServer)
            {
                return;
            }

            await Launcher.Default.OpenAsync(_apiServer.BaseUrl);
        }

        // Refresh

        /// <summary>
        /// Reloads server state and currently declared host entries.
        /// </summary>
        internal async Task RefreshAsync()
        {
            ApplyServerState();
            EnsureModuleDisplayStatesLoading();

            var hosts = await Task.Run(_apiServer.GetHosts);
            SynchronizeHostRows(hosts, _moduleDisplayStates);
        }

        /// <summary>
        /// Updates host rows in place so periodic refreshes do not recreate the list visual tree.
        /// </summary>
        private void SynchronizeHostRows(
            IReadOnlyList<AslmApiHostInfo> hosts,
            IReadOnlyDictionary<string, AslmApiModuleDisplayState> moduleStates)
        {
            var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var orderedHosts = hosts
                .OrderBy(static host => host.Port)
                .ThenBy(static host => host.ModuleId)
                .ThenBy(static host => host.HostKey)
                .ToList();

            for (var desiredIndex = 0; desiredIndex < orderedHosts.Count; desiredIndex++)
            {
                var host = orderedHosts[desiredIndex];
                var rowKey = AslmApiHostViewModel.BuildKey(host.ModuleId, host.HostKey);
                activeKeys.Add(rowKey);
                var moduleState = ResolveModuleDisplayState(host.ModuleId, moduleStates);

                if (_hostRows.TryGetValue(rowKey, out var existingRow))
                {
                    existingRow.Update(host, moduleState);
                    MoveHostRowIfNeeded(existingRow, desiredIndex);
                    continue;
                }

                var newRow = new AslmApiHostViewModel(host, moduleState, _notifications);
                _hostRows[rowKey] = newRow;
                Hosts.Insert(Math.Min(desiredIndex, Hosts.Count), newRow);
            }

            for (var index = Hosts.Count - 1; index >= 0; index--)
            {
                var row = Hosts[index];
                if (activeKeys.Contains(row.Key))
                {
                    continue;
                }

                Hosts.RemoveAt(index);
                _hostRows.Remove(row.Key);
            }
        }

        /// <summary>
        /// Loads the current module display state keyed by stable module id.
        /// </summary>
        private static Dictionary<string, AslmApiModuleDisplayState> BuildModuleDisplayStates(IEnumerable<ModuleConfig> modules)
        {
            return modules
                .Where(static module => !string.IsNullOrWhiteSpace(module.Id))
                .GroupBy(static module => module.Id, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToDictionary(
                    static module => module.Id,
                    static module => new AslmApiModuleDisplayState(
                        string.IsNullOrWhiteSpace(module.Name) ? module.Id : module.Name.Trim(),
                        module.Status.Enabled),
                    StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the human-readable module state used by the host list.
        /// </summary>
        private static AslmApiModuleDisplayState ResolveModuleDisplayState(
            string moduleId,
            IReadOnlyDictionary<string, AslmApiModuleDisplayState> moduleStates)
        {
            if (moduleStates.TryGetValue(moduleId, out var moduleState) &&
                !string.IsNullOrWhiteSpace(moduleState.Name))
            {
                return moduleState;
            }

            return new AslmApiModuleDisplayState(AslmApiHostViewModel.NormalizeDisplayName(moduleId), true);
        }

        /// <summary>
        /// Keeps the displayed host list sorted by port without recreating existing row views.
        /// </summary>
        private void MoveHostRowIfNeeded(AslmApiHostViewModel row, int desiredIndex)
        {
            var currentIndex = Hosts.IndexOf(row);
            if (currentIndex >= 0 && currentIndex != desiredIndex)
            {
                Hosts.Move(currentIndex, Math.Min(desiredIndex, Hosts.Count - 1));
            }
        }

        /// <summary>
        /// Starts a background module-state refresh when the cached display state is stale.
        /// </summary>
        private void EnsureModuleDisplayStatesLoading()
        {
            var isLoadActive = _moduleDisplayStatesLoadTask is { IsCompleted: false };
            var isCacheFresh =
                _moduleDisplayStatesLoaded &&
                DateTime.UtcNow - _moduleDisplayStatesLoadedAt < ModuleStateRefreshInterval;

            if (isLoadActive || isCacheFresh)
            {
                return;
            }

            var loadVersion = ++_moduleDisplayStateLoadVersion;
            _moduleDisplayStatesLoadTask = LoadModuleDisplayStatesAsync(loadVersion);
        }

        /// <summary>
        /// Loads module display state without blocking initial page rendering.
        /// </summary>
        private async Task LoadModuleDisplayStatesAsync(int loadVersion)
        {
            Dictionary<string, AslmApiModuleDisplayState> moduleStates;

            try
            {
                var modules = await Task.Run(() => _moduleInstaller.DiscoverModulesAsync());
                moduleStates = BuildModuleDisplayStates(modules);
            }
            catch
            {
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (loadVersion != _moduleDisplayStateLoadVersion)
                {
                    return;
                }

                _moduleDisplayStates = moduleStates;
                _moduleDisplayStatesLoaded = true;
                _moduleDisplayStatesLoadedAt = DateTime.UtcNow;

                foreach (var row in Hosts)
                {
                    row.UpdateModuleState(ResolveModuleDisplayState(row.ModuleId, _moduleDisplayStates));
                }
            });
        }

        /// <summary>
        /// Starts periodic refresh so the page follows runtime changes to ASLM_Ports.json.
        /// </summary>
        private void StartRefreshLoop()
        {
            StopRefreshLoop();
            _refreshLoopCts = new CancellationTokenSource();
            _ = RunRefreshLoopAsync(_refreshLoopCts.Token);
        }

        /// <summary>
        /// Stops the periodic refresh loop if it is active.
        /// </summary>
        private void StopRefreshLoop()
        {
            _refreshLoopCts?.Cancel();
            _refreshLoopCts?.Dispose();
            _refreshLoopCts = null;
        }

        /// <summary>
        /// Refreshes host rows at a modest interval while the view is visible.
        /// </summary>
        private async Task RunRefreshLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(4), ct);
                    await MainThread.InvokeOnMainThreadAsync(RefreshAsync);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Reacts to server state changes raised outside the page.
        /// </summary>
        private void OnServerStateChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () => await RefreshAsync());
        }

        /// <summary>
        /// Invalidates cached module status after module manifests are saved.
        /// </summary>
        private void OnModulesChanged(object? sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                _moduleDisplayStatesLoaded = false;
                _moduleDisplayStatesLoadedAt = DateTime.MinValue;
                _moduleDisplayStatesLoadTask = null;
                _moduleDisplayStateLoadVersion++;

                if (_isVisible)
                {
                    await RefreshAsync();
                }
            });
        }

        /// <summary>
        /// Updates bound server fields from the service state.
        /// </summary>
        private void ApplyServerState()
        {
            var isRunning = _apiServer.IsRunning;

            ServerUrl = _apiServer.BaseUrl;
            CanOpenServer = isRunning;
        }


        // Helpers

        /// <summary>
        /// Assigns a property value and raises a binding notification when it changes.
        /// </summary>
        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

    }

    /// <summary>
    /// Stores the lightweight module metadata needed by ASLM API host rows.
    /// </summary>
    public sealed record AslmApiModuleDisplayState(string Name, bool IsEnabled);

    // ASLM API host row

    /// <summary>
    /// Exposes one mirror host row to the ASLM API page.
    /// </summary>
    public class AslmApiHostViewModel : INotifyPropertyChanged
    {
        private string _moduleId = string.Empty;
        private string _moduleName = string.Empty;
        private string _hostKey = string.Empty;
        private string _hostName = string.Empty;
        private int _port;
        private string _mirrorUrl = string.Empty;
        private bool _isModuleDisabled;
        private Command? _copyCommand;
        private readonly NotificationCenter _notifications;

        /// <summary>
        /// Creates a host row from the current service host info.
        /// </summary>
        public AslmApiHostViewModel(
            AslmApiHostInfo host,
            AslmApiModuleDisplayState moduleState,
            NotificationCenter notifications)
        {
            _notifications = notifications;
            Update(host, moduleState);
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the module identifier for this host.
        /// </summary>
        public string ModuleId
        {
            get => _moduleId;
            private set => SetProperty(ref _moduleId, value);
        }

        /// <summary>
        /// Gets the human-readable module name for display.
        /// </summary>
        public string ModuleName
        {
            get => _moduleName;
            private set => SetProperty(ref _moduleName, value);
        }

        /// <summary>
        /// Gets the port key declared under the module in the port map.
        /// </summary>
        public string HostKey
        {
            get => _hostKey;
            private set => SetProperty(ref _hostKey, value);
        }

        /// <summary>
        /// Gets the normalized host name for display.
        /// </summary>
        public string HostName
        {
            get => _hostName;
            private set => SetProperty(ref _hostName, value);
        }

        /// <summary>
        /// Gets the local target port used by the module service.
        /// </summary>
        public int Port
        {
            get => _port;
            private set => SetProperty(ref _port, value);
        }

        /// <summary>
        /// Gets the ASLM API mirror URL for this host.
        /// </summary>
        public string MirrorUrl
        {
            get => _mirrorUrl;
            private set => SetProperty(ref _mirrorUrl, value);
        }

        /// <summary>
        /// Gets whether the module that owns this host is disabled.
        /// </summary>
        public bool IsModuleDisabled
        {
            get => _isModuleDisabled;
            private set => SetProperty(ref _isModuleDisabled, value);
        }

        /// <summary>
        /// Gets the stable key used to diff host rows across refreshes.
        /// </summary>
        public string Key => BuildKey(ModuleId, HostKey);

        /// <summary>
        /// Gets the compact title shown in the host row.
        /// </summary>
        public string Title => $"{ModuleName} / {HostName}";

        /// <summary>
        /// Gets the compact status badge text for disabled modules.
        /// </summary>
        public string ModuleStatusText => IsModuleDisabled ? "Disabled" : string.Empty;

        /// <summary>
        /// Gets the command that copies this host URL to the system clipboard.
        /// </summary>
        public Command CopyCommand => _copyCommand ??= new Command(async () => await CopyMirrorUrlAsync());

        /// <summary>
        /// Copies the host URL and publishes a shared toast confirmation.
        /// </summary>
        private async Task CopyMirrorUrlAsync()
        {
            await Clipboard.Default.SetTextAsync(MirrorUrl);
            _notifications.PublishSystemToast(
                "Address copied",
                MirrorUrl,
                "Copied",
                $"aslm-api:{Key.GetHashCode(StringComparison.OrdinalIgnoreCase)}");
        }

        /// <summary>
        /// Updates the row from fresh service data without recreating the row object.
        /// </summary>
        public void Update(AslmApiHostInfo host, AslmApiModuleDisplayState moduleState)
        {
            ModuleId = host.ModuleId;
            UpdateModuleState(moduleState);
            HostKey = host.HostKey;
            HostName = NormalizeHostName(host.HostKey);
            Port = host.Port;
            MirrorUrl = host.MirrorUrl;

            OnPropertyChanged(nameof(Key));
        }

        /// <summary>
        /// Updates only the displayed module state after the background cache refreshes.
        /// </summary>
        public void UpdateModuleState(AslmApiModuleDisplayState moduleState)
        {
            ModuleName = moduleState.Name;
            IsModuleDisabled = !moduleState.IsEnabled;
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(ModuleStatusText));
        }

        /// <summary>
        /// Builds the stable key used by the ASLM API host list.
        /// </summary>
        public static string BuildKey(string moduleId, string hostKey)
        {
            return $"{moduleId}\n{hostKey}";
        }

        /// <summary>
        /// Normalizes a technical identifier into a compact display name.
        /// </summary>
        public static string NormalizeDisplayName(string value)
        {
            var words = (value ?? string.Empty)
                .Replace('-', ' ')
                .Replace('_', ' ')
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (words.Length == 0)
            {
                return string.Empty;
            }

            return string.Join(' ', words.Select(FormatDisplayWord));
        }

        /// <summary>
        /// Normalizes a host port-map key into a user-facing host name.
        /// </summary>
        private static string NormalizeHostName(string hostKey)
        {
            var trimmed = TrimHostPortSuffix(hostKey?.Trim() ?? string.Empty);
            return NormalizeDisplayName(trimmed);
        }

        /// <summary>
        /// Removes the conventional trailing port marker from host keys.
        /// </summary>
        private static string TrimHostPortSuffix(string value)
        {
            foreach (var suffix in new[] { "-port", "_port", " port" })
            {
                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                    value.Length > suffix.Length)
                {
                    return value[..^suffix.Length];
                }
            }

            return value;
        }

        /// <summary>
        /// Formats one identifier word while preserving common technical acronyms.
        /// </summary>
        private static string FormatDisplayWord(string word)
        {
            var lower = word.ToLowerInvariant();
            if (lower is "api" or "ui")
            {
                return lower.ToUpperInvariant();
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(lower);
        }

        /// <summary>
        /// Raises one binding notification for this row.
        /// </summary>
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Assigns a row property and notifies bindings when it changes.
        /// </summary>
        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
