namespace AIStudio.Assistants.Builder;

public enum LuaResponseParseError
{
    NONE,
    MISSING_JSON_OBJECT,
    INVALID_JSON,
    EMPTY_JSON_OBJECT,
    UNSUPPORTED_SCHEMA_VERSION,
    MISSING_PLUGIN_METADATA,
    MISSING_ASSISTANT_METADATA,
    INCOMPLETE_PLUGIN_METADATA,
    INCOMPLETE_ASSISTANT_METADATA,
    MISSING_LUA,
    LUA_MISSING_ID,
}

public static class LuaResponseParseErrorExtension
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(LuaResponseParseError).Namespace, nameof(LuaResponseParseError));

    public static string GetMessage(this LuaResponseParseError parseError, string technicalDetails) => parseError switch
    {
        LuaResponseParseError.MISSING_JSON_OBJECT => TB("The model response is missing or unreadable."),
        LuaResponseParseError.INVALID_JSON => string.IsNullOrWhiteSpace(technicalDetails)
            ? TB("The model returned an invalid response.")
            : string.Format(TB("The model returned an invalid response: {0}"), technicalDetails),
        LuaResponseParseError.EMPTY_JSON_OBJECT => TB("The model returned an empty JSON object."),
        LuaResponseParseError.UNSUPPORTED_SCHEMA_VERSION => TB("The model responded with an unsupported or deprecated JSON schema."),
        LuaResponseParseError.MISSING_PLUGIN_METADATA => TB("The model's answer is missing the plugin metadata."),
        LuaResponseParseError.MISSING_ASSISTANT_METADATA => TB("The model's answer is missing the assistant metadata."),
        LuaResponseParseError.INCOMPLETE_PLUGIN_METADATA => TB("The model's answer contains incomplete plugin metadata."),
        LuaResponseParseError.INCOMPLETE_ASSISTANT_METADATA => TB("The model's answer contains incomplete assistant metadata."),
        LuaResponseParseError.MISSING_LUA => TB("The model response does not contain the generated Lua plugin code."),
        LuaResponseParseError.LUA_MISSING_ID => TB("The generated Lua plugin code does not contain a readable plugin ID."),
        _ => TB("The model returned an unusable JSON response."),
    };
}
