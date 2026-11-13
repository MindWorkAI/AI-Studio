using System.Text.Json;

namespace AIStudio.Assistants.Builder;

/// <summary>
/// The three texts a model writes for a direct chat launcher.
/// </summary>
/// <remarks>
/// A launcher has no system prompt, no form, and no prompt builder, and its chat settings come
/// straight from the Builder form. That leaves nothing for a model to write except the names a
/// person reads, so it is asked for those alone and AI Studio writes the plugin.lua itself.
/// </remarks>
internal sealed class LauncherTextsResponse
{
    public const string SCHEMA_VERSION_VALUE = "assistant_builder_launcher_texts_v1";

    /// <summary>
    /// How the answer of the model is read.
    /// </summary>
    /// <remarks>
    /// An answer which names a field twice leaves open which of the values the model meant, so it is
    /// refused like any other invalid JSON.
    /// </remarks>
    internal static readonly JsonSerializerOptions JSON_OPTIONS = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 8,
        AllowDuplicateProperties = false,
        TypeInfoResolver = LauncherTextsResponseJsonContext.Default,
    }.AsReadOnly();

    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// The plugin name, shown on the plugins page.
    /// </summary>
    public string PluginName { get; set; } = string.Empty;

    /// <summary>
    /// The title on the tile.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The short description, used for both the plugin and the tile.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public static bool TryParse(string modelResponse, out LauncherTextsResponse response, out LuaResponseParseError error, out string technicalDetails)
    {
        response = new();
        error = LuaResponseParseError.NONE;
        technicalDetails = string.Empty;

        var json = ModelAnswerJson.ExtractObject(modelResponse);
        if (json.IsWhiteSpace())
        {
            error = LuaResponseParseError.MISSING_JSON_OBJECT;
            return false;
        }

        LauncherTextsResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<LauncherTextsResponse>(json, JSON_OPTIONS);
        }
        catch (JsonException e)
        {
            error = LuaResponseParseError.INVALID_JSON;
            technicalDetails = e.Message;
            return false;
        }

        if (parsed is null)
        {
            error = LuaResponseParseError.EMPTY_JSON_OBJECT;
            return false;
        }

        if (!string.Equals(parsed.SchemaVersion, SCHEMA_VERSION_VALUE, StringComparison.Ordinal))
        {
            error = LuaResponseParseError.UNSUPPORTED_SCHEMA_VERSION;
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsed.PluginName) ||
            string.IsNullOrWhiteSpace(parsed.Title) ||
            string.IsNullOrWhiteSpace(parsed.Description))
        {
            error = LuaResponseParseError.INCOMPLETE_ASSISTANT_METADATA;
            return false;
        }

        response = parsed;
        return true;
    }
}