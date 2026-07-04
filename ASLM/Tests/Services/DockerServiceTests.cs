// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Services;

namespace ASLM.Tests.Services;

public sealed class DockerServiceTests
{
    [Fact]
    public void IsCheckRequiredOnThisPlatform_matches_windows_runtime()
    {
        var service = new DockerService();

        service.IsCheckRequiredOnThisPlatform().Should().Be(
            OperatingSystem.IsWindows() || OperatingSystem.IsMacCatalyst() || OperatingSystem.IsMacOS());
    }
}
