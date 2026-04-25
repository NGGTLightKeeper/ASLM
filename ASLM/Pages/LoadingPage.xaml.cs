// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ASLM.Pages
{
    // Loading page

    /// <summary>
    /// Initializes persisted app data and routes startup to the next page.
    /// </summary>
    public partial class LoadingPage : ContentPage
    {
        private readonly AppDataService _appData;
        private readonly UpdateSchedulerService _updateScheduler;
        private readonly AslmApiServerService _apiServer;
        private readonly IServiceProvider _services;
        private bool _initialized;

        // Initialization

        /// <summary>
        /// Creates the startup loading page.
        /// </summary>
        public LoadingPage(
            AppDataService appData,
            UpdateSchedulerService updateScheduler,
            AslmApiServerService apiServer,
            IServiceProvider services)
        {
            InitializeComponent();
            _appData = appData;
            _updateScheduler = updateScheduler;
            _apiServer = apiServer;
            _services = services;
        }


        // Lifecycle

        /// <summary>
        /// Runs the startup initialization once and replaces the current page.
        /// </summary>
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            await _appData.InitializeAsync();
            await _apiServer.StartIfEnabledAsync();
            _updateScheduler.Start();

            Page nextPage = _appData.IsFirstRun
                ? _services.GetRequiredService<SetupWizardPage>()
                : _services.GetRequiredService<AppShellPage>();

            // Replace the startup page only when the window is already available.
            if (Window != null)
            {
                Window.Page = nextPage;
            }
        }
    }
}
