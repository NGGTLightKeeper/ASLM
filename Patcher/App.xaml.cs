// Copyright NGGT.LightKeeper. All Rights Reserved.

namespace Patcher;

/// <summary>
/// Hosts the patcher status window.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Creates the patcher application and opens the status page.
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates a compact status window for the update flow.
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage());
        window.Width = 620;
        window.Height = 440;
        window.MinimumWidth = 520;
        window.MinimumHeight = 360;
        return window;
    }
}
