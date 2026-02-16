using Microsoft.Extensions.Logging;
using ASLM.Pages;
using ASLM.Services;

namespace ASLM
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", "FluentUI");
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif

            // Services
            builder.Services.AddSingleton<AppDataService>();
            builder.Services.AddSingleton<EngineInstaller>();
            builder.Services.AddSingleton<ModelInstaller>();
            builder.Services.AddSingleton<ModuleInstaller>();
            builder.Services.AddSingleton<ModuleRunner>();

            // Pages
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<SetupWizardPage>();
            builder.Services.AddTransient<SettingsPage>();

            return builder.Build();
        }
    }
}
