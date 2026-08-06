namespace AIStudio.Tools.Services;

public sealed record AssistantPluginInstallResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, bool ReplacedExisting, string Issue, bool Cancelled = false);