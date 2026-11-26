using AIStudio.AIOperations;
using AIStudio.AIOperations.DataSourceChoice;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.RAG.DataSourceSelectionProcesses;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks which provider selects the data sources in the classic RAG process.
/// </summary>
/// <remarks>
/// The provider reads the descriptions of the data sources, and what it chooses decides what is searched.
/// It therefore has to be allowed to see them, by their security and by the confidence they require, and a
/// provider which is not falls back on the provider of the chat, which the chat already checked. Names are
/// written out in full where a short one would find a namespace of this assembly first.
/// </remarks>
[TestFixture]
public sealed class DataSourceChoiceProviderTests
{
    private const string CHAT_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string OWN_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";

    [Test]
    public void FallsBackToTheChatProvider()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);

        var choice = Choose(settingsManager, [OpenServer()]);

        Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
    }

    [Test]
    public void PrefersItsOwnProvider()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        PickOwnProvider(settingsManager);

        var choice = Choose(settingsManager, [OpenServer()]);

        Assert.That(choice.Provider.Id, Is.EqualTo(OWN_PROVIDER_ID));
    }

    /// <summary>
    /// Checks that a provider which must not see a data source falls back instead of ending the selection.
    /// </summary>
    /// <remarks>
    /// A server for self-hosted providers refuses a cloud provider. The provider of the chat, a self-hosted
    /// one, may see it, and the chat already uses it for the very same data.
    /// </remarks>
    [Test]
    public void AnOwnProviderRefusedByTheSecurityOfADataSourceFallsBack()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.SELF_HOSTED, isSelfHosted: true);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        PickOwnProvider(settingsManager);

        var choice = Choose(settingsManager, [SelfHostedServer()]);

        Assert.Multiple(() =>
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT));
        });
    }

    [Test]
    public void AnOwnProviderBelowWhatADataSourceRequiresFallsBack()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.SELF_HOSTED, isSelfHosted: true);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        PickOwnProvider(settingsManager);

        var choice = Choose(settingsManager, [LocalDirectory(ConfidenceLevel.HIGH)]);

        Assert.Multiple(() =>
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT));
        });
    }

    [Test]
    public void NoProviderWhenNoneMaySeeTheDataSources()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        PickOwnProvider(settingsManager);

        var choice = Choose(settingsManager, [SelfHostedServer()]);

        Assert.Multiple(() =>
        {
            Assert.That(choice.HasProvider, Is.False, "No provider may read the descriptions of a server it must not see.");
            Assert.That(choice.FallbackIssue, Is.EqualTo(AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT));
        });
    }

    [Test]
    public void ItsOwnMinimumAppliesToTheChatProvider()
    {
        var settingsManager = CreateSettingsManager();
        AddProvider(settingsManager, CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        AddProvider(settingsManager, OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        settingsManager.ConfigurationData.AgentDataSourceSelection.PreselectAgentOptions = true;
        settingsManager.ConfigurationData.AgentDataSourceSelection.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        var choice = Choose(settingsManager, [OpenServer()]);

        Assert.That(choice.HasProvider, Is.False);
    }

    private static AIOperationProviderChoice Choose(SettingsManager settingsManager, IReadOnlyList<IDataSource> dataSources)
    {
        var resolver = new AIOperationProviderResolver(settingsManager, NullLogger<AIOperationProviderResolver>.Instance);
        return resolver.Preview(DataSourceChoiceOperation.GetProviderSettings(settingsManager), dataSources.GetRequiredConfidenceLevel(), AISrcSelWithDynHeur.GetChoiceProviderOptions(settingsManager, CHAT_PROVIDER_ID, dataSources));
    }

    private static void PickOwnProvider(SettingsManager settingsManager)
    {
        settingsManager.ConfigurationData.AgentDataSourceSelection.PreselectAgentOptions = true;
        settingsManager.ConfigurationData.AgentDataSourceSelection.PreselectedAgentProvider = OWN_PROVIDER_ID;
    }

    private static IDataSource OpenServer() => new DataSourceERI_V1
    {
        Id = "2a8d5f1c-7e3b-4c9a-b6d2-5f8e1a3c7b49",
        Name = "Server for any provider",
        SecurityPolicy = DataSourceSecurity.ALLOW_ANY,
    };

    private static IDataSource SelfHostedServer() => new DataSourceERI_V1
    {
        Id = "9e4a2c7f-5b1d-4f6e-a8c3-1d7b4e9f2a65",
        Name = "Server for self-hosted providers",
        SecurityPolicy = DataSourceSecurity.SELF_HOSTED,
    };

    private static IDataSource LocalDirectory(ConfidenceLevel confidenceLevel) => new DataSourceLocalDirectory
    {
        Id = "5c1e9a7d-3f2b-4e8a-9d6c-7b4a2e1f8c35",
        Name = "Handbook",
        ConfidenceLevel = confidenceLevel,
    };

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