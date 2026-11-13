namespace AIStudio.Tools.DataBackup;

/// <summary>
/// How far a backup got.
/// </summary>
/// <param name="Phase">What the backup is busy with.</param>
/// <param name="Processed">How many steps of the phase are done: vector stores while the indexes are copied, files while they are written.</param>
/// <param name="Total">How many steps the phase has, or zero while that is not known yet.</param>
public readonly record struct DataBackupProgress(DataBackupPhase Phase, int Processed, int Total);