namespace AIStudio.Tools;

public readonly record struct WorkspaceSearchResult(WorkspaceTreeChat Chat, bool NameMatched, bool ThreadMatched);