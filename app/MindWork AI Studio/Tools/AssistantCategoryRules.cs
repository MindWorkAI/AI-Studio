using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools;

/// <summary>
/// Decides what the categories of an assistant allow, built-in assistants and plugins alike: whether your
/// organization disabled them, and whether the user collapsed or hid them on the assistants page.
/// </summary>
/// <remarks>
/// An organization disables categories through DataApp.DisabledAssistantCategories and
/// DataApp.DisabledCustomAssistantCategories, e.g., to keep entertainment plugins off its devices. An
/// assistant of a disabled category is gone everywhere: from the assistants page, from "Send to …", from
/// links, from direct chat launchers, and from the Assistant Builder. Ask these rules instead of reading the
/// settings: a check of your own would miss CORE, which counts as COMMON, or a custom category spelled
/// differently.<br/><br/>
/// Built-in assistants have exactly one category, see ComponentsExtensions.Category; IsAssistantVisible
/// checks it. A plugin may have several, and it stays available while one of them is not disabled. A plugin
/// your organization deployed itself is never disabled by its categories, see
/// PluginFactory.IsAssistantDeployedByOrganization.
/// </remarks>
public static class AssistantCategoryRules
{
    /// <param name="app">The app settings which hold the categories your organization disabled.</param>
    extension(DataApp app)
    {
        /// <summary>
        /// Whether your organization disabled this category.
        /// </summary>
        /// <remarks>
        /// CORE and COMMON are one category, because both share the heading "General". Disabling CUSTOM
        /// disables every custom category.
        /// </remarks>
        /// <param name="category">The category in question.</param>
        /// <returns>True when your organization disabled the category.</returns>
        public bool IsAssistantCategoryDisabled(AssistantCategoryKey category)
        {
            if (category.Category is PluginCategory.CUSTOM)
                return app.DisabledAssistantCategories.Contains(PluginCategory.CUSTOM)
                       || app.DisabledCustomAssistantCategories.Any(name => category.Equals(new AssistantCategoryKey(PluginCategory.CUSTOM, name)));

            return app.DisabledAssistantCategories.Any(disabledCategory => category.Equals(new AssistantCategoryKey(disabledCategory)));
        }

        /// <summary>
        /// Whether your organization disabled an assistant plugin through its categories.
        /// </summary>
        /// <param name="plugin">The plugin in question.</param>
        /// <param name="isDeployedByOrganization">Whether your organization deployed the plugin itself, see PluginFactory.IsAssistantDeployedByOrganization.</param>
        /// <returns>True when every category of the plugin is disabled and your organization did not deploy it.</returns>
        public bool DisablesAssistantPlugin(IPluginMetadata plugin, bool isDeployedByOrganization)
        {
            if (isDeployedByOrganization)
                return false;

            var categories = AssistantCategoryKey.AllOf(plugin);
            return categories.Count > 0 && categories.All(category => app.IsAssistantCategoryDisabled(category));
        }
    }

    /// <param name="page">The settings of the assistants page, which hold the categories the user collapsed or hid.</param>
    extension(DataAssistantsPage page)
    {
        /// <summary>
        /// Whether the user collapsed this category on the assistants page.
        /// </summary>
        /// <param name="category">The category in question.</param>
        /// <returns>True when the category is collapsed.</returns>
        public bool IsCategoryCollapsed(AssistantCategoryKey category) => Contains(page.CollapsedCategories, page.CollapsedCustomCategories, category);

        /// <summary>
        /// Whether the user hid this category on the assistants page.
        /// </summary>
        /// <param name="category">The category in question.</param>
        /// <returns>True when the category is hidden.</returns>
        public bool IsCategoryHidden(AssistantCategoryKey category) => Contains(page.HiddenCategories, page.HiddenCustomCategories, category);

        /// <summary>
        /// Collapses a category on the assistants page or expands it again.
        /// </summary>
        /// <param name="category">The category in question.</param>
        /// <param name="isCollapsed">True to collapse the category, false to expand it.</param>
        public void SetCategoryCollapsed(AssistantCategoryKey category, bool isCollapsed) => Set(page.CollapsedCategories, page.CollapsedCustomCategories, category, isCollapsed);

        /// <summary>
        /// Hides a category on the assistants page or shows it again.
        /// </summary>
        /// <param name="category">The category in question.</param>
        /// <param name="isHidden">True to hide the category, false to show it.</param>
        public void SetCategoryHidden(AssistantCategoryKey category, bool isHidden) => Set(page.HiddenCategories, page.HiddenCustomCategories, category, isHidden);

        /// <summary>
        /// Whether AI Studio offers the assistants of this category outside of the assistants page, e.g.,
        /// in the "Send to ..." menu.
        /// </summary>
        /// <remarks>
        /// A hidden category follows the option for hidden categories, also when it is collapsed as well;
        /// a collapsed one follows the option for collapsed categories; any other category offers its
        /// assistants. An assistant with several categories is offered as soon as one of them offers it.
        /// Whether your organization disabled the category is a question of its own, see
        /// IsAssistantCategoryDisabled.
        /// </remarks>
        /// <param name="category">The category in question.</param>
        /// <returns>True when AI Studio offers the assistants of the category.</returns>
        public bool IsCategoryOffering(AssistantCategoryKey category)
        {
            if (page.IsCategoryHidden(category))
                return page.HiddenCategoryAssistants is AssistantOfferBehavior.KEEP_OFFERING;

            if (page.IsCategoryCollapsed(category))
                return page.CollapsedCategoryAssistants is AssistantOfferBehavior.KEEP_OFFERING;

            return true;
        }

        /// <summary>
        /// Whether AI Studio offers a built-in assistant outside of the assistants page, e.g., in the
        /// "Send to ..." menu, as far as its category on the page is concerned.
        /// </summary>
        /// <remarks>
        /// A component without a category, such as the chat, is always offered.
        /// </remarks>
        /// <param name="component">The assistant in question.</param>
        /// <returns>True when the category of the assistant offers it.</returns>
        public bool IsOfferingAssistant(Components component) => component.Category is PluginCategory.NONE || page.IsCategoryOffering(new AssistantCategoryKey(component.Category));
    }

    /// <summary>
    /// Whether a category is among those stored in a pair of sets, one for the categories of AI Studio and
    /// one for the names of custom categories.
    /// </summary>
    private static bool Contains(HashSet<PluginCategory> categories, HashSet<string> customNames, AssistantCategoryKey category) => category.Category is PluginCategory.CUSTOM
        ? customNames.Any(name => category.Equals(new AssistantCategoryKey(PluginCategory.CUSTOM, name)))
        : categories.Any(storedCategory => category.Equals(new AssistantCategoryKey(storedCategory)));

    /// <summary>
    /// Adds a category to a pair of sets or removes it, see Contains.
    /// </summary>
    /// <remarks>
    /// Removing a category removes every spelling of it, e.g., CORE together with COMMON, or a custom
    /// name in another case. Adding it stores the spelling of the key, so a set never holds CORE.
    /// </remarks>
    private static void Set(HashSet<PluginCategory> categories, HashSet<string> customNames, AssistantCategoryKey category, bool isContained)
    {
        if (category.Category is PluginCategory.CUSTOM)
        {
            customNames.RemoveWhere(name => category.Equals(new AssistantCategoryKey(PluginCategory.CUSTOM, name)));
            if (isContained)
                customNames.Add(category.CustomName);

            return;
        }

        categories.RemoveWhere(storedCategory => category.Equals(new AssistantCategoryKey(storedCategory)));
        if (isContained)
            categories.Add(category.Category);
    }

    /// <param name="settingsManager">The settings manager which holds the rules of your organization.</param>
    extension(SettingsManager settingsManager)
    {
        /// <summary>
        /// Whether your organization disabled an assistant plugin through its categories.
        /// </summary>
        /// <param name="plugin">The plugin in question.</param>
        /// <returns>True when every category of the plugin is disabled and your organization did not deploy it.</returns>
        public bool IsDisabledByItsCategories(PluginAssistants plugin)
        {
            var configuration = settingsManager.ConfigurationData;
            var isDeployedByOrganization = PluginFactory.IsAssistantDeployedByOrganization(plugin, configuration.AssistantPluginAudit.EnterpriseApprovedPlugins);
            return configuration.App.DisablesAssistantPlugin(plugin, isDeployedByOrganization);
        }

        /// <summary>
        /// Whether an assistant plugin may be offered and started.
        /// </summary>
        /// <remarks>
        /// Check this wherever an assistant plugin enters the app: on the assistants page, when a link
        /// names it, and when a direct chat launcher starts its chat.
        /// </remarks>
        /// <param name="plugin">The plugin in question.</param>
        /// <returns>True when the plugin is enabled and your organization did not disable it through its categories.</returns>
        public bool IsAssistantPluginAvailable(PluginAssistants plugin) => settingsManager.IsPluginEnabled(plugin) && !settingsManager.IsDisabledByItsCategories(plugin);
    }
}
