namespace AIStudio.Assistants.Builder;

internal sealed class AssistantBuilderChatLaunchMetadata
{
    public string WorkspaceName { get; init; } = string.Empty;
    public string? ProviderId { get; init; }
    public string? ProfileId { get; init; }
    public string? ChatTemplateId { get; init; }
    public string[]? DataSourceIds { get; init; }
}