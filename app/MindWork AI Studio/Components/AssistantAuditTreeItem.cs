namespace AIStudio.Components;

public sealed class AssistantAuditTreeItem : ITreeItem
{
    public string Text { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Caption { get; init; } = string.Empty;
    public bool Expandable { get; init; }
    public bool IsComponent { get; init; } = true;
}
