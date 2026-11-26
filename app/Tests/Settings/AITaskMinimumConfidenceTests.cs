using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks when the minimum confidence of an AI task applies.
/// </summary>
/// <remarks>
/// The text content cleaner, the data source selection, and the retrieval context validation apply their
/// provider and their minimum only while their preselection is switched on, like the assistants. The audit
/// of plugins has no such switch, so its minimum always applies. Names are written out in full where a short
/// one would find a namespace of this assembly first, see AIOperationProviderResolverTests.
/// </remarks>
[TestFixture]
public sealed class AITaskMinimumConfidenceTests
{
    [Test]
    public void TheTextContentCleanerAppliesItsMinimumOnlyWithItsPreselection()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.TextContentCleaner.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        AssertMinimumFollowsPreselection(settingsManager, AIStudio.Tools.Components.AGENT_TEXT_CONTENT_CLEANER, on => settingsManager.ConfigurationData.TextContentCleaner.PreselectAgentOptions = on);
    }

    [Test]
    public void TheDataSourceSelectionAppliesItsMinimumOnlyWithItsPreselection()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.AgentDataSourceSelection.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        AssertMinimumFollowsPreselection(settingsManager, AIStudio.Tools.Components.AGENT_DATA_SOURCE_SELECTION, on => settingsManager.ConfigurationData.AgentDataSourceSelection.PreselectAgentOptions = on);
    }

    [Test]
    public void TheRetrievalContextValidationAppliesItsMinimumOnlyWithItsPreselection()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.AgentRetrievalContextValidation.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        AssertMinimumFollowsPreselection(settingsManager, AIStudio.Tools.Components.AGENT_RETRIEVAL_CONTEXT_VALIDATION, on => settingsManager.ConfigurationData.AgentRetrievalContextValidation.PreselectAgentOptions = on);
    }

    [Test]
    public void ThePluginAuditAlwaysAppliesItsMinimum()
    {
        var settingsManager = CreateSettingsManager();
        settingsManager.ConfigurationData.AssistantPluginAudit.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        Assert.That(AIStudio.Tools.ComponentsExtensions.MinimumConfidence(AIStudio.Tools.Components.AGENT_ASSISTANT_PLUGIN_AUDIT, settingsManager), Is.EqualTo(ConfidenceLevel.HIGH), "The audit has no preselection switch, so nothing may turn its minimum off.");
    }

    private static void AssertMinimumFollowsPreselection(SettingsManager settingsManager, AIStudio.Tools.Components component, Action<bool> setPreselection)
    {
        setPreselection(false);
        var minimumWithoutPreselection = AIStudio.Tools.ComponentsExtensions.MinimumConfidence(component, settingsManager);

        setPreselection(true);
        var minimumWithPreselection = AIStudio.Tools.ComponentsExtensions.MinimumConfidence(component, settingsManager);

        Assert.Multiple(() =>
        {
            Assert.That(minimumWithoutPreselection, Is.EqualTo(ConfidenceLevel.NONE), "With the preselection switched off, the stored minimum must not apply.");
            Assert.That(minimumWithPreselection, Is.EqualTo(ConfidenceLevel.HIGH));
        });
    }

    /// <summary>
    /// Builds a settings manager without the rust service, which reading a minimum never asks.
    /// </summary>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);
}