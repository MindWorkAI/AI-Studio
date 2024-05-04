namespace AIStudio.Chat;

/// <summary>
/// A block of content in a chat thread. Might be any type of content, e.g., text, image, voice, etc.
/// </summary>
/// <param name="time">Time when the content block was created.</param>
/// <param name="type">Type of the content block, e.g., text, image, voice, etc.</param>
/// <param name="content">The content of the block.</param>
public class ContentBlock(DateTimeOffset time, ContentType type, IContent content)
{
    /// <summary>
    /// Time when the content block was created.
    /// </summary>
    public DateTimeOffset Time => time;
    
    /// <summary>
    /// Type of the content block, e.g., text, image, voice, etc.
    /// </summary>
    public ContentType ContentType => type;
    
    /// <summary>
    /// The content of the block.
    /// </summary>
    public IContent Content => content;

    /// <summary>
    /// The role of the content block in the chat thread, e.g., user, AI, etc.
    /// </summary>
    public ChatRole Role { get; init; } = ChatRole.NONE;
}