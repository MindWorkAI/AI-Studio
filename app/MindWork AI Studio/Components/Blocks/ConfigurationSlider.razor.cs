using System.Numerics;

using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

public partial class ConfigurationSlider<T> : ConfigurationBase, IMessageBusReceiver where T : struct, INumber<T>
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
    /// Is the option disabled?
    /// </summary>
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;

    private MudSlider<T> slider = null!;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.CONFIGURATION_CHANGED ]);
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private async Task OptionChanged(T updatedValue)
    {
        this.ValueUpdate(updatedValue);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
    
    #region Implementation of IMessageBusReceiver

    public Task ProcessMessage<TMsg>(ComponentBase? sendingComponent, Event triggeredEvent, TMsg? data)
    {
        switch (triggeredEvent)
        {
            case Event.CONFIGURATION_CHANGED:
                this.StateHasChanged();
                break;
        }

        return Task.CompletedTask;
    }

    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #endregion
}