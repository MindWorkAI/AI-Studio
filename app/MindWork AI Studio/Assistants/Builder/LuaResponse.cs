namespace AIStudio.Assistants.Builder;

internal sealed partial class LuaResponse
{
    public const string SCHEMA_VERSION_VALUE = "assistant_builder_lua_response_v2";
    public string SchemaVersion { get; init; } = string.Empty;
    public AssistantBuilderPluginMetadata? Plugin { get; init; }
    public AssistantBuilderAssistantMetadata? Assistant { get; init; }
    public string FullLua { get; init; } = string.Empty;
}

internal sealed class AssistantBuilderPluginMetadata
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string[] Categories { get; init; } = [];
}

internal sealed class AssistantBuilderAssistantMetadata
{
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? SystemPrompt { get; init; }
    public string? SubmitText { get; init; }
    public bool? AllowAiStudioProfiles { get; init; }
    public AssistantBuilderChatLaunchMetadata? Launch { get; init; }
}

internal sealed class AssistantBuilderChatLaunchMetadata
{
    public string WorkspaceName { get; init; } = string.Empty;
    public string? ProviderId { get; init; }
    public string? ProfileId { get; init; }
    public string? ChatTemplateId { get; init; }
    public string[]? DataSourceIds { get; init; }
}
