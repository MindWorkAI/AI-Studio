namespace AIStudio.Tools;

public sealed class HTMLParserWebPage
{
    public required Uri RequestedUrl { get; init; }

    public required Uri FinalUrl { get; init; }

    public required string ContentType { get; init; }

    /// <summary>
    /// The response body as text, as the server sent it.
    /// </summary>
    /// <remarks>
    /// Kept as text rather than parsed, because what it is depends on the content type: an HTML
    /// page is parsed by the retrieval service, while a JSON or plain text document must never
    /// be, since an HTML parser would read every angle bracket in it as markup.<br/><br/>
    /// Empty for a document, whose body went into DocumentPath instead.
    /// </remarks>
    public required string Body { get; init; }

    /// <summary>
    /// The file the body of a document was downloaded to, or null for a page or a text document.
    /// </summary>
    public string? DocumentPath { get; init; }

    /// <summary>
    /// The file name the server gave the body in its Content-Disposition header, if any.
    /// </summary>
    public string? FileName { get; init; }
}