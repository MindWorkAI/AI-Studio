using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

using AIStudio.Provider;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ProviderSelection : MSGComponentBase
{
    [CascadingParameter]
    public Tools.Components? Component { get; set; }

    [Parameter]
    public AIStudio.Settings.Provider ProviderSettings { get; set; } = AIStudio.Settings.Provider.NONE;
    
    [Parameter]
    public EventCallback<AIStudio.Settings.Provider> ProviderSettingsChanged { get; set; }
    
    [Parameter]
    public Func<AIStudio.Settings.Provider, string?> ValidateProvider { get; set; } = _ => null;

    [Parameter]
    public ConfidenceLevel ExplicitMinimumConfidence { get; set; } = ConfidenceLevel.UNKNOWN;
    
    [Inject]
    private ILogger<ProviderSelection> Logger { get; init; } = null!;
    
    private async Task SelectionChanged(AIStudio.Settings.Provider provider)
    {
        this.ProviderSettings = provider;
        await this.ProviderSettingsChanged.InvokeAsync(provider);
    }
    
    [SuppressMessage("Usage", "MWAIS0001:Direct access to `Providers` is not allowed")]
    private IEnumerable<AIStudio.Settings.Provider> GetAvailableProviders()
    {
        switch (this.Component)
        {
            case null:
                this.Logger.LogError("Component is null! Cannot filter providers based on component settings. Missed CascadingParameter?");
                yield break;
            
            case Tools.Components.NONE:
                this.Logger.LogError("Component is NONE! Cannot filter providers based on component settings. Used wrong component?");
                yield break;
            
            case { } component:
                
                // Get the minimum confidence level for this component, and/or the global minimum if enforced:
                var minimumLevel = this.SettingsManager.GetMinimumConfidenceLevel(component);
                
                // Override with the explicit minimum level if set and higher:
                if (this.ExplicitMinimumConfidence is not ConfidenceLevel.UNKNOWN && this.ExplicitMinimumConfidence > minimumLevel)
                    minimumLevel = this.ExplicitMinimumConfidence;
                
                // Filter providers based on the minimum confidence level:
                foreach (var provider in this.SettingsManager.ConfigurationData.Providers)
                    if (provider.UsedLLMProvider != LLMProviders.NONE)
                        if (provider.UsedLLMProvider.GetConfidence(this.SettingsManager).Level >= minimumLevel)
                            yield return provider;
                break;
        }
    }
}
