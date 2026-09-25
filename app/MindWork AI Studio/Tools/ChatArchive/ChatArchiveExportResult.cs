namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// The outcome of a chat archive export.
/// </summary>
/// <param name="Success">Whether the archive was written completely.</param>
/// <param name="ArchivePath">The path of the written archive.</param>
/// <param name="ExportedWorkspaces">The number of exported workspaces.</param>
/// <param name="ExportedChats">The number of exported chats.</param>
/// <param name="IncludedFiles">The number of app-owned files, such as transcripts, which were included in the archive.</param>
/// <param name="UnreadableChats">The number of selected chats which could not be read and are missing from the archive.</param>
/// <param name="Issue">The reason why the export failed, if it did.</param>
/// <param name="Cancelled">Whether the user stopped the export. No archive is left behind then.</param>
public sealed record ChatArchiveExportResult(
    bool Success,
    string ArchivePath,
    int ExportedWorkspaces,
    int ExportedChats,
    int IncludedFiles,
    int UnreadableChats,
    string Issue,
    bool Cancelled);