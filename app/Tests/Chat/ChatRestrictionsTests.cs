using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Chat;

/// <summary>
/// Checks the restrictions content carries out of a chat, and how another thread takes them over.
/// </summary>
/// <remarks>
/// Content which leaves a chat, such as a message sent to an assistant, must demand there what it
/// demanded in the chat: an answer written from mails must not reach a provider or a tool there which
/// the chat would have kept from it. Each part only ever tightens, wherever the content goes.<br/><br/>
///
/// The checks of a provider reach the settings through Program.SERVICE_PROVIDER, as in the app.
/// </remarks>
[TestFixture]
public sealed class ChatRestrictionsTests
{
    private const string DATA_SOURCE_ID = "3e8b1d6f-9a2c-4f7e-b5d1-6c4a9e2f8b17";
    private const string MAILBOX_ID = "5c9e2a7b-3f1d-4e8a-b6c4-2d7f9a1e3b5c";
    private const string CLOUD_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string SELF_HOSTED_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";

    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettings()
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
    public void TheStricterOfEachPartWins()
    {
        var fromDataSource = new ChatRestrictions(ConfidenceLevel.HIGH, DataSourceSecurity.ALLOW_ANY, new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.DATA_SOURCE, DATA_SOURCE_ID));
        var fromMailbox = new ChatRestrictions(ConfidenceLevel.LOW, DataSourceSecurity.SELF_HOSTED, new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.MAILBOX, MAILBOX_ID));

        foreach (var combined in new[] { fromDataSource.StricterOf(fromMailbox), fromMailbox.StricterOf(fromDataSource) })
        {
            Assert.Multiple(() =>
            {
                Assert.That(combined.ProviderConfidence, Is.EqualTo(ConfidenceLevel.HIGH));
                Assert.That(combined.DataSecurity, Is.EqualTo(DataSourceSecurity.SELF_HOSTED), "Data for self-hosted providers only keeps them as the only choice, whichever side brings it.");
                Assert.That(combined.OutboundData.Restriction, Is.EqualTo(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES));
                Assert.That(combined.OutboundData.Reference, Is.EqualTo(MAILBOX_ID), "The source which demands the strictest restriction is the one to name.");
            });
        }
    }

    [Test]
    public void RestrictionsWhichDemandNothingChangeNothing()
    {
        var restrictions = new ChatRestrictions(ConfidenceLevel.MEDIUM, DataSourceSecurity.ALLOW_ANY, new(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, OutboundDataOrigin.DATA_SOURCE, DATA_SOURCE_ID));

        Assert.Multiple(() =>
        {
            AssertSame(restrictions.StricterOf(ChatRestrictions.NONE), restrictions);
            AssertSame(ChatRestrictions.NONE.StricterOf(restrictions), restrictions);
        });
    }

    [Test]
    public void TheRestrictionsOfAChatComeAlongIntoAnother()
    {
        var sourceChat = new ChatThread();
        sourceChat.RequireProviderConfidence(ConfidenceLevel.HIGH);
        sourceChat.RequireDataSecurity(DataSourceSecurity.SELF_HOSTED);
        sourceChat.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.MAILBOX, MAILBOX_ID));
        var otherChat = new ChatThread();

        otherChat.RequireRestrictions(sourceChat.CaptureRestrictions());

        AssertSame(otherChat.CaptureRestrictions(), sourceChat.CaptureRestrictions());
    }

    [Test]
    public void ContentWhichDemandsLessLoosensNothing()
    {
        var chat = new ChatThread();
        chat.RequireProviderConfidence(ConfidenceLevel.HIGH);
        chat.RequireDataSecurity(DataSourceSecurity.SELF_HOSTED);
        chat.RequireOutboundDataRestriction(new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.MAILBOX, MAILBOX_ID));
        var before = chat.CaptureRestrictions();

        chat.RequireRestrictions(new(ConfidenceLevel.LOW, DataSourceSecurity.ALLOW_ANY, new(OutboundDataRestriction.UNRESTRICTED, OutboundDataOrigin.DATA_SOURCE, DATA_SOURCE_ID)));
        chat.RequireRestrictions(ChatRestrictions.NONE);

        AssertSame(chat.CaptureRestrictions(), before);
    }

    [Test]
    public void ASnapshotKeepsWhatTheChatDemandedWhenItWasTaken()
    {
        var chat = new ChatThread();
        chat.RequireProviderConfidence(ConfidenceLevel.LOW);
        var snapshot = chat.CaptureRestrictions();

        chat.RequireProviderConfidence(ConfidenceLevel.HIGH);
        chat.RequireDataSecurity(DataSourceSecurity.SELF_HOSTED);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.ProviderConfidence, Is.EqualTo(ConfidenceLevel.LOW), "Content which left the chat does not hold what the chat read afterward.");
            Assert.That(snapshot.DataSecurity, Is.EqualTo(DataSourceSecurity.NOT_SPECIFIED));
        });
    }

    [Test]
    public void OnlyASelfHostedProviderMaySeeContentForSelfHostedProvidersOnly()
    {
        var cloudProvider = this.AddProvider(CLOUD_PROVIDER_ID, LLMProviders.OPEN_AI, isSelfHosted: false);
        var selfHostedProvider = this.AddProvider(SELF_HOSTED_PROVIDER_ID, LLMProviders.SELF_HOSTED, isSelfHosted: true);
        var restrictions = ChatRestrictions.NONE with { DataSecurity = DataSourceSecurity.SELF_HOSTED };

        Assert.Multiple(() =>
        {
            Assert.That(restrictions.IsLLMProviderAllowed(cloudProvider), Is.False);
            Assert.That(restrictions.IsLLMProviderAllowed(selfHostedProvider), Is.True);
            Assert.That(ChatRestrictions.NONE.IsLLMProviderAllowed(cloudProvider), Is.True, "Content which demands nothing may go to any provider.");
        });
    }

    [Test]
    public void AProviderBelowTheDemandedConfidenceMayNotSeeTheContent()
    {
        var provider = this.AddProvider(CLOUD_PROVIDER_ID, LLMProviders.OPEN_AI, isSelfHosted: false);
        var level = provider.GetConfidenceLevel(this.settingsManager);
        var higherLevels = Enum.GetValues<ConfidenceLevel>().Where(candidate => candidate > level).ToList();
        Assume.That(higherLevels, Is.Not.Empty, "The provider has the highest level there is, so no content can demand more of it.");

        Assert.Multiple(() =>
        {
            Assert.That((ChatRestrictions.NONE with { ProviderConfidence = level }).IsLLMProviderAllowed(provider), Is.True);
            Assert.That((ChatRestrictions.NONE with { ProviderConfidence = higherLevels.Min() }).IsLLMProviderAllowed(provider), Is.False);
        });
    }

    [Test]
    public void AChatAndItsRestrictionsAllowTheSameProviders()
    {
        var cloudProvider = this.AddProvider(CLOUD_PROVIDER_ID, LLMProviders.OPEN_AI, isSelfHosted: false);
        var selfHostedProvider = this.AddProvider(SELF_HOSTED_PROVIDER_ID, LLMProviders.SELF_HOSTED, isSelfHosted: true);
        var chat = new ChatThread();
        chat.RequireDataSecurity(DataSourceSecurity.SELF_HOSTED);
        ChatThread? newChat = null;

        Assert.Multiple(() =>
        {
            Assert.That(chat.IsLLMProviderAllowed(cloudProvider), Is.EqualTo(chat.CaptureRestrictions().IsLLMProviderAllowed(cloudProvider)));
            Assert.That(chat.IsLLMProviderAllowed(selfHostedProvider), Is.EqualTo(chat.CaptureRestrictions().IsLLMProviderAllowed(selfHostedProvider)));
            Assert.That(newChat.IsLLMProviderAllowed(cloudProvider), Is.True, "A new chat demands nothing yet.");
        });
    }

    private static void AssertSame(ChatRestrictions actual, ChatRestrictions expected) => Assert.Multiple(() =>
    {
        Assert.That(actual.ProviderConfidence, Is.EqualTo(expected.ProviderConfidence));
        Assert.That(actual.DataSecurity, Is.EqualTo(expected.DataSecurity));
        Assert.That(actual.OutboundData, Is.EqualTo(expected.OutboundData));
    });

    private AIStudio.Settings.Provider AddProvider(string id, LLMProviders llmProvider, bool isSelfHosted)
    {
        var providers = this.settingsManager.ConfigurationData.Providers;
        providers.Add(new((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null), isSelfHosted));
        return this.settingsManager.GetProviderById(id);
    }
}
