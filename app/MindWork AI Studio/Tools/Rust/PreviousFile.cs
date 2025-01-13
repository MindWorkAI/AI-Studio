namespace AIStudio.Tools.Rust;

/// <summary>
/// Data structure for selecting a file when a previous file was selected.
/// </summary>
/// <param name="FilePath">The path of the previous file.</param>
public readonly record struct PreviousFile(string FilePath);