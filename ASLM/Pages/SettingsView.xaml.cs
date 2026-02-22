using ASLM.Services;

namespace ASLM.Pages
{
    /// <summary>
    /// Settings content view — displays user profile and port allocation settings.
    /// No separate sidebar needed; hosted inside AppShellPage.
    /// </summary>
    public partial class SettingsView : ContentView
    {
        private readonly AppDataService _appData;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsView"/> class.
        /// </summary>
        public SettingsView(AppDataService appData)
        {
            _appData = appData;
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

        // --- Save ---

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
        }

        private void ShowPortError(string message)
        {
            PortErrorLabel.Text = message;
            PortErrorLabel.IsVisible = true;
        }
    }
}
