using AIStudio.Chat;
using AIStudio.Provider;
using AIStudio.Settings;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutionContext
{
    public required ToolDefinition Definition { get; init; }

    /// <summary>
    /// The chat the call was made in.
    /// </summary>
    /// <remarks>
    /// For a tool which works with what the chat was set up with, such as Semantic Search with the
    /// data sources the user picked for it. A tool reads it; what the chat has to keep because of
    /// the result goes back through the ToolExecutionResult instead.
    /// </remarks>
    public required ChatThread ChatThread { get; init; }

    /// <summary>
    /// The provider the call came from.
    /// </summary>
    /// <remarks>
    /// For a tool which checks more than the confidence of the provider, such as Semantic Search:
    /// before it searches, it asks again which data sources this provider may search, because
    /// rounds may have passed since they were offered.
    /// </remarks>
    public required IProvider Provider { get; init; }

    public string ToolCallId { get; init; } = string.Empty;

    public required SettingsManager SettingsManager { get; init; }

    public required IReadOnlyDictionary<string, string> SettingsValues { get; init; }

    public ConfidenceLevel ProviderConfidence { get; init; } = ConfidenceLevel.UNKNOWN;
}