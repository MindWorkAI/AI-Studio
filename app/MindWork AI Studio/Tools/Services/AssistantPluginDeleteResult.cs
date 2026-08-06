namespace AIStudio.Tools.Services;

public sealed record AssistantPluginDeleteResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, string Issue);