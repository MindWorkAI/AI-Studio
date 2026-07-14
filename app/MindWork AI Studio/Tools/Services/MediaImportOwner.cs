using AIStudio.Tools.AssistantSessions;

namespace AIStudio.Tools.Services;

/// <summary>Identifies the chat or assistant that owns a media import.</summary>
public readonly record struct MediaImportOwner(MediaImportOwnerKind Kind, string Id)
{
    public static MediaImportOwner ForChat(Guid chatId) => new(MediaImportOwnerKind.CHAT, chatId.ToString("N"));

    public static MediaImportOwner ForAssistant(AssistantSessionKey key) => new(MediaImportOwnerKind.ASSISTANT, key.ToString());
}