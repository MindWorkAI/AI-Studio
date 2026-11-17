namespace AIStudio.Tools.Services;

/// <summary>
/// The fields of the Builder form, as the prompt for an assistant draft hands them to the model.
/// </summary>
/// <param name="AssistantDescription">What the assistant should do.</param>
/// <param name="Category">The category, or null when the user left it empty.</param>
/// <param name="AssistantTitle">The title, or null when the user left it empty.</param>
/// <param name="TypicalInput">The typical input, or null when the user left it empty.</param>
/// <param name="ExpectedOutput">The expected output, or null when the user left it empty.</param>
/// <param name="RequestedUiInputComponents">The requested input components, or null when the user left them empty.</param>
/// <param name="OutputLanguage">The output language, or null when the user left it empty.</param>
/// <param name="AllowAiStudioProfiles">Whether the assistant offers the profiles of AI Studio.</param>
/// <param name="ExtraRules">The extra rules, or null when the user left them empty.</param>
/// <param name="ExampleRequest">The example request, or null when the user left it empty.</param>
/// <param name="ChatLaunch">The chat a direct chat launcher opens, or null for a form assistant.</param>
internal sealed record AssistantBuilderDraftPromptData(
    string AssistantDescription,
    string? Category,
    string? AssistantTitle,
    string? TypicalInput,
    string? ExpectedOutput,
    string? RequestedUiInputComponents,
    string? OutputLanguage,
    bool AllowAiStudioProfiles,
    string? ExtraRules,
    string? ExampleRequest,
    AssistantBuilderChatLaunchRequest? ChatLaunch);