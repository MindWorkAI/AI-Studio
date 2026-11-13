namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Determines what the import does when a chat from the archive already exists.
/// </summary>
public enum ChatArchiveCollisionBehavior
{
    /// <summary>
    /// Keeps the existing chat and ignores the one from the archive.
    /// </summary>
    SKIP,

    /// <summary>
    /// Imports the chat from the archive as an additional chat.
    /// </summary>
    IMPORT_AS_COPY,
}