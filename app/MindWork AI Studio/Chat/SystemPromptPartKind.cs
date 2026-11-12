namespace AIStudio.Chat;

/// <summary>
/// Where a part of the system prompt comes from.
/// </summary>
/// <remarks>
/// The members are listed in the order in which the parts stand in the system prompt, see
/// ChatThread.BuildSystemPrompt.
/// </remarks>
public enum SystemPromptPartKind
{
    /// <summary>
    /// The current date and time, in front of everything else.
    /// </summary>
    DATE_TIME,

    /// <summary>
    /// The prompt the chat was set up with, used when no chat template replaces it.
    /// </summary>
    BASE_PROMPT,

    /// <summary>
    /// The prompt of the chat template, which replaces the one the chat was set up with.
    /// </summary>
    CHAT_TEMPLATE,

    /// <summary>
    /// What the last request retrieved from the data sources of the chat.
    /// </summary>
    RETRIEVED_DATA,

    /// <summary>
    /// What the selected profile tells about the user.
    /// </summary>
    PROFILE,

    /// <summary>
    /// How to use the tools the request offers.
    /// </summary>
    TOOL_INSTRUCTIONS,
}