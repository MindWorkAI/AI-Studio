using System.Text.Json.Nodes;

using AIStudio.Assistants.VisualBriefing;
using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Settings.DataModel;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Assistants.VisualBriefing;

/// <summary>
/// Checks that a briefing holds to the restrictions of a text sent to it.
/// </summary>
/// <remarks>
/// The Visual Briefing assistant keeps its briefings on disk and builds them in several stages, each with a
/// thread of its own. The restrictions of a text sent to it therefore live in the manifest of the briefing,
/// where every stage of every build finds them, also after a restart. The editor replaces the settings of a
/// briefing with every save, so the restrictions must survive that.<br/><br/>
/// The checks of a provider reach the settings through Program.SERVICE_PROVIDER, as in the app.
/// </remarks>
[TestFixture]
public sealed class VisualBriefingRestrictionsTests
{
    private const string MAILBOX_ID = "5c9e2a7b-3f1d-4e8a-b6c4-2d7f9a1e3b5c";
    private const string CLOUD_PROVIDER_ID = "11111111-1111-1111-1111-111111111111";
    private const string SELF_HOSTED_PROVIDER_ID = "22222222-2222-2222-2222-222222222222";

    private static readonly ChatRestrictions FROM_MAILBOX = new ChatRestrictions(ConfidenceLevel.HIGH, DataSourceSecurity.SELF_HOSTED, new(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.MAILBOX, MAILBOX_ID), []).WithWebAddressesWrittenByModelIn("The minutes are on https://example.org/minutes.");

    private string dataDirectory = string.Empty;
    private SettingsManager settingsManager = null!;
    private ServiceProvider serviceProvider = null!;
    private IServiceProvider previousServiceProvider = null!;

    [SetUp]
    public void CreateSettingsAndStorage()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-visual-briefing-restrictions-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);

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

        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public async Task ABriefingKeepsTheRestrictionsOfItsTextWhenTheEditorSavesIt()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Briefing", string.Empty, new VisualBriefingLocalSettings(), FROM_MAILBOX);

        await store.SaveProjectAsync(created.BriefingId, "Renamed briefing", "Author", new VisualBriefingLocalSettings { Instruction = "Summarize the minutes." }, []);
        var loaded = await this.CreateStore().LoadAsync(created.BriefingId);

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Settings.Instruction, Is.EqualTo("Summarize the minutes."), "The save did not happen.");
            AssertSame(loaded.Restrictions, FROM_MAILBOX);
        });
    }

    [Test]
    public async Task ABriefingNothingWasSentToDemandsNothing()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Briefing", string.Empty, new VisualBriefingLocalSettings(), ChatRestrictions.NONE);

        var loaded = await this.CreateStore().LoadAsync(created.BriefingId);

        Assert.That(loaded, Is.Not.Null);
        AssertSame(loaded!.Restrictions, ChatRestrictions.NONE);
    }

    [Test]
    public async Task ABriefingWrittenBeforeTheRestrictionsExistedStillLoadsAndDemandsNothing()
    {
        var created = await this.CreateStore().CreateAsync("Briefing", string.Empty, new VisualBriefingLocalSettings(), ChatRestrictions.NONE);
        var manifestPath = Directory.GetFiles(this.dataDirectory, "manifest.json", SearchOption.AllDirectories).Single();
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!.AsObject();
        manifest.Remove("restrictions");
        manifest["manifestVersion"] = 1;
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString());

        var loaded = await this.CreateStore().LoadAsync(created.BriefingId);

        Assert.That(loaded, Is.Not.Null, "A briefing of the first manifest version no longer loads.");
        AssertSame(loaded!.Restrictions, ChatRestrictions.NONE);
    }

    [Test]
    public async Task ASavedBriefingTellsAnOlderVersionThatItNeedsANewerOne()
    {
        var store = this.CreateStore();
        var created = await store.CreateAsync("Briefing", string.Empty, new VisualBriefingLocalSettings(), FROM_MAILBOX);
        var manifestPath = Directory.GetFiles(this.dataDirectory, "manifest.json", SearchOption.AllDirectories).Single();
        var manifest = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!.AsObject();
        manifest["manifestVersion"] = 1;
        await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString());

        await store.SaveProjectAsync(created.BriefingId, "Briefing", string.Empty, new VisualBriefingLocalSettings(), []);
        var saved = JsonNode.Parse(await File.ReadAllTextAsync(manifestPath))!.AsObject();

        Assert.That(saved["manifestVersion"]!.GetValue<int>(), Is.EqualTo(VisualBriefingVersions.MANIFEST), "An older AI Studio would fail on the restrictions instead of saying it needs a newer version.");
    }

    [Test]
    public void TheBuildRefusesAProviderWhichMayNotSeeTheTextSentToTheBriefing()
    {
        var cloudProvider = this.AddProvider(CLOUD_PROVIDER_ID, LLMProviders.OPEN_AI, isSelfHosted: false);
        var selfHostedProvider = this.AddProvider(SELF_HOSTED_PROVIDER_ID, LLMProviders.SELF_HOSTED, isSelfHosted: true);
        var restricted = new VisualBriefingManifest { Restrictions = ChatRestrictions.NONE with { DataSecurity = DataSourceSecurity.SELF_HOSTED } };

        var exception = Assert.Throws<VisualBriefingBuildException>(() => VisualBriefingBuildOrchestrator.ValidateProviderMaySeeContent(restricted, cloudProvider));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Code, Is.EqualTo(VisualBriefingFailureCode.PROVIDER_NOT_ALLOWED));
            Assert.DoesNotThrow(() => VisualBriefingBuildOrchestrator.ValidateProviderMaySeeContent(restricted, selfHostedProvider));
            Assert.DoesNotThrow(() => VisualBriefingBuildOrchestrator.ValidateProviderMaySeeContent(new VisualBriefingManifest(), cloudProvider), "A briefing nothing was sent to may use any provider.");
        });
    }

    private VisualBriefingStore CreateStore() => new(null!, NullLogger<VisualBriefingStore>.Instance, new VisualBriefingStorageOptions { DataDirectory = this.dataDirectory });

    private static void AssertSame(ChatRestrictions actual, ChatRestrictions expected) => Assert.Multiple(() =>
    {
        Assert.That(actual.ProviderConfidence, Is.EqualTo(expected.ProviderConfidence));
        Assert.That(actual.DataSecurity, Is.EqualTo(expected.DataSecurity));
        Assert.That(actual.OutboundData, Is.EqualTo(expected.OutboundData));
        Assert.That(actual.WebAddressesWrittenByModels, Is.EquivalentTo(expected.WebAddressesWrittenByModels));
    });

    private AIStudio.Settings.Provider AddProvider(string id, LLMProviders llmProvider, bool isSelfHosted)
    {
        var providers = this.settingsManager.ConfigurationData.Providers;
        providers.Add(new((uint)providers.Count + 1, id, $"Instance {providers.Count + 1}", llmProvider, new("test-model", null), isSelfHosted));
        return this.settingsManager.GetProviderById(id);
    }
}
