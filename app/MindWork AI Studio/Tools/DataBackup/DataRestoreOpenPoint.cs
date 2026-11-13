namespace AIStudio.Tools.DataBackup;

/// <summary>
/// One thing the user still has to do after a restore.
/// </summary>
/// <param name="Kind">What to do.</param>
/// <param name="Name">What it concerns: the name of a provider, a data source, or a tool, or a path.</param>
/// <param name="Setting">For a tool: the label of the setting which lacks its value; otherwise empty.</param>
public sealed record DataRestoreOpenPoint(DataRestoreOpenPointKind Kind, string Name, string Setting = "");