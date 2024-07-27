using System.Numerics;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

public partial class ConfigurationSlider<T> : ConfigurationBase where T : struct, INumber<T>
{
    /// <summary>
    /// The minimum value for the slider.
    /// </summary>
    [Parameter]
    public T Min { get; set; } = T.Zero;

    /// <summary>
    /// The maximum value for the slider.
    /// </summary>
    [Parameter]
    public T Max { get; set; } = T.One;

    /// <summary>
    /// The step size for the slider.
    /// </summary>
    [Parameter]
    public T Step { get; set; } = T.One;

    /// <summary>
    /// The unit to display next to the slider's value.
    /// </summary>
    [Parameter]
    public string Unit { get; set; } = string.Empty;
    
    /// <summary>
    /// The value used for the slider.
    /// </summary>
    [Parameter]
    public Func<T> Value { get; set; } = () => T.Zero;
    
    /// <summary>
    /// An action which is called when the option is changed.
    /// </summary>
    [Parameter]
    public Action<T> ValueUpdate { get; set; } = _ => { };
    
    private async Task OptionChanged(T updatedValue)
    {
        this.ValueUpdate(updatedValue);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
}