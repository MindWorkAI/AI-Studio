using AIStudio.Components;
using AIStudio.Tools.PluginSystem;

using Microsoft.AspNetCore.Components;

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
        this.ApplyFilters([], [ Event.PLUGINS_RELOADED ]);
        
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

    private async Task PluginActivationStateChanged(IPluginMetadata pluginMeta)
    {
        if (this.SettingsManager.IsPluginEnabled(pluginMeta))
            this.SettingsManager.ConfigurationData.EnabledPlugins.Remove(pluginMeta.Id);
        else
            this.SettingsManager.ConfigurationData.EnabledPlugins.Add(pluginMeta.Id);
        
        await this.SettingsManager.StoreSettings();
        await this.MessageBus.SendMessage<bool>(this, Event.CONFIGURATION_CHANGED);
    }
    
    private bool TryGetSourceWebsite(IPluginMetadata pluginMeta, out string sourceUrl)
    {
        sourceUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(pluginMeta.SourceURL))
            return false;

        var normalizedSourceUrl = pluginMeta.SourceURL.Trim();
        if (!Uri.TryCreate(normalizedSourceUrl, UriKind.Absolute, out var sourceUri))
            return false;

        if (sourceUri.Scheme is not ("http" or "https"))
            return false;

        sourceUrl = sourceUri.ToString();
        return true;
    }

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.PLUGINS_RELOADED:
                await this.InvokeAsync(this.StateHasChanged);
                break;
        }
    }

    #endregion
}
