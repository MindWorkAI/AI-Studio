namespace AIStudio.Chat.Compression;

/// <summary>
/// One message of the conversation as the AI reads it for the summary, together with its size.
/// </summary>
/// <param name="Text">The message, rendered for the summary, see ChatCompressionInput.Render.</param>
/// <param name="Tokens">How many tokens the text takes, counted or estimated.</param>
public readonly record struct CountedMessage(string Text, int Tokens);