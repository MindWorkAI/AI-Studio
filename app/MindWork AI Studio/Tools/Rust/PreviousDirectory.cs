namespace AIStudio.Tools.Rust;

/// <summary>
/// Data structure for selecting a directory when a previous directory was selected.
/// </summary>
/// <param name="Path">The path of the previous directory.</param>
public readonly record struct PreviousDirectory(string Path);