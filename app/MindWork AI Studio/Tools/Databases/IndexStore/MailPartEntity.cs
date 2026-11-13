namespace AIStudio.Tools.Databases.IndexStore;

/// <summary>
/// One part of a mail as AI Studio read it: the header block, the body or an attachment.
/// </summary>
/// <remarks>
/// The chunks hold the same text, but cut into overlapping pieces. Reading a mail as a whole, or
/// checking its header block, needs it in one piece, and without asking the server again.
/// </remarks>
internal sealed class MailPartEntity
{
    public int Id { get; set; }

    public string ParentFileId { get; set; } = string.Empty;

    /// <summary>
    /// HEADERS, BODY or ATTACHMENT, stored by name.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// The place of the part among the parts of its kind, starting at zero.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// The file name of an attachment, empty for the other parts.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// The size of the part on the server, in bytes.
    /// </summary>
    public long PartSize { get; set; }

    /// <summary>
    /// The text of the part, or null when there is none. TextState says why.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Whether the text could be read and, when not, why, stored by name.
    /// </summary>
    public string TextState { get; set; } = string.Empty;

    public MailMessageEntity? Message { get; set; }
}