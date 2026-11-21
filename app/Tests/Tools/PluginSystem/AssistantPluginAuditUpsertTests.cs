using AIStudio.Settings;
using AIStudio.Tools.PluginSystem.Assistants;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks how the settings keep the audits of assistant plugins.
/// </summary>
/// <remarks>
/// The settings keep one audit per plugin. A newer audit takes the place of the older one, also when it
/// checked another version of the plugin, and the audits of the other plugins keep their places. Every place
/// which stores an audit goes through AssistantPluginAuditService.UpsertAudit.
/// </remarks>
[TestFixture]
public sealed class AssistantPluginAuditUpsertTests
{
    private static readonly Guid FIRST_PLUGIN_ID = Guid.Parse("6d2f8a41-3c7e-4b9a-9e15-0a8c4f27d613");

    private static readonly Guid SECOND_PLUGIN_ID = Guid.Parse("b84e1c90-5f2a-4d63-8a7b-2e9d0c4f6a18");

    private static readonly Guid THIRD_PLUGIN_ID = Guid.Parse("1fa7c3d5-8e64-4b02-b9c1-7d5e2a0f8b34");

    [Test]
    public void AnAuditForANewPluginJoinsTheOthers()
    {
        var settingsManager = CreateSettingsManager();
        var firstAudit = CreateAudit(FIRST_PLUGIN_ID, "hash-of-the-first-plugin");
        var secondAudit = CreateAudit(SECOND_PLUGIN_ID, "hash-of-the-second-plugin");

        AssistantPluginAuditService.UpsertAudit(settingsManager, firstAudit);
        AssistantPluginAuditService.UpsertAudit(settingsManager, secondAudit);

        Assert.That(settingsManager.ConfigurationData.AssistantPluginAudits, Is.EqualTo(new[] { firstAudit, secondAudit }), "An audit of a plugin without one joins the audits at their end.");
    }

    [Test]
    public void ANewerAuditReplacesTheOldOneInItsPlace()
    {
        var settingsManager = CreateSettingsManager();
        var firstAudit = CreateAudit(FIRST_PLUGIN_ID, "hash-of-the-first-plugin");
        var olderSecondAudit = CreateAudit(SECOND_PLUGIN_ID, "hash-of-the-second-plugin");
        var thirdAudit = CreateAudit(THIRD_PLUGIN_ID, "hash-of-the-third-plugin");
        AssistantPluginAuditService.UpsertAudit(settingsManager, firstAudit);
        AssistantPluginAuditService.UpsertAudit(settingsManager, olderSecondAudit);
        AssistantPluginAuditService.UpsertAudit(settingsManager, thirdAudit);

        var newerSecondAudit = CreateAudit(SECOND_PLUGIN_ID, "hash-of-the-second-plugin");
        AssistantPluginAuditService.UpsertAudit(settingsManager, newerSecondAudit);

        Assert.That(settingsManager.ConfigurationData.AssistantPluginAudits, Is.EqualTo(new[] { firstAudit, newerSecondAudit, thirdAudit }), "A newer audit takes the place of the older one, so the order of the audits stays as it was.");
    }

    [Test]
    public void ANewerAuditReplacesTheOldOneOfAnotherVersion()
    {
        var settingsManager = CreateSettingsManager();
        var auditOfTheFirstVersion = CreateAudit(FIRST_PLUGIN_ID, "hash-of-the-first-version");
        var auditOfTheSecondVersion = CreateAudit(FIRST_PLUGIN_ID, "hash-of-the-second-version");

        AssistantPluginAuditService.UpsertAudit(settingsManager, auditOfTheFirstVersion);
        AssistantPluginAuditService.UpsertAudit(settingsManager, auditOfTheSecondVersion);

        Assert.That(settingsManager.ConfigurationData.AssistantPluginAudits, Is.EqualTo(new[] { auditOfTheSecondVersion }), "The settings keep one audit per plugin, not one per version of it.");
    }

    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    private static PluginAssistantAudit CreateAudit(Guid pluginId, string pluginHash) => new()
    {
        PluginId = pluginId,
        PluginHash = pluginHash,
    };
}