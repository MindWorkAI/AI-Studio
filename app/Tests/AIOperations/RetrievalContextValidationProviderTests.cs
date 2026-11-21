using AIStudio.AIOperations;
using AIStudio.AIOperations.RetrievalContextValidation;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks which provider validates the passages which the classic RAG process found.
/// </summary>
/// <remarks>
/// The provider reads the passages, so it has to meet what the chat requires of every provider which reads
/// its data: the data security and the confidence. A provider which does not falls back on the provider of
/// the chat. Names are written out in full where a short one would find a namespace of this assembly first.
/// </remarks>
[TestFixture]
public sealed class RetrievalContextValidationProviderTests
{
    private const string CHAT_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string OWN_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";

    [Test]
    public void PrefersItsOwnProvider()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        PickOwnProvider(settingsManager);

        var choice = Choose(settingsManager, DataSourceSecurity.ALLOW_ANY, ConfidenceLevel.NONE);

        Assert.That(choice.Provider.Id, Is.EqualTo(OWN_PROVIDER_ID));
    }

    [Test]
    public void AnOwnProviderRefusedByTheDataSecurityOfTheChatFallsBack()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.SELF_HOSTED, isSelfHosted: true);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        PickOwnProvider(settingsManager);

        var choice = Choose(settingsManager, DataSourceSecurity.SELF_HOSTED, ConfidenceLevel.NONE);

        Assert.Multiple(() =>
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID), "Passages from a server for self-hosted providers must not reach a cloud provider through the validation.");
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT));
        });
    }

    [Test]
    public void AnOwnProviderBelowTheConfidenceOfTheChatFallsBack()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.SELF_HOSTED, isSelfHosted: true);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        PickOwnProvider(settingsManager);

        var choice = Choose(settingsManager, DataSourceSecurity.ALLOW_ANY, ConfidenceLevel.HIGH);

        Assert.Multiple(() =>
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT));
        });
    }

    private static AIOperationProviderChoice Choose(SettingsManager settingsManager, DataSourceSecurity dataSecurity, ConfidenceLevel requiredConfidence)
    {
        var resolver = new AIOperationProviderResolver(settingsManager, NullLogger<AIOperationProviderResolver>.Instance);
        return resolver.Preview(RetrievalContextValidationOperation.GetProviderSettings(settingsManager), requiredConfidence, ClassicRagProviderOptions.ForDataAccess(settingsManager, CHAT_PROVIDER_ID, dataSecurity, requiredConfidence));
    }

    private static void PickOwnProvider(SettingsManager settingsManager)
    {
        settingsManager.ConfigurationData.AgentRetrievalContextValidation.PreselectAgentOptions = true;
        settingsManager.ConfigurationData.AgentRetrievalContextValidation.PreselectedAgentProvider = OWN_PROVIDER_ID;
    }

    /// <summary>
    /// Builds a settings manager without the rust service, which choosing a provider never asks.
    /// </summary>
    private static SettingsManager CreateSettingsManager() => new(NullLogger<SettingsManager>.Instance, null!);

    private static void AddProvider(SettingsManager settingsManager, string id, LLMProviders llmProvider, bool isSelfHosted = false)
    {
        var providers = settingsManager.ConfigurationData.Providers;
        providers.Add(new((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null), IsSelfHosted: isSelfHosted));
    }
}
