namespace AIStudio.Tools.Services;

public sealed record AssistantPluginCheckResult(bool Success, Guid PluginId, string PluginName, string Issue);