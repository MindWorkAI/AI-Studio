// ReSharper disable NotAccessedPositionalProperty.Global
namespace AIStudio.Tools;

/// <summary>
/// A chat as the list of workspaces and chats shows it.
/// </summary>
/// <param name="WorkspaceId">The workspace of the chat, or Guid.Empty for a chat outside of workspaces.</param>
/// <param name="ChatId">The chat.</param>
/// <param name="ChatPath">The directory of the chat, or empty while it is not stored.</param>
/// <param name="Name">The name of the chat.</param>
/// <param name="LastEditTime">When the chat was written last.</param>
/// <param name="IsTemporary">Whether the chat lies outside of workspaces.</param>
/// <param name="IsNewChat">Whether it is the new chat of its place, see ChatThread.IsNewChat.</param>
public readonly record struct WorkspaceTreeChat(Guid WorkspaceId, Guid ChatId, string ChatPath, string Name, DateTimeOffset LastEditTime, bool IsTemporary, bool IsNewChat = false)
{
    /// <summary>
    /// The chats of a place in the order the list shows them: the new chat first, then the most recently changed.
    /// </summary>
    /// <param name="chats">The chats of one place.</param>
    /// <returns>The chats in that order.</returns>
    public static List<WorkspaceTreeChat> InListOrder(IEnumerable<WorkspaceTreeChat> chats) => chats
        .OrderByDescending(chat => chat.IsNewChat)
        .ThenByDescending(chat => chat.LastEditTime)
        .ToList();
}