// Copyright NGGT.LightKeeper. All Rights Reserved.

using ASLM.Models;
using ASLM.Services;
using ASLM.Tests.TestSupport;

namespace ASLM.Tests.Services;

public sealed class SettingsServiceTests
{
    [Theory]
    [InlineData("20000", "30000", true)]
    [InlineData("abc", "30000", false)]
    [InlineData("20000", "99999", false)]
    [InlineData("20050", "20000", false)]
    public void TryParsePorts_validates_ranges_and_overlap(string official, string thirdParty, bool expectedSuccess)
    {
        var result = SettingsService.TryParsePorts(official, thirdParty);

        result.Success.Should().Be(expectedSuccess);
        if (expectedSuccess)
        {
            result.OfficialPort.Should().Be(int.Parse(official));
            result.ThirdPartyPort.Should().Be(int.Parse(thirdParty));
        }
        else
        {
            result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Theory]
    [InlineData(" Alice ", true, "Alice")]
    [InlineData("   ", false, "")]
    public void TryValidateDisplayName_trims_and_rejects_empty(string draft, bool expected, string expectedName)
    {
        var success = SettingsService.TryValidateDisplayName(draft, out var name, out var error);

        success.Should().Be(expected);
        name.Should().Be(expectedName);
        if (!expected)
        {
            error.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void TryValidateAndBuildUpdateSettings_rejects_invalid_period()
    {
        var draft = new UpdateBaseline(
            true,
            false,
            "0",
            "stable",
            "manual",
            "main");

        var success = SettingsService.TryValidateAndBuildUpdateSettings(draft, out _, out var error);

        success.Should().BeFalse();
        error.Should().Contain("720");
    }

    [Fact]
    public void BuildSaveMessage_describes_deferred_settings()
    {
        var message = SettingsService.BuildSaveMessage(
            true,
            false,
            ["Setting A", "Setting B"]);

        message.Should().Contain("could not be applied immediately");
        message.Should().Contain("Setting A");
    }

    [Fact]
    public void BuildAslmDraftSnapshot_reads_app_data()
    {
        _ = new AslmFileSystemLayout();
        var store = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        store.Data.User.Name = "Tester";
        store.Data.Ports.OfficialStart = 21000;
        store.Data.Ports.ThirdPartyStart = 31000;

        var draft = SettingsService.BuildAslmDraftSnapshot(store, apiServerEnabled: true);

        draft.UserName.Should().Be("Tester");
        draft.OfficialPort.Should().Be("21000");
        draft.ApiServerEnabled.Should().BeTrue();
    }

    [Fact]
    public void ApplyDraftsToAppData_persists_values_in_memory()
    {
        _ = new AslmFileSystemLayout();
        var store = new AppDataStore(TestLoggerFactory.Create<AppDataStore>());
        var console = new ConsoleBaseline(false, true, false);
        var updates = new AppUpdateSettings { AutoCheckPeriodHours = 12 };

        SettingsService.ApplyDraftsToAppData(store, "Bob", 22000, 32000, console, updates);

        store.Data.User.Name.Should().Be("Bob");
        store.Data.Ports.OfficialStart.Should().Be(22000);
        store.Data.Consoles.ShowCompletedProcesses.Should().BeTrue();
        store.Data.Updates.AutoCheckPeriodHours.Should().Be(12);
    }

    [Fact]
    public void HasUnsaved_changes_detect_differences()
    {
        var baseline = new AslmBaseline("Alice", "20000", "30000", true);
        SettingsService.HasUnsavedAccountChanges("Bob", baseline).Should().BeTrue();
        SettingsService.HasUnsavedPortChanges("20001", "30000", baseline).Should().BeTrue();
        SettingsService.HasUnsavedApiServerChanges(false, baseline).Should().BeTrue();
    }

    [Fact]
    public void ShouldDisplaySetting_hides_automatic_types()
    {
        SettingsService.ShouldDisplaySetting(new ModuleSetting { Type = "port" }).Should().BeFalse();
        SettingsService.ShouldDisplaySetting(new ModuleSetting { Type = "theme" }).Should().BeFalse();
        SettingsService.ShouldDisplaySetting(new ModuleSetting { Type = "text" }).Should().BeTrue();
    }

    [Fact]
    public void ResetModuleToDefaults_restores_manifest_defaults()
    {
        var module = ModuleConfigBuilder.Create(configure: m =>
        {
            m.Settings =
            [
                new ModuleSetting
                {
                    Key = "flag",
                    Type = "bool",
                    Default = "false",
                    Value = "true",
                    UseCustomValue = true
                }
            ];
        });

        SettingsService.ResetModuleToDefaults(module);

        Convert.ToString(module.Settings[0].Value).Should().Be("False");
    }

    [Fact]
    public void GetGroupForCategory_maps_module_kind()
    {
        var moduleCategory = new SettingsCategory(
            "module::x",
            "X",
            "desc",
            SettingsCategoryKind.Module,
            ModuleConfigBuilder.Create(),
            false);

        SettingsService.GetGroupForCategory(moduleCategory).Should().Be(SettingsCategoryGroup.Modules);
    }
}
