// ReSharper disable NotAccessedPositionalProperty.Global
namespace AIStudio.Tools;

public readonly record struct WorkspaceTreeChat(Guid WorkspaceId, Guid ChatId, string ChatPath, string Name, DateTimeOffset LastEditTime, bool IsTemporary);