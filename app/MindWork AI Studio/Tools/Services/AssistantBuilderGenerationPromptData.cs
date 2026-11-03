namespace AIStudio.Tools.Services;

/// <summary>
/// The approved draft and the review notes, as the prompt for the Lua code of a form assistant hands them to the model.
/// </summary>
/// <param name="ApprovedAssistantDraft">The approved draft.</param>
/// <param name="ReviewNotes">The review notes, or null when the user left them empty.</param>
internal sealed record AssistantBuilderGenerationPromptData(string ApprovedAssistantDraft, string? ReviewNotes);