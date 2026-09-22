using System.Text.Json.Nodes;

using AIStudio.Provider;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutionResult
{
    public string? TextContent { get; init; }

    public JsonNode? JsonContent { get; init; }

    public IReadOnlyList<Source> Sources { get; init; } = [];

    public ConfidenceLevel RequiredProviderConfidence { get; init; } = ConfidenceLevel.NONE;

    public string ToModelContent()
    {
        if (this.JsonContent is not null)
            return this.JsonContent.ToJsonString();

        return this.TextContent ?? string.Empty;
    }
}