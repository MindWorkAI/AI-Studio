using System.Numerics;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public partial class MudTextSlider<T> : ComponentBase where T : struct, INumber<T>
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
    
    [Parameter]
    public T Value { get; set; }
    
    [Parameter]
    public EventCallback<T> ValueChanged { get; set; }
    
    /// <summary>
    /// The label to display above the slider.
    /// </summary>
    [Parameter]
    public string Label { get; set; } = string.Empty;
    
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;

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
    
    private async Task EnsureMinMax()
    {
        if (this.Value < this.Min)
            await this.ValueUpdated(this.Min);

        else if(this.Value > this.Max)
            await this.ValueUpdated(this.Max);
    }

    private async Task ValueUpdated(T value)
    {
        this.Value = value;
        await this.ValueChanged.InvokeAsync(this.Value);
    }
}