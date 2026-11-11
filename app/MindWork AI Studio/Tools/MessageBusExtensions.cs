using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public static class MessageBusExtensions
{
    extension(ComponentBase component)
    {
        public async Task SendMessage<T>(Event triggeredEvent, T? data = default)
        {
            await MessageBus.INSTANCE.SendMessage(component, triggeredEvent, data);
        }
    }
    
    extension(IMessageBusReceiver component)
    {
        public void ApplyFilters(ComponentBase[] components, Event[] events)
        {
            MessageBus.INSTANCE.ApplyFilters(component, components, events.ToHashSet());
        }
    }
}