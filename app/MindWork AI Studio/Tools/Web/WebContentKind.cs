namespace AIStudio.Tools.Web;

/// <summary>
/// What a retrieved web resource turned out to be, which decides how its content was read.
/// </summary>
public enum WebContentKind
{
    /// <summary>
    /// An HTML page. Its readable part was extracted and converted to Markdown, so the content
    /// can be shorter than the page when the extraction missed something.
    /// </summary>
    HTML_PAGE,

    /// <summary>
    /// A text document such as plain text, JSON, XML, or CSV. The extracted Markdown is its text
    /// as the server sent it, so nothing of it was left out.
    /// </summary>
    TEXT_DOCUMENT,
}