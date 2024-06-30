using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public interface IMessageBusReceiver
{
    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data);
}