namespace AIStudio.Tools.DataBackup;

/// <summary>
/// How far the preparation of a restore got.
/// </summary>
/// <param name="Processed">How many files of the backup are unpacked.</param>
/// <param name="Total">How many files there are to unpack, or zero while that is not known yet.</param>
public readonly record struct DataRestoreProgress(int Processed, int Total);