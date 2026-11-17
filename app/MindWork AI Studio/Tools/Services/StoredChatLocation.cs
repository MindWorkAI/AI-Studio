using AIStudio.Assistants.BeyondFactsLab;

namespace AIStudio.Tools.Services;

/// <summary>
/// Where the cleanup of the provenance store at the start finds a chat: in a workspace, among the temporary chats,
/// or in the BeyondFacts Lab.
/// </summary>
/// <remarks>
/// Create it with InWorkspace or InLab.
/// </remarks>
/// <param name="WorkspaceId">The workspace of a chat outside the lab, Guid.Empty for a temporary chat.</param>
/// <param name="LabThread">Where a chat of the lab belongs, or null for any other chat.</param>
public readonly record struct StoredChatLocation(Guid WorkspaceId, BflThreadLocation? LabThread)
{
    /// <summary>
    /// Gets the place of a chat in a workspace or among the temporary chats.
    /// </summary>
    /// <param name="workspaceId">The workspace, Guid.Empty for a temporary chat.</param>
    public static StoredChatLocation InWorkspace(Guid workspaceId) => new(workspaceId, null);

    /// <summary>
    /// Gets the place of a chat of the BeyondFacts Lab.
    /// </summary>
    /// <param name="thread">Where the chat belongs in the lab.</param>
    public static StoredChatLocation InLab(BflThreadLocation thread) => new(Guid.Empty, thread);
}