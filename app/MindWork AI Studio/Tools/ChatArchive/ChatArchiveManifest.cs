namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// The chats a backup holds, so that the user sees them before any is added.
/// </summary>
public sealed record ChatArchiveManifest
{
    /// <summary>
    /// When the backup was made.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The AI Studio version which made the backup. Informational only.
    /// </summary>
    public string AppVersion { get; init; } = string.Empty;

    /// <summary>
    /// All workspaces with chats in the backup.
    /// </summary>
    public List<ChatArchiveManifestWorkspace> Workspaces { get; init; } = [];

    /// <summary>
    /// All temporary chats in the backup.
    /// </summary>
    public List<ChatArchiveManifestChat> TemporaryChats { get; init; } = [];

    /// <summary>
    /// Gets the total number of chats in the backup.
    /// </summary>
    public int TotalChatCount => this.Workspaces.Sum(workspace => workspace.Chats.Count) + this.TemporaryChats.Count;
}