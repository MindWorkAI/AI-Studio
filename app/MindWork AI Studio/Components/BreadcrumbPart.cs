namespace AIStudio.Components;

/// <summary>
/// A part of the path above the open chat, which shows that part in the list once the user clicks it.
/// </summary>
public enum BreadcrumbPart
{
    /// <summary>
    /// Where the chat is: its workspace, or the chats outside of workspaces.
    /// </summary>
    PLACE,

    /// <summary>
    /// The chat itself.
    /// </summary>
    CHAT,
}