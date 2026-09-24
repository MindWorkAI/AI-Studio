using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutionResult
{
    public string? TextContent { get; init; }

    public JsonNode? JsonContent { get; init; }

    public IReadOnlyList<Source> Sources { get; init; } = [];

    public ConfidenceLevel RequiredProviderConfidence { get; init; } = ConfidenceLevel.NONE;

    /// <summary>
    /// The data security the chat has to keep from now on, because of what this result brings in.
    /// </summary>
    /// <remarks>
    /// The other axis next to RequiredProviderConfidence. A data source which may only be used with
    /// self-hosted providers says so here, and the chat then refuses every other provider from now
    /// on, see ChatThread.RequireDataSecurity. Left at NOT_SPECIFIED, the result says nothing about
    /// it, and the chat stays as it was.
    /// </remarks>
    public DataSourceSecurity RequiredDataSecurity { get; init; } = DataSourceSecurity.NOT_SPECIFIED;

    public string ToModelContent()
    {
        if (this.JsonContent is not null)
            return this.JsonContent.ToJsonString();

        return this.TextContent ?? string.Empty;
    }
}