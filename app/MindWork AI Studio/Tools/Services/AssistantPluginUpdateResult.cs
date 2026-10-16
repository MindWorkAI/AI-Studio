namespace AIStudio.Tools.Services;

public sealed record AssistantPluginUpdateResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, string Issue);