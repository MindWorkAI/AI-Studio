namespace AIStudio.Provider.SelfHosted;

/// <summary>
/// Chat message model.
/// </summary>
/// <param name="Content">The text content of the message.</param>
/// <param name="Role">The role of the message.</param>
public record TextMessage(string Content, string Role) : IMessage<string>
{
    public TextMessage() : this(string.Empty, string.Empty)
    {
    }
}