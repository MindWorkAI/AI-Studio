namespace AIStudio.Tools.Services;

/// <summary>
/// The result of sharing or exporting a plugin archive.
/// </summary>
/// <param name="Outcome">What became of the request.</param>
/// <param name="PluginName">The name of the plugin.</param>
/// <param name="ArchivePath">Where the archive was written; empty when there is none.</param>
/// <param name="Issue">Why the plugin could not be shared or exported; empty otherwise.</param>
public sealed record PluginShareResult(PluginShareOutcome Outcome, string PluginName, string ArchivePath, string Issue);