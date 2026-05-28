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
}
