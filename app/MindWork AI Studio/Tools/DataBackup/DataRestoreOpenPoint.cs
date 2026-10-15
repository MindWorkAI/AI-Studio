namespace AIStudio.Tools.DataBackup;

/// <summary>
/// One thing the user still has to do after a restore.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Name">What it concerns: the name of a provider or a data source, or a path.</param>
public sealed record DataRestoreOpenPoint(DataRestoreOpenPointKind Kind, string Name);