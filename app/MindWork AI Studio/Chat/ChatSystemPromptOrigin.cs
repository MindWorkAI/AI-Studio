using AIStudio.Assistants;

namespace AIStudio.Chat;

/// <summary>
/// Which assistant chose the system prompt of a chat, and how.
/// </summary>
/// <remarks>
/// Stored with a chat which continues the conversation of an assistant, so that the dialog showing the
/// system prompt can name the assistant, and tell the prompt of the assistant apart from a prompt the
/// assistant wrote for the chat. A chat without it got its prompt elsewhere: from AI Studio, from a
/// chat template, or from a version of AI Studio before v26.11.1.
/// </remarks>
public sealed record ChatSystemPromptOrigin
{
    /// <summary>
    /// Whether the chat got the prompt of the assistant or a prompt the assistant wrote for the chat.
    /// </summary>
    public ChatSystemPromptSource Source { get; set; } = ChatSystemPromptSource.ASSISTANT;

    /// <summary>
    /// The name of the assistant, as AI Studio showed it when the chat started.
    /// </summary>
    public string AssistantName { get; set; } = string.Empty;
}