using AIStudio.Settings;
using AIStudio.Tools;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components.Blocks;

/// <summary>
/// Configuration component for selecting a value from a list.
/// </summary>
/// <typeparam name="T">The type of the value to select.</typeparam>
public partial class ConfigurationSelect<T> : ConfigurationBase, IMessageBusReceiver
{
    /// <summary>
    /// The data to select from.
    /// </summary>
    [Parameter]
    public IEnumerable<ConfigurationSelectData<T>> Data { get; set; } = [];
    
    /// <summary>
    /// The selected value.
    /// </summary>
    [Parameter]
    public Func<T> SelectedValue { get; set; } = () => default!;
    
    /// <summary>
    /// An action that is called when the selection changes.
    /// </summary>
    [Parameter]
    public Action<T> SelectionUpdate { get; set; } = _ => { };
    
    /// <summary>
    /// Is the selection component disabled?
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

    private async Task OptionChanged(T updatedValue)
    {
        this.SelectionUpdate(updatedValue);
        await this.SettingsManager.StoreSettings();
        await this.InformAboutChange();
    }
    
    private static string GetClass => $"{MARGIN_CLASS} rounded-lg";
    
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