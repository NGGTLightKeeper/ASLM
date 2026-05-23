// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Localization;
using ASLM.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ASLM.Pages
{
    /// <summary>
    /// Initializes persisted app data and routes startup to the next page.
    /// </summary>
    public partial class LoadingPage : ContentPage, ILocalizable
    {
        private readonly AppDataStore _appData;
        private readonly NotificationCenter _notifications;
        private readonly UpdateScheduler _updateScheduler;
        private readonly AslmApiServer _apiServer;
        private readonly AslmModuleInteropServer _moduleInteropServer;
        private readonly ThemeService _themeService;
        private readonly CustomThemesStore _customThemesStore;
        private readonly ModuleTrustService _moduleTrustService;
        private readonly AppLocalizationService _localization;
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
            AslmModuleInteropServer moduleInteropServer,
            ThemeService themeService,
            CustomThemesStore customThemesStore,
            ModuleTrustService moduleTrustService,
            AppLocalizationService localization,
            IServiceProvider services)
        {
            InitializeComponent();
            _appData = appData;
            _notifications = notifications;
            _updateScheduler = updateScheduler;
            _apiServer = apiServer;
            _moduleInteropServer = moduleInteropServer;
            _themeService = themeService;
            _customThemesStore = customThemesStore;
            _moduleTrustService = moduleTrustService;
            _localization = localization;
            _services = services;
            LocalizableAttach.Hook(this, _localization, this);
        }


        // Localization

        /// <summary>
        /// Applies the loading label text for the current culture.
        /// </summary>
        public void ApplyLocalization() =>
            LoadingLabel.Text = L.Get(LocalizationKeys.Loading_Text);


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
            _localization.ApplyCulture();
            await Task.Run(() => _moduleTrustService.InitializeAsync());
            await Task.Run(() => _customThemesStore.LoadAsync());
            await Task.Run(() => _notifications.InitializeAsync());
            await Task.Run(() => _apiServer.StartIfEnabledAsync());
            await Task.Run(() => _moduleInteropServer.EnsureStartedAsync());
            await Task.Run(_updateScheduler.Start);
            _themeService.ApplyFromSettings();

            Page nextPage = _appData.IsFirstRun
                ? _services.GetRequiredService<SetupWizardPage>()
                : _services.GetRequiredService<AppShellPage>();

            if (nextPage is ILocalizable localizable)
            {
                localizable.ApplyLocalization();
            }

            if (Window != null)
            {
                Window.Page = nextPage;
            }
        }
    }
}
