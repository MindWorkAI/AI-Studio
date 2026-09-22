namespace AIStudio.Assistants.Builder;

internal sealed class AssistantBuilderChatLaunchMetadata
{
    /// <summary>
    /// The workspace the chat is created in. A model leaves this field out for a launcher that
    /// opens a chat without a workspace, which is why it stays empty rather than null.
    /// </summary>
    public string WorkspaceName { get; init; } = string.Empty;
    public string? ProviderId { get; init; }
    public string? ProfileId { get; init; }
    public string? ChatTemplateId { get; init; }
    public string[]? DataSourceIds { get; init; }
    public string[]? ToolIds { get; init; }
}