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
        private readonly AppDataStore _appData;
        private readonly NotificationCenter _notifications;
        private readonly UpdateScheduler _updateScheduler;
        private readonly AslmApiServer _apiServer;
        private readonly ThemeService _themeService;
        private readonly CustomThemesStore _customThemesStore;
        private readonly IServiceProvider _services;
        private bool _initialized;

        // Initialization

        /// <summary>
        /// Creates the startup loading page.
        /// </summary>
        public LoadingPage(
            AppDataStore appData,
            NotificationCenter notifications,
            UpdateScheduler updateScheduler,
            AslmApiServer apiServer,
            ThemeService themeService,
            CustomThemesStore customThemesStore,
            IServiceProvider services)
        {
            InitializeComponent();
            _appData = appData;
            _notifications = notifications;
            _updateScheduler = updateScheduler;
            _apiServer = apiServer;
            _themeService = themeService;
            _customThemesStore = customThemesStore;
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
            await Task.Run(() => _appData.InitializeAsync());
            await Task.Run(() => _customThemesStore.LoadAsync());
            await Task.Run(() => _notifications.InitializeAsync());
            await Task.Run(() => _apiServer.StartIfEnabledAsync());
            await Task.Run(_updateScheduler.Start);
            _themeService.ApplyFromSettings();

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
