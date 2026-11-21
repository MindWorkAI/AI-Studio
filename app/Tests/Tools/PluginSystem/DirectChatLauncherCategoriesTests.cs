using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tests.Tools.PluginSystem;

/// <summary>
/// Checks that AI Studio writes the categories of a direct chat launcher so that they load back.
/// </summary>
/// <remarks>
/// AI Studio writes the plugin.lua of a launcher itself, when the Assistant Builder creates one and
/// whenever a user changes its chat settings. Its categories decide where the assistants page shows it,
/// so they have to survive both.
/// </remarks>
[TestFixture]
public sealed class DirectChatLauncherCategoriesTests
{
    private static readonly Guid PLUGIN_ID = Guid.Parse("5b7d9f1a-3c5e-4a7b-8d9f-1b3d5f7a9c2e");

    [Test]
    public async Task CustomCategoriesLoadBack()
    {
        var lua = DirectChatLauncherLuaWriter.Write(CreateMetadata([PluginCategory.BUSINESS, PluginCategory.CUSTOM], ["Space Travel"]), CreateDefinition());
        using var plugin = await PluginFactory.Load(null, lua);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.Categories, Is.EqualTo(new[] { PluginCategory.BUSINESS, PluginCategory.CUSTOM }));
            Assert.That(plugin.CustomCategories, Is.EqualTo(new[] { "Space Travel" }));
        }
    }

    [Test]
    public async Task ALauncherWithoutCustomCategoriesWritesNoTableForThem()
    {
        var lua = DirectChatLauncherLuaWriter.Write(CreateMetadata([PluginCategory.EDUCATION], []), CreateDefinition());
        using var plugin = await PluginFactory.Load(null, lua);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(lua, Does.Not.Contain("CUSTOM_CATEGORIES"));
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.Categories, Is.EqualTo(new[] { PluginCategory.EDUCATION }));
        }
    }

    [Test]
    public async Task CustomWithoutItsNamesFallsBackInsteadOfBreakingThePlugin()
    {
        var lua = DirectChatLauncherLuaWriter.Write(CreateMetadata([PluginCategory.CUSTOM], []), CreateDefinition());
        using var plugin = await PluginFactory.Load(null, lua);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(plugin.IsValid, Is.True, string.Join(" ", plugin.Issues));
            Assert.That(plugin.Categories, Is.EqualTo(new[] { PluginCategory.COMMON }), "A launcher which lost the names of its custom categories lands under \"General\".");
        }
    }

    private static DirectChatLauncherPluginMetadata CreateMetadata(PluginCategory[] categories, string[] customCategories) => new(PLUGIN_ID, "1.0.0", null, ["MindWork AI"], "MindWork AI",
        "https://github.com/MindWorkAI/AI-Studio", categories, customCategories, [PluginTargetGroup.EVERYONE], IsMaintained: true, DeprecationMessage: string.Empty, IsAssistantBuilderGenerated: true);

    private static DirectChatLauncherDefinition CreateDefinition() => new("Launcher for the tests", "Launcher for the tests", "Opens a disappearing chat.", new AssistantChatLaunchConfiguration(string.Empty, null, null, null, null, null));
}