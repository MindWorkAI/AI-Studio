namespace AIStudio.Tools;

/// <summary>
/// A workspace among the results of a search.
/// </summary>
/// <param name="WorkspaceId">The ID of the workspace.</param>
/// <param name="WorkspacePath">The folder of the workspace.</param>
/// <param name="Name">The name of the workspace.</param>
/// <param name="Chats">The chats of the workspace which the search found.</param>
/// <param name="NameMatched">Whether its name holds all words. Its chats count only by what they hold themselves.</param>
public readonly record struct WorkspaceSearchWorkspace(Guid WorkspaceId, string WorkspacePath, string Name, IReadOnlyList<WorkspaceSearchResult> Chats, bool NameMatched = false);