// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace ASLM.Models
{
    /// <summary>
    /// Distinguishes the top-level notification groups shown in the notifications page.
    /// </summary>
    public enum AppNotificationCategory
    {
        Updates,
        Downloads,
        System
    }

    /// <summary>
    /// Distinguishes the visual importance of one application notification.
    /// </summary>
    public enum AppNotificationSeverity
    {
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// Represents one internal ASLM notification with optional progress state.
    /// </summary>
    public sealed class AppNotification : INotifyPropertyChanged
    {
        private string _title;
        private string _message;
        private string _statusText;
        private string _detailText;
        private AppNotificationSeverity _severity;
        private DateTimeOffset _updatedAt;
        private bool _isInProgress;
        private double _progressFraction;
        private bool _hasProgress;

        /// <summary>
        /// Creates a notification with stable identity and initial display fields.
        /// </summary>
        public AppNotification(
            string id,
            AppNotificationCategory category,
            AppNotificationSeverity severity,
            string title,
            string message,
            string sourceKind,
            string sourceId)
        {
            Id = id;
            Category = category;
            SourceKind = sourceKind;
            SourceId = sourceId;
            CreatedAt = DateTimeOffset.Now;
            _updatedAt = CreatedAt;
            _title = title;
            _message = message;
            _statusText = string.Empty;
            _detailText = string.Empty;
            _severity = severity;
        }

        /// <summary>
        /// Restores a notification from persisted storage.
        /// </summary>
        internal AppNotification(
            string id,
            AppNotificationCategory category,
            AppNotificationSeverity severity,
            string title,
            string message,
            string sourceKind,
            string sourceId,
            DateTimeOffset createdAt,
            DateTimeOffset updatedAt,
            bool isInProgress,
            double progressFraction,
            bool hasProgress,
            string statusText,
            string detailText)
        {
            Id = id;
            Category = category;
            SourceKind = sourceKind;
            SourceId = sourceId;
            CreatedAt = createdAt;
            _updatedAt = updatedAt;
            _title = title;
            _message = message;
            _statusText = statusText;
            _detailText = detailText;
            _severity = severity;
            _isInProgress = isInProgress;
            _progressFraction = Math.Clamp(progressFraction, 0, 1);
            _hasProgress = hasProgress;
        }

        /// <inheritdoc />
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the stable notification identity used for deduplication.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the notification group used by the notifications page filters.
        /// </summary>
        public AppNotificationCategory Category { get; }

        /// <summary>
        /// Gets the source type that produced this notification.
        /// </summary>
        public string SourceKind { get; }

        /// <summary>
        /// Gets the source identifier that produced this notification.
        /// </summary>
        public string SourceId { get; }

        /// <summary>
        /// Gets the creation timestamp.
        /// </summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets the last update timestamp.
        /// </summary>
        public DateTimeOffset UpdatedAt
        {
            get => _updatedAt;
            private set => SetField(ref _updatedAt, value);
        }

        /// <summary>
        /// Gets the title shown in the notification list.
        /// </summary>
        public string Title
        {
            get => _title;
            private set => SetField(ref _title, value);
        }

        /// <summary>
        /// Gets the main notification message.
        /// </summary>
        public string Message
        {
            get => _message;
            private set => SetField(ref _message, value);
        }

        /// <summary>
        /// Gets the short status line shown above progress details.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (SetField(ref _statusText, value))
                {
                    OnPropertyChanged(nameof(HasStatusText));
                    OnPropertyChanged(nameof(DetailLine));
                }
            }
        }

        /// <summary>
        /// Gets the optional detailed text shown under the main message.
        /// </summary>
        public string DetailText
        {
            get => _detailText;
            private set
            {
                if (SetField(ref _detailText, value))
                {
                    OnPropertyChanged(nameof(HasDetailText));
                    OnPropertyChanged(nameof(DetailLine));
                }
            }
        }

        /// <summary>
        /// Gets the current visual severity.
        /// </summary>
        public AppNotificationSeverity Severity
        {
            get => _severity;
            private set
            {
                if (SetField(ref _severity, value))
                {
                    OnPropertyChanged(nameof(SeverityLabel));
                    OnPropertyChanged(nameof(AccentColor));
                }
            }
        }

        /// <summary>
        /// Gets whether a long-running operation is still active.
        /// </summary>
        public bool IsInProgress
        {
            get => _isInProgress;
            private set => SetField(ref _isInProgress, value);
        }

        /// <summary>
        /// Gets the normalized progress fraction for determinate operations.
        /// </summary>
        public double ProgressFraction
        {
            get => _progressFraction;
            private set
            {
                var clamped = Math.Clamp(value, 0, 1);
                if (Math.Abs(_progressFraction - clamped) < 0.0001)
                {
                    return;
                }

                _progressFraction = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressPercentLabel));
            }
        }

        /// <summary>
        /// Gets whether the progress bar should be shown.
        /// </summary>
        public bool HasProgress
        {
            get => _hasProgress;
            private set => SetField(ref _hasProgress, value);
        }

        /// <summary>
        /// Gets whether the status line contains visible text.
        /// </summary>
        public bool HasStatusText => !string.IsNullOrWhiteSpace(StatusText);

        /// <summary>
        /// Gets whether the detail line contains visible text.
        /// </summary>
        public bool HasDetailText => !string.IsNullOrWhiteSpace(DetailText);

        /// <summary>
        /// Gets the category label shown in the notification card metadata.
        /// </summary>
        public string CategoryLabel => Category.ToString();

        /// <summary>
        /// Gets the severity label shown in the notification card metadata.
        /// </summary>
        public string SeverityLabel => Severity.ToString();

        /// <summary>
        /// Gets the compact progress label.
        /// </summary>
        public string ProgressPercentLabel => $"{ProgressFraction * 100.0:F0}%";

        /// <summary>
        /// Gets the timestamp label shown in the notification card.
        /// </summary>
        public string TimestampLabel => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        /// <summary>
        /// Gets the compact secondary line used by the notifications popover and toast cards.
        /// </summary>
        public string DetailLine
        {
            get
            {
                var status = !string.IsNullOrWhiteSpace(StatusText) ? StatusText : SeverityLabel;
                var detail = !string.IsNullOrWhiteSpace(DetailText) ? $" - {DetailText}" : string.Empty;
                return $"{status}{detail} - {TimestampLabel}";
            }
        }

        /// <summary>
        /// Gets the accent color associated with the notification severity.
        /// </summary>
        public Color AccentColor => Severity switch
        {
            AppNotificationSeverity.Success => Color.FromArgb("#32D74B"),
            AppNotificationSeverity.Warning => Color.FromArgb("#FFD60A"),
            AppNotificationSeverity.Error => Color.FromArgb("#FF453A"),
            _ => Color.FromArgb("#0A84FF")
        };

        /// <summary>
        /// Refreshes the notification text and severity while preserving its identity.
        /// </summary>
        internal void UpdateContent(
            AppNotificationSeverity severity,
            string title,
            string message,
            string statusText,
            string detailText)
        {
            Severity = severity;
            Title = title;
            Message = message;
            StatusText = statusText;
            DetailText = detailText;
            UpdatedAt = DateTimeOffset.Now;

            OnPropertyChanged(nameof(TimestampLabel));
            OnPropertyChanged(nameof(DetailLine));
        }

        /// <summary>
        /// Updates determinate or indeterminate progress for the notification.
        /// </summary>
        internal void UpdateProgress(bool hasProgress, double progressFraction, bool isInProgress)
        {
            HasProgress = hasProgress;
            ProgressFraction = progressFraction;
            IsInProgress = isInProgress;
            UpdatedAt = DateTimeOffset.Now;
            OnPropertyChanged(nameof(TimestampLabel));
            OnPropertyChanged(nameof(DetailLine));
        }

        /// <summary>
        /// Raises a property-changed event for one property.
        /// </summary>
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Replaces a field value and raises a property change when it actually changed.
        /// </summary>
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
