using AIStudio.AIOperations;
using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks that the resolver gathers the facts about the candidates from the settings.
/// </summary>
/// <remarks>
/// AIOperationProviderChoiceTests covers the decision itself. Here, it is about what the resolver reads:
/// a deleted provider, a prohibition of the organization, the global minimum, the check of the caller, and
/// the app-wide default with its shortcut for a single provider. Names are written out in full where a
/// short one would find a namespace of this assembly first, see PreselectedProviderTests.
/// </remarks>
[TestFixture]
public sealed class AIOperationProviderResolverTests
{
    private const string OWN_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string FALLBACK_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";
    private const string APP_DEFAULT_PROVIDER_ID = "33333333-3333-3333-3333-333333333333";
    private const string DELETED_PROVIDER_ID = "44444444-4444-4444-4444-444444444444";

    [Test]
    public void APickedProviderWhichWasDeletedIsMissing()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, FALLBACK_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.SELF_HOSTED);

        var choice = CreateResolver(settingsManager).Preview(new(DELETED_PROVIDER_ID, ConfidenceLevel.NONE), ConfidenceLevel.NONE, new() { FallbackProviderIds = [FALLBACK_PROVIDER_ID] });

        Assert.Multiple(() =>
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(FALLBACK_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.MISSING), "A provider which was picked and deleted since is worth a hint, unlike one nobody picked.");
        });
    }

    [Test]
    public void AProviderOfNoneCountsAsNotPicked()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, FALLBACK_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.SELF_HOSTED);

        var choice = CreateResolver(settingsManager).Preview(new(AIStudio.Settings.Provider.NONE.Id, ConfidenceLevel.NONE), ConfidenceLevel.NONE, new() { FallbackProviderIds = [FALLBACK_PROVIDER_ID] });

        Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_SET), "Some settings store the ID of Provider.NONE for an empty choice.");
    }

    [Test]
    public void AProhibitionOfTheOrganizationIsRead()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, FALLBACK_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        settingsManager.ConfigurationData.App.ProhibitedProviders.Add(LLMProviders.OPEN_AI);

        var choice = CreateResolver(settingsManager).Preview(new(OWN_PROVIDER_ID, ConfidenceLevel.NONE), ConfidenceLevel.NONE, new() { FallbackProviderIds = [FALLBACK_PROVIDER_ID] });

        Assert.Multiple(() =>
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(FALLBACK_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.PROHIBITED));
        });
    }

    [Test]
    public void TheGlobalMinimumIsRead()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, FALLBACK_PROVIDER_ID, LLMProviders.OPEN_AI);
        settingsManager.ConfigurationData.Confidence.EnforceGlobalMinimumConfidence = true;
        settingsManager.ConfigurationData.Confidence.GlobalMinimumConfidence = ConfidenceLevel.HIGH;

        var choice = CreateResolver(settingsManager).Preview(new(OWN_PROVIDER_ID, ConfidenceLevel.NONE), ConfidenceLevel.NONE, new() { FallbackProviderIds = [FALLBACK_PROVIDER_ID], OwnMinimumConfidenceAppliesToFallbacks = false });

        Assert.Multiple(() =>
        {
            Assert.That(choice.HasProvider, Is.False, "What the organization enforces leaves no fallback out, even one exempt from the minimum of the operation.");
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE));
            Assert.That(choice.FallbackIssue, Is.EqualTo(AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE));
        });
    }

    [Test]
    public void TheCheckOfTheCallerSeesEveryConfiguredCandidate()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, FALLBACK_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        var checkedIds = new List<string>();

        var choice = CreateResolver(settingsManager).Preview(new(OWN_PROVIDER_ID, ConfidenceLevel.NONE), ConfidenceLevel.NONE, new()
        {
            FallbackProviderIds = [DELETED_PROVIDER_ID, FALLBACK_PROVIDER_ID],
            CheckProvider = provider =>
            {
                checkedIds.Add(provider.Id);
                return provider.Id == OWN_PROVIDER_ID ? AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT : AIOperationProviderIssue.NONE;
            },
        });

        Assert.Multiple(() =>
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(FALLBACK_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT));
            Assert.That(checkedIds, Is.EqualTo(new[] { OWN_PROVIDER_ID, FALLBACK_PROVIDER_ID }), "The check gets the configured providers only, never a missing one.");
        });
    }

    [Test]
    public void TheAppDefaultIsTheOneTheSettingsName()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, FALLBACK_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        settingsManager.ConfigurationData.App.PreselectedProvider = APP_DEFAULT_PROVIDER_ID;

        var choice = CreateResolver(settingsManager).Preview(AIOperationProviderSettings.NONE, ConfidenceLevel.NONE, new() { FallbackProviderIds = [null, settingsManager.AppDefaultProviderId] });

        Assert.That(choice.Provider.Id, Is.EqualTo(APP_DEFAULT_PROVIDER_ID));
    }

    [Test]
    public void TheOnlyProviderIsTheAppDefault()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, FALLBACK_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        settingsManager.ConfigurationData.App.PreselectedProvider = DELETED_PROVIDER_ID;

        Assert.That(settingsManager.AppDefaultProviderId, Is.EqualTo(FALLBACK_PROVIDER_ID), "With a single provider, there is nothing else to choose, as in GetPreselectedProvider.");
    }

    [Test]
    public void WithoutADefaultThereIsNoAppDefault()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, FALLBACK_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        AddProvider(settingsManager, APP_DEFAULT_PROVIDER_ID, LLMProviders.SELF_HOSTED);

        var choice = CreateResolver(settingsManager).Preview(AIOperationProviderSettings.NONE, ConfidenceLevel.NONE, new() { FallbackProviderIds = [settingsManager.AppDefaultProviderId] });

        Assert.Multiple(() =>
        {
            Assert.That(choice.HasProvider, Is.False, "Two configured providers are not a choice.");
            Assert.That(choice.FallbackIssue, Is.EqualTo(AIOperationProviderIssue.NOT_SET));
        });
    }

    /// <summary>
    /// Builds a settings manager the way these tests need it.
    /// </summary>
    /// <remarks>
    /// The rust service is handed in as null on purpose: choosing a provider never asks it.
    /// </remarks>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    private static AIOperationProviderResolver CreateResolver(SettingsManager settingsManager) => new(settingsManager, NullLogger<AIOperationProviderResolver>.Instance);

    private static void AddProvider(SettingsManager settingsManager, string id, LLMProviders llmProvider)
    {
        var providers = settingsManager.ConfigurationData.Providers;
        providers.Add(new((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null)));
    }
}
