namespace AIStudio.Tools;

/// <summary>
/// A list of excluded files and folders, split into the entries for folders and those for files.
/// </summary>
/// <param name="Folders">The entries which name folders, e.g., bin/, joined into one list.</param>
/// <param name="Files">The entries which name files, e.g., *.min.js, joined into one list.</param>
internal readonly record struct FileExclusionGroups(string Folders, string Files);