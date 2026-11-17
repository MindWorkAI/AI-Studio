namespace AIStudio.Assistants.Builder;

internal sealed class AssistantBuilderAssistantMetadata
{
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? SystemPrompt { get; set; }
    public string? SubmitText { get; set; }
    public bool? AllowAiStudioProfiles { get; set; }
    public string[]? ToolIds { get; set; }
    public AssistantBuilderChatLaunchMetadata? Launch { get; set; }
}