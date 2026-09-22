namespace AIStudio.Tools.ToolCallingSystem;

public sealed class ToolSelectionState
{
    public HashSet<string> SelectedToolIds { get; init; } = [];
}