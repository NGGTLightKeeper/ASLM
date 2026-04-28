// Copyright NGGT.LightKeeper. All Rights Reserved.

using Microsoft.UI.Xaml;

namespace ASLM.Patcher.WinUI;

/// <summary>
/// Hosts the WinUI application wrapper for the MAUI patcher.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Creates the WinUI application instance.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Builds the shared MAUI application instance for the Windows host.
    /// </summary>
    protected override MauiApp CreateMauiApp()
    {
        return MauiProgram.CreateMauiApp();
    }
}
