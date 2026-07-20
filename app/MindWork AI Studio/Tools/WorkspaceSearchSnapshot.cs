namespace AIStudio.Tools;

public readonly record struct WorkspaceSearchSnapshot(IReadOnlyList<WorkspaceSearchWorkspace> Workspaces, IReadOnlyList<WorkspaceSearchResult> TemporaryChats);