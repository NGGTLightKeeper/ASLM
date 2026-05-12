// Copyright NGGT.LightKeeper. All Rights Reserved.

using Microsoft.Extensions.Logging;
#if WINDOWS
using Microsoft.Maui.Handlers;
#endif

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

#if WINDOWS
        ConfigureWindowsCompactControlSizing();
#endif

        return builder.Build();
    }

#if WINDOWS
    /// <summary>
    /// Removes WinUI default minimum widths so standalone inputs stay compact.
    /// </summary>
    private static void ConfigureWindowsCompactControlSizing()
    {
        CheckBoxHandler.Mapper.AppendToMapping("CompactWindowsCheckBox", (handler, view) =>
        {
            handler.PlatformView.MinWidth = 16;
            handler.PlatformView.MinHeight = 16;
            handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
        });

        SwitchHandler.Mapper.AppendToMapping("CompactWindowsSwitch", (handler, view) =>
        {
            handler.PlatformView.MinWidth = 16;
            handler.PlatformView.MinHeight = 16;
            handler.PlatformView.Padding = new Microsoft.UI.Xaml.Thickness(0);
        });
    }
#endif
}
