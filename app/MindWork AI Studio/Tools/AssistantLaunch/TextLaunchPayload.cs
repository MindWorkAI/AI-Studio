namespace AIStudio.Tools.AssistantLaunch;

/// <summary>
/// A text sent to an assistant or into the chat, such as the result of an assistant or a message of a chat.
/// </summary>
/// <remarks>
/// The assistant puts the text into its main field, and the chat puts it into the field for the next message,
/// see AssistantLaunchService.
/// </remarks>
/// <param name="Text">The text sent.</param>
public sealed record TextLaunchPayload(string Text);
