// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ASLM.Models;
using ASLM.Services;

namespace ASLM.Pages
{
    // Notifications popover view

    /// <summary>
    /// Displays internal ASLM notifications in a compact downloads-style popover.
    /// </summary>
    public partial class NotificationsView : ContentView, INotifyPropertyChanged
    {
        private const double PopupWidth = 450;
        private const double PopupHeight = 520;
        private const double PopupGap = 8;
        private const double ScreenPadding = 12;

        private readonly NotificationService _notificationService;

        /// <summary>
        /// Raised when the user asks to close the notifications popover.
        /// </summary>
        public event EventHandler? CloseRequested;

        /// <inheritdoc />
        public new event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates the notifications popover and hooks service updates.
        /// </summary>
        public NotificationsView(NotificationService notificationService)
        {
            _notificationService = notificationService;

            InitializeComponent();
            BindingContext = this;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Gets the notifications currently visible in the popover.
        /// </summary>
        public ObservableCollection<AppNotification> VisibleNotifications { get; } = [];

        /// <summary>
        /// Gets the compact total count label.
        /// </summary>
        public string SummaryText => _notificationService.Notifications.Count == 0
            ? "No notifications"
            : $"{_notificationService.Notifications.Count} total";

        /// <summary>
        /// Gets the small section label above the list.
        /// </summary>
        public string SectionTitle => "Recent";

        /// <summary>
        /// Gets whether the current filter has visible notifications.
        /// </summary>
        public bool HasVisibleNotifications => VisibleNotifications.Count > 0;

        /// <summary>
        /// Gets whether the empty state should be shown.
        /// </summary>
        public bool IsEmptyVisible => !HasVisibleNotifications;

        /// <summary>
        /// Positions the popover next to the notifications sidebar button.
        /// </summary>
        public Task OpenAtAsync(Rect anchorBounds, double hostWidth, double hostHeight)
        {
            PositionPopup(anchorBounds, hostWidth, hostHeight);
            RefreshVisibleNotifications();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Refreshes the popover before the shell shows it.
        /// </summary>
        public Task RefreshAsync()
        {
            RefreshVisibleNotifications();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Subscribes to notification service changes once the view enters the visual tree.
        /// </summary>
        private void OnLoaded(object? sender, EventArgs e)
        {
            _notificationService.NotificationsChanged -= OnNotificationsChanged;
            _notificationService.NotificationsChanged += OnNotificationsChanged;
            RefreshVisibleNotifications();
        }

        /// <summary>
        /// Unsubscribes from service changes when the popover is removed.
        /// </summary>
        private void OnUnloaded(object? sender, EventArgs e)
        {
            _notificationService.NotificationsChanged -= OnNotificationsChanged;
        }

        /// <summary>
        /// Applies popover margins while keeping it inside the host bounds.
        /// </summary>
        private void PositionPopup(Rect anchorBounds, double hostWidth, double hostHeight)
        {
            var x = anchorBounds.Right + PopupGap;
            var y = anchorBounds.Top - (PopupHeight / 2) + (anchorBounds.Height / 2);

            if (hostWidth > 0)
            {
                x = Math.Min(x, Math.Max(ScreenPadding, hostWidth - PopupWidth - ScreenPadding));
            }

            if (hostHeight > 0)
            {
                y = Math.Min(y, Math.Max(ScreenPadding, hostHeight - PopupHeight - ScreenPadding));
            }

            x = Math.Max(ScreenPadding, x);
            y = Math.Max(ScreenPadding, y);

            NotificationsPopup.Margin = new Thickness(x, y, 0, 0);
        }

        /// <summary>
        /// Closes the popover when the transparent background is tapped.
        /// </summary>
        private void OnBackgroundTapped(object? sender, EventArgs e)
        {
            RequestClose();
        }

        /// <summary>
        /// Swallows taps inside the popover so the backdrop does not close it.
        /// </summary>
        private void OnDialogTapped(object? sender, EventArgs e)
        {
            // Intentionally left blank so dialog taps stay inside the popover.
        }

        /// <summary>
        /// Closes the popover from the header close button.
        /// </summary>
        private void OnCloseClicked(object? sender, EventArgs e)
        {
            RequestClose();
        }

        /// <summary>
        /// Raises the close request event for the shell.
        /// </summary>
        private void RequestClose()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Dismisses one notification from its row action.
        /// </summary>
        private void OnDismissClicked(object? sender, EventArgs e)
        {
            if (sender is Button { BindingContext: AppNotification notification })
            {
                _notificationService.Dismiss(notification);
            }
        }

        /// <summary>
        /// Rebuilds the visible list when service state changes.
        /// </summary>
        private void OnNotificationsChanged(object? sender, EventArgs e)
        {
            RefreshVisibleNotifications();
        }

        /// <summary>
        /// Rebuilds the visible notifications collection newest first.
        /// </summary>
        private void RefreshVisibleNotifications()
        {
            var filtered = _notificationService.Notifications
                .OrderByDescending(notification => notification.UpdatedAt)
                .ToList();

            VisibleNotifications.Clear();
            foreach (var notification in filtered)
            {
                VisibleNotifications.Add(notification);
            }

            RaiseViewStateProperties();
        }

        /// <summary>
        /// Raises all view-level properties affected by service changes.
        /// </summary>
        private void RaiseViewStateProperties()
        {
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(SectionTitle));
            OnPropertyChanged(nameof(HasVisibleNotifications));
            OnPropertyChanged(nameof(IsEmptyVisible));
        }

        /// <summary>
        /// Raises the view property changed event.
        /// </summary>
        protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
