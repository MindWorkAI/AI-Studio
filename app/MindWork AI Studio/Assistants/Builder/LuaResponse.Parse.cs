using System.Text.Json;

namespace AIStudio.Assistants.Builder;

internal sealed partial class LuaResponse
{
    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = 32,
    };

    public static bool TryParse(string modelResponse, out LuaResponse response, out LuaResponseParseError error, out string technicalDetails)
    {
        response = new();
        error = LuaResponseParseError.NONE;
        technicalDetails = string.Empty;

        var json = ExtractJson(modelResponse);
        if (string.IsNullOrWhiteSpace(json))
        {
            error = LuaResponseParseError.MISSING_JSON_OBJECT;
            return false;
        }

        LuaResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<LuaResponse>(json, JSON_OPTIONS);
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

        if (!parsed.IsValid(out error))
            return false;

        response = parsed;
        return true;
    }

    private bool IsValid(out LuaResponseParseError error)
    {
        error = LuaResponseParseError.NONE;

        if (!string.Equals(this.SchemaVersion, SCHEMA_VERSION_VALUE, StringComparison.Ordinal))
        {
            error = LuaResponseParseError.UNSUPPORTED_SCHEMA_VERSION;
            return false;
        }

        if (this.Plugin is null)
        {
            error = LuaResponseParseError.MISSING_PLUGIN_METADATA;
            return false;
        }

        if (this.Assistant is null)
        {
            error = LuaResponseParseError.MISSING_ASSISTANT_METADATA;
            return false;
        }

        if (string.IsNullOrWhiteSpace(this.Plugin.Name) ||
            string.IsNullOrWhiteSpace(this.Plugin.Description) ||
            this.Plugin.Categories.Length == 0 ||
            this.Plugin.Categories.Any(string.IsNullOrWhiteSpace))
        {
            error = LuaResponseParseError.INCOMPLETE_PLUGIN_METADATA;
            return false;
        }

        if (string.IsNullOrWhiteSpace(this.Assistant.Title) ||
            string.IsNullOrWhiteSpace(this.Assistant.Description) ||
            !IsValidAssistantMetadata(this.Assistant))
        {
            error = LuaResponseParseError.INCOMPLETE_ASSISTANT_METADATA;
            return false;
        }

        if (string.IsNullOrWhiteSpace(this.FullLua))
        {
            error = LuaResponseParseError.MISSING_LUA;
            return false;
        }

        if (!this.FullLua.Contains("ID = \"", StringComparison.Ordinal))
        {
            error = LuaResponseParseError.LUA_MISSING_ID;
            return false;
        }

        return true;
    }

    private static bool IsValidAssistantMetadata(AssistantBuilderAssistantMetadata assistant) => assistant.Kind switch
    {
        "FORM" => !string.IsNullOrWhiteSpace(assistant.SystemPrompt) &&
                  !string.IsNullOrWhiteSpace(assistant.SubmitText) &&
                  assistant.AllowAiStudioProfiles.HasValue &&
                  IsValidToolIds(assistant.ToolIds) &&
                  assistant.Launch is null,

        // A launcher names its tools inside launch, so the same field one level up would be a
        // second, competing selection:
        "CHAT_LAUNCHER" => assistant.SystemPrompt is null &&
                           assistant.SubmitText is null &&
                           assistant.AllowAiStudioProfiles is null &&
                           assistant.ToolIds is null &&
                           IsValidChatLaunchMetadata(assistant.Launch),
        _ => false,
    };

    private static bool IsValidChatLaunchMetadata(AssistantBuilderChatLaunchMetadata? launch)
    {
        if (launch is null || string.IsNullOrWhiteSpace(launch.WorkspaceName))
            return false;

        if (!IsOptionalGuid(launch.ProviderId, allowEmpty: false) ||
            !IsOptionalGuid(launch.ProfileId, allowEmpty: true) ||
            !IsOptionalGuid(launch.ChatTemplateId, allowEmpty: true))
            return false;

        if (launch.DataSourceIds is not null &&
            (launch.DataSourceIds.Length == 0 ||
             !launch.DataSourceIds.All(id => Guid.TryParse(id, out var parsed) && parsed != Guid.Empty) ||
             launch.DataSourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count() != launch.DataSourceIds.Length))
            return false;

        return IsValidToolIds(launch.ToolIds);
    }

    /// <remarks>
    /// Tool IDs are plain names, so only their shape can be checked here. Whether the named tools
    /// exist is decided later, against the tools this AI Studio actually has.
    /// </remarks>
    private static bool IsValidToolIds(string[]? toolIds) =>
        toolIds is null ||
        toolIds.Length > 0 &&
        toolIds.All(id => !string.IsNullOrWhiteSpace(id)) &&
        toolIds.Distinct(StringComparer.Ordinal).Count() == toolIds.Length;

    private static bool IsOptionalGuid(string? value, bool allowEmpty) => value is null ||
        Guid.TryParse(value, out var parsed) && (allowEmpty || parsed != Guid.Empty);

    /// <summary>
    /// Reads the first complete JSON object out of a model answer that may carry text around it.
    /// </summary>
    /// <remarks>
    /// Shared with the launcher texts response, which is a different shape but arrives the same
    /// way, wrapped in whatever prose the model felt like adding.
    /// </remarks>
    internal static string ExtractJson(string input)
    {
        var start = input.IndexOf('{');
        if (start < 0)
            return string.Empty;

        var depth = 0;
        var insideString = false;
        for (var index = start; index < input.Length; index++)
        {
            if (input[index] == '"' && !IsEscaped(input, index))
                insideString = !insideString;

            if (insideString)
                continue;

            switch (input[index])
            {
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    break;
            }

            if (depth == 0)
                return input[start..(index + 1)];
        }

        return string.Empty;
    }

    private static bool IsEscaped(string input, int index)
    {
        var backslashCount = 0;
        for (var i = index - 1; i >= 0 && input[i] == '\\'; i--)
            backslashCount++;

        return backslashCount % 2 == 1;
    }
}
