using AIStudio.AIOperations;
using AIStudio.AIOperations.PluginAudit;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem.Assistants;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks which provider audits a plugin.
/// </summary>
/// <remarks>
/// The audit may be given a model of its own, which an organization rolls out. Without one, it falls back on
/// the app-wide default, and then on the provider at hand: the one picked in the audit dialog, or for the
/// assistant being built. Names are written out in full where a short one would find a namespace of this
/// assembly first, see AIOperationProviderResolverTests.
/// </remarks>
[TestFixture]
public sealed class PluginAuditProviderTests
{
    private const string AT_HAND_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string AUDIT_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";
    private const string APP_DEFAULT_PROVIDER_ID = "33333333-3333-3333-3333-333333333333";

    /// <summary>
    /// Checks that the audit's provider needs no switch to be used.
    /// </summary>
    /// <remarks>
    /// Unlike the content cleaner, the audit has no "preselect options" flag: an organization rolls its
    /// provider out and that is what audits run with. A test which assumed the two behaved alike would
    /// pass here for the wrong reason.
    /// </remarks>
    [Test]
    public void UsesItsOwnProviderWithoutAPreselectionSwitch()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, AUDIT_PROVIDER_ID, LLMProviders.MISTRAL);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.OPEN_AI);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;
        settingsManager.ConfigurationData.AssistantPluginAudit.PreselectedProvider = AUDIT_PROVIDER_ID;

        var choice = Choose(settingsManager, null);

        Assert.That(choice.Provider.Id, Is.EqualTo(AUDIT_PROVIDER_ID), "A provider rolled out for audits outranks the app-wide default.");
    }

    [Test]
    public void FallsBackToTheAppDefault()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, AT_HAND_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;

        var choice = Choose(settingsManager, null);

        Assert.That(choice.Provider.Id, Is.EqualTo(APP_DEFAULT_PROVIDER_ID), "Without a dedicated audit provider, the app-wide default is what audits run with.");
    }

    /// <summary>
    /// Checks the state the audit dialog was useless in before it offered a provider itself.
    /// </summary>
    [Test]
    public void EndsUpWithNothingWhenNeitherIsConfigured()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, AT_HAND_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, AUDIT_PROVIDER_ID, LLMProviders.MISTRAL);

        var choice = Choose(settingsManager, null);

        Assert.Multiple(() =>
        {
            Assert.That(choice.HasProvider, Is.False, "Two configured providers are not a choice: without one being named for audits, the audit has none.");
            Assert.That(AssistantPluginAuditService.GetRelevantIssue(choice), Is.EqualTo(AIOperationProviderIssue.NOT_SET), "The dialog then says that nothing is set, not that something is wrong.");
        });
    }

    [Test]
    public void RejectsAnAppDefaultBelowTheGlobalMinimum()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, AT_HAND_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.OPEN_AI);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;
        settingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        settingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var choice = Choose(settingsManager, null);

        Assert.Multiple(() =>
        {
            Assert.That(choice.HasProvider, Is.False, "The app-wide default is not exempt from what the organization enforces.");
            Assert.That(AssistantPluginAuditService.GetRelevantIssue(choice), Is.EqualTo(AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE));
        });
    }

    [Test]
    public void FallsBackToTheProviderAtHandLast()
    {
        var settingsManager = CreateSettingsManager();
        var providerAtHand = AddProvider(settingsManager, AT_HAND_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, AUDIT_PROVIDER_ID, LLMProviders.MISTRAL);

        var choice = Choose(settingsManager, providerAtHand);

        Assert.That(choice.Provider.Id, Is.EqualTo(AT_HAND_PROVIDER_ID));
    }

    [Test]
    public void TheAppDefaultComesBeforeTheProviderAtHand()
    {
        var settingsManager = CreateSettingsManager();
        var providerAtHand = AddProvider(settingsManager, AT_HAND_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;

        var choice = Choose(settingsManager, providerAtHand);

        Assert.That(choice.Provider.Id, Is.EqualTo(APP_DEFAULT_PROVIDER_ID));
    }

    /// <summary>
    /// Checks that a provider picked for something else clears the bar of the audit before it reads a plugin.
    /// </summary>
    [Test]
    public void ItsOwnMinimumAppliesToTheProviderAtHand()
    {
        var settingsManager = CreateSettingsManager();
        var providerAtHand = AddProvider(settingsManager, AT_HAND_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, AUDIT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.AssistantPluginAudit.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        var choice = Choose(settingsManager, providerAtHand);

        Assert.Multiple(() =>
        {
            Assert.That(choice.HasProvider, Is.False, "A provider an organization ruled out for audits must not read a plugin because it was at hand.");
            Assert.That(AssistantPluginAuditService.GetRelevantIssue(choice), Is.EqualTo(AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE));
        });
    }

    private static AIOperationProviderChoice Choose(SettingsManager settingsManager, AIStudio.Settings.Provider? providerAtHand)
    {
        var resolver = new AIOperationProviderResolver(settingsManager, NullLogger<AIOperationProviderResolver>.Instance);
        return resolver.Preview(PluginAuditOperation.GetProviderSettings(settingsManager), ConfidenceLevel.NONE, AssistantPluginAuditService.GetProviderOptions(settingsManager, providerAtHand));
    }

    /// <summary>
    /// Builds a settings manager without the rust service, which choosing a provider never asks.
    /// </summary>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    /// <summary>
    /// Adds a provider to the settings.
    /// </summary>
    /// <remarks>
    /// Every test here configures at least two of them: with exactly one configured provider, it is the
    /// app-wide default whatever the settings name, and a test would pass no matter what the fallback does.
    /// </remarks>
    private static AIStudio.Settings.Provider AddProvider(SettingsManager settingsManager, string id, LLMProviders llmProvider)
    {
        var providers = settingsManager.ConfigurationData.Providers;
        var provider = new AIStudio.Settings.Provider((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null));
        providers.Add(provider);
        return provider;
    }
}