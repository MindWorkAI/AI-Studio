using AIStudio.Tools.DataBackup;

namespace AIStudio.Tools.ChatArchive;

/// <summary>
/// Where a backup keeps its chats, for adding them to the chats there are.
/// </summary>
/// <remarks>
/// A backup holds the chats in the layout of the data folder, cf. DataBackupFormat: every chat in a
/// directory of its own, below its workspace or among the temporary chats.
/// </remarks>
public static class ChatArchiveFormat
{
    /// <summary>
    /// Where a backup keeps the workspaces, each in a directory named after its ID.
    /// </summary>
    public static readonly string WORKSPACES_PREFIX = DataBackupFormat.DATA_ROOT + DataBackupContents.ENTRIES.First(entry => entry.Kind is DataBackupEntryKind.WORKSPACES).RelativePath;

    /// <summary>
    /// Where a backup keeps the temporary chats, each in a directory named after its ID.
    /// </summary>
    public static readonly string TEMPORARY_CHATS_PREFIX = DataBackupFormat.DATA_ROOT + DataBackupContents.ENTRIES.First(entry => entry.Kind is DataBackupEntryKind.TEMPORARY_CHATS).RelativePath;

    /// <summary>
    /// The file storing the workspace or chat name.
    /// </summary>
    public const string NAME_FILE_NAME = "name";

    /// <summary>
    /// The file storing the serialized chat thread.
    /// </summary>
    public const string THREAD_FILE_NAME = "thread.json";
}