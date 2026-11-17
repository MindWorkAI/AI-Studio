using AIStudio.Settings;
using AIStudio.Settings.DataModel;
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
    /// Gets the categories of AI Studio the builder offers under the rules of your organization, in the
    /// order of the assistants page.
    /// </summary>
    /// <param name="app">The app settings which hold the categories your organization disabled.</param>
    /// <returns>The offered categories without the ones your organization disabled.</returns>
    public static IReadOnlyList<PluginCategory> OfferedUnder(DataApp app) => Offered
        .Where(category => !app.IsAssistantCategoryDisabled(new AssistantCategoryKey(category)))
        .ToList();

    /// <summary>
    /// Whether the builder offers custom categories under the rules of your organization.
    /// </summary>
    /// <param name="app">The app settings which hold the categories your organization disabled.</param>
    /// <returns>False when your organization disabled CUSTOM, i.e., every custom category.</returns>
    public static bool IsCustomOfferedUnder(DataApp app) => !app.DisabledAssistantCategories.Contains(PluginCategory.CUSTOM);

    /// <summary>
    /// Finds the categories of a new or revised assistant which your organization disabled.
    /// </summary>
    /// <remarks>
    /// The builder creates no assistant with a disabled category, even when another category would
    /// keep it available on the assistants page: the user would build something the organization
    /// does not want on its devices.
    /// </remarks>
    /// <param name="app">The app settings which hold the categories your organization disabled.</param>
    /// <param name="categories">The categories of the assistant.</param>
    /// <param name="customCategories">The names of its custom categories.</param>
    /// <returns>The disabled categories, each once, or an empty list.</returns>
    public static IReadOnlyList<AssistantCategoryKey> FindDisabled(DataApp app, IEnumerable<PluginCategory> categories, IEnumerable<string> customCategories) =>
        AssistantCategoryKey.AllOf(categories, customCategories)
            .Where(category => app.IsAssistantCategoryDisabled(category))
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
    /// Lists the categories a model may choose from: one line per key, with the name users read.
    /// </summary>
    /// <param name="app">The app settings which hold the categories your organization disabled.</param>
    /// <returns>The list of the offered categories as Markdown, without the ones your organization disabled.</returns>
    public static string ListForPrompt(DataApp app) => string.Join('\n', OfferedUnder(app).Select(category => $"- {Enum.GetName(category)}: {category.Name}"));

    /// <summary>
    /// Reads the categories a model names for a plugin, and refuses anything else than what the plugin
    /// loader accepts.
    /// </summary>
    /// <remarks>
    /// The loader reads CATEGORIES by the exact names of the categories and drops what it does not know.
    /// A model which writes "business" or "1" would thus leave a plugin without its category, or without
    /// any. We refuse such an answer instead of guessing what the model meant. CORE stays valid, so that
    /// a revision of a plugin written before COMMON existed keeps working. The names of custom categories
    /// belong to CUSTOM, in both directions, as in the loader.
    /// </remarks>
    /// <param name="categoryNames">The keys of the categories, as the model wrote them.</param>
    /// <param name="customNames">The names of the custom categories, as the model wrote them.</param>
    /// <param name="categories">The categories, each once, in the order of the model.</param>
    /// <param name="customCategories">The trimmed names of the custom categories, each once.</param>
    /// <returns>True when the model named at least one category, and only valid ones.</returns>
    public static bool TryRead(IReadOnlyList<string> categoryNames, IReadOnlyList<string> customNames, out PluginCategory[] categories, out string[] customCategories)
    {
        categories = [];
        customCategories = [];
        if (categoryNames.Count == 0)
            return false;

        var readCategories = new List<PluginCategory>();
        foreach (var categoryName in categoryNames)
        {
            if (!PluginCategory.TryParseName(categoryName, out var category) || category is PluginCategory.NONE || !string.Equals(Enum.GetName(category), categoryName, StringComparison.Ordinal))
                return false;

            if (!readCategories.Contains(category))
                readCategories.Add(category);
        }

        if (customNames.Any(string.IsNullOrWhiteSpace))
            return false;

        var readCustomNames = customNames.Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (readCategories.Contains(PluginCategory.CUSTOM) != readCustomNames.Length > 0)
            return false;

        categories = readCategories.ToArray();
        customCategories = readCustomNames;
        return true;
    }

    /// <summary>
    /// Whether a loaded plugin states the same categories as the model answer which came with it.
    /// </summary>
    /// <param name="plugin">The plugin as AI Studio loaded it.</param>
    /// <param name="categories">The categories the answer names.</param>
    /// <param name="customCategories">The custom categories the answer names.</param>
    /// <returns>True when both name the same categories, in any order.</returns>
    public static bool MatchPlugin(IPluginMetadata plugin, IReadOnlyCollection<PluginCategory> categories, IReadOnlyCollection<string> customCategories) =>
        plugin.Categories.ToHashSet().SetEquals(categories) &&
        plugin.CustomCategories.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(customCategories);
}