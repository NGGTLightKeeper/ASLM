using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Application settings page with sidebar category navigation.
    /// </summary>
    public partial class SettingsPage : ContentPage
    {
        private readonly AppDataService _appData;
        private readonly IServiceProvider _services;

        public SettingsPage(AppDataService appData, IServiceProvider services)
        {
            _appData = appData;
            _services = services;
            InitializeComponent();
            LoadSettings();

            // Left-align nav buttons via native handler
            NavUserProfile.HandlerChanged += AlignButtonLeft;
            NavPorts.HandlerChanged += AlignButtonLeft;
        }

        /// <summary>Populates the UI fields with current persisted values.</summary>
        private void LoadSettings()
        {
            UsernameEntry.Text = _appData.Data.User.Name;
            OfficialPortEntry.Text = _appData.Data.Ports.OfficialStart.ToString();
            ThirdPartyPortEntry.Text = _appData.Data.Ports.ThirdPartyStart.ToString();
        }

        // --- Category Navigation ---------------------------------------------

        private void OnUserProfileClicked(object? sender, EventArgs e)
        {
            UserProfileSection.Focus();
            SettingsScroll.ScrollToAsync(UserProfileSection, ScrollToPosition.Start, true);
            HighlightCategory(NavUserProfile);
        }

        private void OnPortsClicked(object? sender, EventArgs e)
        {
            PortsSection.Focus();
            SettingsScroll.ScrollToAsync(PortsSection, ScrollToPosition.Start, true);
            HighlightCategory(NavPorts);
        }

        private void HighlightCategory(Button active)
        {
            NavUserProfile.TextColor = Color.FromArgb("#888");
            NavPorts.TextColor = Color.FromArgb("#888");
            active.TextColor = Colors.White;
        }

        // --- Save / Cancel ---------------------------------------------------

        private async void OnSaveClicked(object? sender, EventArgs e)
        {
            if (!int.TryParse(OfficialPortEntry.Text, out var op) || op < 1024 || op > 65000)
            {
                ShowPortError("Official port must be between 1024 and 65000.");
                return;
            }
            if (!int.TryParse(ThirdPartyPortEntry.Text, out var tp) || tp < 1024 || tp > 64000)
            {
                ShowPortError("Third-party port must be between 1024 and 64000.");
                return;
            }

            // Check overlap: [op, op+100) vs [tp, tp+1000)
            int opEnd = op + 100;
            int tpEnd = tp + 1000;
            if (op < tpEnd && tp < opEnd)
            {
                ShowPortError($"Port ranges overlap! Official {op}–{opEnd - 1} conflicts with Third-party {tp}–{tpEnd - 1}.");
                return;
            }

            PortErrorLabel.IsVisible = false;

            _appData.Data.User.Name = UsernameEntry.Text?.Trim() ?? "";
            _appData.Data.Ports.OfficialStart = op;
            _appData.Data.Ports.ThirdPartyStart = tp;
            await _appData.SaveAsync();

            NavigateToMain();
        }

        private void ShowPortError(string message)
        {
            PortErrorLabel.Text = message;
            PortErrorLabel.IsVisible = true;
        }

        private void OnCancelClicked(object? sender, EventArgs e)
        {
            NavigateToMain();
        }

        /// <summary>Replaces the current window page with a fresh MainPage.</summary>
        private void NavigateToMain()
        {
            if (Application.Current?.Windows.Count > 0)
            {
                var mainPage = _services.GetRequiredService<MainPage>();
                Application.Current.Windows[0].Page = mainPage;
            }
        }

        /// <summary>Sets native WinUI button content alignment to left.</summary>
        private static void AlignButtonLeft(object? sender, EventArgs e)
        {
#if WINDOWS
            if (sender is Button btn && btn.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Button native)
            {
                native.HorizontalContentAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Left;
            }
#endif
        }
    }
}
