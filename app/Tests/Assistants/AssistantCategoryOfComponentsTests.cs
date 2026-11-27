using AIStudio.Tools;
using AIStudio.Tools.PluginSystem;

using ComponentKind = AIStudio.Tools.Components;

namespace AIStudio.Tests.Assistants;

/// <summary>
/// Checks that every built-in assistant states its category.
/// </summary>
/// <remarks>
/// The assistants page and everything which offers assistants elsewhere decide by the category, e.g.,
/// when an organization disables one. An assistant without a category would escape such a decision, so a
/// new assistant has to get one, see Components.Category.
/// </remarks>
[TestFixture]
public sealed class AssistantCategoryOfComponentsTests
{
    /// <summary>
    /// The components which are no assistant of their own.
    /// </summary>
    private static readonly ComponentKind[] NO_ASSISTANTS =
    [
        ComponentKind.NONE,
        ComponentKind.CHAT,
        ComponentKind.DYNAMIC_ASSISTANT,
        ComponentKind.WRITER,
        ComponentKind.APP_SETTINGS,
        ComponentKind.AI_TASK_TEXT_CONTENT_CLEANER,
        ComponentKind.AI_TASK_DATA_SOURCE_SELECTION,
        ComponentKind.AI_TASK_RETRIEVAL_CONTEXT_VALIDATION,
        ComponentKind.AI_TASK_ASSISTANT_PLUGIN_AUDIT,
    ];

    [Test]
    public void EveryAssistantHasACategory()
    {
        var withoutCategory = Enum.GetValues<ComponentKind>()
            .Where(component => !NO_ASSISTANTS.Contains(component))
            .Where(component => component.Category is PluginCategory.NONE)
            .ToList();

        Assert.That(withoutCategory, Is.Empty, "Give each of these assistants its category in Components.Category, or list it here when it is no assistant.");
    }

    [Test]
    public void NoAssistantUsesTheCategoryOfConfigurationPlugins()
    {
        var withCore = Enum.GetValues<ComponentKind>().Where(component => component.Category is PluginCategory.CORE or PluginCategory.CUSTOM).ToList();

        Assert.That(withCore, Is.Empty, "Built-in assistants use COMMON instead of CORE, and never a custom category.");
    }

    [Test]
    public void TheComponentsWhichAreNoAssistantHaveNoCategory()
    {
        var withCategory = NO_ASSISTANTS.Where(component => component.Category is not PluginCategory.NONE).ToList();

        Assert.That(withCategory, Is.Empty);
    }
}