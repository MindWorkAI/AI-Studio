namespace AIStudio.Assistants;

/// <summary>
/// Where the system prompt of a chat comes from which continues the conversation of an assistant.
/// </summary>
/// <remarks>
/// Assistant plugins name these values in their key ChatSystemPrompt, so the names must not change.
/// </remarks>
public enum ChatSystemPromptSource
{
    /// <summary>
    /// The default prompt of AI Studio, which a new chat without a chat template gets as well.
    /// </summary>
    AI_STUDIO_DEFAULT,

    /// <summary>
    /// The prompt with which the assistant ran its conversation.
    /// </summary>
    ASSISTANT,

    /// <summary>
    /// A prompt which the assistant writes for the chat, e.g., because its own prompt forbids follow-up questions
    /// or demands JSON.
    /// </summary>
    CUSTOM,
}