namespace AIStudio.Tools.DataBackup;

/// <summary>
/// A file or folder AI Studio keeps in its data or configuration folder.
/// </summary>
/// <param name="Location">The folder in which the entry lies.</param>
/// <param name="RelativePath">The path of the entry within that folder, with forward slashes. A folder ends with a slash.</param>
/// <param name="Kind">What the entry holds, and with it, whether it belongs into a backup.</param>
public sealed record DataBackupEntry(DataBackupLocation Location, string RelativePath, DataBackupEntryKind Kind)
{
    /// <summary>
    /// Whether the entry belongs into a backup.
    /// </summary>
    public bool IsIncluded => this.Kind is DataBackupEntryKind.SETTINGS
        or DataBackupEntryKind.WORKSPACES
        or DataBackupEntryKind.TEMPORARY_CHATS
        or DataBackupEntryKind.PLUGINS
        or DataBackupEntryKind.TOKENIZERS
        or DataBackupEntryKind.VISUAL_BRIEFINGS
        or DataBackupEntryKind.DATA_SOURCE_INDEX
        or DataBackupEntryKind.VECTOR_STORES;

    /// <summary>
    /// Whether the entry is a folder rather than a single file.
    /// </summary>
    public bool IsFolder => this.RelativePath.EndsWith(DataBackupContents.SEPARATOR);
}