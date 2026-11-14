namespace AIStudio.Chat.Embedded;

/// <summary>
/// A message for an embedded chat, together with what the assistant decides about the request it starts.
/// </summary>
public sealed record EmbeddedChatRequest
{
    /// <summary>
    /// The provider which answers.
    /// </summary>
    public required AIStudio.Settings.Provider Provider { get; init; }

    /// <summary>
    /// The message, with its attachments. The chat stores it as it is.
    /// </summary>
    public required ContentText Message { get; init; }

    /// <summary>
    /// Whether the user does not see the message, e.g., a request which lets the model open a conversation.
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// Whether messages to the user leave out which provider and which model answer, see IProvider.HidesIdentity.
    /// </summary>
    public bool HidesModelIdentity { get; init; }

    /// <summary>
    /// Readies the chat for the request before the message goes in: its system prompt, profile, tools, and data
    /// sources, which a chat partly does not store. Returns false to send nothing; it tells the user why itself.
    /// </summary>
    public Func<ChatThread, Task<bool>> PrepareAsync { get; init; } = _ => Task.FromResult(true);

    /// <summary>
    /// Gets the chat which the request sends, e.g., with images which the stored chat never holds. The chat itself
    /// by default.
    /// </summary>
    public Func<ChatThread, ChatThread> ThreadForRequest { get; init; } = thread => thread;
}