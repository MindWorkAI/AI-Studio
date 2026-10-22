namespace AIStudio.Tools.Rust;

/// <summary>
/// Asks the runtime to open a document in the program the system uses for it.
/// </summary>
/// <param name="Path">The document to open.</param>
/// <param name="Page">The page to show, counted from one, or null when the document has none.</param>
public readonly record struct OpenDocumentRequest(string Path, int? Page);