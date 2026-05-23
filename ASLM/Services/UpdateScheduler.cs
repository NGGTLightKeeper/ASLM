// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;
using Microsoft.Extensions.Logging;

namespace ASLM.Services
{
    /// <summary>
    /// Runs periodic background update checks according to user preferences.
    /// </summary>
    public sealed class UpdateScheduler : IDisposable
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan FailureRetryDelay = TimeSpan.FromMinutes(15);

        private readonly AppDataStore _appData;
        private readonly UpdateManager _updateManager;
        private readonly ILogger<UpdateScheduler> _logger;

        private CancellationTokenSource? _cts;
        private Task? _worker;


        // Initialization

        /// <summary>
        /// Creates the background update scheduler.
        /// </summary>
        public UpdateScheduler(
            AppDataStore appData,
            UpdateManager updateManager,
            ILogger<UpdateScheduler> logger)
        {
            _appData = appData;
            _updateManager = updateManager;
            _logger = logger;
        }


        // Startup

        /// <summary>
        /// Starts the background loop once.
        /// </summary>
        public void Start()
        {
            if (_worker != null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _worker = Task.Run(() => RunAsync(_cts.Token));
        }


        // Shutdown

        /// <summary>
        /// Stops the background loop.
        /// </summary>
        public async Task StopAsync()
        {
            if (_cts == null || _worker == null)
            {
                return;
            }

            _cts.Cancel();
            try
            {
                await _worker;
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }

            _cts.Dispose();
            _cts = null;
            _worker = null;
        }


        // Worker loop

        /// <summary>
        /// Runs the scheduler loop until the application shuts down.
        /// </summary>
        private async Task RunAsync(CancellationToken ct)
        {
            // Give the app a short moment to finish startup I/O before beginning network work.
            await Task.Delay(StartupDelay, ct);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await RunDueCheckAsync(ct);

                    // Re-read the persisted period after every pass so changes in Settings apply without restart.
                    var delay = GetSchedulerDelay(_appData.Data.Updates);
                    await Task.Delay(delay, ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Scheduled update check failed.");
                    await Task.Delay(FailureRetryDelay, ct);
                }
            }
        }

        /// <summary>
        /// Executes one due update check and applies automatic updates when enabled.
        /// </summary>
        private async Task RunDueCheckAsync(CancellationToken ct)
        {
            _appData.Data.Updates.Normalize();
            var settings = _appData.Data.Updates;
            if (!settings.CheckEnabled || !IsDue(settings))
            {
                return;
            }

            settings.LastAutoCheckUtc = DateTime.UtcNow.ToString("o");
            await _appData.SaveAsync();

            var publishNotifications = !settings.AutoUpdateEnabled;
            var updates = await _updateManager.CheckAllUpdatesAsync(ct, publishNotifications);
            if (!settings.AutoUpdateEnabled || updates.Count == 0)
            {
                return;
            }

            var log = new Progress<string>(message =>
                _logger.LogInformation("[Updater] {Message}", message));

            await _updateManager.ApplyDiscoveredUpdatesAsync(updates, log, ct);
        }


        // Scheduling helpers

        /// <summary>
        /// Returns whether the configured automatic check window has elapsed.
        /// </summary>
        private static bool IsDue(AppUpdateSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.LastAutoCheckUtc))
            {
                return true;
            }

            if (!DateTimeOffset.TryParse(settings.LastAutoCheckUtc, out var lastCheck))
            {
                return true;
            }

            return DateTimeOffset.UtcNow - lastCheck >= GetSchedulerDelay(settings);
        }

        /// <summary>
        /// Converts the persisted interval into a bounded scheduler delay.
        /// </summary>
        private static TimeSpan GetSchedulerDelay(AppUpdateSettings settings)
        {
            settings.Normalize();
            return TimeSpan.FromHours(settings.AutoCheckPeriodHours);
        }


        // Disposal

        /// <summary>
        /// Cancels the background worker when the scheduler is disposed by the host.
        /// </summary>
        public void Dispose()
        {
            if (_cts == null)
            {
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
    }
}
