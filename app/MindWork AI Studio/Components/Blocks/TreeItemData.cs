namespace AIStudio.Components.Blocks;

public class TreeItemData : ITreeItem
{
    public WorkspaceBranch Branch { get; init; } = WorkspaceBranch.NONE;
    
    public int Depth { get; init; }
    
    public string Text { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;

    public bool IsChat { get; init; }

    public string Path { get; init; } = string.Empty;

    public bool Expandable { get; init; } = true;

    public HashSet<ITreeItem> Children { get; init; } = [];
}