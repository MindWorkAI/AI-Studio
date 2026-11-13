using AIStudio.Assistants.BeyondFactsLab;
using AIStudio.Tools.ToolCallingSystem;

namespace AIStudio.Tests.BeyondFactsLab;

/// <summary>
/// Checks what the BeyondFacts Lab tells the person when its research cannot search the web.
/// </summary>
[TestFixture]
public sealed class BflResearchAvailabilityTests
{
    [Test]
    public void EveryReasonHasAMessageOfItsOwn()
    {
        var reasons = Enum.GetValues<ToolOfferBlockReason>().Where(reason => reason is not ToolOfferBlockReason.NONE).ToList();
        var messages = reasons.Select(BflResearchAvailability.MessageFor).ToList();
        var unknown = BflResearchAvailability.MessageFor((ToolOfferBlockReason)int.MaxValue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(messages, Has.All.Not.Empty);
            Assert.That(messages, Is.Unique);
            Assert.That(messages, Has.None.EqualTo(unknown), "Every reason this version knows has a text of its own.");
            Assert.That(unknown, Is.Not.Empty);
        }
    }

    [Test]
    public void NothingInTheWayNeedsNoMessage()
    {
        var availability = new BflResearchAvailability(ToolOfferBlockReason.NONE, ToolOfferBlockReason.NONE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(availability.CanResearch, Is.True);
            Assert.That(availability.Message, Is.Empty);
        }
    }

    [Test]
    public void TheWebSearchDecidesWhetherTheLabCanResearch()
    {
        var withoutReadingPages = new BflResearchAvailability(ToolOfferBlockReason.NONE, ToolOfferBlockReason.TOOL_SWITCHED_OFF);
        var withoutSearching = new BflResearchAvailability(ToolOfferBlockReason.NOT_CONFIGURED, ToolOfferBlockReason.NONE);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(withoutReadingPages.CanResearch, Is.True);
            Assert.That(withoutReadingPages.Message, Is.Empty);
            Assert.That(withoutSearching.CanResearch, Is.False);
            Assert.That(withoutSearching.Message, Is.EqualTo(BflResearchAvailability.MessageFor(ToolOfferBlockReason.NOT_CONFIGURED)));
        }
    }
}