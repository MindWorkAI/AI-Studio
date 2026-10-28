using AIStudio.Chat;

namespace AIStudio.Tools.AssistantLaunch;

/// <summary>
/// A text sent to an assistant or into the chat, such as the result of an assistant or a message of a chat.
/// </summary>
/// <remarks>
/// The assistant puts the text into its main field, and the chat puts it into the field for the next message,
/// see AssistantLaunchService. The restrictions go along and hold there as they held where the text was written:
/// a text is never handed over without them.
/// </remarks>
/// <param name="Text">The text sent.</param>
/// <param name="Restrictions">What the text demands, because of where it came from.</param>
public sealed record TextLaunchPayload(string Text, ChatRestrictions Restrictions);
