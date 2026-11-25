namespace AIStudio.Chat.Compression;

/// <summary>
/// Where the conversation goes on once a chat is compressed.
/// </summary>
public enum ChatCompressionTarget
{
    /// <summary>
    /// In a new chat of the workspace, while the compressed chat stays as it was, see ChatComponent.
    /// </summary>
    NEW_CHAT,

    /// <summary>
    /// In the same chat, whose earlier messages give way to the summary, e.g., the embedded chat of an assistant,
    /// which has no workspace to keep a copy in, see EmbeddedChat.
    /// </summary>
    SAME_CHAT,
}