using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

namespace AIStudio.Pages;

public partial class Plugins : MSGComponentBase
{
    private const string GROUP_ENABLED = "Enabled";
    private const string GROUP_DISABLED = "Disabled";
    private const string GROUP_INTERNAL = "Internal";
    
    private TableGroupDefinition<IPluginMetadata> groupConfig = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.PLUGINS_RELOADED ]);
        
        this.groupConfig = new TableGroupDefinition<IPluginMetadata>
        {
            Expandable = true,
            IsInitiallyExpanded = true,
            Selector = pluginMeta =>
            {
                if (pluginMeta.IsInternal)
                    return GROUP_INTERNAL;
                
                return this.SettingsManager.IsPluginEnabled(pluginMeta)
                    ? GROUP_ENABLED
                    : GROUP_DISABLED;
            }
        };
        
        await base.OnInitializedAsync();
    }

    #endregion

    #region Overrides of MSGComponentBase

    public override string ComponentName => nameof(Plugins);

    #endregion

    private async Task PluginActivationStateChanged(IPluginMetadata pluginMeta)
    {
        if (this.SettingsManager.IsPluginEnabled(pluginMeta))
            this.SettingsManager.ConfigurationData.EnabledPlugins.Remove(pluginMeta.Id);
        else
            this.SettingsManager.ConfigurationData.EnabledPlugins.Add(pluginMeta.Id);
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
}