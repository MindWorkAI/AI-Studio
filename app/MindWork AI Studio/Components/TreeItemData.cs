namespace AIStudio.Components;

public class TreeItemData : ITreeItem
{
    public WorkspaceBranch Branch { get; init; } = WorkspaceBranch.NONE;
    
    public int Depth { get; init; }
    
    public string Text { get; init; } = string.Empty;
    
    public string ShortenedText => this.Text.Length > 30 ? this.Text[..30] + "..." : this.Text;

    public string Icon { get; init; } = string.Empty;

    public TreeItemType Type { get; init; }

    public string Path { get; init; } = string.Empty;

    public bool Expandable { get; init; } = true;

    public DateTimeOffset LastEditTime { get; init; }

    public IReadOnlyCollection<TreeItemData<ITreeItem>> Children { get; init; } = [];
}