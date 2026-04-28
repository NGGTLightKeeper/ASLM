// Copyright NGGT.LightKeeper. All Rights Reserved.

namespace ASLM.Patcher;

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
        const int width = 720;
        const int height = 540;

        var window = new Window(new MainPage());
        window.Title = "ASLM Patcher";

        window.Width = width;
        window.Height = height;

        window.MinimumWidth = width;
        window.MaximumWidth = width;
        window.MinimumHeight = height;
        window.MaximumHeight = height;

        return window;
    }
}
