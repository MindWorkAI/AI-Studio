using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public static class MessageBusExtensions
{
    public static async Task SendMessage<T>(this ComponentBase component, Event triggeredEvent, T? data = default)
    {
        await MessageBus.INSTANCE.SendMessage(component, triggeredEvent, data);
    }
    
    public static void ApplyFilters(this IMessageBusReceiver component, ComponentBase[] components, Event[] events)
    {
        MessageBus.INSTANCE.ApplyFilters(component, components, events);
    }
}