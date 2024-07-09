namespace AIStudio.Components.Blocks;

public readonly record struct TreeButton<T>(WorkspaceBranch Branch, int Depth, string Text, string Icon) : ITreeItem<T>;