using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks how the assistants page groups the assistant plugins by their categories.
/// </summary>
/// <remarks>
/// Every assistant plugin appears under the categories it names, next to the built-in assistants of
/// the same category. The built-in sections lead the page in the order users know, the categories only
/// plugins name follow, and "AI Studio Development" closes the page.
/// </remarks>
[TestFixture]
public sealed class AssistantPageSectionsTests
{
    [Test]
    public void APluginWithCoreAppearsUnderGeneral()
    {
        var sections = Build(new FakeAssistantPlugin("Old builder assistant", [PluginCategory.CORE]));

        Assert.That(sections.Select(section => section.Category), Is.EqualTo(new[] { new AssistantCategoryKey(PluginCategory.COMMON) }));
    }

    [Test]
    public void APluginAppearsUnderEachOfItsCategories()
    {
        var plugin = new FakeAssistantPlugin("Market research", [PluginCategory.BUSINESS, PluginCategory.SCIENCE]);
        var sections = Build(plugin);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sections.PluginsOf(new AssistantCategoryKey(PluginCategory.BUSINESS)), Is.EqualTo(new[] { plugin }));
            Assert.That(sections.PluginsOf(new AssistantCategoryKey(PluginCategory.SCIENCE)), Is.EqualTo(new[] { plugin }));
        }
    }

    [Test]
    public void APluginAppearsOncePerSection()
    {
        var plugin = new FakeAssistantPlugin("Everyday helper", [PluginCategory.CORE, PluginCategory.COMMON]);
        var sections = Build(plugin);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sections, Has.Count.EqualTo(1));
            Assert.That(sections[0].Assistants, Is.EqualTo(new[] { plugin }), "CORE and COMMON share the section \"General\".");
        }
    }

    [Test]
    public void CustomCategoriesWhichDifferOnlyInTheirCaseShareOneSection()
    {
        var first = new FakeAssistantPlugin("Orbit planner", [PluginCategory.CUSTOM], ["Space Travel"]);
        var second = new FakeAssistantPlugin("Launch checklist", [PluginCategory.CUSTOM], ["space travel"]);
        var sections = Build(first, second);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(sections, Has.Count.EqualTo(1));
            Assert.That(sections[0].Category.Name, Is.EqualTo("Space Travel"), "The section keeps the spelling it met first.");
            Assert.That(sections[0].Assistants, Is.EqualTo(new[] { second, first }), "The plugins of a section are sorted by title.");
        }
    }

    [Test]
    public void TheSectionsFollowTheOrderOfThePage()
    {
        var sections = Build(
            new FakeAssistantPlugin("Log helper", [PluginCategory.AI_STUDIO_DEVELOPMENT]),
            new FakeAssistantPlugin("Orbit planner", [PluginCategory.CUSTOM], ["Space Travel"]),
            new FakeAssistantPlugin("Quiz", [PluginCategory.ENTERTAINMENT]),
            new FakeAssistantPlugin("Code reviewer", [PluginCategory.SOFTWARE_DEVELOPMENT]),
            new FakeAssistantPlugin("Lab notes", [PluginCategory.SCIENCE]),
            new FakeAssistantPlugin("Flash cards", [PluginCategory.EDUCATION]),
            new FakeAssistantPlugin("Invoice checker", [PluginCategory.BUSINESS]),
            new FakeAssistantPlugin("Letter writer", [PluginCategory.COMMON]),
            new FakeAssistantPlugin("Deep space notes", [PluginCategory.CUSTOM], ["Deep Space"]));

        var expected = new[]
        {
            new AssistantCategoryKey(PluginCategory.COMMON),
            new AssistantCategoryKey(PluginCategory.BUSINESS),
            new AssistantCategoryKey(PluginCategory.EDUCATION),
            new AssistantCategoryKey(PluginCategory.SOFTWARE_DEVELOPMENT),
            new AssistantCategoryKey(PluginCategory.ENTERTAINMENT),
            new AssistantCategoryKey(PluginCategory.SCIENCE),
            new AssistantCategoryKey(PluginCategory.CUSTOM, "Deep Space"),
            new AssistantCategoryKey(PluginCategory.CUSTOM, "Space Travel"),
            new AssistantCategoryKey(PluginCategory.AI_STUDIO_DEVELOPMENT),
        };

        Assert.That(sections.Select(section => section.Category), Is.EqualTo(expected));
    }

    [Test]
    public void OnlyTheSectionsOfBuiltInAssistantsCountAsBuiltIn()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(AssistantPageSections.IsBuiltIn(new AssistantCategoryKey(PluginCategory.CORE)), Is.True);
            Assert.That(AssistantPageSections.IsBuiltIn(new AssistantCategoryKey(PluginCategory.AI_STUDIO_DEVELOPMENT)), Is.True);
            Assert.That(AssistantPageSections.IsBuiltIn(new AssistantCategoryKey(PluginCategory.SCIENCE)), Is.False);
            Assert.That(AssistantPageSections.IsBuiltIn(new AssistantCategoryKey(PluginCategory.CUSTOM, "General")), Is.False, "A custom category never takes the place of a category of AI Studio, whatever its name.");
        }
    }

    [Test]
    public void TheSettingsOfferTheShownAndTheHiddenCategoriesInTheOrderOfThePage()
    {
        var app = new DataApp { DisabledAssistantCategories = [PluginCategory.ENTERTAINMENT] };
        var page = new DataAssistantsPage
        {
            HiddenCategories = [PluginCategory.SCIENCE, PluginCategory.ENTERTAINMENT, PluginCategory.BUSINESS],
            HiddenCustomCategories = ["space travel"],
        };

        var categories = AssistantPageSections.SelectForSettings(
            [new AssistantCategoryKey(PluginCategory.AI_STUDIO_DEVELOPMENT), new AssistantCategoryKey(PluginCategory.CORE), new AssistantCategoryKey(PluginCategory.CUSTOM, "Space Travel")],
            app,
            page);

        Assert.That(categories, Is.EqualTo(new[]
        {
            new AssistantCategoryKey(PluginCategory.COMMON),
            new AssistantCategoryKey(PluginCategory.BUSINESS),
            new AssistantCategoryKey(PluginCategory.SCIENCE),
            new AssistantCategoryKey(PluginCategory.CUSTOM, "Space Travel"),
            new AssistantCategoryKey(PluginCategory.AI_STUDIO_DEVELOPMENT),
        }), "A category your organization disabled gets no switch, since no switch of the user brings it back.");
    }

    private static IReadOnlyList<AssistantPageSection<FakeAssistantPlugin>> Build(params FakeAssistantPlugin[] plugins) => AssistantPageSections.Build(plugins, plugin => plugin.Name);
}