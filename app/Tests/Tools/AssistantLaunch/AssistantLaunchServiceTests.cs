using AIStudio.Tools;
using AIStudio.Tools.AssistantLaunch;

namespace AIStudio.Tests.Tools.AssistantLaunch;

/// <summary>
/// Checks which destinations a component offers when the user sends content on.
/// </summary>
/// <remarks>
/// The assistants and the chat offer the same destinations, so they all ask the same place. An assistant
/// which offered itself would take the content nowhere: its page is open already and does not pick it up.
/// The components are written out in full, since a short name would find a namespace of this assembly first.
/// </remarks>
[TestFixture]
public sealed class AssistantLaunchServiceTests
{
    [Test]
    public void AnAssistantNeverOffersItself()
    {
        var destinations = AssistantLaunchService.SelectDestinations(AIStudio.Tools.Components.TRANSLATION_ASSISTANT, _ => true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(destinations, Does.Not.Contain(AIStudio.Tools.Components.TRANSLATION_ASSISTANT));
            Assert.That(destinations, Does.Contain(AIStudio.Tools.Components.SLIDE_BUILDER_ASSISTANT));
            Assert.That(destinations, Does.Contain(AIStudio.Tools.Components.CHAT));
        }
    }

    [Test]
    public void TheChatOffersANewChat()
    {
        var destinations = AssistantLaunchService.SelectDestinations(AIStudio.Tools.Components.CHAT, _ => true);
        Assert.That(destinations, Does.Contain(AIStudio.Tools.Components.CHAT), "A new chat is another chat than the one which sends.");
    }

    [Test]
    public void OnlyComponentsWhichTakeContentAreOffered()
    {
        var destinations = AssistantLaunchService.SelectDestinations(AIStudio.Tools.Components.CHAT, _ => true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(destinations, Is.Not.Empty);
            Assert.That(destinations.All(destination => destination.AllowSendTo()), Is.True);
            Assert.That(destinations, Does.Not.Contain(AIStudio.Tools.Components.DYNAMIC_ASSISTANT), "Assistant plugins cannot take content yet.");
            Assert.That(destinations, Does.Not.Contain(AIStudio.Tools.Components.NONE));
        }
    }

    [Test]
    public void AnAssistantTheUserDoesNotSeeIsNotOffered()
    {
        var destinations = AssistantLaunchService.SelectDestinations(AIStudio.Tools.Components.CHAT, destination => destination is not AIStudio.Tools.Components.SLIDE_BUILDER_ASSISTANT);
        Assert.That(destinations, Does.Not.Contain(AIStudio.Tools.Components.SLIDE_BUILDER_ASSISTANT));
    }

    [Test]
    public void ShorterNamesComeFirst()
    {
        var lengths = AssistantLaunchService.SelectDestinations(AIStudio.Tools.Components.CHAT, _ => true).Select(destination => destination.Name().Length).ToList();
        Assert.That(lengths, Is.Ordered);
    }
}
