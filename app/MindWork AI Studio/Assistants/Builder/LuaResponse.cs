namespace AIStudio.Assistants.Builder;

internal sealed partial class LuaResponse
{
    public const string SCHEMA_VERSION_VALUE = "assistant_builder_lua_response_v2";
    public string SchemaVersion { get; set; } = string.Empty;
    public AssistantBuilderPluginMetadata? Plugin { get; set; }
    public AssistantBuilderAssistantMetadata? Assistant { get; set; }
    public string FullLua { get; set; } = string.Empty;
}