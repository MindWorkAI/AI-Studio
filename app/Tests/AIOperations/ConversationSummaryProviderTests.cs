using AIStudio.AIOperations;
using AIStudio.Chat;
using AIStudio.Chat.Compression;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.AIOperations;

/// <summary>
/// Checks which model writes the summary when a chat is compressed.
/// </summary>
/// <remarks>
/// A model picked for compressing writes the summary when it may; otherwise, the model of the chat does, which saw
/// everything the summary reads already. Whether a model may see the chat is decided through
/// Program.SERVICE_PROVIDER, as in the app, see ChatRestrictionsExtensions.IsLLMProviderAllowed. Every test
/// configures at least two providers, since a single one is the app-wide default whatever the settings name. Names
/// are written out in full where a short one would find a namespace of this assembly first, see
/// AIOperationProviderResolverTests.
/// </remarks>
[TestFixture]
public sealed class ConversationSummaryProviderTests
{
    private const string CHAT_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string OWN_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";
    private const string DELETED_PROVIDER_ID = "33333333-3333-3333-3333-333333333333";
    private const int LARGE_WINDOW = 128_000;
    private const int SMALL_WINDOW = 4_096;

    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateApplicationState()
    {
        this.settingsManager = new SettingsManager(NullLogger<SettingsManager>.Instance, null!);

        this.previousServiceProvider = Program.SERVICE_PROVIDER;
        this.serviceProvider = new ServiceCollection().AddSingleton(this.settingsManager).BuildServiceProvider();
        Program.SERVICE_PROVIDER = this.serviceProvider;
    }

    [TearDown]
    public void RestoreApplicationState()
    {
        Program.SERVICE_PROVIDER = this.previousServiceProvider;
        this.serviceProvider.Dispose();
    }

    [Test]
    public void WithoutAModelOfItsOwnTheModelOfTheChatWrites()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.MISTRAL);

        var choice = this.ChooseWriter(new ChatThread(), chatProvider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_SET), "Nobody picked a model for compressing, so the dialog has nothing to explain.");
        }
    }

    [Test]
    public void AModelPickedForCompressingWrites()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        this.settingsManager.ConfigurationData.ChatCompression.PreselectedProvider = OWN_PROVIDER_ID;

        var choice = this.ChooseWriter(new ChatThread(), chatProvider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(OWN_PROVIDER_ID));
            Assert.That(choice.UsesFallback, Is.False);
        }
    }

    [Test]
    public void ADeletedModelLeavesTheSummaryToTheModelOfTheChat()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        this.settingsManager.ConfigurationData.ChatCompression.PreselectedProvider = DELETED_PROVIDER_ID;

        var choice = this.ChooseWriter(new ChatThread(), chatProvider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.MISSING), "The dialog tells why the picked model does not write the summary.");
        }
    }

    /// <summary>
    /// Checks that the minimum confidence of compressing rules out only the model picked for it.
    /// </summary>
    /// <remarks>
    /// The model of the chat saw everything the summary reads already. Ruling it out as well would only stop the
    /// summary, without keeping any data from any model. Under TRUST_ALL, a cloud provider reaches MEDIUM, so a
    /// minimum of HIGH rules both out.
    /// </remarks>
    [Test]
    public void TheMinimumOfCompressingNeverRulesOutTheModelOfTheChat()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.MISTRAL);
        this.settingsManager.ConfigurationData.ChatCompression.PreselectedProvider = OWN_PROVIDER_ID;
        this.settingsManager.ConfigurationData.ChatCompression.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        var choice = this.ChooseWriter(new ChatThread(), chatProvider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID), "The model of the chat writes the summary all the same.");
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.BELOW_MINIMUM_CONFIDENCE), "The picked model stays below the minimum.");
        }
    }

    [Test]
    public void AModelPickedForCompressingWhichReachesTheMinimumWrites()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        this.settingsManager.ConfigurationData.ChatCompression.PreselectedProvider = OWN_PROVIDER_ID;
        this.settingsManager.ConfigurationData.ChatCompression.MinimumProviderConfidence = ConfidenceLevel.HIGH;

        var choice = this.ChooseWriter(new ChatThread(), chatProvider);

        Assert.That(choice.Provider.Id, Is.EqualTo(OWN_PROVIDER_ID), "Under TRUST_ALL, a self-hosted provider reaches HIGH.");
    }

    [Test]
    public void AChatWhichRequiresAHigherConfidenceKeepsItsSummaryFromWeakerModels()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.settingsManager.ConfigurationData.ChatCompression.PreselectedProvider = OWN_PROVIDER_ID;

        var chat = new ChatThread();
        chat.RequireProviderConfidence(ConfidenceLevel.HIGH);
        var choice = this.ChooseWriter(chat, chatProvider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT), "The chat holds data which the picked model must not see.");
        }
    }

    [Test]
    public void AChatWithDataForSelfHostedModelsKeepsItsSummaryThere()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.settingsManager.ConfigurationData.ChatCompression.PreselectedProvider = OWN_PROVIDER_ID;

        var chat = new ChatThread();
        chat.RequireDataSecurity(DataSourceSecurity.SELF_HOSTED);
        var choice = this.ChooseWriter(chat, chatProvider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.NOT_ALLOWED_FOR_CONTENT), "A data source of the chat allows only self-hosted models.");
        }
    }

    [Test]
    public void APickedModelTooSmallForASummaryIsPassedOver()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.OPEN_AI);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.MISTRAL, SMALL_WINDOW);
        this.settingsManager.ConfigurationData.ChatCompression.PreselectedProvider = OWN_PROVIDER_ID;

        var choice = this.ChooseWriter(new ChatThread(), chatProvider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.Provider.Id, Is.EqualTo(CHAT_PROVIDER_ID));
            Assert.That(choice.PreferredIssue, Is.EqualTo(AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL));
        }
    }

    /// <summary>
    /// Checks that a large model picked for compressing summarizes the chat of a small one.
    /// </summary>
    /// <remarks>
    /// This is the main reason to pick a model for compressing: a small local model cannot write a summary of its
    /// own chat, but it can continue with one.
    /// </remarks>
    [Test]
    public void ALargeModelSummarizesTheChatOfASmallOne()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.SELF_HOSTED, SMALL_WINDOW);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.SELF_HOSTED);
        this.settingsManager.ConfigurationData.ChatCompression.PreselectedProvider = OWN_PROVIDER_ID;

        var choice = this.ChooseWriter(new ChatThread(), chatProvider);

        Assert.That(choice.Provider.Id, Is.EqualTo(OWN_PROVIDER_ID));
    }

    [Test]
    public void WhenNoModelIsLargeEnoughNothingWrites()
    {
        var chatProvider = this.AddProvider(CHAT_PROVIDER_ID, LLMProviders.OPEN_AI, SMALL_WINDOW);
        this.AddProvider(OWN_PROVIDER_ID, LLMProviders.MISTRAL);

        var choice = this.ChooseWriter(new ChatThread(), chatProvider);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(choice.HasProvider, Is.False);
            Assert.That(choice.FallbackIssue, Is.EqualTo(AIOperationProviderIssue.CONTEXT_WINDOW_TOO_SMALL), "The chat hides its button for compressing for this reason only.");
        }
    }

    /// <summary>
    /// Chooses the writer as the dialog and the notice before sending do.
    /// </summary>
    /// <remarks>
    /// Choosing counts nothing, so the counter gets no rust service.
    /// </remarks>
    private AIOperationProviderChoice ChooseWriter(ChatThread chat, AIStudio.Settings.Provider chatProvider)
    {
        var resolver = new AIOperationProviderResolver(this.settingsManager, NullLogger<AIOperationProviderResolver>.Instance);
        var tokenCounter = new ConversationTokenCounter(null!, null!, NullLogger<ConversationTokenCounter>.Instance);
        return new ChatCompressionService(tokenCounter, resolver, this.settingsManager).ChooseWriter(chat, chatProvider);
    }

    /// <summary>
    /// Adds a provider to the settings, with a context window stated by its expert settings.
    /// </summary>
    /// <remarks>
    /// Stating the window keeps the tests independent of what the model rules know about the test model. A
    /// self-hosted provider is marked as such, as the provider dialog does, since only that mark lets it see the
    /// data of a data source which allows self-hosted models only.
    /// </remarks>
    private AIStudio.Settings.Provider AddProvider(string id, LLMProviders llmProvider, int contextWindowTokens = LARGE_WINDOW)
    {
        var providers = this.settingsManager.ConfigurationData.Providers;
        var provider = new AIStudio.Settings.Provider((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null), IsSelfHosted: llmProvider is LLMProviders.SELF_HOSTED, CapabilityOverrides: new ProviderCapabilityOverrides { ContextWindowTokens = contextWindowTokens });
        providers.Add(provider);
        return provider;
    }
}