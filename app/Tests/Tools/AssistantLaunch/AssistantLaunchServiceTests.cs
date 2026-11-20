using AIStudio.Settings.DataModel;
using AIStudio.Tools;
using AIStudio.Tools.AssistantLaunch;
using AIStudio.Tools.PluginSystem;

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
            Assert.That(destinations.All(destination => destination.AllowSendTo), Is.True);
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

    [TestCase(false, AssistantOfferBehavior.KEEP_OFFERING, AssistantOfferBehavior.TREAT_AS_ABSENT, ExpectedResult = true, TestName = "ACollapsedCategoryKeepsItsAssistantsByDefault")]
    [TestCase(true, AssistantOfferBehavior.KEEP_OFFERING, AssistantOfferBehavior.TREAT_AS_ABSENT, ExpectedResult = false, TestName = "AHiddenCategoryDropsItsAssistantsByDefault")]
    [TestCase(false, AssistantOfferBehavior.TREAT_AS_ABSENT, AssistantOfferBehavior.KEEP_OFFERING, ExpectedResult = false, TestName = "ACollapsedCategoryDropsItsAssistantsWhenTheUserSaysSo")]
    [TestCase(true, AssistantOfferBehavior.TREAT_AS_ABSENT, AssistantOfferBehavior.KEEP_OFFERING, ExpectedResult = true, TestName = "AHiddenCategoryKeepsItsAssistantsWhenTheUserSaysSo")]
    public bool TheMenuFollowsTheOptionsForCollapsedAndHiddenCategories(bool isHidden, AssistantOfferBehavior collapsedCategoryAssistants, AssistantOfferBehavior hiddenCategoryAssistants)
    {
        var page = new DataAssistantsPage
        {
            CollapsedCategoryAssistants = collapsedCategoryAssistants,
            HiddenCategoryAssistants = hiddenCategoryAssistants
        };

        var business = new AssistantCategoryKey(PluginCategory.BUSINESS);
        if (isHidden)
            page.SetCategoryHidden(business, true);
        else
            page.SetCategoryCollapsed(business, true);

        var destinations = AssistantLaunchService.SelectDestinations(AIStudio.Tools.Components.CHAT, page.IsOfferingAssistant);

        Assert.That(destinations, Does.Contain(AIStudio.Tools.Components.TRANSLATION_ASSISTANT), "An assistant of another category stays in any case.");
        return destinations.Contains(AIStudio.Tools.Components.EMAIL_ASSISTANT);
    }

    [Test]
    public void ShorterNamesComeFirst()
    {
        var lengths = AssistantLaunchService.SelectDestinations(AIStudio.Tools.Components.CHAT, _ => true).Select(destination => destination.Name.Length).ToList();
        Assert.That(lengths, Is.Ordered);
    }
}
