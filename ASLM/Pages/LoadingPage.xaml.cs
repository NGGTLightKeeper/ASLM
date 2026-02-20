using ASLM.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ASLM.Pages
{
    public partial class LoadingPage : ContentPage
    {
        private readonly AppDataService _appData;
        private readonly IServiceProvider _services;

        public LoadingPage(AppDataService appData, IServiceProvider services)
        {
            InitializeComponent();
            _appData = appData;
            _services = services;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _appData.InitializeAsync();

            Page nextPage;
            if (_appData.IsFirstRun)
            {
                nextPage = _services.GetRequiredService<SetupWizardPage>();
            }
            else
            {
                nextPage = _services.GetRequiredService<MainPage>();
            }

            // Replace the current page in the window
            if (Window != null)
            {
                Window.Page = nextPage;
            }
        }
    }
}
