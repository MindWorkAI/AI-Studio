namespace AIStudio.Tools.Services;

public sealed record PluginShareResult(bool Success, string PluginName, string ArchivePath, string Issue, bool Cancelled = false);