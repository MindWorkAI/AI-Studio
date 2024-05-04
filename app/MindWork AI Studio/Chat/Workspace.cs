namespace AIStudio.Chat;

/// <summary>
/// Data about a workspace.
/// </summary>
/// <param name="name">The name of the workspace.</param>
public sealed class Workspace(string name)
{
    public string Name { get; set; } = name;

    public List<ChatThread> Threads { get; set; } = new();
}