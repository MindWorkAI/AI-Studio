namespace AIStudio.Assistants.Builder;

internal sealed partial class LuaResponse
{
    public const string SCHEMA_VERSION_VALUE = "assistant_builder_lua_response_v2";
    public string SchemaVersion { get; init; } = string.Empty;
    public AssistantBuilderPluginMetadata? Plugin { get; init; }
    public AssistantBuilderAssistantMetadata? Assistant { get; init; }
    public string FullLua { get; init; } = string.Empty;
}