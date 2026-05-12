// Copyright NGGT.LightKeeper. All Rights Reserved.

using System.Text;

namespace ASLM.Patcher;

/// <summary>
/// Shows live patcher status while the file replacement runs in the background.
/// </summary>
public partial class MainPage : ContentPage
{
    private static readonly bool CloseAutomaticallyAfterPatch = true;

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

        StatusLabel.Text = exitCode == 0
            ? "Update finished. ASLM has been started."
            : "Update failed. ASLM has been started.";
        SubtitleLabel.Text = exitCode == 0
            ? "Finished"
            : "Failed";

        if (CloseAutomaticallyAfterPatch)
        {
            await Task.Delay(900);
            Application.Current?.Quit();
        }
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
