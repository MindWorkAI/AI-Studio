using AIStudio.Settings;
using AIStudio.Settings.DataModel;
using AIStudio.Tools.PluginSystem;
using AIStudio.Tools.PluginSystem.Assistants;

namespace AIStudio.Tools;

/// <summary>
/// Decides what the categories of an assistant allow, built-in assistants and plugins alike.
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
