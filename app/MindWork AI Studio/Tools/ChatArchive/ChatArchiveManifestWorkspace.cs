namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Describes one exported workspace inside a chat archive.
/// </summary>
public sealed record ChatArchiveManifestWorkspace
{
    /// <summary>
    /// The unique identifier of the workspace.
    /// </summary>
    public Guid WorkspaceId { get; init; }

    /// <summary>
    /// The name of the workspace at the time of the export.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// All chats of this workspace contained in the archive.
    /// </summary>
    public List<ChatArchiveManifestChat> Chats { get; init; } = [];
}