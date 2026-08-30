using System.Collections.Concurrent;

using AIStudio.Tools.Services;

using Microsoft.AspNetCore.Components;
// ReSharper disable RedundantRecordClassKeyword

namespace AIStudio.Tools;

public sealed class MessageBus
{
    public static readonly MessageBus INSTANCE = new();
    
    private readonly ConcurrentDictionary<IMessageBusReceiver, ComponentBase[]> componentFilters = new();
    private readonly ConcurrentDictionary<IMessageBusReceiver, Event[]> componentEvents = new();
    private readonly ConcurrentDictionary<IMessageBusReceiver, CircuitStateService> receiverCircuits = new();
    private readonly ConcurrentDictionary<Event, ConcurrentQueue<Message>> deferredMessages = new();
    private readonly ConcurrentQueue<Message> messageQueue = new();
    private readonly SemaphoreSlim sendingSemaphore = new(1, 1);

    private static ILogger<MessageBus>? LOG; 

    private MessageBus()
    {
    }
    
    public void Initialize(ILogger<MessageBus> logger)
    {
        LOG = logger;
        LOG.LogInformation("Message bus initialized.");
    }

    /// <summary>
    /// Define for which components and events you want to receive messages.
    /// </summary>
    /// <param name="receiver">That's you, the receiver.</param>
    /// <param name="filterComponents">A list of components for which you want to receive messages. Use an empty list to receive messages from all components.</param>
    /// <param name="events">A list of events for which you want to receive messages.</param>
    public void ApplyFilters(IMessageBusReceiver receiver, ComponentBase[] filterComponents, HashSet<Event> events)
    {
        this.componentFilters[receiver] = filterComponents;
        this.componentEvents[receiver] = events.ToArray();
    }
    
    /// <summary>
    /// Registers a receiver at the bus.
    /// </summary>
    /// <param name="receiver">That's you, the receiver.</param>
    /// <param name="circuitState">The circuit this receiver belongs to. Components hand over their circuit
    /// so the bus can let them go when that circuit ends. Services which live longer than any circuit,
    /// such as hosted services, hand over nothing.</param>
    public void RegisterComponent(IMessageBusReceiver receiver, CircuitStateService? circuitState = null)
    {
        this.componentFilters.TryAdd(receiver, []);
        this.componentEvents.TryAdd(receiver, []);

        if (circuitState is not null)
            this.receiverCircuits[receiver] = circuitState;
    }

    public void Unregister(IMessageBusReceiver receiver)
    {
        this.componentFilters.TryRemove(receiver, out _);
        this.componentEvents.TryRemove(receiver, out _);
        this.receiverCircuits.TryRemove(receiver, out _);
    }

    /// <summary>
    /// Removes all receivers which belong to one circuit.
    /// </summary>
    /// <remarks>
    /// The circuit handler calls this when a circuit ends. Components deregister themselves when they get
    /// disposed, but a circuit which was retained and then dropped does not give all of them that chance.
    /// Since the bus holds a strong reference to every receiver, those leftovers would stay and would be
    /// served forever.
    /// </remarks>
    /// <param name="circuitState">The circuit whose receivers must go.</param>
    /// <returns>The number of removed receivers.</returns>
    public int UnregisterCircuit(CircuitStateService circuitState)
    {
        var numRemovedReceivers = 0;
        foreach (var (receiver, receiverCircuit) in this.receiverCircuits)
        {
            if (!ReferenceEquals(receiverCircuit, circuitState))
                continue;

            this.Unregister(receiver);
            numRemovedReceivers++;
        }

        return numRemovedReceivers;
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
                    if (componentFilter.Length > 0 && message.SendingComponent is not null && !componentFilter.Contains(message.SendingComponent))
                        continue;

                    var eventFilter = this.componentEvents[receiver];
                    if (eventFilter.Length == 0 || eventFilter.Contains(message.TriggeredEvent))

                        // We don't await the task here because we don't want to block the message bus:
                        _ = DeliverMessage(receiver, message);
                }
            }
        }
        catch (Exception e)
        {
            LOG?.LogError(e, "Error while sending message.");
        }
        finally
        {
            this.sendingSemaphore.Release();
        }
    }

    /// <summary>
    /// Hands one message to one receiver and observes how that went.
    /// </summary>
    /// <remarks>
    /// The bus must not wait for a receiver, since one slow receiver would hold up everybody else. Not
    /// waiting is not the same as not caring, though: a receiver whose circuit is gone fails with a
    /// disconnect or disposal exception, and nobody would ever see where it came from. Such a task
    /// carries its fault until the finalizer reports it as an unobserved task exception — naming a task
    /// type instead of the receiver and the event. This is where we give those failures a name.
    /// </remarks>
    /// <param name="receiver">The receiver of the message.</param>
    /// <param name="message">The message to deliver.</param>
    private static async Task DeliverMessage(IMessageBusReceiver receiver, Message message)
    {
        try
        {
            await receiver.ProcessMessage(message.SendingComponent, message.TriggeredEvent, message.Data);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
            //
            // Expected whenever the browser connection of a receiver is gone: the app keeps circuits
            // of reloaded or sleeping windows around, and their components still receive events.
            //
            LOG?.LogDebug("The receiver '{ReceiverName}' did not process the event '{Event}' because its circuit was gone: {Reason}", receiver.GetType().Name, message.TriggeredEvent, exception.Message);
        }
        catch (Exception exception)
        {
            LOG?.LogError(exception, "The receiver '{ReceiverName}' failed while processing the event '{Event}'.", receiver.GetType().Name, message.TriggeredEvent);
        }
    }

    public Task SendError(DataErrorMessage dataErrorMessage) => this.SendMessage(null, Event.SHOW_ERROR, dataErrorMessage);
    
    public Task SendWarning(DataWarningMessage dataWarningMessage) => this.SendMessage(null, Event.SHOW_WARNING, dataWarningMessage);
    
    public Task SendSuccess(DataSuccessMessage dataSuccessMessage) => this.SendMessage(null, Event.SHOW_SUCCESS, dataSuccessMessage);

    public Task SendInfo(DataInfoMessage dataInfoMessage) => this.SendMessage(null, Event.SHOW_INFO, dataInfoMessage);

    /// <summary>
    /// Stores a message until someone asks for it, cf. TakeDeferredMessages. This is how a
    /// component hands data to a component which does not exist yet, e.g. an assistant which
    /// sends its result to the chat before the user gets there.
    /// </summary>
    /// <param name="sendingComponent">That's you, the sender.</param>
    /// <param name="triggeredEvent">The event this message belongs to.</param>
    /// <param name="data">The data to hand over.</param>
    public void DeferMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data = default)
    {
        var queue = this.deferredMessages.GetOrAdd(triggeredEvent, _ => new());
        queue.Enqueue(new Message(sendingComponent, triggeredEvent, data));
    }

    /// <summary>
    /// Takes all deferred messages of an event out of the bus.
    /// </summary>
    /// <remarks>
    /// This empties the queue and returns what was in it. It used to be a lazy iterator, which
    /// meant that a caller stopping after the first message left the rest of the queue behind:
    /// those messages were never delivered, and the data they carry — a complete chat thread, for
    /// instance — stayed alive for as long as the app ran. Returning a list makes that impossible.
    /// Callers who expect a single message take the last one, since that is the most recent thing
    /// the user asked for.
    /// </remarks>
    /// <param name="triggeredEvent">The event whose messages you want.</param>
    /// <returns>The deferred messages, oldest first. Empty when there are none.</returns>
    public IReadOnlyList<T?> TakeDeferredMessages<T>(Event triggeredEvent)
    {
        //
        // Removing the queue along with its messages is what keeps the dictionary from growing:
        // otherwise, every event which ever deferred a message would keep an empty queue forever.
        //
        if (!this.deferredMessages.TryRemove(triggeredEvent, out var queue))
            return [];

        var messages = new List<T?>();
        while (queue.TryDequeue(out var message))
            messages.Add(message.Data is T data ? data : default);

        return messages;
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