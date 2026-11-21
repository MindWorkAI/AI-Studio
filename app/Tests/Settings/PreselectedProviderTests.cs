using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which provider an agent is handed when nobody picked one for it.
/// </summary>
/// <remarks>
/// Agents such as the security audit may be given a model of their own, because a small and
/// cheap one is enough for what they do. Almost nobody does that, so what matters in practice is
/// what happens when they have none: the app-wide default, or nothing at all. The text content
/// cleaner chooses its provider as an AI operation; TextContentCleaningProviderTests covers it.
///
/// Names are written out in full throughout. This assembly has an AIStudio.Tests.Tools and an
/// AIStudio.Tests.Provider of its own, and the app has an AIStudio.Components -- all three are
/// what a short name finds from here, and a using alias does not help, because names from the
/// enclosing namespaces win over it.
/// </remarks>
[TestFixture]
public sealed class PreselectedProviderTests
{
    private const string ASSISTANT_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string AGENT_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";
    private const string APP_DEFAULT_PROVIDER_ID = "33333333-3333-3333-3333-333333333333";

    /// <summary>
    /// Checks that the audit agent's provider needs no switch to be used.
    /// </summary>
    /// <remarks>
    /// Unlike the content cleaner, the audit agent has no "preselect options" flag: an organization
    /// rolls its provider out and that is what audits run with. A test which assumed the two agents
    /// behaved alike would pass here for the wrong reason.
    /// </remarks>
    [Test]
    public void AuditAgentUsesItsOwnProviderWithoutAPreselectionSwitch()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, AGENT_PROVIDER_ID, LLMProviders.MISTRAL);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.OPEN_AI);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;
        settingsManager.ConfigurationData.AssistantPluginAudit.PreselectedAgentProvider = AGENT_PROVIDER_ID;

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_ASSISTANT_PLUGIN_AUDIT, null, true);

        Assert.That(provider.Id, Is.EqualTo(AGENT_PROVIDER_ID), "A provider rolled out for audits outranks the app-wide default.");
    }

    [Test]
    public void AuditAgentFallsBackToTheAppDefault()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_ASSISTANT_PLUGIN_AUDIT, null, true);

        Assert.That(provider.Id, Is.EqualTo(APP_DEFAULT_PROVIDER_ID), "Without a dedicated audit provider, the app-wide default is what audits run with.");
    }

    /// <summary>
    /// Checks the state the audit dialog was useless in before it offered a provider itself.
    /// </summary>
    [Test]
    public void AuditAgentEndsUpWithNothingWhenNeitherIsConfigured()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, AGENT_PROVIDER_ID, LLMProviders.MISTRAL);

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_ASSISTANT_PLUGIN_AUDIT, null, true);

        Assert.That(provider, Is.EqualTo(AIStudio.Settings.Provider.NONE), "Two configured providers are not a choice: without one being named for audits, the agent has none.");
    }

    [Test]
    public void AuditAgentRejectsAnAppDefaultBelowTheGlobalMinimum()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.OPEN_AI);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;
        settingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        settingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_ASSISTANT_PLUGIN_AUDIT, null, true);

        Assert.That(provider, Is.EqualTo(AIStudio.Settings.Provider.NONE), "The app-wide default is not exempt from what the organization enforces.");
    }

    /// <summary>
    /// Builds a settings manager the way these tests need it.
    /// </summary>
    /// <remarks>
    /// The rust service is handed in as null on purpose: resolving a provider never asks it
    /// anything. It reads the configured providers, the confidence scheme and the preselections,
    /// all of which are plain settings. Should that change, the test says so by failing loudly
    /// rather than by quietly measuring something else.
    /// </remarks>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    /// <summary>
    /// Adds a provider to the settings.
    /// </summary>
    /// <remarks>
    /// Every test here configures at least two of them, and not for variety: with exactly one
    /// configured provider, resolving takes a shortcut and returns it without looking at any
    /// preselection. A single-provider test would pass no matter what the fallback does.
    /// </remarks>
    private static void AddProvider(SettingsManager settingsManager, string id, LLMProviders llmProvider)
    {
        var providers = settingsManager.ConfigurationData.Providers;
        providers.Add(new((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null)));
    }
}