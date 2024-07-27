using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

/// <summary>
/// Configuration component for any boolean option.
/// </summary>
public partial class ConfigurationOption : ConfigurationBase, IMessageBusReceiver
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

    /// <summary>
    /// Is the option disabled?
    /// </summary>
    [Parameter]
    public Func<bool> Disabled { get; set; } = () => false;
    
    #region Overrides of ComponentBase

    protected override async Task OnInitializedAsync()
    {
        // Register this component with the message bus:
        this.MessageBus.RegisterComponent(this);
        this.MessageBus.ApplyFilters(this, [], [ Event.CONFIGURATION_CHANGED ]);
        
        await base.OnInitializedAsync();
    }

    #endregion
    
    private async Task OptionChanged(bool updatedState)
    {
        this.StateUpdate(updatedState);
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