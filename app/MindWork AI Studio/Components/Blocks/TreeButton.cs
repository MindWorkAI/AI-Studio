namespace AIStudio.Components.Blocks;

public readonly record struct TreeButton(WorkspaceBranch Branch, int Depth, string Text, string Icon) : ITreeItem;