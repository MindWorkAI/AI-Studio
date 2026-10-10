namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Describes one exported chat inside a chat archive.
/// </summary>
public sealed record ChatArchiveManifestChat
{
    /// <summary>
    /// The unique identifier of the chat.
    /// </summary>
    public Guid ChatId { get; init; }

    /// <summary>
    /// The name of the chat at the time of the export.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The time of the last edit at the time of the export.
    /// </summary>
    public DateTimeOffset LastEditTime { get; init; }

    /// <summary>
    /// The number of app-owned files of this chat which were included in the archive,
    /// such as transcripts.
    /// </summary>
    public int IncludedFiles { get; init; }
}