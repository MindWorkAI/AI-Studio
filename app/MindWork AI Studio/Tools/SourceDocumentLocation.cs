namespace AIStudio.Tools;

/// <summary>
/// Where a source points in the file system, and where inside the document it was found.
/// </summary>
/// <param name="Path">The document in the file system, spelled the way this system spells a path.</param>
/// <param name="PageNumber">The page the passage stands on, counted from one, or null when no page is known.</param>
public readonly record struct SourceDocumentLocation(string Path, int? PageNumber);