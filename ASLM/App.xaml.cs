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
            // Check if any engines need installation
            var installer = _services.GetRequiredService<EngineInstaller>();
            var engines = installer.DiscoverEngines();
            var needsSetup = engines.Any(e => !e.Status.Installed);

            if (needsSetup)
            {
                return new Window(_services.GetRequiredService<EngineSetupPage>());
            }

            return new Window(_services.GetRequiredService<MainPage>());
        }
    }
}
