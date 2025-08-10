using System.Numerics;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class ConfigurationSlider<T> : ConfigurationBaseCore where T : struct, INumber<T>
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
    
    #region Overrides of ConfigurationBase

    /// <inheritdoc />
    protected override bool Stretch => true;

    /// <inheritdoc />
    protected override Variant Variant => Variant.Outlined;

    protected override string Label => this.OptionDescription;

    #endregion
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        await this.EnsureMinMax();
        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await this.EnsureMinMax();
        await base.OnParametersSetAsync();
    }

    #endregion
    
    private async Task OptionChanged(T updatedValue)
    {
        this.ValueUpdate(updatedValue);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
    
    private async Task EnsureMinMax()
    {
        if (this.Value() < this.Min)
            await this.OptionChanged(this.Min);

        else if(this.Value() > this.Max)
            await this.OptionChanged(this.Max);
    }
}