namespace AIStudio.Tools.DataBackup;

/// <summary>
/// What a backup is busy with.
/// </summary>
public enum DataBackupPhase
{
    /// <summary>
    /// It collects the settings and pauses the indexing.
    /// </summary>
    PREPARING,

    /// <summary>
    /// It copies the indexes of the data sources, one vector store after the other.
    /// </summary>
    COPYING_INDEXES,

    /// <summary>
    /// It writes the files into the backup.
    /// </summary>
    WRITING,
}