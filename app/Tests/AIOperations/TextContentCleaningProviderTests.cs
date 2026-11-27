using AIStudio.AIOperations;
using AIStudio.AIOperations.TextContentCleaning;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks which provider the text content cleaner is handed when a web page is read in an assistant.
/// </summary>
/// <remarks>
/// The cleaner may be given a model of its own, because a small and cheap one is enough for what it does.
/// Almost nobody does that, so what matters in practice is what happens when it has none: the model of the
/// assistant it sits in, the app-wide default, or nothing at all. Two bugs shipped in that fallback, which is
/// why it is pinned down here, with the options the reader passes. The component lifecycle itself is not
/// covered; there is no bUnit in this solution. Names are written out in full where a short one would find a
/// namespace of this assembly first, see AIOperationProviderResolverTests.
/// </remarks>
[TestFixture]
public sealed class TextContentCleaningProviderTests
{
    private const string ASSISTANT_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string CLEANER_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";
    private const string APP_DEFAULT_PROVIDER_ID = "33333333-3333-3333-3333-333333333333";

    [Test]
    public void FallsBackToTheAssistantProvider()
    {
        var settingsManager = CreateSettingsManager();
        var assistantProvider = AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, CLEANER_PROVIDER_ID, LLMProviders.MISTRAL);

        var choice = Choose(settingsManager, assistantProvider);

        Assert.That(choice.Provider.Id, Is.EqualTo(ASSISTANT_PROVIDER_ID), "With nothing configured for the cleaner, it has to use the model of the assistant around it.");
    }

    [Test]
    public void FallsBackToTheAppDefault()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;

        var choice = Choose(settingsManager, AIStudio.Settings.Provider.NONE);

        Assert.That(choice.Provider.Id, Is.EqualTo(APP_DEFAULT_PROVIDER_ID), "Without an assistant model, the app-wide default is what is left before giving up.");
    }

    [Test]
    public void PrefersItsOwnProviderOverTheAssistantOne()
    {
        var settingsManager = CreateSettingsManager();
        var assistantProvider = AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, CLEANER_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.TextContentCleaner.PreselectOptions = true;
        settingsManager.ConfigurationData.TextContentCleaner.PreselectedProvider = CLEANER_PROVIDER_ID;

        var choice = Choose(settingsManager, assistantProvider);

        Assert.That(choice.Provider.Id, Is.EqualTo(CLEANER_PROVIDER_ID), "A model picked for the cleaner is the whole point of picking one, so it outranks the assistant's.");
    }

    /// <summary>
    /// Checks that the switch above the cleaner's provider field really turns that provider off.
    /// </summary>
    /// <remarks>
    /// The provider id stays in the settings when the switch goes off, so the only thing saying it
    /// must not be used is this one flag. Settings which hand out the stored id instead of asking the
    /// flag would keep using a provider the user switched away from.
    /// </remarks>
    [Test]
    public void IgnoresItsOwnProviderWhenPreselectionIsOff()
    {
        var settingsManager = CreateSettingsManager();
        var assistantProvider = AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, CLEANER_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.TextContentCleaner.PreselectOptions = false;
        settingsManager.ConfigurationData.TextContentCleaner.PreselectedProvider = CLEANER_PROVIDER_ID;

        var choice = Choose(settingsManager, assistantProvider);

        Assert.That(choice.Provider.Id, Is.EqualTo(ASSISTANT_PROVIDER_ID), "With its preselection switched off, the cleaner has to fall back to the assistant's model.");
    }

    /// <summary>
    /// Checks that a model too untrusted for the cleaner is not used just because it is there.
    /// </summary>
    /// <remarks>
    /// This is the case the user meets as a hint next to the cleaner switch: a model is selected in
    /// the assistant, and the cleaner still has none. Under TRUST_ALL every provider reaches MEDIUM
    /// and a self-hosted one reaches HIGH, so a minimum of HIGH separates the two.
    /// </remarks>
    [Test]
    public void RejectsAnAssistantProviderBelowTheGlobalMinimum()
    {
        var settingsManager = CreateSettingsManager();
        var assistantProvider = AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, CLEANER_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        settingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var choice = Choose(settingsManager, assistantProvider);

        Assert.Multiple(() =>
        {
            Assert.That(choice.HasProvider, Is.False, "A provider the organization ruled out must not reach the cleaner through the assistant.");
            Assert.That(choice.FallbackIssue, Is.EqualTo(AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE), "The hint next to the switch tells this reason from a missing model.");
        });
    }

    [Test]
    public void AcceptsAnAssistantProviderMeetingTheGlobalMinimum()
    {
        var settingsManager = CreateSettingsManager();
        var assistantProvider = AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, CLEANER_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        settingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var choice = Choose(settingsManager, assistantProvider);

        Assert.That(choice.Provider.Id, Is.EqualTo(ASSISTANT_PROVIDER_ID), "A provider which clears the bar has to be handed over, or the hint would never go away.");
    }

    /// <summary>
    /// Checks that the minimum of the cleaner itself applies to the model of the assistant as well.
    /// </summary>
    /// <remarks>
    /// Whether a model may read the page is decided by what the cleaner requires, not by what the model was
    /// picked for. Otherwise a model ruled out for the cleaner would clean the page anyway.
    /// </remarks>
    [Test]
    public void ItsOwnMinimumAppliesToTheAssistantProvider()
    {
        var settingsManager = CreateSettingsManager();
        var assistantProvider = AddProvider(settingsManager, ASSISTANT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, CLEANER_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.TextContentCleaner.PreselectOptions = true;
        settingsManager.ConfigurationData.TextContentCleaner.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        var choice = Choose(settingsManager, assistantProvider);

        Assert.That(choice.HasProvider, Is.False);
    }

    private static AIOperationProviderChoice Choose(SettingsManager settingsManager, AIStudio.Settings.Provider assistantProvider)
    {
        var resolver = new AIOperationProviderResolver(settingsManager, NullLogger<AIOperationProviderResolver>.Instance);
        return resolver.Preview(TextContentCleaningOperation.GetProviderSettings(settingsManager), ConfidenceLevel.NONE, AIStudio.Components.ReadWebContent.GetCleanerProviderOptions(settingsManager, assistantProvider));
    }

    /// <summary>
    /// Builds a settings manager without the rust service, which choosing a provider never asks.
    /// </summary>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    /// <summary>
    /// Adds a provider to the settings.
    /// </summary>
    /// <remarks>
    /// Every test here configures at least two of them, and not for variety: with exactly one configured
    /// provider, it is the app-wide default whatever the settings name. A single-provider test would pass no
    /// matter what the fallback does.
    /// </remarks>
    private static AIStudio.Settings.Provider AddProvider(SettingsManager settingsManager, string id, LLMProviders llmProvider)
    {
        var providers = settingsManager.ConfigurationData.Providers;
        var provider = new AIStudio.Settings.Provider((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null));
        providers.Add(provider);
        return provider;
    }
}