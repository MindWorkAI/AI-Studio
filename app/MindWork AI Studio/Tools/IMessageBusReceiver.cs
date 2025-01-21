using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public interface IMessageBusReceiver
{
    public string ComponentName { get; }
    
    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data);
    
    public Task<TResult?> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data);
}