namespace AIStudio.Tools.DataBackup;

/// <summary>
/// One file of a backup, as the manifest lists it.
/// </summary>
/// <param name="Name">The name of the entry within the backup.</param>
/// <param name="Size">The size of the file in bytes.</param>
/// <param name="Sha256">The SHA-256 hash of the file, in lowercase hex digits.</param>
public sealed record DataBackupManifestEntry(string Name, long Size, string Sha256);