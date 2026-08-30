namespace AIStudio.Assistants.Builder;

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