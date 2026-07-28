using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tools.Media;

/// <summary>Identifies the chat or assistant that owns a media import.</summary>
public readonly record struct MediaImportOwner(MediaImportOwnerKind Kind, string Id)
{
    public static MediaImportOwner ForChat(Guid chatId) => new(MediaImportOwnerKind.CHAT, chatId.ToString("N"));

    public static MediaImportOwner ForAssistant(AssistantSessionKey key) => new(MediaImportOwnerKind.ASSISTANT, key.ToString());

    /// <summary>
    /// Creates a persistent media-import owner for a visual briefing.
    /// </summary>
    /// <param name="briefingId">The stable briefing identifier.</param>
    /// <returns>The media-import owner.</returns>
    public static MediaImportOwner ForVisualBriefing(Guid briefingId) => new(MediaImportOwnerKind.VISUAL_BRIEFING, briefingId.ToString("D"));
}