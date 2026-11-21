namespace AIStudio.Chat.Embedded;

/// <summary>
/// What an assistant decided while it readied a request of an embedded chat: whether the request goes out, and what it
/// carries beside the stored chat.
/// </summary>
/// <remarks>
/// Both are decided together, since what a request carries may only be known once it was readied, e.g., which images
/// of the assistant fit beside the system prompt. A ready preparation which carries more starts from READY:
/// <c>EmbeddedChatPreparation.READY with { ThreadForRequest = … }</c>.
/// </remarks>
public sealed record EmbeddedChatPreparation
{
    /// <summary>
    /// The request goes out with the chat as it is.
    /// </summary>
    public static readonly EmbeddedChatPreparation READY = new() { IsReady = true };

    /// <summary>
    /// The request does not go out; whoever readied it told the user why.
    /// </summary>
    public static readonly EmbeddedChatPreparation REFUSED = new() { IsReady = false };

    /// <summary>
    /// Gets whether the request goes out.
    /// </summary>
    public bool IsReady { get; private init; }

    /// <summary>
    /// Gets the chat which the request sends, e.g., with images which the stored chat never holds. The chat itself by
    /// default.
    /// </summary>
    /// <remarks>
    /// Called once the message and the block of the answer are in the chat, so that the request carries both. Change a
    /// copy only: the chat is stored as it is.
    /// </remarks>
    public Func<ChatThread, ChatThread> ThreadForRequest { get; init; } = thread => thread;
}