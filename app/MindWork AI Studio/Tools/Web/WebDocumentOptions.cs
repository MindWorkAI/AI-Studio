namespace AIStudio.Tools.Web;

/// <summary>
/// How a request reads a document, such as a PDF, which an address returns instead of a page.
/// </summary>
public sealed class WebDocumentOptions
{
    /// <summary>
    /// Decides whether the document may be read. Gets the address which returned it, after every
    /// redirect, and whether that address is a non-public one, see RetrievedWebPage.ReachedNonPublicAddress.
    /// </summary>
    /// <remarks>
    /// Asked once the headers have arrived and before the body is downloaded, so a refused document
    /// costs nothing but its headers.
    /// </remarks>
    public required Func<Uri, bool, bool> IsAllowed { get; init; }

    /// <summary>
    /// The largest document which is downloaded.
    /// </summary>
    public required long MaxBytes { get; init; }

    /// <summary>
    /// How many characters of its text are kept at most, see FileExtractionResult.IsCutAtCharacterLimit.
    /// </summary>
    public required int MaxCharacters { get; init; }
}