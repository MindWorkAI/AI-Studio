namespace AIStudio.Chat;

/// <summary>
/// A block of content in a chat thread. Might be any type of content, e.g., text, image, voice, etc.
/// </summary>
public class ContentBlock
{
    /// <summary>
    /// Time when the content block was created.
    /// </summary>
    public DateTimeOffset Time { get; set; }
    
    /// <summary>
    /// Type of the content block, e.g., text, image, voice, etc.
    /// </summary>
    public ContentType ContentType { get; set; } = ContentType.NONE;
    
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
    public bool HideFromUser { get; set; }

    public ContentBlock DeepClone(bool changeHideState = false, bool hideFromUser = true) => new()
    {
        Time = this.Time,
        ContentType = this.ContentType,
        Content = this.Content?.DeepClone(),
        Role = this.Role,
        HideFromUser = changeHideState ? hideFromUser : this.HideFromUser,
    };

    /// <summary>
    /// Clones this block for a place apart from its chat, e.g., the example conversation of a chat template.
    /// </summary>
    /// <remarks>
    /// Without anything which refers to the provenance store: its entries belong to the chat and are
    /// deleted with it. The passages of an answer stay behind, and every chat which receives an
    /// attached document reads it anew from its file, cf. FileAttachment.ProvenanceId.
    /// </remarks>
    public ContentBlock CloneApartFromChat(bool changeHideState = false, bool hideFromUser = true)
    {
        var clone = this.DeepClone(changeHideState, hideFromUser);
        if (clone.Content is ContentText text)
            text.SourcePassages = [];

        if (clone.Content is not null)
            clone.Content.FileAttachments = clone.Content.FileAttachments.Select(attachment => attachment.WithoutStoredText()).ToList();

        return clone;
    }
}