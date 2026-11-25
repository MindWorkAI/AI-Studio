using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.PluginSystem.Assistants;

/// <summary>
/// Decides which sections the assistants page shows and in which order.
/// </summary>
/// <remarks>
/// The built-in assistants and the assistant plugins share one set of categories, see
/// AssistantCategoryKey. The page shows the sections of its built-in assistants first, in
/// the order users know: "General", "Business", "Learning &amp; Reflecting", and "Software Development".
/// The categories which only plugins name follow, the ones of AI Studio before the custom ones, each
/// group sorted by its name. "AI Studio Development" comes last, since it concerns the development of
/// AI Studio itself.
/// </remarks>
public static class AssistantPageSections
{
    /// <summary>
    /// The groups of sections, in the order of the page.
    /// </summary>
    private enum PageGroup
    {
        LEADING,
        OTHER,
        CUSTOM,
        TRAILING,
    }

    /// <summary>
    /// A plugin in one of its categories.
    /// </summary>
    /// <param name="Category">One category of the plugin.</param>
    /// <param name="Plugin">The plugin.</param>
    /// <typeparam name="TPlugin">The type of the plugin.</typeparam>
    private sealed record SectionMember<TPlugin>(AssistantCategoryKey Category, TPlugin Plugin);

    /// <summary>
    /// The categories of the built-in assistants, which lead the page, in their order.
    /// </summary>
    public static readonly IReadOnlyList<AssistantCategoryKey> LEADING_CATEGORIES =
    [
        new(PluginCategory.COMMON),
        new(PluginCategory.BUSINESS),
        new(PluginCategory.EDUCATION),
        new(PluginCategory.SOFTWARE_DEVELOPMENT),
    ];

    /// <summary>
    /// The category of the built-in assistants which ends the page.
    /// </summary>
    public static readonly AssistantCategoryKey TRAILING_CATEGORY = new(PluginCategory.AI_STUDIO_DEVELOPMENT);

    /// <summary>
    /// Whether the page shows a section of this category for its built-in assistants anyway.
    /// </summary>
    /// <param name="category">The category in question.</param>
    /// <returns>True for the categories of the built-in assistants.</returns>
    public static bool IsBuiltIn(AssistantCategoryKey category) => LEADING_CATEGORIES.Contains(category) || category == TRAILING_CATEGORY;

    /// <summary>
    /// Compares two categories by their place on the assistants page.
    /// </summary>
    /// <param name="left">The first category.</param>
    /// <param name="right">The second category.</param>
    /// <returns>Less than zero when the first category comes first, zero when both are the same, otherwise more than zero.</returns>
    public static int Compare(AssistantCategoryKey left, AssistantCategoryKey right)
    {
        var byGroup = Group(left).CompareTo(Group(right));
        if (byGroup != 0)
            return byGroup;

        var leftIndex = IndexOfLeading(left);
        if (leftIndex >= 0)
            return leftIndex.CompareTo(IndexOfLeading(right));

        return NameComparer.Compare(left.Name, right.Name);
    }

    /// <summary>
    /// Selects the categories the settings dialog of the assistants page offers, in the order of the page.
    /// </summary>
    /// <remarks>
    /// The dialog offers the categories the page shows right now, so that every switch changes something
    /// the user sees, and every category the user hid, so that each one can come back. Categories your
    /// organization disabled are missing: no switch of the user brings them back.
    /// </remarks>
    /// <param name="shownCategories">The categories which show at least one assistant right now.</param>
    /// <param name="app">The app settings which hold the categories your organization disabled.</param>
    /// <param name="page">The settings of the assistants page, which hold the categories the user hid.</param>
    /// <returns>The categories for the dialog, each once.</returns>
    public static IReadOnlyList<AssistantCategoryKey> SelectForSettings(IEnumerable<AssistantCategoryKey> shownCategories, DataApp app, DataAssistantsPage page)
    {
        var hiddenCategories = page.HiddenCategories
            .Select(category => new AssistantCategoryKey(category))
            .Concat(page.HiddenCustomCategories.Select(name => new AssistantCategoryKey(PluginCategory.CUSTOM, name)));

        return shownCategories
            .Concat(hiddenCategories)
            .Distinct()
            .Where(category => !app.IsAssistantCategoryDisabled(category))
            .Order(Comparer<AssistantCategoryKey>.Create(Compare))
            .ToList();
    }

    /// <summary>
    /// Groups the assistant plugins into the sections of the assistants page.
    /// </summary>
    /// <remarks>
    /// A plugin appears in each of its categories, but only once per section. The sections come in the
    /// order of the page, and their plugins sorted by title.
    /// </remarks>
    /// <param name="plugins">The assistant plugins to show.</param>
    /// <param name="titleOf">Gets the title under which a plugin appears.</param>
    /// <typeparam name="TPlugin">The type of the plugins.</typeparam>
    /// <returns>One section per category any of the plugins names.</returns>
    public static IReadOnlyList<AssistantPageSection<TPlugin>> Build<TPlugin>(IEnumerable<TPlugin> plugins, Func<TPlugin, string> titleOf) where TPlugin : IPluginMetadata
    {
        var titleComparer = NameComparer;
        return plugins
            .SelectMany(plugin => AssistantCategoryKey.AllOf(plugin).Select(category => new SectionMember<TPlugin>(category, plugin)))
            .GroupBy(member => member.Category)
            .Order(Comparer<IGrouping<AssistantCategoryKey, SectionMember<TPlugin>>>.Create((left, right) => Compare(left.Key, right.Key)))
            .Select(group => new AssistantPageSection<TPlugin>(group.Key, group.Select(member => member.Plugin).OrderBy(titleOf, titleComparer).ToList()))
            .ToList();
    }

    /// <param name="sections">The sections of the assistants page.</param>
    /// <typeparam name="TPlugin">The type of the plugins.</typeparam>
    extension<TPlugin>(IReadOnlyList<AssistantPageSection<TPlugin>> sections)
    {
        /// <summary>
        /// Gets the assistant plugins of one category.
        /// </summary>
        /// <param name="category">The category in question.</param>
        /// <returns>The plugins of the category, or none when no plugin names it.</returns>
        public IReadOnlyList<TPlugin> PluginsOf(AssistantCategoryKey category) => sections.FirstOrDefault(section => section.Category == category)?.Assistants ?? [];
    }

    /// <summary>
    /// Compares names the way the user reads them: in the culture of the active language, without regard to case.
    /// </summary>
    private static StringComparer NameComparer => StringComparer.Create(I18N.I.Culture, ignoreCase: true);

    private static int IndexOfLeading(AssistantCategoryKey category)
    {
        for (var index = 0; index < LEADING_CATEGORIES.Count; index++)
            if (LEADING_CATEGORIES[index] == category)
                return index;

        return -1;
    }

    private static PageGroup Group(AssistantCategoryKey category)
    {
        if (IndexOfLeading(category) >= 0)
            return PageGroup.LEADING;

        if (category == TRAILING_CATEGORY)
            return PageGroup.TRAILING;

        return category.Category is PluginCategory.CUSTOM ? PageGroup.CUSTOM : PageGroup.OTHER;
    }
}