namespace AIStudio.Tools.DataBackup;

/// <summary>
/// How the preparation of a restore ended.
/// </summary>
/// <param name="Success">Whether the restore is prepared and waits for the next start.</param>
/// <param name="Cancelled">Whether the user stopped the preparation.</param>
/// <param name="Issue">Why the restore cannot be prepared, for the user. Empty when it is prepared.</param>
public sealed record DataRestoreResult(bool Success, bool Cancelled, string Issue);