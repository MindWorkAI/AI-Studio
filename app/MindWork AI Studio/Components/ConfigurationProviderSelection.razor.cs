using AIStudio.Provider;
using AIStudio.Settings;
using AIStudio.Tools.PluginSystem;
using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfigurationProviderSelection : MSGComponentBase
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfigurationProviderSelection).Namespace, nameof(ConfigurationProviderSelection));
    
    [Parameter]
    public Func<string> SelectedValue { get; set; } = () => string.Empty;
    
    [Parameter]
    public Action<string> SelectionUpdate { get; set; } = _ => { };

    [Parameter]
    public IEnumerable<ConfigurationSelectData<string>> Data { get; set; } = new List<ConfigurationSelectData<string>>();
    
    [Parameter]
    public Func<string> HelpText { get; set; } = () => TB("Select a provider that is preselected.");

    [Parameter]
    public Tools.Components Component { get; set; } = Tools.Components.NONE;

    [Parameter]
    public ConfidenceLevel ExplicitMinimumConfidence { get; set; } = ConfidenceLevel.UNKNOWN;
    
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;
    
    [Parameter]
    public Func<bool> IsLocked { get; set; } = () => false;

    private IEnumerable<ConfigurationSelectData<string>> FilteredData()
    {
        if(this.Component is not Tools.Components.NONE and not Tools.Components.APP_SETTINGS)
            yield return new(T("Use app default"), string.Empty);

        //
        // Filter the providers based on the minimum confidence level of this component, the enforced
        // global minimum, and the explicit minimum level when it is higher. Providers which no longer
        // exist resolve to `Provider.NONE` and are dropped by the confidence check as well:
        //
        foreach (var providerId in this.Data)
        {
            var provider = this.SettingsManager.GetProviderById(providerId.Value);
            if (this.SettingsManager.IsProviderConfident(provider, this.Component, this.ExplicitMinimumConfidence))
                yield return providerId;
        }
    }

    private AIStudio.Settings.Provider? GetProvider(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return null;

        var provider = this.SettingsManager.GetProviderById(providerId);
        return provider == AIStudio.Settings.Provider.NONE ? null : provider;
    }

    #region Overrides of MSGComponentBase

    protected override async Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data) where T : default
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                
                if(string.IsNullOrWhiteSpace(this.SelectedValue()))
                    break;
                
                // Check if the selected value is still valid:
                if (this.Data.All(x => x.Value != this.SelectedValue()))
                {
                    this.SelectedValue = () => string.Empty;
                    this.SelectionUpdate(string.Empty);
                    await this.SettingsManager.StoreSettings();
                }
                
                this.StateHasChanged();
                break;
        }
    }

    #endregion
}
