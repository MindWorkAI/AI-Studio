namespace AIStudio.Tools.Web;

/// <summary>
/// Reads a text document fetched from the web, such as plain text, JSON, XML, or CSV.
/// </summary>
/// <remarks>
/// The text is returned as the server sent it. There is no main part to extract from a JSON
/// response, and converting it to Markdown would only take away what the model needs: exact
/// keys, quotes, and indentation. Only the line endings are unified, and the byte order mark
/// is dropped, because it is not part of the text.<br/><br/>
/// The text is not filtered for prompt injections here, just like an extracted HTML page is
/// not. The callers filter it once they have cut it down to what reaches the model. The
/// runtime's filter also decodes the escapes of JSON and XML, which a model reads fluently.
/// </remarks>
internal static class WebTextContentExtractor
{
    private const char BYTE_ORDER_MARK = '﻿';

    /// <summary>
    /// Takes the text of a document. Throws an InvalidOperationException when the body is not
    /// text, whatever the server declared.
    /// </summary>
    /// <param name="body">The response body as text.</param>
    /// <param name="mediaType">The media type the server declared, for the error message.</param>
    /// <param name="finalUrl">Where the document was found after every redirect, for the error message.</param>
    /// <returns>The document, with its text as the content and no metadata.</returns>
    public static ExtractedWebPage Extract(string body, string mediaType, Uri finalUrl)
    {
        //
        // Text holds no NUL characters, while nearly every binary format does. A server naming
        // a PDF or an archive text/plain is common enough, and its bytes decoded as text would
        // reach the model as noise.
        //
        if (body.Contains('\0'))
            throw new InvalidOperationException($"The response of '{finalUrl}' is declared as '{mediaType}' but does not contain readable text.");

        var text = body
            .TrimStart(BYTE_ORDER_MARK)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd();

        // Only blank lines are dropped at the start. The indentation of the first line carries
        // meaning in YAML or in source code:
        text = TrimLeadingBlankLines(text);

        return new ExtractedWebPage
        {
            Title = string.Empty,
            Description = string.Empty,
            Authors = [],
            PublishedTime = string.Empty,
            ModifiedTime = string.Empty,
            Language = string.Empty,
            SiteName = string.Empty,
            CanonicalUrl = null,
            Markdown = text,
            Outline = [],
        };
    }

    private static string TrimLeadingBlankLines(string text)
    {
        var start = 0;
        while (true)
        {
            var lineEnd = text.IndexOf('\n', start);
            if (lineEnd < 0 || !string.IsNullOrWhiteSpace(text[start..lineEnd]))
                return text[start..];

            start = lineEnd + 1;
        }
    }
}