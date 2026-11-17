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
        var sections = Build(new FakePlugin("Old builder assistant", [PluginCategory.CORE]));

        Assert.That(sections.Select(section => section.Category), Is.EqualTo(new[] { new AssistantCategoryKey(PluginCategory.COMMON) }));
    }

    [Test]
    public void APluginAppearsUnderEachOfItsCategories()
    {
        var plugin = new FakePlugin("Market research", [PluginCategory.BUSINESS, PluginCategory.SCIENCE]);
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
        var plugin = new FakePlugin("Everyday helper", [PluginCategory.CORE, PluginCategory.COMMON]);
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
        var first = new FakePlugin("Orbit planner", [PluginCategory.CUSTOM], ["Space Travel"]);
        var second = new FakePlugin("Launch checklist", [PluginCategory.CUSTOM], ["space travel"]);
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
            new FakePlugin("Log helper", [PluginCategory.AI_STUDIO_DEVELOPMENT]),
            new FakePlugin("Orbit planner", [PluginCategory.CUSTOM], ["Space Travel"]),
            new FakePlugin("Quiz", [PluginCategory.ENTERTAINMENT]),
            new FakePlugin("Code reviewer", [PluginCategory.SOFTWARE_DEVELOPMENT]),
            new FakePlugin("Lab notes", [PluginCategory.SCIENCE]),
            new FakePlugin("Flash cards", [PluginCategory.EDUCATION]),
            new FakePlugin("Invoice checker", [PluginCategory.BUSINESS]),
            new FakePlugin("Letter writer", [PluginCategory.COMMON]),
            new FakePlugin("Deep space notes", [PluginCategory.CUSTOM], ["Deep Space"]));

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

    private static IReadOnlyList<AssistantPageSection<FakePlugin>> Build(params FakePlugin[] plugins) => AssistantPageSections.Build(plugins, plugin => plugin.Name);

    /// <summary>
    /// An assistant plugin, reduced to what the sections need.
    /// </summary>
    private sealed class FakePlugin(string name, PluginCategory[] categories, string[]? customCategories = null) : IPluginMetadata
    {
        public string IconDataUrl => string.Empty;

        public PluginType Type => PluginType.ASSISTANT;

        public Guid Id { get; } = Guid.NewGuid();

        public string Name => name;

        public string Description => string.Empty;

        public PluginVersion Version => new(1, 0, 0);

        public DateTimeOffset? LastChanged => null;

        public string[] Authors => [];

        public string SupportContact => string.Empty;

        public string SourceURL => string.Empty;

        public PluginCategory[] Categories => categories;

        public string[] CustomCategories => customCategories ?? [];

        public PluginTargetGroup[] TargetGroups => [];

        public bool IsMaintained => true;

        public string DeprecationMessage => string.Empty;

        public bool IsInternal => false;
    }
}