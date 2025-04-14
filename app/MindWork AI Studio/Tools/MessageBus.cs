using System.Collections.Concurrent;

using Microsoft.AspNetCore.Components;
// ReSharper disable RedundantRecordClassKeyword

namespace AIStudio.Tools;

public sealed class MessageBus
{
    public static readonly MessageBus INSTANCE = new();
    
    private readonly ConcurrentDictionary<IMessageBusReceiver, ComponentBase[]> componentFilters = new();
    private readonly ConcurrentDictionary<IMessageBusReceiver, Event[]> componentEvents = new();
    private readonly ConcurrentDictionary<Event, ConcurrentQueue<Message>> deferredMessages = new();
    private readonly ConcurrentQueue<Message> messageQueue = new();
    private readonly SemaphoreSlim sendingSemaphore = new(1, 1);

    private MessageBus()
    {
    }

    public void ApplyFilters(IMessageBusReceiver receiver, ComponentBase[] components, Event[] events)
    {
        this.componentFilters[receiver] = components;
        this.componentEvents[receiver] = events;
    }
    
    public void RegisterComponent(IMessageBusReceiver receiver)
    {
        this.componentFilters.TryAdd(receiver, []);
        this.componentEvents.TryAdd(receiver, []);
    }
    
    public void Unregister(IMessageBusReceiver receiver)
    {
        this.componentFilters.TryRemove(receiver, out _);
        this.componentEvents.TryRemove(receiver, out _);
    }
    
    private record class Message(ComponentBase? SendingComponent, Event TriggeredEvent, object? Data);
    
    public async Task SendMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data = default)
    {
        this.messageQueue.Enqueue(new Message(sendingComponent, triggeredEvent, data));
        
        try
        {
            await this.sendingSemaphore.WaitAsync();
            while (this.messageQueue.TryDequeue(out var message))
            {
                foreach (var (receiver, componentFilter) in this.componentFilters)
                {
                    if (componentFilter.Length > 0 && sendingComponent is not null && !componentFilter.Contains(sendingComponent))
                        continue;

                    var eventFilter = this.componentEvents[receiver];
                    if (eventFilter.Length == 0 || eventFilter.Contains(triggeredEvent))
                        // We don't await the task here because we don't want to block the message bus:
                        _ = receiver.ProcessMessage(message.SendingComponent, message.TriggeredEvent, message.Data);
                }
            }
        }
        finally
        {
            this.sendingSemaphore.Release();
        }
    }

    public Task SendError(Error error) => this.SendMessage(null, Event.SHOW_ERROR, error);
    
    public Task SendWarning(Warning warning) => this.SendMessage(null, Event.SHOW_WARNING, warning);
    
    public Task SendSuccess(Success success) => this.SendMessage(null, Event.SHOW_SUCCESS, success);

    public void DeferMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data = default)
    {
        if (this.deferredMessages.TryGetValue(triggeredEvent, out var queue))
            queue.Enqueue(new Message(sendingComponent, triggeredEvent, data));
        else
        {
            this.deferredMessages[triggeredEvent] = new();
            this.deferredMessages[triggeredEvent].Enqueue(new Message(sendingComponent, triggeredEvent, data));
        }
    }
    
    public IEnumerable<T?> CheckDeferredMessages<T>(Event triggeredEvent)
    {
        if (this.deferredMessages.TryGetValue(triggeredEvent, out var queue))
            while (queue.TryDequeue(out var message))
                yield return message.Data is T data ? data : default;
    }
    
    public async Task<TResult?> SendMessageUseFirstResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data = default)
    {
        foreach (var (receiver, componentFilter) in this.componentFilters)
        {
            if (componentFilter.Length > 0 && sendingComponent is not null && !componentFilter.Contains(sendingComponent))
                continue;

            var eventFilter = this.componentEvents[receiver];
            if (eventFilter.Length == 0 || eventFilter.Contains(triggeredEvent))
            {
                var result = await receiver.ProcessMessageWithResult<TPayload, TResult>(sendingComponent, triggeredEvent, data);
                if (result is not null)
                    return (TResult) result;
            }
        }
        
        return default;
    }
}