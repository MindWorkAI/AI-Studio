namespace AIStudio.Tools.Services;

public sealed record AssistantPluginDraftGenerationRequest(
    string AssistantDescription,
    string Category,
    string AssistantTitle,
    string TypicalInput,
    string ExpectedOutput,
    string RequestedUiInputComponents,
    string OutputLanguage,
    bool AllowAiStudioProfiles,
    string ExtraRules,
    string ExampleRequest,
    AssistantBuilderChatLaunchRequest? ChatLaunch);