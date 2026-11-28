using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tools.Imports;

/// <summary>Identifies the chat, assistant, or visual briefing that owns an import, e.g., the transcription of a media file.</summary>
public readonly record struct ImportOwner(ImportOwnerKind Kind, string Id)
{
    public static ImportOwner ForChat(Guid chatId) => new(ImportOwnerKind.CHAT, chatId.ToString("N"));

    public static ImportOwner ForAssistant(AssistantSessionKey key) => new(ImportOwnerKind.ASSISTANT, key.ToString());

    /// <summary>
    /// Creates a persistent import owner for a visual briefing.
    /// </summary>
    /// <param name="briefingId">The stable briefing identifier.</param>
    /// <returns>The import owner.</returns>
    public static ImportOwner ForVisualBriefing(Guid briefingId) => new(ImportOwnerKind.VISUAL_BRIEFING, briefingId.ToString("D"));
}