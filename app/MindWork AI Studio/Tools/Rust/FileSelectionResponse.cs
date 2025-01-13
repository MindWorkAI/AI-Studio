namespace AIStudio.Tools.Rust;

/// <summary>
/// Data structure for selecting a file.
/// </summary>
/// <param name="UserCancelled">Was the file selection canceled?</param>
/// <param name="SelectedFilePath">The selected file, if any.</param>
public readonly record struct FileSelectionResponse(bool UserCancelled, string SelectedFilePath);