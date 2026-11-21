namespace AIStudio.Chat.Compression;

/// <summary>
/// Where a part of the conversation begins, see ChatCompressionInput.TakePart.
/// </summary>
/// <param name="Message">The index of the first message of the part.</param>
/// <param name="Offset">Where in the text of that message the part begins: 0, unless the part before split the message.</param>
public readonly record struct ChatCompressionPosition(int Message, int Offset)
{
    /// <summary>
    /// The beginning of the conversation.
    /// </summary>
    public static readonly ChatCompressionPosition START = new(0, 0);
}