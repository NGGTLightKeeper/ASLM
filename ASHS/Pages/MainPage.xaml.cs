using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace ASHS.Pages
{
    public partial class MainPage : ContentPage
    {
        private const string LocalUrl = "http://127.0.0.1:8000";

        public MainPage()
        {
            InitializeComponent();
            _ = WaitForServerAsync(); // Started asynchronously
        }

        private async Task WaitForServerAsync()
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

            while (true)
            {
                try
                {
                    // Check if server is responsive.
                    // We use GetAsync to ensure it returns a valid response.
                    var response = await client.GetAsync(LocalUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        break;
                    }
                }
                catch
                {
                    // Server not ready yet.
                }

                await Task.Delay(2000);
            }

            // Server is up. Load the URL and show the WebView.
            // Ensure UI updates happen on the main thread (usually safe after await, but good practice to be sure)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Browser.Source = LocalUrl;
                LoadingOverlay.IsVisible = false;
                Browser.IsVisible = true;
            });
        }

        private async void Browser_Navigating(object sender, WebNavigatingEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Url))
                return;

            string url = e.Url;
            Uri uri;
            try
            {
                uri = new Uri(url);
            }
            catch
            {
                // If it's not a valid absolute URI, let the WebView handle it (might be javascript: or relative)
                // Or cancel it if we want strict mode.
                // Usually relative URIs are not passed here as absolute strings?
                // Let's assume standard http/https.
                return;
            }

            // Check if the link is internal (same host and port)
            // Handle both 127.0.0.1 and localhost
            // Check Scheme too to be safe (http/https)
            bool isInternal = (uri.Scheme == "http" || uri.Scheme == "https") &&
                              (uri.Host == "127.0.0.1" || uri.Host == "localhost") &&
                              uri.Port == 8000;

            if (!isInternal)
            {
                e.Cancel = true;
                try
                {
                    await Launcher.OpenAsync(uri);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to open external link: {ex.Message}");
                }
            }
        }
    }
}
