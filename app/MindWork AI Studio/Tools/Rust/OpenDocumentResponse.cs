namespace AIStudio.Tools.Rust;

/// <summary>
/// Says how opening a document went.
/// </summary>
/// <param name="Success">Whether the document was opened at all.</param>
/// <param name="PageOutcome">
/// What became of the page the document was to be shown at. A page which did not arrive is no error,
/// since the document opens either way, but the user hears why, see SourceDocumentLocationExtensions.
/// </param>
/// <param name="Issue">Why the document could not be opened, or an empty text when it was.</param>
public readonly record struct OpenDocumentResponse(bool Success, DocumentPageOutcome PageOutcome, string Issue);