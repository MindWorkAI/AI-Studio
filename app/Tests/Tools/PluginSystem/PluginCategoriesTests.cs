using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks how a plugin states its categories, its custom ones included.
/// </summary>
/// <remarks>
/// The assistants page shows each assistant under its categories. A plugin may also name categories of
/// its own: it declares CUSTOM in CATEGORIES and lists their names in CUSTOM_CATEGORIES. Both belong
/// together, so a plugin which states only one of them is invalid, and its authors notice it right away.
/// </remarks>
[TestFixture]
public sealed class PluginCategoriesTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("8d2e4b6a-1c3f-4a5e-9b7d-0f2a4c6e8b1d");

    [Test]
    public async Task TheNewCategoriesAreRead()
    {
        using var plugin = await LoadModelPlugin("""CATEGORIES = {"COMMON", "AI_STUDIO_DEVELOPMENT"}""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.Categories, Is.EqualTo(new[] { PluginCategory.COMMON, PluginCategory.AI_STUDIO_DEVELOPMENT }));
            Assert.That(plugin.CustomCategories, Is.Empty);
        }
    }

    [Test]
    public async Task CustomCategoriesAreRead()
    {
        using var plugin = await LoadModelPlugin("""
            CATEGORIES = {"SCIENCE", "CUSTOM"}
            CUSTOM_CATEGORIES = {"Space Travel", " Deep Space "}
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.Categories, Is.EqualTo(new[] { PluginCategory.SCIENCE, PluginCategory.CUSTOM }));
            Assert.That(plugin.CustomCategories, Is.EqualTo(new[] { "Space Travel", "Deep Space" }), "Names are trimmed, so that equal names share one heading.");
        }
    }

    [Test]
    public async Task NamesWhichDifferOnlyInTheirCaseAreMerged()
    {
        using var plugin = await LoadModelPlugin("""
            CATEGORIES = {"CUSTOM"}
            CUSTOM_CATEGORIES = {"Space Travel", "space travel", "Deep Space"}
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.CustomCategories, Is.EqualTo(new[] { "Space Travel", "Deep Space" }), "The first spelling wins.");
        }
    }

    [Test]
    public async Task AnEmptyTableWithoutCustomIsNoIssue()
    {
        using var plugin = await LoadModelPlugin("""
            CATEGORIES = {"BUSINESS"}
            CUSTOM_CATEGORIES = {}
            """);

        Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
    }

    [TestCase("""CATEGORIES = {"CUSTOM"}""", TestName = "CustomWithoutTheTable")]
    [TestCase("""
        CATEGORIES = {"CUSTOM"}
        CUSTOM_CATEGORIES = {}
        """, TestName = "CustomWithAnEmptyTable")]
    [TestCase("""
        CATEGORIES = {"BUSINESS"}
        CUSTOM_CATEGORIES = {"Space Travel"}
        """, TestName = "NamesWithoutCustom")]
    [TestCase("""
        CATEGORIES = {"CUSTOM"}
        CUSTOM_CATEGORIES = {"Space Travel", "  "}
        """, TestName = "AnEmptyName")]
    [TestCase("""
        CATEGORIES = {"CUSTOM"}
        CUSTOM_CATEGORIES = {"Space Travel", 42}
        """, TestName = "ANameWhichIsNoText")]
    [TestCase("""
        CATEGORIES = {"CUSTOM"}
        CUSTOM_CATEGORIES = "Space Travel"
        """, TestName = "ANameInsteadOfATable")]
    public async Task AnIncompleteDeclarationMakesThePluginInvalid(string categoriesLines)
    {
        using var plugin = await LoadModelPlugin(categoriesLines);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plugin.IsValid, Is.False);
            Assert.That(plugin.Issues, Has.Some.Contains("CUSTOM"));
        }
    }

    /// <summary>
    /// Loads a model plugin, the plugin type with the fewest fields beyond the ones every plugin has.
    /// </summary>
    /// <param name="categoriesLines">The Lua lines which state CATEGORIES and, optionally, CUSTOM_CATEGORIES.</param>
    private static Task<PluginBase> LoadModelPlugin(string categoriesLines) => PluginFactory.Load(null, $$"""
        ID = "{{PLUGIN_ID}}"
        NAME = "Models for the tests"
        DESCRIPTION = "A model plugin which declares no models."
        VERSION = "1.0.0"
        TYPE = "MODEL"
        AUTHORS = {"MindWork AI"}
        SUPPORT_CONTACT = "MindWork AI"
        SOURCE_URL = "https://github.com/MindWorkAI/AI-Studio"
        {{categoriesLines}}
        TARGET_GROUPS = {"EVERYONE"}
        IS_MAINTAINED = true
        DEPRECATION_MESSAGE = ""
        MODELS = {}
        """);
}
