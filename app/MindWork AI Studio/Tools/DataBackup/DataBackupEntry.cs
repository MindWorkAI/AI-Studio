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
        or DataBackupEntryKind.CHAT_PROVENANCE
        or DataBackupEntryKind.PLUGINS
        or DataBackupEntryKind.TOKENIZERS
        or DataBackupEntryKind.VISUAL_BRIEFINGS
        or DataBackupEntryKind.DATA_SOURCE_INDEX
        or DataBackupEntryKind.VECTOR_STORES
        or DataBackupEntryKind.MEDIA_TRANSCRIPTS;

    /// <summary>
    /// Whether a backup takes the entry from the copies it makes while the indexing pauses, rather than from the data folder.
    /// </summary>
    /// <remarks>
    /// The indexing writes to these entries, and they refer to each other: the index knows which
    /// transcript belongs to which file. A backup therefore copies them together while nothing
    /// writes, cf. DataBackupService.CopyIndexesAsync, and a restore replaces them only together.
    /// </remarks>
    public bool ComesFromIndexCopies => this.Kind is DataBackupEntryKind.DATA_SOURCE_INDEX
        or DataBackupEntryKind.VECTOR_STORES
        or DataBackupEntryKind.MEDIA_TRANSCRIPTS;

    /// <summary>
    /// Whether a backup takes the entry from the copy it makes of the provenance store, rather than from the data folder.
    /// </summary>
    /// <remarks>
    /// The chats write to the store at any time, so a file-by-file copy could catch it halfway
    /// through a write. Unlike the indexes, it is copied for every backup, since it belongs to the
    /// chats, which every backup holds. Cf. DataBackupSources.CopyProvenanceStoreAsync.
    /// </remarks>
    public bool ComesFromProvenanceCopy => this.Kind is DataBackupEntryKind.CHAT_PROVENANCE;

    /// <summary>
    /// Whether the entry is a folder rather than a single file.
    /// </summary>
    public bool IsFolder => this.RelativePath.EndsWith(DataBackupContents.SEPARATOR);
}