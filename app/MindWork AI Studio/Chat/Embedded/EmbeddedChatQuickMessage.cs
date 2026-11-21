namespace AIStudio.Chat.Embedded;

/// <summary>
/// A message which the user of an embedded chat sends with one click, such as asking for the evaluation at the end of
/// an exercise.
/// </summary>
/// <remarks>
/// The message goes into the chat as the user's own and stays visible there, so that the conversation reads the same
/// whether the user clicked or typed it. It is written in the language of the app; the model reads it like any other
/// message, so its prompt names what the message asks for, not its words.
/// </remarks>
/// <param name="Label">The label of the button.</param>
/// <param name="Icon">The icon of the button.</param>
/// <param name="Message">The message the button sends.</param>
/// <param name="Hint">What the chat says beside the button, such as when to use it; empty for nothing.</param>
public sealed record EmbeddedChatQuickMessage(string Label, string Icon, string Message, string Hint);