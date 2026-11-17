using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Assistants.Builder;

/// <summary>
/// The categories the Assistant Builder offers and how its draft names them.
/// </summary>
/// <remarks>
/// The draft is Markdown which the user may edit, and the step which writes the plugin reads the category
/// from it. The section therefore names the category in a fixed form: the key of a category of AI Studio,
/// such as <c>BUSINESS</c>, or <c>CUSTOM: </c> followed by the name of a category of the user's own.
/// CORE belongs to configuration and language plugins, so the builder never offers it.
/// </remarks>
public static class AssistantBuilderCategories
{
    /// <summary>
    /// The prefix of a custom category in the draft.
    /// </summary>
    public const string CUSTOM_PREFIX = "CUSTOM:";

    /// <summary>
    /// Gets the categories of AI Studio the builder offers, in the order of the assistants page.
    /// </summary>
    public static IReadOnlyList<PluginCategory> Offered => Enum.GetValues<PluginCategory>()
        .Where(category => category is not (PluginCategory.NONE or PluginCategory.CORE or PluginCategory.CUSTOM))
        .Order(Comparer<PluginCategory>.Create((left, right) => AssistantPageSections.Compare(new(left), new(right))))
        .ToList();

    /// <summary>
    /// Writes a category the way the draft names it.
    /// </summary>
    /// <param name="category">The selected category, or NONE when the user left the choice to the model.</param>
    /// <param name="customName">The name of the custom category, when the category is CUSTOM.</param>
    /// <returns>The category in the form of the draft, or an empty string for NONE.</returns>
    public static string FormatForDraft(PluginCategory category, string customName) => category switch
    {
        PluginCategory.NONE => string.Empty,
        PluginCategory.CUSTOM => $"{CUSTOM_PREFIX} {customName.Trim()}",

        _ => Enum.GetName(category)!,
    };

    /// <summary>
    /// Lists the categories for a prompt: one line per key, with the name users read.
    /// </summary>
    /// <returns>The list of the offered categories as Markdown.</returns>
    public static string ListForPrompt() => string.Join('\n', Offered.Select(category => $"- {Enum.GetName(category)}: {category.Name}"));
}