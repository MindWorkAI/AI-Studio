namespace AIStudio.Tools.Rust;

/// <summary>
/// Data structure for selecting a directory.
/// </summary>
/// <param name="UserCancelled">Was the directory selection canceled?</param>
/// <param name="SelectedDirectory">The selected directory, if any.</param>
public readonly record struct DirectorySelectionResponse(bool UserCancelled, string SelectedDirectory);