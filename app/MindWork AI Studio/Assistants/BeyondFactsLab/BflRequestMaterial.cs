using AIStudio.Chat;

namespace AIStudio.Assistants.BeyondFactsLab;

/// <summary>
/// What a request of a chat of the lab carries beside the conversation.
/// </summary>
/// <param name="SystemPrompt">The system prompt, with the text of the material.</param>
/// <param name="Images">The images of the material which go along with the first message of the conversation.</param>
public sealed record BflRequestMaterial(string SystemPrompt, IReadOnlyList<FileAttachmentImage> Images);