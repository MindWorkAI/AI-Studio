namespace AIStudio.Tools.DataBackup;

/// <summary>
/// How a backup ended.
/// </summary>
/// <param name="Success">Whether the backup was written completely. A cancelled backup counts as no success.</param>
/// <param name="Cancelled">Whether the user stopped the backup.</param>
/// <param name="ArchivePath">The file of the backup.</param>
/// <param name="FileCount">How many files the backup holds.</param>
/// <param name="Bytes">How large these files are together, before they were compressed.</param>
/// <param name="IncludesDataSourceIndexes">Whether the backup holds the indexes of the data sources.</param>
/// <param name="Issue">Why the backup failed, for the user. Empty when it did not fail.</param>
public sealed record DataBackupResult(bool Success, bool Cancelled, string ArchivePath, int FileCount, long Bytes, bool IncludesDataSourceIndexes, string Issue);