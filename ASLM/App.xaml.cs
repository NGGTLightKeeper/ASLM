using ASLM.Pages;
using ASLM.Services;

namespace ASLM
{
    public partial class App : Application
    {
        private readonly IServiceProvider _services;

        public App(IServiceProvider services)
        {
            InitializeComponent();
            _services = services;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var page = CreateStartupPage();
            var window = new Window(page)
            {
                Title = "ASLM",
                MinimumWidth = 960,
                MinimumHeight = 540
            };
            return window;
        }

        public Page CreateStartupPage()
        {
            var appData = _services.GetRequiredService<AppDataService>();

            if (appData.IsFirstRun)
            {
                return _services.GetRequiredService<SetupWizardPage>();
            }

            return _services.GetRequiredService<MainPage>();
        }
    }
}
