// Copyright NGGT.LightKeeper. All Rights Reserved.

using Microsoft.Extensions.Logging;

namespace ASLM.Installer;

// MAUI host setup.

/// <summary>
/// Builds the MAUI application used by the standalone installer.
/// </summary>
public static class MauiProgram
{
    // Application composition.

    /// <summary>
    /// Registers UI services and returns the configured MAUI app.
    /// </summary>
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .Services
            .AddSingleton<InstallerService>()
            .AddSingleton<LegalDocumentService>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
