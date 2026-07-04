// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Patcher;

/// <summary>
/// Console entry point for the headless patcher helper used on macOS.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Runs the shared patch operation and returns its exit code.
    /// </summary>
    private static Task<int> Main(string[] args)
    {
        return PatcherRunner.RunAsync(args, progress: null);
    }
}
