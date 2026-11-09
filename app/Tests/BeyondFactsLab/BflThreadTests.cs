using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks the IDs of the chats of the BeyondFacts Lab and how the lab sets them up.
/// </summary>
[TestFixture]
public sealed class BflThreadTests
{
    private static readonly Guid PROJECT_ID = Guid.Parse("6f1c2b0e-3a51-4c3e-9d2f-0b7a4e5c8d91");

    [Test]
    public void TheIdOfAChatFollowsFromItsPlace()
    {
        // Computed independently, from SHA-256 over "beyondfacts-lab/thread/<project>/05-strategy". A stored chat keeps
        // its ID, so this value never changes:
        var expected = Guid.Parse("a6e33c75-66c3-8ac8-bae2-8b6a90fb6f19");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(BflThreadId.Of(PROJECT_ID, BflStep.STRATEGY), Is.EqualTo(expected));
            Assert.That(new BflStepThreadLocation(PROJECT_ID, BflStep.STRATEGY).ChatId, Is.EqualTo(expected));
        }
    }

    [Test]
    public void EveryChatHasAnIdOfItsOwn()
    {
        var otherProject = Guid.NewGuid();
        var ids = BflStep.All.SelectMany(step => new[] { BflThreadId.Of(PROJECT_ID, step), BflThreadId.Of(otherProject, step) }).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(ids, Is.Unique);
            Assert.That(ids, Has.None.EqualTo(Guid.Empty));
            Assert.That(ids, Has.None.EqualTo(PROJECT_ID));
        }
    }

    [Test]
    public void ThePlaceOfAStepServesAsAKey()
    {
        BflThreadLocation first = new BflStepThreadLocation(PROJECT_ID, BflStep.STRATEGY);
        BflThreadLocation second = new BflStepThreadLocation(PROJECT_ID, BflStep.STRATEGY);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(new BflStepThreadLocation(PROJECT_ID, BflStep.FRAMEWORK)));
            Assert.That(first.Key, Is.EqualTo($"{PROJECT_ID:D}/05-strategy"));
            Assert.That(first.ChatName, Is.EqualTo("05-strategy"));
        }
    }

    [Test]
    public void TheIdIsAVersion8Uuid()
    {
        var id = BflThreadId.Of(PROJECT_ID, BflStep.FRAMEWORK);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(id.Version, Is.EqualTo(8));
            Assert.That(id.Variant & 0xC, Is.EqualTo(0x8), "The variant of RFC 9562.");
        }
    }

    [Test]
    public void ANewChatHasNothingButItsPlace()
    {
        var location = new BflStepThreadLocation(PROJECT_ID, BflStep.KNOWLEDGE);

        var thread = BflThreadFactory.Create(location);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thread.ChatId, Is.EqualTo(location.ChatId));
            Assert.That(thread.Blocks, Is.Empty);
            Assert.That(thread.SelectedProfile, Is.EqualTo(Profile.NO_PROFILE.Id));
            Assert.That(thread.SelectedChatTemplate, Is.EqualTo(ChatTemplate.NO_CHAT_TEMPLATE.Id));
            Assert.That(thread.SelectedToolIds, Is.Empty);
            Assert.That(thread.DataSourceOptions.DisableDataSources, Is.True);
        }
    }

    [Test]
    public void AChatIsReadiedAsAChatOfTheLabWithoutToolsOrDataSources()
    {
        var thread = BflThreadFactory.Create(new BflStepThreadLocation(PROJECT_ID, BflStep.KNOWLEDGE));
        thread.DataSourceOptions.DisableDataSources = false;
        thread.RuntimeSelectedToolIds = ["web_search"];
        var provider = new AIStudio.Settings.Provider(1, "provider-1", "Test", LLMProviders.SELF_HOSTED, new Model("model", null));

        BflThreadFactory.PrepareForRequest(thread, provider, "Prompt");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(thread.SystemPrompt, Is.EqualTo("Prompt"));
            Assert.That(thread.SelectedProvider, Is.EqualTo("provider-1"));
            Assert.That(thread.RuntimeComponent, Is.EqualTo(AIStudio.Tools.Components.BEYOND_FACTS_LAB_ASSISTANT));
            Assert.That(thread.RuntimeToolsAreAssistantManaged, Is.True);
            Assert.That(thread.RuntimeSelectedToolIds, Is.Empty);
            Assert.That(thread.DataSourceOptions.DisableDataSources, Is.True);
            Assert.That(thread.SelectedProfile, Is.EqualTo(Profile.NO_PROFILE.Id));
        }
    }
}