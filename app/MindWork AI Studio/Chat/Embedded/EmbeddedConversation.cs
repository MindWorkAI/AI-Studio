namespace AIStudio.Chat.Embedded;

/// <summary>
/// An embedded chat as EmbeddedChatService keeps it, with whether the model answers in it right now.
/// </summary>
public sealed class EmbeddedConversation
{
    /// <summary>
    /// The ID of the chat.
    /// </summary>
    public required Guid ChatId { get; init; }

    /// <summary>
    /// The assistant which shows the chat. Its requests register in AssistantSessionService under it.
    /// </summary>
    public required Tools.Components Component { get; init; }

    /// <summary>
    /// Where the chat lives.
    /// </summary>
    public required IEmbeddedChatStore Store { get; init; }

    /// <summary>
    /// The thread of the chat, or null when this version cannot open the stored one.
    /// </summary>
    public ChatThread? Thread { get; internal set; }

    /// <summary>
    /// Whether the model answers in the chat right now.
    /// </summary>
    public bool IsRunning { get; internal set; }

    /// <summary>
    /// Whether the chat could be opened.
    /// </summary>
    public bool IsAvailable => this.Thread is not null;

    /// <summary>
    /// Whether the chat holds any message the user sees.
    /// </summary>
    public bool HasVisibleMessages => this.Thread?.Blocks.Any(block => !block.HideFromUser) ?? false;

    /// <summary>
    /// How many messages the user wrote in the chat, those the user does not see left aside.
    /// </summary>
    public int UserMessageCount => this.Thread?.Blocks.Count(block => block is { Role: ChatRole.USER, HideFromUser: false }) ?? 0;

    /// <summary>
    /// Whether the chat holds an answer of the model which the user sees.
    /// </summary>
    public bool HasAnswer => this.Thread?.Blocks.Any(block => block is { Role: ChatRole.AI, HideFromUser: false }) ?? false;
}