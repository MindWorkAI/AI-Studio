using AIStudio.Chat;

namespace AIStudio.Assistants;

/// <summary>
/// Which system prompt a chat gets which continues the conversation of an assistant.
/// </summary>
/// <remarks>
/// Every assistant makes this choice itself, see AssistantBase.ChatSystemPrompt. The prompt of an assistant
/// may forbid follow-up questions or demand JSON, while the default prompt of AI Studio knows nothing about
/// the role and the settings of the assistant. A prompt of its own for the chat keeps the role and the
/// settings, but leaves out the rules for the result.<br/><br/>
///
/// That prompt is built only when it is needed, so the choice itself can be read without the state of the
/// assistant.
/// </remarks>
/// <param name="Source">Where the system prompt of the chat comes from.</param>
/// <param name="BuildCustomPrompt">Builds the prompt of the assistant for the chat; only read for ChatSystemPromptSource.CUSTOM.</param>
public sealed record ChatSystemPromptChoice(ChatSystemPromptSource Source, Func<string> BuildCustomPrompt)
{
    /// <summary>
    /// The chat gets the default prompt of AI Studio.
    /// </summary>
    public static readonly ChatSystemPromptChoice AI_STUDIO_DEFAULT = new(ChatSystemPromptSource.AI_STUDIO_DEFAULT, () => string.Empty);

    /// <summary>
    /// The chat gets the prompt with which the assistant ran its conversation.
    /// </summary>
    public static readonly ChatSystemPromptChoice ASSISTANT = new(ChatSystemPromptSource.ASSISTANT, () => string.Empty);

    /// <summary>
    /// The chat gets a prompt which the assistant writes for it.
    /// </summary>
    /// <param name="buildPrompt">Builds the prompt from the settings of the assistant.</param>
    /// <returns>The choice of a prompt of its own.</returns>
    public static ChatSystemPromptChoice Custom(Func<string> buildPrompt) => new(ChatSystemPromptSource.CUSTOM, buildPrompt);

    /// <summary>
    /// Creates the system prompt of the chat.
    /// </summary>
    /// <param name="promptOfAssistant">The prompt with which the assistant ran its conversation.</param>
    /// <returns>The system prompt of the chat; empty when the assistant wrote an empty prompt for the chat.</returns>
    public string CreatePrompt(string promptOfAssistant) => this.Source switch
    {
        ChatSystemPromptSource.ASSISTANT => promptOfAssistant,
        ChatSystemPromptSource.CUSTOM => this.BuildCustomPrompt(),

        _ => SystemPrompts.DEFAULT,
    };
}