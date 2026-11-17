namespace AIStudio.Assistants.Builder;

internal sealed class AssistantBuilderChatLaunchMetadata
{
    /// <summary>
    /// The workspace the chat is created in. A model leaves this field out for a launcher that
    /// opens a chat without a workspace, which is why it stays empty rather than null.
    /// </summary>
    public string WorkspaceName { get; set; } = string.Empty;
    public string? ProviderId { get; set; }
    public string? ProfileId { get; set; }
    public string? ChatTemplateId { get; set; }
    public string[]? DataSourceIds { get; set; }
    public string[]? ToolIds { get; set; }
}