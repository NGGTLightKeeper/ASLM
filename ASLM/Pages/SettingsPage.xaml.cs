using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Application settings page.
    /// Allows editing user profile and port allocation.
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
        }

        /// <summary>Populates the UI fields with current persisted values.</summary>
        private void LoadSettings()
        {
            UsernameEntry.Text = _appData.Data.User.Name;
            OfficialPortEntry.Text = _appData.Data.Ports.OfficialStart.ToString();
            ThirdPartyPortEntry.Text = _appData.Data.Ports.ThirdPartyStart.ToString();
        }

        private async void OnSaveClicked(object? sender, EventArgs e)
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

            _appData.Data.User.Name = UsernameEntry.Text?.Trim() ?? "";
            _appData.Data.Ports.OfficialStart = op;
            _appData.Data.Ports.ThirdPartyStart = tp;
            await _appData.SaveAsync();

            NavigateToMain();
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
    }
}
