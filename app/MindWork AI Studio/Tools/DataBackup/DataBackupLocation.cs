namespace AIStudio.Tools.DataBackup;

/// <summary>
/// The folder in which an entry of the backup contents lies.
/// </summary>
public enum DataBackupLocation
{
    /// <summary>
    /// The data folder, where AI Studio keeps the chats, the plugins, and the indexes of the data sources.
    /// </summary>
    DATA_DIRECTORY,

    /// <summary>
    /// The configuration folder, where AI Studio keeps its settings.
    /// </summary>
    CONFIG_DIRECTORY,
}