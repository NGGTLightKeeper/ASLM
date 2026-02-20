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
            return window;
        }

        /// <summary>
        /// Creates the initial loading page.
        /// </summary>
        public Page CreateStartupPage()
        {
            return _services.GetRequiredService<LoadingPage>();
        }
    }
}
