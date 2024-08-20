using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

/// <summary>
/// Configuration component for any boolean option.
/// </summary>
public partial class ConfigurationOption : ConfigurationBase
{
    /// <summary>
    /// Text to display when the option is true.
    /// </summary>
    [Parameter]
    public string LabelOn { get; set; } = string.Empty;
    
    /// <summary>
    /// Text to display when the option is false.
    /// </summary>
    [Parameter]
    public string LabelOff { get; set; } = string.Empty;
    
    /// <summary>
    /// The boolean state of the option.
    /// </summary>
    [Parameter]
    public Func<bool> State { get; set; } = () => false;
    
    /// <summary>
    /// An action which is called when the option is changed.
    /// </summary>
    [Parameter]
    public Action<bool> StateUpdate { get; set; } = _ => { };
    
    private async Task OptionChanged(bool updatedState)
    {
        this.StateUpdate(updatedState);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
}