namespace AIStudio.Tools.DataBackup;

/// <summary>
/// The manifest of a backup, or why it could not be read.
/// </summary>
/// <param name="Manifest">The manifest, or null when the backup cannot be used.</param>
/// <param name="Issue">Why the backup cannot be used, for the user. Empty when it can.</param>
public sealed record DataBackupManifestReadResult(DataBackupManifest? Manifest, string Issue);