namespace AIStudio.Tools.Services;

/// <summary>
/// The approved draft, the review notes, and the chosen chat, as the prompt for the texts of a direct chat launcher
/// hands them to the model.
/// </summary>
/// <param name="ApprovedAssistantDraft">The approved draft.</param>
/// <param name="ReviewNotes">The review notes, or null when the user left them empty.</param>
/// <param name="ChatLaunch">The chat the launcher opens.</param>
internal sealed record AssistantBuilderLauncherPromptData(string ApprovedAssistantDraft, string? ReviewNotes, AssistantBuilderChatLaunchRequest ChatLaunch);