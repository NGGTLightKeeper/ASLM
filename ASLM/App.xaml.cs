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
            return new Window(page);
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
