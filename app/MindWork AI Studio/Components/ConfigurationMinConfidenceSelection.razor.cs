using AIStudio.Provider;
using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfigurationMinConfidenceSelection : ComponentBase
{
    /// <summary>
    /// The selected value.
    /// </summary>
    [Parameter]
    public Func<ConfidenceLevel> SelectedValue { get; set; } = () => default!;
    
    /// <summary>
    /// An action that is called when the selection changes.
    /// </summary>
    [Parameter]
    public Action<ConfidenceLevel> SelectionUpdate { get; set; } = _ => { };

    /// <summary>
    /// Is the selection component disabled?
    /// </summary>
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;

    /// <summary>
    /// Boolean value indicating whether the selection is restricted to a global minimum confidence level.
    /// </summary>
    [Parameter]
    public bool RestrictToGlobalMinimumConfidence { get; set; }
    
    [Inject]
    private SettingsManager SettingsManager { get; init; } = null!;

    private ConfidenceLevel FilteredSelectedValue()
    {
        if (this.SelectedValue() is ConfidenceLevel.NONE)
            return ConfidenceLevel.NONE;
        
        if(this.RestrictToGlobalMinimumConfidence && this.SettingsManager.ConfigurationData.LLMProviders.EnforceGlobalMinimumConfidence)
        {
            var minimumLevel = this.SettingsManager.ConfigurationData.LLMProviders.GlobalMinimumConfidence;
            if(this.SelectedValue() < minimumLevel)
                return minimumLevel;
        }
        
        return this.SelectedValue();
    }
}