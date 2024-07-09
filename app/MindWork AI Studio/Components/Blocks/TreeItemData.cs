namespace AIStudio.Components.Blocks;

public class TreeItemData<T> : ITreeItem<T>
{
    public WorkspaceBranch Branch { get; init; } = WorkspaceBranch.NONE;
    
    public int Depth { get; init; }
    
    public string Text { get; init; } = string.Empty;

    public string Icon { get; init; } = string.Empty;

    public T? Value { get; init; }

    public bool Expandable { get; init; } = true;

    public HashSet<ITreeItem<T>> Children { get; } = [];
}