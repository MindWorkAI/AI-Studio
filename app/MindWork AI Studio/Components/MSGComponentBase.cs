using AIStudio.Settings;

using Microsoft.AspNetCore.Components;

namespace AIStudio.Components;

public abstract class MSGComponentBase : ComponentBase, IDisposable, IMessageBusReceiver
{
    [Inject]
    protected SettingsManager SettingsManager { get; init; } = null!;
    
    [Inject]
    protected MessageBus MessageBus { get; init; } = null!;

    #region Overrides of ComponentBase

    protected override void OnInitialized()
    {
        this.MessageBus.RegisterComponent(this);
        base.OnInitialized();
    }

    #endregion

    #region Implementation of IMessageBusReceiver

    public abstract string ComponentName { get; }

    public async Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        switch (triggeredEvent)
        {
            case Event.COLOR_THEME_CHANGED:
                this.StateHasChanged();
                break;
            
            default:
                await this.ProcessIncomingMessage(sendingComponent, triggeredEvent, data);
                break;
        }
    }

    {
        return await this.ProcessIncomingMessageWithResult<TPayload, TResult>(sendingComponent, triggeredEvent, data);
    }

    #endregion

    protected virtual Task ProcessIncomingMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data)
    {
        return Task.CompletedTask;
    }

    protected virtual Task<TResult?> ProcessIncomingMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data)
    {
        return Task.FromResult<TResult?>(default);
    }

    #region Implementation of IDisposable

    public void Dispose()
    {
        this.MessageBus.Unregister(this);
    }

    #endregion
    
    protected async Task SendMessage<T>(Event triggeredEvent, T? data = default)
    {
        await this.MessageBus.SendMessage(this, triggeredEvent, data);
    }
    
    protected async Task<TResult?> SendMessageWithResult<TPayload, TResult>(Event triggeredEvent, TPayload? data)
    {
        return await this.MessageBus.SendMessageUseFirstResult<TPayload, TResult>(this, triggeredEvent, data);
    }
    
    protected void ApplyFilters(ComponentBase[] components, Event[] events)
    {
        // Append the color theme changed event to the list of events:
        var eventsList = new List<Event>(events)
        {
            Event.COLOR_THEME_CHANGED
        };
        
        this.MessageBus.ApplyFilters(this, components, eventsList.ToArray());
    }
}