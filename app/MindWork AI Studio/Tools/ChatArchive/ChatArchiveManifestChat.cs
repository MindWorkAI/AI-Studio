namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// One chat in a backup.
/// </summary>
public sealed record ChatArchiveManifestChat
{
    /// <summary>
    /// The unique identifier of the chat.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// The name of the chat when the backup was made.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}