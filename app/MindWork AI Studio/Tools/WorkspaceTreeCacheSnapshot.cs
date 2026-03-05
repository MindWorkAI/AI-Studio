namespace AIStudio.Tools;

public readonly record struct WorkspaceTreeCacheSnapshot(IReadOnlyList<WorkspaceTreeWorkspace> Workspaces, IReadOnlyList<WorkspaceTreeChat> TemporaryChats);