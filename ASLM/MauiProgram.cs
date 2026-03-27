// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Pages;
using ASLM.Services;
using Microsoft.Extensions.Logging;

namespace ASLM
{
    // MAUI bootstrap

    /// <summary>
    /// Configures the MAUI host, services, and page registrations.
    /// </summary>
    public static class MauiProgram
    {
        // App builder

        /// <summary>
        /// Creates the configured MAUI application instance.
        /// </summary>
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // Configure the shared application host and bundled fonts.
            builder
                .UseMauiApp<App>()
                .ConfigureMauiHandlers(handlers =>
                {
                    handlers.AddHandler<ConsoleOutputView, ConsoleOutputViewHandler>();
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", "FluentUI");
                });

#if DEBUG
            // Keep debug logging enabled in development builds.
            builder.Logging.AddDebug();
            builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            // Service registrations
            builder.Services.AddSingleton<AppDataService>();
            builder.Services.AddSingleton<EngineInstaller>();
            builder.Services.AddSingleton<ModelInstaller>();
            builder.Services.AddSingleton<ModuleInstaller>();
            builder.Services.AddSingleton<ModuleConsoleService>();
            builder.Services.AddSingleton<ProcessTracker>();
            builder.Services.AddSingleton<ModuleRunner>();
            builder.Services.AddSingleton<PortManager>();

            // Page registrations
            builder.Services.AddTransient<AppShellPage>();
            builder.Services.AddTransient<SetupWizardPage>();
            builder.Services.AddTransient<LoadingPage>();

            // Content view registrations
            builder.Services.AddTransient<HomeView>();
            builder.Services.AddTransient<ConsolesView>();
            builder.Services.AddTransient<ModuleManagementView>();
            builder.Services.AddTransient<DownloadModulesView>();
            builder.Services.AddTransient<SettingsView>();

            return builder.Build();
        }
    }
}
