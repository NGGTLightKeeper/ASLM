// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ASLM.Services;

namespace ASLM.Pages
{
    // ASLM API page

    /// <summary>
    /// Displays and controls the local ASLM API mirror server.
    /// </summary>
    public partial class AslmApiView : ContentView, INotifyPropertyChanged
    {
        private readonly AslmApiServerService _apiServer;
        private readonly Dictionary<string, AslmApiHostViewModel> _hostRows = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _refreshLoopCts;
        private bool _suppressToggleEvent;
        private string _subtitle = string.Empty;
        private string _toggleLabel = string.Empty;
        private string _statusText = string.Empty;
        private string _statusBadgeText = string.Empty;
        private string _serverUrl = string.Empty;
        private string _hostsCaption = string.Empty;
        private Color _statusBadgeBackground = Color.FromArgb("#3A3A3C");
        private bool _canOpenServer;

        /// <summary>
        /// Stores the host rows displayed by the page.
        /// </summary>
        public ObservableCollection<AslmApiHostViewModel> Hosts { get; } = new();

        // Initialization

        /// <summary>
        /// Creates the ASLM API page and hooks service state notifications.
        /// </summary>
        public AslmApiView(AslmApiServerService apiServer)
        {
            _apiServer = apiServer;

            InitializeComponent();
            BindingContext = this;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            _apiServer.StateChanged += OnServerStateChanged;
            ApplyCurrentStateToToggle();
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
        /// Gets the page subtitle shown below the header.
        /// </summary>
        public string Subtitle
        {
            get => _subtitle;
            private set => SetProperty(ref _subtitle, value);
        }

        /// <summary>
        /// Gets the text shown beside the enable switch.
        /// </summary>
        public string ToggleLabel
        {
            get => _toggleLabel;
            private set => SetProperty(ref _toggleLabel, value);
        }

        /// <summary>
        /// Gets the detailed server status line.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Gets the compact server status badge text.
        /// </summary>
        public string StatusBadgeText
        {
            get => _statusBadgeText;
            private set => SetProperty(ref _statusBadgeText, value);
        }

        /// <summary>
        /// Gets the compact server status badge background.
        /// </summary>
        public Color StatusBadgeBackground
        {
            get => _statusBadgeBackground;
            private set => SetProperty(ref _statusBadgeBackground, value);
        }

        /// <summary>
        /// Gets the mirror server root URL.
        /// </summary>
        public string ServerUrl
        {
            get => _serverUrl;
            private set => SetProperty(ref _serverUrl, value);
        }

        /// <summary>
        /// Gets the host list caption.
        /// </summary>
        public string HostsCaption
        {
            get => _hostsCaption;
            private set => SetProperty(ref _hostsCaption, value);
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
        private async void OnLoaded(object? sender, EventArgs e)
        {
            await RefreshAsync();
            StartRefreshLoop();
        }

        /// <summary>
        /// Stops periodic host refresh when the page leaves the visual tree.
        /// </summary>
        private void OnUnloaded(object? sender, EventArgs e)
        {
            StopRefreshLoop();
        }


        // Actions

        /// <summary>
        /// Enables or disables the ASLM API mirror server.
        /// </summary>
        private async void OnServerToggled(object? sender, ToggledEventArgs e)
        {
            if (_suppressToggleEvent)
            {
                return;
            }

            await Task.Run(() => _apiServer.SetEnabledAsync(e.Value));
            await RefreshAsync();
        }

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
            ApplyCurrentStateToToggle();
            ApplyServerStatus();

            var hosts = await Task.Run(() => _apiServer.GetHostsWithAvailabilityAsync());
            SynchronizeHostRows(hosts);

            HostsCaption = hosts.Count == 0
                ? "No hosts are declared in the runtime port map."
                : $"{hosts.Count} host{(hosts.Count == 1 ? string.Empty : "s")} declared in the runtime port map.";
        }

        /// <summary>
        /// Updates host rows in place so periodic refreshes do not recreate the list visual tree.
        /// </summary>
        private void SynchronizeHostRows(IReadOnlyList<AslmApiHostInfo> hosts)
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

                if (_hostRows.TryGetValue(rowKey, out var existingRow))
                {
                    existingRow.Update(host);
                    MoveHostRowIfNeeded(existingRow, desiredIndex);
                    continue;
                }

                var newRow = new AslmApiHostViewModel(host);
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
        /// Refreshes host availability at a modest interval while the view is visible.
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
        /// Updates the switch without re-entering the toggle handler.
        /// </summary>
        private void ApplyCurrentStateToToggle()
        {
            _suppressToggleEvent = true;
            ServerSwitch.IsToggled = _apiServer.IsEnabled;
            _suppressToggleEvent = false;
        }

        /// <summary>
        /// Updates bound server status fields from the service state.
        /// </summary>
        private void ApplyServerStatus()
        {
            var isEnabled = _apiServer.IsEnabled;
            var isRunning = _apiServer.IsRunning;
            var lastError = _apiServer.LastError;

            Subtitle = "Mirror and open module hosts routed from Data/App/ASLM_Ports.json.";
            ToggleLabel = isEnabled ? "Enabled" : "Disabled";
            ServerUrl = _apiServer.BaseUrl;
            CanOpenServer = isRunning;

            if (!isEnabled)
            {
                StatusBadgeText = "Off";
                StatusBadgeBackground = Color.FromArgb("#3A3A3C");
                StatusText = "The local mirror server is disabled.";
                return;
            }

            if (isRunning)
            {
                StatusBadgeText = "Running";
                StatusBadgeBackground = Color.FromArgb("#2032D74B");
                StatusText = "The local mirror server is listening on localhost.";
                return;
            }

            StatusBadgeText = "Error";
            StatusBadgeBackground = Color.FromArgb("#4C1F24");
            StatusText = string.IsNullOrWhiteSpace(lastError)
                ? "The local mirror server is enabled but not running."
                : lastError;
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

    // ASLM API host row

    /// <summary>
    /// Exposes one mirror host row to the ASLM API page.
    /// </summary>
    public class AslmApiHostViewModel : INotifyPropertyChanged
    {
        private string _moduleId = string.Empty;
        private string _hostKey = string.Empty;
        private int _port;
        private string _mirrorUrl = string.Empty;
        private string _targetUrl = string.Empty;
        private bool _isOnline;
        private Command? _openCommand;

        /// <summary>
        /// Creates a host row from the current service host info.
        /// </summary>
        public AslmApiHostViewModel(AslmApiHostInfo host)
        {
            Update(host);
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
        /// Gets the port key declared under the module in the port map.
        /// </summary>
        public string HostKey
        {
            get => _hostKey;
            private set => SetProperty(ref _hostKey, value);
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
        /// Gets the direct localhost target URL behind this host.
        /// </summary>
        public string TargetUrl
        {
            get => _targetUrl;
            private set => SetProperty(ref _targetUrl, value);
        }

        /// <summary>
        /// Gets whether the target port accepted a connection during the latest refresh.
        /// </summary>
        public bool IsOnline
        {
            get => _isOnline;
            private set
            {
                if (!SetProperty(ref _isOnline, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBackground));
            }
        }

        /// <summary>
        /// Gets the command that opens this host in the system browser.
        /// </summary>
        public ICommand OpenCommand => _openCommand ??= new Command(async () => await Launcher.Default.OpenAsync(MirrorUrl));

        /// <summary>
        /// Gets the stable key used to diff host rows across refreshes.
        /// </summary>
        public string Key => BuildKey(ModuleId, HostKey);

        /// <summary>
        /// Gets the compact title shown in the host row.
        /// </summary>
        public string Title => $"{ModuleId} / {HostKey}";

        /// <summary>
        /// Gets the direct target text shown below the mirror URL.
        /// </summary>
        public string TargetText => $"Target: {TargetUrl}";

        /// <summary>
        /// Gets the current availability badge text.
        /// </summary>
        public string StatusText => IsOnline ? "Online" : "Offline";

        /// <summary>
        /// Gets the current availability badge background.
        /// </summary>
        public Color StatusBackground => IsOnline
            ? Color.FromArgb("#2032D74B")
            : Color.FromArgb("#3A3A3C");

        /// <summary>
        /// Updates the row from fresh service data without recreating the row object.
        /// </summary>
        public void Update(AslmApiHostInfo host)
        {
            ModuleId = host.ModuleId;
            HostKey = host.HostKey;
            Port = host.Port;
            MirrorUrl = host.MirrorUrl;
            TargetUrl = host.TargetUrl;
            IsOnline = host.IsOnline == true;

            OnPropertyChanged(nameof(Key));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(TargetText));
        }

        /// <summary>
        /// Builds the stable key used by the ASLM API host list.
        /// </summary>
        public static string BuildKey(string moduleId, string hostKey)
        {
            return $"{moduleId}\n{hostKey}";
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
