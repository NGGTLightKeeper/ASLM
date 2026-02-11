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
            // 1. Check Engines
            var engineInstaller = _services.GetRequiredService<EngineInstaller>();
            var engines = engineInstaller.DiscoverEngines();
            if (engines.Any(e => !e.Status.Installed))
            {
                return _services.GetRequiredService<EngineSetupPage>();
            }

            // 2. Check Models
            var modelInstaller = _services.GetRequiredService<ModelInstaller>();
            var models = modelInstaller.DiscoverModels();
            if (models.Any(m => !m.Status.Installed))
            {
                return _services.GetRequiredService<ModelSetupPage>();
            }

            // 3. Main App
            return _services.GetRequiredService<MainPage>();
        }
    }
}
