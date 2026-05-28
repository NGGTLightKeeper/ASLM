// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Services;

namespace ASLM.Tests.Services;

public sealed class GitHubUpdateClientTests
{
    [Fact]
    public async Task GetReleasesAsync_returns_empty_for_blank_repo()
    {
        var client = new GitHubUpdateClient();

        var releases = await client.GetReleasesAsync("  ", includePrerelease: true);

        releases.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBranchesAsync_returns_empty_for_blank_repo()
    {
        var client = new GitHubUpdateClient();

        var branches = await client.GetBranchesAsync(string.Empty);

        branches.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestReleaseAsync_returns_null_for_blank_repo()
    {
        var client = new GitHubUpdateClient();

        var release = await client.GetLatestReleaseAsync(string.Empty, includePrerelease: false);

        release.Should().BeNull();
    }
}
