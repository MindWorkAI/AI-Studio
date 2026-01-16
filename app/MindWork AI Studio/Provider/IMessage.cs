namespace AIStudio.Provider;

/// <summary>
/// Standard interface for messages exchanged with AI models.
/// </summary>
/// <typeparam name="T">The type of the message content.</typeparam>
public interface IMessage<T> : IMessageBase
{
    /// <summary>
    /// Gets the main content of the message exchanged with the AI model.
    /// The content encapsulates the core information or data being transmitted,
    /// and its type can vary based on the specific implementation or use case.
    /// </summary>
    public T Content { get; init; }
}