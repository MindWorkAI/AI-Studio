namespace AIStudio.Tools.Services;

public sealed record PluginDeleteResult(bool Success, Guid PluginId, string PluginName, string PluginDirectory, string Issue);