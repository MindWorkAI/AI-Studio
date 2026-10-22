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
    /// be, since an HTML parser would read every angle bracket in it as markup.
    /// </remarks>
    public required string Body { get; init; }
}