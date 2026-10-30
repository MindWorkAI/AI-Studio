using System.Globalization;
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
    
    /// <summary>
    /// Whether the option changes while the user drags the slider, or only once they release it.
    /// </summary>
    /// <remarks>
    /// Defaults to true. An option which changes the layout of the page, such as the interface size,
    /// should wait for the release: a page which changes under the pointer moves the slider away from
    /// it. The slider shows the value it is dragged to either way.
    /// </remarks>
    [Parameter]
    public bool Immediate { get; set; } = true;

    /// <summary>
    /// The value the user drags the slider to, while the option waits for the release, see Immediate.
    /// </summary>
    private T? draggedValue;

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
        this.draggedValue = null;
        this.ValueUpdate(updatedValue);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }

    /// <summary>
    /// Shows the value the user drags the slider to, while the option waits for the release.
    /// </summary>
    private void ShowDraggedValue(ChangeEventArgs args)
    {
        if (this.Immediate)
            return;

        // The range input reports its value with a dot as decimal separator, whatever the language:
        if (T.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var value))
            this.draggedValue = value;
    }
    
    private async Task EnsureMinMax()
    {
        if (this.Value() < this.Min)
            await this.OptionChanged(this.Min);

        else if(this.Value() > this.Max)
            await this.OptionChanged(this.Max);
    }
}