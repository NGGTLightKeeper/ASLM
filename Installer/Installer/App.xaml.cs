// Copyright NGGT.LightKeeper. All Rights Reserved.

namespace ASLM.Installer;

// Installer application shell.

/// <summary>
/// Hosts the installer wizard and configures the primary window.
/// </summary>
public partial class App : Application
{
    // Application lifecycle.

    /// <summary>
    /// Creates the installer application.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    // Window configuration.

    /// <summary>
    /// Creates and sizes the primary installer window.
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage());
        window.Title = "ASLM Installer";
        window.Width = 720;
        window.Height = 520;
        window.MinimumWidth = 620;
        window.MinimumHeight = 440;
        return window;
    }
}
