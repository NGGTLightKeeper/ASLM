// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ASLM.Tests.Services;

public sealed class GitHubUpdateClientTests
{
    [Fact]
    public async Task GetReleasesAsync_returns_empty_for_blank_repo()
    {
        var client = CreateClient();

        var releases = await client.GetReleasesAsync("  ", includePrerelease: true);

        releases.Should().BeEmpty();
    }

    [Fact]
    public async Task GetBranchesAsync_returns_empty_for_blank_repo()
    {
        var client = CreateClient();

        var branches = await client.GetBranchesAsync(string.Empty);

        branches.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestReleaseAsync_returns_null_for_blank_repo()
    {
        var client = CreateClient();

        var release = await client.GetLatestReleaseAsync(string.Empty, includePrerelease: false);

        release.Should().BeNull();
    }

    private static GitHubUpdateClient CreateClient()
    {
        var store = new GitHubRateLimitStore(NullLogger<GitHubRateLimitStore>.Instance);
        return new GitHubUpdateClient(store);
    }
}
