namespace AIStudio.Components;

public readonly record struct TreeButton(WorkspaceBranch Branch, int Depth, string Text, string Icon, Func<Task> Action) : ITreeItem;