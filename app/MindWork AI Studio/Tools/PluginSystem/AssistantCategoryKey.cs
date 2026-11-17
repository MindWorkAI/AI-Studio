namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// One category of assistants, as the assistants page groups them: a category of AI Studio, or a
/// custom category a plugin names itself.
/// </summary>
/// <remarks>
/// Two rules decide when two categories are the same. CORE belongs to configuration and language
/// plugins; an assistant plugin which names it nevertheless appears under "General", so CORE becomes
/// COMMON. Custom categories come in the language of their authors, so two plugins may well spell one
/// category differently: their names are compared without regard to case.
/// </remarks>
public sealed record AssistantCategoryKey
{
    /// <param name="category">The category of AI Studio.</param>
    /// <param name="customName">The name of the custom category, when the category is CUSTOM.</param>
    public AssistantCategoryKey(PluginCategory category, string customName = "")
    {
        this.Category = category is PluginCategory.CORE ? PluginCategory.COMMON : category;
        this.CustomName = this.Category is PluginCategory.CUSTOM ? customName.Trim() : string.Empty;
    }

    /// <summary>
    /// The category of AI Studio, never CORE.
    /// </summary>
    public PluginCategory Category { get; }

    /// <summary>
    /// The name of the custom category, or an empty string for any other category.
    /// </summary>
    public string CustomName { get; }

    /// <summary>
    /// The name of the category as the user reads it: localized for a category of AI Studio, as the
    /// plugin wrote it for a custom one.
    /// </summary>
    public string Name => this.Category is PluginCategory.CUSTOM ? this.CustomName : this.Category.Name;

    /// <summary>
    /// Gets the categories a plugin names, its custom ones included.
    /// </summary>
    /// <remarks>
    /// CUSTOM stands for all names of CUSTOM_CATEGORIES. Each category appears once, also when a plugin
    /// names CORE and COMMON at the same time.
    /// </remarks>
    /// <param name="plugin">The plugin in question.</param>
    /// <returns>The categories of the plugin, in the order the plugin names them.</returns>
    public static IReadOnlyList<AssistantCategoryKey> AllOf(IPluginMetadata plugin) => AllOf(plugin.Categories, plugin.CustomCategories);

    /// <summary>
    /// Gets the categories which CATEGORIES and CUSTOM_CATEGORIES name together.
    /// </summary>
    /// <remarks>
    /// For categories which are not loaded yet, e.g., those of a model answer. See the overload for a plugin.
    /// </remarks>
    /// <param name="categories">The categories of AI Studio.</param>
    /// <param name="customCategories">The names of the custom categories.</param>
    /// <returns>The categories, each once, in the order they are named.</returns>
    public static IReadOnlyList<AssistantCategoryKey> AllOf(IEnumerable<PluginCategory> categories, IEnumerable<string> customCategories) => categories
        .SelectMany(category => category is PluginCategory.CUSTOM
            ? customCategories.Select(name => new AssistantCategoryKey(PluginCategory.CUSTOM, name))
            : [new AssistantCategoryKey(category)])
        .Distinct()
        .ToList();

    /// <inheritdoc />
    public bool Equals(AssistantCategoryKey? other) => other is not null && this.Category == other.Category && string.Equals(this.CustomName, other.CustomName, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(this.Category, StringComparer.OrdinalIgnoreCase.GetHashCode(this.CustomName));
}