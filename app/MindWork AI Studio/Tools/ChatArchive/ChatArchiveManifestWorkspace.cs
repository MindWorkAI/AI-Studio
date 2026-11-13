namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// One workspace with chats in a backup.
/// </summary>
public sealed record ChatArchiveManifestWorkspace
{
    /// <summary>
    /// The unique identifier of the workspace.
    /// </summary>
    public Guid WorkspaceId { get; init; }

    /// <summary>
    /// The name of the workspace when the backup was made.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// All chats of this workspace in the backup.
    /// </summary>
    public List<ChatArchiveManifestChat> Chats { get; init; } = [];
}