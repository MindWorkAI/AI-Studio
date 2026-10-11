using Microsoft.AspNetCore.Components;

namespace AIStudio.Tools;

public interface IMessageBusReceiver
{
    public Task ProcessMessage<T>(ComponentBase? sendingComponent, Event triggeredEvent, T? data);
    
    /// <summary>
    /// Answers a question which was asked through MessageBus.SendMessageUseFirstResult.
    /// </summary>
    /// <returns>The answer, or MessageBusAnswer.NONE when this receiver does not handle the question.</returns>
    public Task<MessageBusAnswer<TResult>> ProcessMessageWithResult<TPayload, TResult>(ComponentBase? sendingComponent, Event triggeredEvent, TPayload? data);
}