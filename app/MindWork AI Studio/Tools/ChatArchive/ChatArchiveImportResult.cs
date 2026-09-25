namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// The outcome of a chat archive import.
/// </summary>
/// <param name="Success">Whether the import ran without an error.</param>
/// <param name="ImportedWorkspaces">The number of workspaces which received chats.</param>
/// <param name="ImportedChats">The number of imported chats.</param>
/// <param name="SkippedChats">The number of chats which were skipped, because they already existed.</param>
/// <param name="FailedChats">The number of chats which could not be imported.</param>
/// <param name="Issue">The reason why the import failed, if it did.</param>
/// <param name="Cancelled">Whether the user stopped the import. Chats imported up to that point are kept.</param>
public sealed record ChatArchiveImportResult(
    bool Success,
    int ImportedWorkspaces,
    int ImportedChats,
    int SkippedChats,
    int FailedChats,
    string Issue,
    bool Cancelled);