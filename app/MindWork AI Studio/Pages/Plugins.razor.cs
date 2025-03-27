using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Pages;

public partial class Plugins : ComponentBase
{
    private const string GROUP_ENABLED = "Enabled";
    private const string GROUP_DISABLED = "Disabled";
    private const string GROUP_INTERNAL = "Internal";
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;
    
    private TableGroupDefinition<IPluginMetadata> groupConfig = null!;

    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        this.groupConfig = new TableGroupDefinition<IPluginMetadata>
        {
            Expandable = true,
            IsInitiallyExpanded = true,
            Selector = pluginMeta =>
            {
                if (pluginMeta.IsInternal)
                    return GROUP_INTERNAL;
                
                return this.SettingsManager.ConfigurationData.EnabledPlugins.Contains(pluginMeta.Id)
                    ? GROUP_ENABLED
                    : GROUP_DISABLED;
            }
        };
        
        await base.OnInitializedAsync();
    }

    #endregion
}