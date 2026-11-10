using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Chat;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.ToolCallingSystem;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the tools of the chats of the BeyondFacts Lab, and that the chat of a step never searches the web.
/// </summary>
/// <remarks>
/// That the level ONLY_LINKS_FROM_CHAT keeps a web search from running, while Read Web Page still reads the addresses
/// given to the model, is checked by ToolOutboundDataTests for every chat alike.
/// </remarks>
[TestFixture]
public sealed class BflChatToolsTests
{
    private static readonly Guid PROJECT_ID = Guid.Parse("6f1c2b0e-3a51-4c3e-9d2f-0b7a4e5c8d91");

    private string dataDirectory = string.Empty;

    [SetUp]
    public void CreateDataDirectory()
    {
        this.dataDirectory = Path.Combine(Path.GetTempPath(), $"ai-studio-beyondfacts-lab-{Guid.NewGuid():N}");
        Directory.CreateDirectory(this.dataDirectory);
    }

    [TearDown]
    public void DeleteDataDirectory()
    {
        if (Directory.Exists(this.dataDirectory))
            Directory.Delete(this.dataDirectory, true);
    }

    [Test]
    public void TheChatOfEveryStepReadsWebPagesButNeverSearches()
    {
        using (Assert.EnterMultipleScope())
        {
            foreach (var step in BflStep.All)
            {
                var tools = BflChatTools.For(new BflStepThreadLocation(PROJECT_ID, step));
                Assert.That(tools, Is.EquivalentTo(new[] { ToolSelectionRules.READ_WEB_PAGE_TOOL_ID }), step.ToString());
                Assert.That(tools, Does.Not.Contain(ToolSelectionRules.WEB_SEARCH_TOOL_ID), step.ToString());
            }
        }
    }

    [Test]
    public void TheOnboardingHasNoTools()
    {
        Assert.That(BflChatTools.For(BflOnboardingThreadLocation.INSTANCE), Is.Empty);
    }

    [Test]
    public void TheChatOfAStepReadsOnlyTheAddressesGivenToItFromTheStart()
    {
        var stepChat = BflThreadFactory.Create(new BflStepThreadLocation(PROJECT_ID, BflStep.STRATEGY));
        var onboarding = BflThreadFactory.Create(BflOnboardingThreadLocation.INSTANCE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stepChat.RequiredOutboundDataRestriction, Is.EqualTo(BflChatTools.STEP_REQUIREMENT));
            Assert.That(BflChatTools.STEP_REQUIREMENT.Restriction, Is.EqualTo(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT));
            Assert.That(BflChatTools.STEP_REQUIREMENT.Origin, Is.EqualTo(OutboundDataOrigin.BEYOND_FACTS_LAB));
            Assert.That(onboarding.RequiredOutboundDataRestriction, Is.EqualTo(OutboundDataRequirement.NONE));
        }
    }

    [Test]
    public void AnEarlierChatGetsTheRestrictionAndAStricterOneStays()
    {
        var earlier = new ChatThread();
        var stricter = new ChatThread();
        var strictest = new OutboundDataRequirement(OutboundDataRestriction.ONLY_CONFIGURED_SERVICES, OutboundDataOrigin.INTERNAL_WEB_PAGE, "intranet.example.org");
        stricter.RequireOutboundDataRestriction(strictest);

        BflChatTools.Restrict(earlier, new BflStepThreadLocation(PROJECT_ID, BflStep.FRAMEWORK));
        BflChatTools.Restrict(stricter, new BflStepThreadLocation(PROJECT_ID, BflStep.FRAMEWORK));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(earlier.RequiredOutboundDataRestriction, Is.EqualTo(BflChatTools.STEP_REQUIREMENT));
            Assert.That(stricter.RequiredOutboundDataRestriction, Is.EqualTo(strictest));
        }
    }

    [Test]
    public async Task TheRestrictionIsStoredWithTheChat()
    {
        var store = new BeyondFactsLabStore(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory });
        var created = await store.CreateAsync("Panel");
        var location = new BflStepThreadLocation(created.ProjectId, BflStep.KNOWLEDGE);

        await store.SaveThreadAsync(location, BflThreadFactory.Create(location));
        var loaded = await new BeyondFactsLabStore(NullLogger<BeyondFactsLabStore>.Instance, new BflStorageOptions { DataDirectory = this.dataDirectory }).LoadThreadAsync(location);

        Assert.That(loaded!.RequiredOutboundDataRestriction, Is.EqualTo(BflChatTools.STEP_REQUIREMENT));
    }

    [Test]
    public void AToolKeptBackNamesTheLab()
    {
        var message = BflChatTools.STEP_REQUIREMENT.GetToolBlockedMessage(new Data());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(message, Does.Contain("BeyondFacts Lab"));
            Assert.That(message, Is.Not.EqualTo(new OutboundDataRequirement(OutboundDataRestriction.ONLY_LINKS_FROM_CHAT, (OutboundDataOrigin)999, string.Empty).GetToolBlockedMessage(new Data())));
        }
    }

    [Test]
    public void ARequestOfAStepOffersTheToolsOfItsPlace()
    {
        var location = new BflStepThreadLocation(PROJECT_ID, BflStep.TEST_SITUATION);
        var thread = BflThreadFactory.Create(location);
        var provider = new AIStudio.Settings.Provider(1, "provider-1", "Test", AIStudio.Provider.LLMProviders.SELF_HOSTED, new AIStudio.Provider.Model("model", null));

        BflThreadFactory.PrepareForRequest(thread, provider, "Prompt", BflChatTools.For(location));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thread.RuntimeSelectedToolIds, Is.EquivalentTo(new[] { ToolSelectionRules.READ_WEB_PAGE_TOOL_ID }));
            Assert.That(thread.RuntimeToolsAreAssistantManaged, Is.True);
        }
    }
}