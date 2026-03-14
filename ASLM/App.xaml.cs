using ASLM.Pages;
using ASLM.Services;

namespace ASLM
{
    /// <summary>
    /// Represents the main application entry point.
    /// </summary>
    public partial class App : Application
    {
        private readonly IServiceProvider _services;
        private bool _isShuttingDown;

        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App(IServiceProvider services)
        {
            InitializeComponent();
            _services = services;
        }

        /// <inheritdoc />
        protected override Window CreateWindow(IActivationState? activationState)
        {
            var page = CreateStartupPage();
            var window = new Window(page)
            {
                Title = "ASLM",
                MinimumWidth = 960,
                MinimumHeight = 540
            };

            // Stop all module processes when the window is destroyed.
            // This provides graceful shutdown; the Launcher's Job Object
            // handles crash/forced kill scenarios.
            window.Destroying += OnWindowDestroying;

            return window;
        }

        /// <summary>
        /// Creates the initial loading page.
        /// </summary>
        public Page CreateStartupPage()
        {
            return _services.GetRequiredService<LoadingPage>();
        }

        /// <summary>
        /// Handles window destruction; stops all running module processes gracefully.
        /// The Launcher's Job Object ensures cleanup even if this doesn't complete in time.
        /// </summary>
        private void OnWindowDestroying(object? sender, EventArgs e)
        {
            if (_isShuttingDown)
                return;

            _isShuttingDown = true;

            try
            {
                var runner = _services.GetRequiredService<ModuleRunner>();
                runner.StopAllModulesAsync().GetAwaiter().GetResult();
                runner.Dispose();

                var tracker = _services.GetRequiredService<ProcessTracker>();
                tracker.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Shutdown cleanup failed: {ex}");
            }
        }
    }
}
