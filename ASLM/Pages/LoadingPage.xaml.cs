using ASLM.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ASLM.Pages
{
    /// <summary>
    /// Startup splash page that initializes persisted app data and routes to the next page.
    /// </summary>
    public partial class LoadingPage : ContentPage
    {
        private readonly AppDataService _appData;
        private readonly IServiceProvider _services;
        private bool _initialized;

        public LoadingPage(AppDataService appData, IServiceProvider services)
        {
            InitializeComponent();
            _appData = appData;
            _services = services;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized)
                return;

            _initialized = true;
            await _appData.InitializeAsync();

            Page nextPage;
            if (_appData.IsFirstRun)
            {
                nextPage = _services.GetRequiredService<SetupWizardPage>();
            }
            else
            {
                nextPage = _services.GetRequiredService<AppShellPage>();
            }

            // Replace the current page in the window
            if (Window != null)
            {
                Window.Page = nextPage;
            }
        }
    }
}
