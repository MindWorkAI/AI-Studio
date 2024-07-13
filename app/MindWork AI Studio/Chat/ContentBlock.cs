namespace AIStudio.Chat;

/// <summary>
/// A block of content in a chat thread. Might be any type of content, e.g., text, image, voice, etc.
/// </summary>
public class ContentBlock
{
    /// <summary>
    /// Time when the content block was created.
    /// </summary>
    public DateTimeOffset Time { get; init; }
    
    /// <summary>
    /// Type of the content block, e.g., text, image, voice, etc.
    /// </summary>
    public ContentType ContentType { get; init; } = ContentType.NONE;
    
    /// <summary>
    /// The content of the block.
    /// </summary>
    public IContent? Content { get; init; } = null;

    /// <summary>
    /// The role of the content block in the chat thread, e.g., user, AI, etc.
    /// </summary>
    public ChatRole Role { get; init; } = ChatRole.NONE;
}