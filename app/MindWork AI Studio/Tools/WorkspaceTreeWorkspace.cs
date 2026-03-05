namespace AIStudio.Tools;

public readonly record struct WorkspaceTreeWorkspace(Guid WorkspaceId, string WorkspacePath, string Name, bool ChatsLoaded, IReadOnlyList<WorkspaceTreeChat> Chats);