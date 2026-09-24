using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

using AIStudio.Provider;
using AIStudio.Settings.DataModel;

namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolExecutionResult
{
    /// <summary>
    /// How a JSON result is written for the model.
    /// </summary>
    /// <remarks>
    /// The result goes into the request as a string, and the request is serialized once more on its
    /// way to the provider, so the model reads whatever this escapes as the escape itself. The
    /// default encoder escapes every character outside ASCII and those HTML treats specially, for
    /// JSON embedded in a web page, which this never is: a German document would reach the model
    /// with every umlaut as six characters. The relaxed encoder escapes only what JSON requires.
    /// </remarks>
    private static readonly JsonSerializerOptions MODEL_CONTENT_OPTIONS = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

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
            return this.JsonContent.ToJsonString(MODEL_CONTENT_OPTIONS);

        return this.TextContent ?? string.Empty;
    }
}