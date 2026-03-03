namespace AIStudio.Tools.Rust;

/// <summary>
/// Data structure for selecting multiple files.
/// </summary>
/// <param name="UserCancelled">Was the file selection canceled?</param>
/// <param name="SelectedFilePaths">The selected files, if any.</param>
public readonly record struct FilesSelectionResponse(bool UserCancelled, IReadOnlyList<string> SelectedFilePaths);
