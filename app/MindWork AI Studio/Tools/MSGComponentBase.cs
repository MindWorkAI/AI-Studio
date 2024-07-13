using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public abstract class MSGComponentBase : ComponentBase, IDisposable, IMessageBusReceiver
{
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

    public abstract Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data);
    
    public abstract Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data);

    #endregion

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
        this.MessageBus.ApplyFilters(this, components, events);
    }
}