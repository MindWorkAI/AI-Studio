using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Settings;

/// <summary>
/// Checks which provider an agent is handed when nobody picked one for it.
/// </summary>
/// <remarks>
/// Agents such as the content cleaner or the security audit may be given a model of their own,
/// because a small and cheap one is enough for what they do. Almost nobody does that, so what
/// matters in practice is what happens when they have none: the model of the assistant they sit
/// in, the app-wide default, or nothing at all. Two bugs shipped in that fallback, and both of
/// them lived here rather than in the components -- which is why this is where they are pinned
/// down. The component lifecycle itself is not covered; there is no bUnit in this solution.
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

    [Test]
    public void ContentCleanerFallsBackToTheAssistantProvider()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, AGENT_PROVIDER_ID, LLMProviders.MISTRAL);

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_TEXT_CONTENT_CLEANER, ASSISTANT_PROVIDER_ID, true);

        Assert.That(provider.Id, Is.EqualTo(ASSISTANT_PROVIDER_ID), "With nothing configured for the cleaner, it has to use the model of the assistant around it.");
    }

    [Test]
    public void ContentCleanerFallsBackToTheAppDefault()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_TEXT_CONTENT_CLEANER, null, true);

        Assert.That(provider.Id, Is.EqualTo(APP_DEFAULT_PROVIDER_ID), "Without an assistant model, the app-wide default is what is left before giving up.");
    }

    [Test]
    public void ContentCleanerPrefersItsOwnProviderOverTheAssistantOne()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, AGENT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.TextContentCleaner.PreselectAgentOptions = true;
        settingsManager.ConfigurationData.TextContentCleaner.PreselectedAgentProvider = AGENT_PROVIDER_ID;

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_TEXT_CONTENT_CLEANER, ASSISTANT_PROVIDER_ID, true);

        Assert.That(provider.Id, Is.EqualTo(AGENT_PROVIDER_ID), "A model picked for the cleaner is the whole point of picking one, so it outranks the assistant's.");
    }

    /// <summary>
    /// Checks that the switch above the cleaner's provider field really turns that provider off.
    /// </summary>
    /// <remarks>
    /// The provider id stays in the settings when the switch goes off, so the only thing saying it
    /// must not be used is this one flag. A component which reads the stored id instead of asking
    /// here would keep using a provider the user switched away from.
    /// </remarks>
    [Test]
    public void ContentCleanerIgnoresItsOwnProviderWhenPreselectionIsOff()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, AGENT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.TextContentCleaner.PreselectAgentOptions = false;
        settingsManager.ConfigurationData.TextContentCleaner.PreselectedAgentProvider = AGENT_PROVIDER_ID;

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_TEXT_CONTENT_CLEANER, ASSISTANT_PROVIDER_ID, true);

        Assert.That(provider.Id, Is.EqualTo(ASSISTANT_PROVIDER_ID), "With its preselection switched off, the cleaner has to fall back to the assistant's model.");
    }

    /// <summary>
    /// Checks that a model too untrusted for the cleaner is not used just because it is there.
    /// </summary>
    /// <remarks>
    /// This is the case the user meets as a hint next to the cleaner switch: a model is selected in
    /// the assistant, and the cleaner still has none. Under TRUST_ALL every provider reaches MEDIUM
    /// and a self-hosted one reaches HIGH, so a global minimum of HIGH separates the two.
    /// </remarks>
    [Test]
    public void ContentCleanerRejectsAnAssistantProviderBelowTheGlobalMinimum()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, AGENT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        settingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_TEXT_CONTENT_CLEANER, ASSISTANT_PROVIDER_ID, true);

        Assert.That(provider, Is.EqualTo(AIStudio.Settings.Provider.NONE), "A provider the organization ruled out must not reach the cleaner through the assistant.");
    }

    [Test]
    public void ContentCleanerAcceptsAnAssistantProviderMeetingTheGlobalMinimum()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, AGENT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        settingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var provider = settingsManager.GetPreselectedProvider(AIStudio.Tools.Components.AGENT_TEXT_CONTENT_CLEANER, ASSISTANT_PROVIDER_ID, true);

        Assert.That(provider.Id, Is.EqualTo(ASSISTANT_PROVIDER_ID), "A provider which clears the bar has to be handed over, or the hint would never go away.");
    }

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