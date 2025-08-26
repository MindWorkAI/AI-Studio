using AIStudio.Settings;

using Lua;

namespace AIStudio.Tools.PluginSystem;

public sealed class PluginConfiguration(bool isInternal, LuaState state, PluginType type) : PluginBase(isInternal, state, type)
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(PluginConfiguration).Namespace, nameof(PluginConfiguration));
    private static readonly SettingsManager SETTINGS_MANAGER = Program.SERVICE_PROVIDER.GetRequiredService<SettingsManager>();
    
    private List<PluginConfigurationObject> configObjects = [];
    
    /// <summary>
    /// The list of configuration objects. Configuration objects are, e.g., providers or chat templates. 
    /// </summary>
    public IEnumerable<PluginConfigurationObject> ConfigObjects => this.configObjects;
    
    public async Task InitializeAsync(bool dryRun)
    {
        if(!this.TryProcessConfiguration(dryRun, out var issue))
            this.pluginIssues.Add(issue);

        if (!dryRun)
        {
            await SETTINGS_MANAGER.StoreSettings();
            await MessageBus.INSTANCE.SendMessage<bool>(null, Event.CONFIGURATION_CHANGED);
        }
    }

    /// <summary>
    /// Tries to initialize the UI text content of the plugin.
    /// </summary>
    /// <param name="dryRun">When true, the method will not apply any changes but only check if the configuration can be read.</param>
    /// <param name="message">The error message, when the UI text content could not be read.</param>
    /// <returns>True, when the UI text content could be read successfully.</returns>
    private bool TryProcessConfiguration(bool dryRun, out string message)
    {
        this.configObjects.Clear();
        
        // Ensure that the main CONFIG table exists and is a valid Lua table:
        if (!this.state.Environment["CONFIG"].TryRead<LuaTable>(out var mainTable))
        {
            message = TB("The CONFIG table does not exist or is not a valid table.");
            return false;
        }
        
        // Check for the main SETTINGS table:
        if (!mainTable.TryGetValue("SETTINGS", out var settingsValue) || !settingsValue.TryRead<LuaTable>(out var settingsTable))
        {
            message = TB("The SETTINGS table does not exist or is not a valid table.");
            return false;
        }
        
        // Config: check for updates, and if so, how often?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.UpdateInterval, this.Id, settingsTable, dryRun);
        
        // Config: allow the user to add providers?
        ManagedConfiguration.TryProcessConfiguration(x => x.App, x => x.AllowUserToAddProvider, this.Id, settingsTable, dryRun);
        
        // Handle configured LLM providers:
        if (!PluginConfigurationObject.TryParse(PluginConfigurationObjectType.LLM_PROVIDER, x => x.Providers, x => x.NextProviderNum, mainTable, this.Id, ref this.configObjects, dryRun))
        {
            message = TB("At least one configured LLM provider is not valid or could not be parsed, or the LLM_PROVIDERS table does not exist.");
            return false;
        }
        
        // Handle configured chat templates:
        if (!PluginConfigurationObject.TryParse(PluginConfigurationObjectType.CHAT_TEMPLATE, x => x.ChatTemplates, x => x.NextChatTemplateNum, mainTable, this.Id, ref this.configObjects, dryRun))
        {
            message = TB("At least one configured chat template is not valid or could not be parsed, or the CHAT_TEMPLATES table does not exist.");
            return false;
        }
        
        message = string.Empty;
        return true;
    }
}