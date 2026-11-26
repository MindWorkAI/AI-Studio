using System.Text;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// The text of a document which is text itself, e.g., Markdown, LaTeX, or HTML, together with how it is encoded.
/// </summary>
/// <remarks>
/// <para>
/// The new document keeps the encoding of the source, its byte order mark included, so that the programs which
/// read the source read the new document alike. Without a byte order mark, the text counts as UTF-8.
/// </para>
/// <para>
/// Reading is strict: a document whose bytes do not fit its encoding, e.g., a page in Windows-1252, or which
/// names another encoding than UTF-8 for itself, e.g., by the charset of a web page, is refused with an
/// UnsupportedTextEncodingException. Decoding it anyway would replace its characters beyond ASCII with a
/// placeholder everywhere in the file, also outside of the segments, and the new words would come out in bytes
/// which its readers take for other characters.
/// </para>
/// </remarks>
/// <param name="Text">The text of the document.</param>
/// <param name="Encoding">The encoding of the document, whose preamble is its byte order mark, if any.</param>
public sealed record TextDocumentFile(string Text, Encoding Encoding)
{
    /// <summary>
    /// Reads a document.
    /// </summary>
    /// <param name="path">The document.</param>
    /// <param name="declaredEncodingOf">Finds the encoding which a text names for itself, e.g., the charset of a web page; null for a format without such a name. A byte order mark takes precedence over it, as it does for browsers.</param>
    /// <param name="token">Cancels the reading.</param>
    /// <exception cref="UnsupportedTextEncodingException">The document is not valid in its encoding, or names another encoding than UTF-8.</exception>
    public static async Task<TextDocumentFile> ReadAsync(string path, Func<string, string?>? declaredEncodingOf, CancellationToken token)
    {
        var bytes = await File.ReadAllBytesAsync(path, token);
        var hasByteOrderMark = ByteOrderMark.TryFind(bytes, out var markedEncoding, out var markLength);
        var encoding = markedEncoding ?? ByteOrderMark.STRICT_UTF8_WITHOUT_MARK;

        string text;
        try
        {
            text = encoding.GetString(bytes, markLength, bytes.Length - markLength);
        }
        catch (DecoderFallbackException e)
        {
            throw new UnsupportedTextEncodingException(path, e);
        }

        if (!hasByteOrderMark && declaredEncodingOf?.Invoke(text) is { } declaredEncoding && !IsUtf8(declaredEncoding))
            throw new UnsupportedTextEncodingException(path, declaredEncoding.Trim());

        return new(text, encoding);
    }

    /// <summary>
    /// Writes a text with the encoding of this document.
    /// </summary>
    /// <remarks>
    /// The encoding throws on a character it cannot write, e.g., half of a surrogate pair, rather than writing a
    /// placeholder.
    /// </remarks>
    /// <param name="path">The new document, which the user chose.</param>
    /// <param name="text">The text of the new document.</param>
    /// <param name="token">Cancels the writing.</param>
    public Task WriteAsync(string path, string text, CancellationToken token) => File.WriteAllTextAsync(path, text, this.Encoding, token);

    /// <summary>
    /// Whether the name of an encoding stands for UTF-8, e.g., "UTF-8" for a web page or "utf8" for LaTeX.
    /// </summary>
    /// <param name="name">The name, as a document writes it.</param>
    public static bool IsUtf8(string name)
    {
        var letters = new string(name.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return letters is "utf8" or "utf8x" or "unicode11utf8" or "xunicode20utf8";
    }
}