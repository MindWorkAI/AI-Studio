using AIStudio.Tools.PluginSystem;

namespace AIStudio.Components;

/// <summary>
/// Knows which categories the assistants page shows right now.
/// </summary>
/// <remarks>
/// Whether a category shows any assistant is known only after its blocks rendered: the built-in assistants
/// come as markup and decide on their own whether they are visible. The category blocks therefore report
/// here after every rendering, just as the assistant blocks report to their category. The page asks which
/// categories its settings dialog offers, and whether it shows any category at all.
/// </remarks>
public sealed class AssistantCategoryRegistry
{
    private readonly HashSet<AssistantCategoryBlock> blocks = [];

    /// <summary>
    /// Raised when the page went from showing no category to showing one, or the other way round.
    /// </summary>
    /// <remarks>
    /// Only this change matters to the page itself, so it renders again only then, not whenever a block
    /// reports.
    /// </remarks>
    public event Action? ShowsAnyCategoryChanged;

    /// <summary>
    /// Whether the page shows at least one category, or null until the first block reported.
    /// </summary>
    public bool? ShowsAnyCategory { get; private set; }

    /// <summary>
    /// Gets the categories which show at least one assistant right now.
    /// </summary>
    public IReadOnlyList<AssistantCategoryKey> ShownCategories => this.blocks
        .Where(block => block.IsShowingAssistants)
        .Select(block => block.Category)
        .Distinct()
        .ToList();

    /// <summary>
    /// Adds a category block of the page.
    /// </summary>
    /// <param name="block">The block which joins the page.</param>
    internal void Register(AssistantCategoryBlock block) => this.blocks.Add(block);

    /// <summary>
    /// Removes a category block of the page, e.g., when the plugins of its category are gone.
    /// </summary>
    /// <param name="block">The block which leaves the page.</param>
    internal void Unregister(AssistantCategoryBlock block) => this.blocks.Remove(block);

    /// <summary>
    /// Takes note that a block rendered and might show something else now.
    /// </summary>
    internal void ReportChange()
    {
        var showsAnyCategory = this.blocks.Any(block => block.IsShowingAssistants);
        if (this.ShowsAnyCategory == showsAnyCategory)
            return;

        this.ShowsAnyCategory = showsAnyCategory;
        this.ShowsAnyCategoryChanged?.Invoke();
    }
}