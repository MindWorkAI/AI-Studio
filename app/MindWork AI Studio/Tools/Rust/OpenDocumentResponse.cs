namespace AIStudio.Tools.Rust;

/// <summary>
/// Says how opening a document went.
/// </summary>
/// <param name="Success">Whether the document was opened at all.</param>
/// <param name="PageApplied">
/// Whether the document was handed to its program together with the page. False means it opens on
/// its first page: no page was asked for, the system uses a program which cannot be told one, or
/// starting that program failed. None of these is an error, so this belongs in the log rather than
/// in front of the user, who is told the page by the source itself.
/// </param>
/// <param name="Issue">Why the document could not be opened, or an empty text when it was.</param>
public readonly record struct OpenDocumentResponse(bool Success, bool PageApplied, string Issue);