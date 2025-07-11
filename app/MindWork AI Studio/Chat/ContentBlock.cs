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
    public IContent? Content { get; set; }

    /// <summary>
    /// The role of the content block in the chat thread, e.g., user, AI, etc.
    /// </summary>
    public ChatRole Role { get; set; } = ChatRole.NONE;

    /// <summary>
    /// Should the content block be hidden from the user?
    /// </summary>
    public bool HideFromUser { get; init; }

    public ContentBlock DeepClone(bool changeHideState = false, bool hideFromUser = true) => new()
    {
        Time = this.Time,
        ContentType = this.ContentType,
        Content = this.Content?.DeepClone(),
        Role = this.Role,
        HideFromUser = changeHideState ? hideFromUser : this.HideFromUser,
    };
}