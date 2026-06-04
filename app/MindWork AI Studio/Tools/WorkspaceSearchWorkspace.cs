namespace AIStudio.Tools;

public readonly record struct WorkspaceSearchWorkspace(Guid WorkspaceId, string WorkspacePath, string Name, IReadOnlyList<WorkspaceSearchResult> Chats);