// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;
using ASLM.Services;
using ASLM.Tests.TestSupport;

namespace ASLM.Tests.Services;

public sealed class PortRegistryTests
{
    [Fact]
    public void ResolveModulePagePortKey_prefers_http_setting()
    {
        var module = ModuleConfigBuilder.Create(configure: m =>
        {
            m.Settings =
            [
                new ModuleSetting { Key = "admin", Type = "port" },
                new ModuleSetting { Key = "http", Type = "port" }
            ];
        });

        PortRegistry.ResolveModulePagePortKey(module).Should().Be("http");
    }

    [Fact]
    public void GetOrAssignPorts_allocates_within_official_range()
    {
        _ = new AslmFileSystemLayout();
        var appData = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        appData.Data.Ports.OfficialStart = 25000;
        appData.Data.Ports.OfficialCount = 50;
        appData.Data.Ports.ThirdPartyStart = 35000;
        appData.Data.Ports.ThirdPartyCount = 50;

        var registry = new PortRegistry(appData);
        var module = ModuleConfigBuilder.Create(id: "official-module");

        var ports = registry.GetOrAssignPorts(module);

        ports.Should().ContainKey("http");
        ports["http"].Should().BeInRange(25000, 25049);
    }

    [Fact]
    public void GetOrAssignInternalServicePort_is_stable_for_service_id()
    {
        _ = new AslmFileSystemLayout();
        var appData = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        appData.Data.Ports.OfficialStart = 26000;
        appData.Data.Ports.OfficialCount = 100;

        var registry = new PortRegistry(appData);

        var first = registry.GetOrAssignInternalServicePort(PortRegistry.AslmApiServiceId, PortRegistry.AslmApiPortKey);
        var second = registry.GetOrAssignInternalServicePort(PortRegistry.AslmApiServiceId, PortRegistry.AslmApiPortKey);

        second.Should().Be(first);
        registry.TryGetInternalServicePort(PortRegistry.AslmApiServiceId, PortRegistry.AslmApiPortKey)
            .Should().Be(first);
    }

    [Fact]
    public void RemoveModulePorts_clears_assignments()
    {
        _ = new AslmFileSystemLayout();
        var appData = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        var registry = new PortRegistry(appData);
        var module = ModuleConfigBuilder.Create();

        registry.GetOrAssignPorts(module);
        registry.RemoveModulePorts(module.Id);

        registry.TryGetPort(module.Id, "http").Should().BeNull();
    }

    [Fact]
    public void TryGetAssignedPorts_returns_existing_without_allocation()
    {
        _ = new AslmFileSystemLayout();
        var appData = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        appData.Data.Ports.OfficialStart = 27000;
        appData.Data.Ports.OfficialCount = 50;
        var registry = new PortRegistry(appData);
        var module = ModuleConfigBuilder.Create(id: "snapshot-module");

        // Before any assignment the read-only view returns null.
        registry.TryGetAssignedPorts(module.Id).Should().BeNull();

        // After assignment the read-only view reflects the same ports.
        var assigned = registry.GetOrAssignPorts(module);
        var snapshot = registry.TryGetAssignedPorts(module.Id);

        snapshot.Should().NotBeNull();
        snapshot!["http"].Should().Be(assigned["http"]);
    }

    [Fact]
    public void TryGetAssignedPorts_does_not_persist_new_entry()
    {
        _ = new AslmFileSystemLayout();
        var appData = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        var registry = new PortRegistry(appData);
        var module = ModuleConfigBuilder.Create(id: "never-assigned");

        registry.TryGetAssignedPorts(module.Id).Should().BeNull();

        // A second call on the same unassigned module still returns null.
        registry.TryGetAssignedPorts(module.Id).Should().BeNull();
    }

    [Fact]
    public void TryGetModulePageUrl_returns_loopback_for_assigned_module()
    {
        _ = new AslmFileSystemLayout();
        var appData = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        appData.Data.Ports.OfficialStart = 28000;
        appData.Data.Ports.OfficialCount = 50;
        var registry = new PortRegistry(appData);
        var module = ModuleConfigBuilder.Create(id: "page-url-module");

        registry.GetOrAssignPorts(module);
        var url = registry.TryGetModulePageUrl(module);

        url.Should().StartWith("http://127.0.0.1:");
        url.Should().EndWith("/");
    }

    [Fact]
    public void TryGetModulePageUrl_returns_null_when_not_assigned()
    {
        _ = new AslmFileSystemLayout();
        var appData = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        var registry = new PortRegistry(appData);
        var module = ModuleConfigBuilder.Create(id: "unassigned-page-url");

        registry.TryGetModulePageUrl(module).Should().BeNull();
    }

    [Theory]
    [InlineData("ui-port", "ui")]
    [InlineData("api-port", "api")]
    [InlineData("example_port", "example")]
    [InlineData("http", "http")]
    [InlineData("server port", "server")]
    public void BuildHostRouteKey_strips_known_suffixes(string hostKey, string expected)
    {
        PortRegistry.BuildHostRouteKey(hostKey).Should().Be(expected);
    }
}
