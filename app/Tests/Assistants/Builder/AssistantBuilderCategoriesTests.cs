using System.Text.Json;

using AIStudio.Assistants.Builder;
using AIStudio.Settings.DataModel;
using AIStudio.Tools;
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
        var lines = AssistantBuilderCategories.ListForPrompt(new DataApp()).Split('\n');

        Assert.That(lines.Select(line => line.Split(':')[0].TrimStart('-', ' ')), Is.EqualTo(AssistantBuilderCategories.Offered.Select(category => Enum.GetName(category))));
    }

    [Test]
    public void TheBuilderLeavesOutWhatYourOrganizationDisabled()
    {
        var app = new DataApp { DisabledAssistantCategories = [PluginCategory.ENTERTAINMENT, PluginCategory.CORE, PluginCategory.CUSTOM] };
        var offered = AssistantBuilderCategories.OfferedUnder(app);
        var listedKeys = AssistantBuilderCategories.ListForPrompt(app).Split('\n').Select(line => line.Split(':')[0].TrimStart('-', ' '));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(offered, Does.Not.Contain(PluginCategory.ENTERTAINMENT));
            Assert.That(offered, Does.Not.Contain(PluginCategory.COMMON), "CORE and COMMON are one category.");
            Assert.That(offered, Does.Contain(PluginCategory.BUSINESS));
            Assert.That(listedKeys, Is.EqualTo(offered.Select(category => Enum.GetName(category))), "The model must not choose a disabled category either.");
            Assert.That(AssistantBuilderCategories.IsCustomOfferedUnder(app), Is.False);
            Assert.That(AssistantBuilderCategories.IsCustomOfferedUnder(new DataApp()), Is.True);
        }
    }

    [Test]
    public void EveryDisabledCategoryOfAnAssistantIsFound()
    {
        var app = new DataApp
        {
            DisabledAssistantCategories = [PluginCategory.ENTERTAINMENT],
            DisabledCustomAssistantCategories = ["fun & games"],
        };

        var disabled = AssistantBuilderCategories.FindDisabled(app, [PluginCategory.ENTERTAINMENT, PluginCategory.BUSINESS, PluginCategory.CUSTOM], ["Fun & Games", "Space Travel"]);

        Assert.That(disabled, Is.EqualTo(new[] { new AssistantCategoryKey(PluginCategory.ENTERTAINMENT), new AssistantCategoryKey(PluginCategory.CUSTOM, "Fun & Games") }),
            "The builder refuses a disabled category even when another one is free: the user would build something the organization does not want.");
    }

    [Test]
    public void ValidCategoriesAreRead()
    {
        var isRead = AssistantBuilderCategories.TryRead(["BUSINESS", "CUSTOM", "BUSINESS"], [" Space Travel ", "space travel"], out var categories, out var customCategories);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isRead, Is.True);
            Assert.That(categories, Is.EqualTo(new[] { PluginCategory.BUSINESS, PluginCategory.CUSTOM }));
            Assert.That(customCategories, Is.EqualTo(new[] { "Space Travel" }));
        }
    }

    [Test]
    public void CoreStaysValidForRevisionsOfOlderPlugins()
    {
        Assert.That(AssistantBuilderCategories.TryRead(["CORE"], [], out _, out _), Is.True);
    }

    [TestCase(new string[0], new string[0], TestName = "NoCategory")]
    [TestCase(new[] { "business" }, new string[0], TestName = "AnotherSpelling")]
    [TestCase(new[] { "1" }, new string[0], TestName = "ANumber")]
    [TestCase(new[] { "NONE" }, new string[0], TestName = "None")]
    [TestCase(new[] { "BUSINESS, SCIENCE" }, new string[0], TestName = "TwoKeysInOne")]
    [TestCase(new[] { "MARKETING" }, new string[0], TestName = "AnUnknownKey")]
    [TestCase(new[] { "CUSTOM" }, new string[0], TestName = "CustomWithoutAName")]
    [TestCase(new[] { "BUSINESS" }, new[] { "Space Travel" }, TestName = "ANameWithoutCustom")]
    [TestCase(new[] { "CUSTOM" }, new[] { " " }, TestName = "AnEmptyName")]
    public void AnythingTheLoaderWouldNotAcceptIsRefused(string[] categoryNames, string[] customNames)
    {
        Assert.That(AssistantBuilderCategories.TryRead(categoryNames, customNames, out _, out _), Is.False);
    }

    [Test]
    public async Task TheSchemaListsExactlyTheCategoriesOfAIStudio()
    {
        var schema = await AppResources.ReadTextAsync("Assistants/Builder/AssistantBuilderLuaResponse.schema.json");
        using var document = JsonDocument.Parse(schema);
        var keys = document.RootElement.GetProperty("properties").GetProperty("plugin").GetProperty("properties").GetProperty("categories").GetProperty("items").GetProperty("enum")
            .EnumerateArray()
            .Select(key => key.GetString())
            .ToList();

        var expected = Enum.GetValues<PluginCategory>().Where(category => category is not PluginCategory.NONE).Select(category => Enum.GetName(category));
        Assert.That(keys, Is.EquivalentTo(expected), "Every category of AI Studio but NONE belongs into the schema, so that a model may name it.");
    }

    [Test]
    public void AnAnswerWithAnUnknownCategoryIsRefused()
    {
        var isParsed = LuaResponse.TryParse($$"""
            {
              "schema_version": "{{LuaResponse.SCHEMA_VERSION_VALUE}}",
              "plugin": { "name": "Orbit planner", "description": "Plans orbits.", "categories": ["SPACE"] },
              "assistant": { "kind": "FORM", "title": "Orbit planner", "description": "Plans orbits.", "system_prompt": "Plan orbits.", "submit_text": "Plan", "allow_ai_studio_profiles": true },
              "full_lua": "ID = \"00000000-0000-0000-0000-000000000000\""
            }
            """, out _, out var error, out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isParsed, Is.False);
            Assert.That(error, Is.EqualTo(LuaResponseParseError.INVALID_CATEGORIES));
        }
    }

    [Test]
    public void ALauncherAnswerWithACustomCategoryWithoutItsNameIsRefused()
    {
        var isParsed = LauncherTextsResponse.TryParse($$"""
            {
              "schema_version": "{{LauncherTextsResponse.SCHEMA_VERSION_VALUE}}",
              "plugin_name": "Weekly report",
              "title": "Weekly report",
              "description": "Opens the chat for the weekly report.",
              "categories": ["CUSTOM"],
              "custom_categories": []
            }
            """, out _, out var error, out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isParsed, Is.False);
            Assert.That(error, Is.EqualTo(LuaResponseParseError.INVALID_CATEGORIES));
        }
    }

    [Test]
    public void ALauncherAnswerWithItsCategoryIsRead()
    {
        var isParsed = LauncherTextsResponse.TryParse($$"""
            {
              "schema_version": "{{LauncherTextsResponse.SCHEMA_VERSION_VALUE}}",
              "plugin_name": "Weekly report",
              "title": "Weekly report",
              "description": "Opens the chat for the weekly report.",
              "categories": ["BUSINESS"],
              "custom_categories": []
            }
            """, out var response, out var error, out _);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(isParsed, Is.True, error.ToString());
            Assert.That(response.Categories, Is.EqualTo(new[] { "BUSINESS" }));
        }
    }
}