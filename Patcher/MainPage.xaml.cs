// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text;

namespace Patcher;

/// <summary>
/// Shows live patcher status while the file replacement runs in the background.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly StringBuilder _log = new();
    private bool _started;

    /// <summary>
    /// Creates the patcher status page.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Starts the patch operation after the native window is visible.
    /// </summary>
    private async void OnLoaded(object? sender, EventArgs e)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var progress = new Progress<PatcherProgress>(OnProgress);
        var exitCode = await PatcherRunner.RunAsync(args, progress);

        BusyIndicator.IsRunning = false;
        BusyIndicator.Color = exitCode == 0
            ? (Color)Application.Current!.Resources["SystemGreen"]
            : (Color)Application.Current!.Resources["SystemRed"];
        StatusLabel.Text = exitCode == 0
            ? "Update finished. Starting ASLM..."
            : "Update failed. Starting ASLM...";
        SubtitleLabel.Text = exitCode == 0
            ? "Finished"
            : "Failed";

        await Task.Delay(900);
        Application.Current?.Quit();
    }

    /// <summary>
    /// Appends one patcher progress message to the visible console.
    /// </summary>
    private void OnProgress(PatcherProgress progress)
    {
        StatusLabel.Text = progress.Message;
        _log.AppendLine(progress.Message);
        LogEditor.Text = _log.ToString();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Yield();
            await LogScroll.ScrollToAsync(0, LogScroll.ContentSize.Height, false);
        });
    }
}
