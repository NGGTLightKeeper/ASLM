// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ASLM.Models;
using Microsoft.Maui.ApplicationModel;

namespace ASLM.Services
{
    /// <summary>
    /// Stores internal ASLM notifications, persists them, and exposes helpers for update and download events.
    /// </summary>
    public sealed class NotificationService
    {
        private const string NotificationsFileName = "ASLM_Notifications.json";
        private const int MaxPersistedNotifications = 200;

        private readonly ObservableCollection<AppNotification> _notifications = [];
        private readonly ReadOnlyObservableCollection<AppNotification> _readOnlyNotifications;
        private readonly string _filePath;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        private CancellationTokenSource? _saveCts;
        private bool _isInitialized;

        /// <summary>
        /// Creates the notification service and resolves its persistence file path.
        /// </summary>
        public NotificationService()
        {
            _readOnlyNotifications = new ReadOnlyObservableCollection<AppNotification>(_notifications);
            _filePath = Path.Combine(GetRootDirectory(), "Data", "App", NotificationsFileName);
        }

        /// <summary>
        /// Raised when notification counts or filter projections may have changed.
        /// </summary>
        public event EventHandler? NotificationsChanged;

        /// <summary>
        /// Raised when a new notification should be shown as an in-app toast.
        /// </summary>
        public event EventHandler<AppNotification>? NotificationPublished;

        /// <summary>
        /// Gets the live notification collection sorted by newest update first.
        /// </summary>
        public ReadOnlyObservableCollection<AppNotification> Notifications => _readOnlyNotifications;

        /// <summary>
        /// Loads persisted notifications once during application startup.
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            await LoadAsync();
            _isInitialized = true;
        }

        /// <summary>
        /// Publishes a notification for one newly discovered update candidate.
        /// </summary>
        public void PublishUpdateCandidate(UpdateCandidate candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate.TargetKind) ||
                string.IsNullOrWhiteSpace(candidate.TargetId) ||
                string.IsNullOrWhiteSpace(candidate.RemoteVersion))
            {
                return;
            }

            var isApp = string.Equals(candidate.TargetKind, "app", StringComparison.OrdinalIgnoreCase);
            var title = isApp
                ? "ASLM update available"
                : "Module update available";
            var currentVersion = string.IsNullOrWhiteSpace(candidate.CurrentVersion)
                ? "installed version"
                : candidate.CurrentVersion;
            var message = $"{candidate.Name}: {currentVersion} -> {candidate.RemoteVersion}";
            var detail = string.IsNullOrWhiteSpace(candidate.Channel)
                ? string.Empty
                : $"Channel: {candidate.Channel}";

            UpsertNotification(
                id: BuildUpdateNotificationId(candidate),
                category: AppNotificationCategory.Updates,
                severity: AppNotificationSeverity.Info,
                title: title,
                message: message,
                statusText: "New version found",
                detailText: detail,
                sourceKind: candidate.TargetKind,
                sourceId: candidate.TargetId,
                showToastForNew: true);
        }

        /// <summary>
        /// Publishes ten sample notifications with a fixed delay between arrivals.
        /// </summary>
        public async Task PublishStartupTestNotificationsAsync(CancellationToken ct = default)
        {
            var sessionId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            var samples = CreateStartupTestNotifications(sessionId);

            foreach (var sample in samples)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), ct);
                UpsertNotification(
                    id: sample.Id,
                    category: sample.Category,
                    severity: sample.Severity,
                    title: sample.Title,
                    message: sample.Message,
                    statusText: sample.StatusText,
                    detailText: string.Empty,
                    sourceKind: "test",
                    sourceId: sample.SourceId,
                    showToastForNew: true,
                    afterUpdate: sample.ProgressFraction.HasValue
                        ? notification => notification.UpdateProgress(
                            hasProgress: true,
                            progressFraction: sample.ProgressFraction.Value,
                            isInProgress: sample.IsInProgress)
                        : null);
            }
        }

        /// <summary>
        /// Starts or refreshes a download notification.
        /// </summary>
        public void StartDownload(string operationKey, string title, string message, string sourceKind, string sourceId)
        {
            UpsertNotification(
                id: BuildDownloadNotificationId(operationKey),
                category: AppNotificationCategory.Downloads,
                severity: AppNotificationSeverity.Info,
                title: title,
                message: message,
                statusText: "Starting download...",
                detailText: string.Empty,
                sourceKind: sourceKind,
                sourceId: sourceId,
                showToastForNew: true,
                afterUpdate: notification => notification.UpdateProgress(hasProgress: true, progressFraction: 0, isInProgress: true));
        }

        /// <summary>
        /// Updates a download notification with byte progress.
        /// </summary>
        public void ReportDownloadProgress(string operationKey, DownloadProgress progress, string? statusText = null)
        {
            RunOnMainThread(() =>
            {
                var notification = FindNotification(BuildDownloadNotificationId(operationKey));
                if (notification == null)
                {
                    return;
                }

                var detail = progress.TotalBytes > 0
                    ? $"{FormatBytes(progress.DownloadedBytes)} / {FormatBytes(progress.TotalBytes)}"
                    : FormatBytes(progress.DownloadedBytes);

                notification.UpdateContent(
                    AppNotificationSeverity.Info,
                    notification.Title,
                    notification.Message,
                    statusText ?? "Downloading...",
                    detail);
                notification.UpdateProgress(hasProgress: true, progress.Fraction, isInProgress: true);
                Resort(notification);
                RaiseNotificationsChanged();
                QueueSave(1000);
            });
        }

        /// <summary>
        /// Updates a download notification with a textual status line.
        /// </summary>
        public void ReportDownloadStatus(string operationKey, string statusText)
        {
            RunOnMainThread(() =>
            {
                var notification = FindNotification(BuildDownloadNotificationId(operationKey));
                if (notification == null)
                {
                    return;
                }

                notification.UpdateContent(
                    AppNotificationSeverity.Info,
                    notification.Title,
                    notification.Message,
                    statusText,
                    notification.DetailText);
                notification.UpdateProgress(notification.HasProgress, notification.ProgressFraction, isInProgress: true);
                Resort(notification);
                RaiseNotificationsChanged();
                QueueSave(500);
            });
        }

        /// <summary>
        /// Marks a download notification as completed successfully.
        /// </summary>
        public void CompleteDownload(string operationKey, string statusText)
        {
            FinishDownload(operationKey, AppNotificationSeverity.Success, statusText, 1.0);
        }

        /// <summary>
        /// Marks a download notification as failed.
        /// </summary>
        public void FailDownload(string operationKey, string statusText)
        {
            FinishDownload(operationKey, AppNotificationSeverity.Error, statusText, 0);
        }

        /// <summary>
        /// Creates a progress bridge that updates notifications and forwards progress to an existing sink.
        /// </summary>
        public IProgress<DownloadProgress> CreateDownloadProgressBridge(
            string operationKey,
            IProgress<DownloadProgress>? innerProgress = null)
        {
            return new Progress<DownloadProgress>(progress =>
            {
                ReportDownloadProgress(operationKey, progress);
                innerProgress?.Report(progress);
            });
        }

        /// <summary>
        /// Removes one notification from the visible list.
        /// </summary>
        public void Dismiss(AppNotification notification)
        {
            RunOnMainThread(() =>
            {
                _notifications.Remove(notification);
                RaiseNotificationsChanged();
                QueueSave(0);
            });
        }

        /// <summary>
        /// Builds a stable operation key for download notifications.
        /// </summary>
        public static string BuildOperationKey(string sourceKind, string sourceId)
        {
            return $"{sourceKind.Trim().ToLowerInvariant()}:{sourceId.Trim().ToLowerInvariant()}";
        }

        /// <summary>
        /// Creates the delayed startup sample notifications for the current launch.
        /// </summary>
        private static List<TestNotificationSample> CreateStartupTestNotifications(string sessionId)
        {
            return
            [
                new($"test:{sessionId}:01", AppNotificationCategory.Updates, AppNotificationSeverity.Info, "ASLM update found", "ASLM 1.1.0 is available.", "New version", "update"),
                new($"test:{sessionId}:02", AppNotificationCategory.Downloads, AppNotificationSeverity.Info, "Downloading module", "ASLM Chat package is in progress.", "Downloading", "download-1", 0.18, true),
                new($"test:{sessionId}:03", AppNotificationCategory.Downloads, AppNotificationSeverity.Info, "Model download", "Gpt2 GGUF Q8_0 is loading.", "Downloading", "download-2", 0.42, true),
                new($"test:{sessionId}:04", AppNotificationCategory.System, AppNotificationSeverity.Success, "Module ready", "Voice module installed successfully.", "Completed", "success-1"),
                new($"test:{sessionId}:05", AppNotificationCategory.System, AppNotificationSeverity.Warning, "Restart recommended", "Some changes will apply after restart.", "Attention", "warning-1"),
                new($"test:{sessionId}:06", AppNotificationCategory.System, AppNotificationSeverity.Error, "Download failed", "A test package could not be loaded.", "Failed", "error-1"),
                new($"test:{sessionId}:07", AppNotificationCategory.Updates, AppNotificationSeverity.Info, "Module update found", "ASLM Chat has a newer release.", "New version", "update-2"),
                new($"test:{sessionId}:08", AppNotificationCategory.Downloads, AppNotificationSeverity.Success, "Download complete", "QuantFactory model is ready.", "Completed", "success-2", 1.0, false),
                new($"test:{sessionId}:09", AppNotificationCategory.System, AppNotificationSeverity.Warning, "Port range notice", "Third-party module ports are nearly full.", "Attention", "warning-2"),
                new($"test:{sessionId}:10", AppNotificationCategory.System, AppNotificationSeverity.Info, "Background check complete", "Startup update scan finished.", "Ready", "info-1")
            ];
        }

        /// <summary>
        /// Loads persisted notifications from disk and restores their progress state.
        /// </summary>
        private async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return;
                }

                var json = await File.ReadAllTextAsync(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                var store = JsonSerializer.Deserialize<NotificationStore>(json, _jsonOptions) ?? new NotificationStore();
                foreach (var item in store.Notifications
                             .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                             .OrderByDescending(item => item.UpdatedAt)
                             .Take(MaxPersistedNotifications))
                {
                    _notifications.Add(new AppNotification(
                        item.Id,
                        item.Category,
                        item.Severity,
                        item.Title,
                        item.Message,
                        item.SourceKind,
                        item.SourceId,
                        item.CreatedAt,
                        item.UpdatedAt,
                        item.IsInProgress,
                        item.ProgressFraction,
                        item.HasProgress,
                        item.StatusText,
                        item.DetailText));
                }

                RaiseNotificationsChanged();
            }
            catch
            {
                _notifications.Clear();
            }
        }

        /// <summary>
        /// Finishes a download notification with the requested severity and status text.
        /// </summary>
        private void FinishDownload(
            string operationKey,
            AppNotificationSeverity severity,
            string statusText,
            double finalProgress)
        {
            RunOnMainThread(() =>
            {
                var notification = FindNotification(BuildDownloadNotificationId(operationKey));
                if (notification == null)
                {
                    return;
                }

                notification.UpdateContent(
                    severity,
                    notification.Title,
                    notification.Message,
                    statusText,
                    notification.DetailText);
                notification.UpdateProgress(notification.HasProgress, finalProgress, isInProgress: false);
                Resort(notification);
                RaiseNotificationsChanged();
                QueueSave(0);
                NotificationPublished?.Invoke(this, notification);
            });
        }

        /// <summary>
        /// Inserts or updates one notification on the UI thread.
        /// </summary>
        private void UpsertNotification(
            string id,
            AppNotificationCategory category,
            AppNotificationSeverity severity,
            string title,
            string message,
            string statusText,
            string detailText,
            string sourceKind,
            string sourceId,
            bool showToastForNew,
            Action<AppNotification>? afterUpdate = null)
        {
            RunOnMainThread(() =>
            {
                var notification = FindNotification(id);
                var isNew = notification == null;
                if (notification == null)
                {
                    notification = new AppNotification(id, category, severity, title, message, sourceKind, sourceId);
                    _notifications.Insert(0, notification);
                }

                notification.UpdateContent(
                    severity,
                    title,
                    message,
                    statusText,
                    detailText);
                afterUpdate?.Invoke(notification);
                Resort(notification);
                TrimOldNotifications();
                RaiseNotificationsChanged();
                QueueSave(200);

                if (isNew && showToastForNew)
                {
                    NotificationPublished?.Invoke(this, notification);
                }
            });
        }

        /// <summary>
        /// Finds one notification by stable identity.
        /// </summary>
        private AppNotification? FindNotification(string id)
        {
            return _notifications.FirstOrDefault(notification =>
                string.Equals(notification.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Moves an updated notification back into newest-first order.
        /// </summary>
        private void Resort(AppNotification notification)
        {
            var currentIndex = _notifications.IndexOf(notification);
            if (currentIndex < 0)
            {
                return;
            }

            _notifications.RemoveAt(currentIndex);

            var insertIndex = 0;
            while (insertIndex < _notifications.Count &&
                   _notifications[insertIndex].UpdatedAt > notification.UpdatedAt)
            {
                insertIndex++;
            }

            _notifications.Insert(insertIndex, notification);
        }

        /// <summary>
        /// Keeps the persisted file bounded so startup remains quick.
        /// </summary>
        private void TrimOldNotifications()
        {
            while (_notifications.Count > MaxPersistedNotifications)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }
        }

        /// <summary>
        /// Queues a debounced save of the current notification snapshot.
        /// </summary>
        private void QueueSave(int delayMs)
        {
            if (!_isInitialized)
            {
                return;
            }

            _saveCts?.Cancel();
            _saveCts?.Dispose();
            _saveCts = new CancellationTokenSource();
            var ct = _saveCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (delayMs > 0)
                    {
                        await Task.Delay(delayMs, ct);
                    }

                    await SaveSnapshotAsync(ct);
                }
                catch (OperationCanceledException)
                {
                }
            }, ct);
        }

        /// <summary>
        /// Saves a point-in-time copy of the notification collection to disk.
        /// </summary>
        private async Task SaveSnapshotAsync(CancellationToken ct)
        {
            var snapshot = await MainThread.InvokeOnMainThreadAsync(() => new NotificationStore
            {
                Notifications = _notifications
                    .OrderByDescending(notification => notification.UpdatedAt)
                    .Take(MaxPersistedNotifications)
                    .Select(CreateStoreItem)
                    .ToList()
            });

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
            await File.WriteAllTextAsync(_filePath, json, ct);
        }

        /// <summary>
        /// Converts a live notification into its persisted DTO.
        /// </summary>
        private static NotificationStoreItem CreateStoreItem(AppNotification notification)
        {
            return new NotificationStoreItem
            {
                Id = notification.Id,
                Category = notification.Category,
                Severity = notification.Severity,
                Title = notification.Title,
                Message = notification.Message,
                StatusText = notification.StatusText,
                DetailText = notification.DetailText,
                SourceKind = notification.SourceKind,
                SourceId = notification.SourceId,
                CreatedAt = notification.CreatedAt,
                UpdatedAt = notification.UpdatedAt,
                IsInProgress = notification.IsInProgress,
                ProgressFraction = notification.ProgressFraction,
                HasProgress = notification.HasProgress
            };
        }

        /// <summary>
        /// Runs one notification mutation on the MAUI UI thread.
        /// </summary>
        private static void RunOnMainThread(Action action)
        {
            if (MainThread.IsMainThread)
            {
                action();
                return;
            }

            MainThread.BeginInvokeOnMainThread(action);
        }

        /// <summary>
        /// Raises the aggregate notification change event.
        /// </summary>
        private void RaiseNotificationsChanged()
        {
            NotificationsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Builds a stable notification id for one update candidate.
        /// </summary>
        private static string BuildUpdateNotificationId(UpdateCandidate candidate)
        {
            return $"update:{candidate.TargetKind}:{candidate.TargetId}:{candidate.RemoteVersion}".ToLowerInvariant();
        }

        /// <summary>
        /// Builds a stable notification id for one download operation.
        /// </summary>
        private static string BuildDownloadNotificationId(string operationKey)
        {
            return $"download:{operationKey}".ToLowerInvariant();
        }

        /// <summary>
        /// Formats byte counts into compact transfer labels.
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

        /// <summary>
        /// Returns the application root directory above the deployed App folder.
        /// </summary>
        private static string GetRootDirectory()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return Directory.GetParent(appDir)?.FullName ?? appDir;
        }

        /// <summary>
        /// Represents the persisted notifications file.
        /// </summary>
        private sealed class NotificationStore
        {
            public int FileVersion { get; set; } = 1;
            public List<NotificationStoreItem> Notifications { get; set; } = [];
        }

        /// <summary>
        /// Represents one persisted notification.
        /// </summary>
        private sealed class NotificationStoreItem
        {
            public string Id { get; set; } = string.Empty;
            public AppNotificationCategory Category { get; set; }
            public AppNotificationSeverity Severity { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string StatusText { get; set; } = string.Empty;
            public string DetailText { get; set; } = string.Empty;
            public string SourceKind { get; set; } = string.Empty;
            public string SourceId { get; set; } = string.Empty;
            public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
            public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
            public bool IsInProgress { get; set; }
            public double ProgressFraction { get; set; }
            public bool HasProgress { get; set; }
        }

        /// <summary>
        /// Describes one startup test notification before it is published.
        /// </summary>
        private sealed record TestNotificationSample(
            string Id,
            AppNotificationCategory Category,
            AppNotificationSeverity Severity,
            string Title,
            string Message,
            string StatusText,
            string SourceId,
            double? ProgressFraction = null,
            bool IsInProgress = false);
    }
}
