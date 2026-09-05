using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutionContext
{
    public required ToolDefinition Definition { get; init; }

    public string ToolCallId { get; init; } = string.Empty;

    public required SettingsManager SettingsManager { get; init; }

    public required IReadOnlyDictionary<string, string> SettingsValues { get; init; }

    public ConfidenceLevel ProviderConfidence { get; init; } = ConfidenceLevel.UNKNOWN;

    public bool ProviderIsTrustedByConfiguration { get; init; }
}