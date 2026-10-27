namespace AIStudio.Tools.DataBackup;

/// <summary>
/// What a backup holds, and what wrote it.
/// </summary>
/// <param name="FormatVersion">The version of the format, cf. DataBackupFormat.FORMAT_VERSION.</param>
/// <param name="AppVersion">The version of AI Studio which wrote the backup.</param>
/// <param name="SettingsVersion">The version of the settings in the backup.</param>
/// <param name="CreatedAtUtc">When the backup was written.</param>
/// <param name="SourceDataDirectory">The data folder the backup was made from, for whoever wonders where it came from.</param>
/// <param name="SourceConfigDirectory">The configuration folder the backup was made from.</param>
/// <param name="IncludesDataSourceIndexes">Whether the backup holds the indexes of the data sources and the transcripts of their media files. Without them, a restore keeps the indexes and transcripts there are.</param>
/// <param name="Entries">Every file of the backup, the manifest aside.</param>
public sealed record DataBackupManifest(
    int FormatVersion,
    string AppVersion,
    string SettingsVersion,
    DateTimeOffset CreatedAtUtc,
    string SourceDataDirectory,
    string SourceConfigDirectory,
    bool IncludesDataSourceIndexes,
    IReadOnlyList<DataBackupManifestEntry> Entries);