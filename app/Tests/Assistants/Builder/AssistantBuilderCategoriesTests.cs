using AIStudio.Assistants.Builder;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Assistants.Builder;

/// <summary>
/// Checks which categories the Assistant Builder offers and how its draft names them.
/// </summary>
/// <remarks>
/// The builder offers every category of AI Studio but CORE, which belongs to configuration and language
/// plugins, in the order of the assistants page. The draft names the choice in a fixed form, so that the
/// step which writes the plugin can take it over.
/// </remarks>
[TestFixture]
public sealed class AssistantBuilderCategoriesTests
{
    [Test]
    public void TheBuilderOffersEveryCategoryButCore()
    {
        var expected = Enum.GetValues<PluginCategory>().Where(category => category is not (PluginCategory.NONE or PluginCategory.CORE or PluginCategory.CUSTOM));

        Assert.That(AssistantBuilderCategories.Offered, Is.EquivalentTo(expected));
    }

    [Test]
    public void TheCategoriesFollowTheOrderOfTheAssistantsPage()
    {
        var offered = AssistantBuilderCategories.Offered;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(offered.Take(4), Is.EqualTo(new[] { PluginCategory.COMMON, PluginCategory.BUSINESS, PluginCategory.EDUCATION, PluginCategory.SOFTWARE_DEVELOPMENT }));
            Assert.That(offered[^1], Is.EqualTo(PluginCategory.AI_STUDIO_DEVELOPMENT));
        }
    }

    [TestCase(PluginCategory.NONE, "", "")]
    [TestCase(PluginCategory.BUSINESS, "", "BUSINESS")]
    [TestCase(PluginCategory.SOFTWARE_DEVELOPMENT, "ignored", "SOFTWARE_DEVELOPMENT")]
    [TestCase(PluginCategory.CUSTOM, "  Space Travel ", "CUSTOM: Space Travel")]
    public void TheDraftNamesTheCategoryInItsFixedForm(PluginCategory category, string customName, string expected)
    {
        Assert.That(AssistantBuilderCategories.FormatForDraft(category, customName), Is.EqualTo(expected));
    }

    [Test]
    public void ThePromptListsEveryOfferedKey()
    {
        var lines = AssistantBuilderCategories.ListForPrompt().Split('\n');

        Assert.That(lines.Select(line => line.Split(':')[0].TrimStart('-', ' ')), Is.EqualTo(AssistantBuilderCategories.Offered.Select(category => Enum.GetName(category))));
    }
}