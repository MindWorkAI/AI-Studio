using System.Text;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// The text of a document which is text itself, e.g., Markdown, LaTeX, or HTML, together with how it is encoded.
/// </summary>
/// <remarks>
/// The new document keeps the encoding of the source, its byte order mark included, so that the programs which
/// read the source read the new document alike. Without a byte order mark, the text counts as UTF-8.
/// </remarks>
/// <param name="Text">The text of the document.</param>
/// <param name="Encoding">The encoding of the document, whose preamble is its byte order mark, if any.</param>
public sealed record TextDocumentFile(string Text, Encoding Encoding)
{
    /// <summary>
    /// Reads a document.
    /// </summary>
    /// <param name="path">The document.</param>
    /// <param name="token">Cancels the reading.</param>
    public static async Task<TextDocumentFile> ReadAsync(string path, CancellationToken token)
    {
        var bytes = await File.ReadAllBytesAsync(path, token);
        var encoding = EncodingOf(bytes);
        var preambleLength = encoding.GetPreamble().Length;
        return new(encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength), encoding);
    }

    /// <summary>
    /// Writes a text with the encoding of this document.
    /// </summary>
    /// <param name="path">The new document, which the user chose.</param>
    /// <param name="text">The text of the new document.</param>
    /// <param name="token">Cancels the writing.</param>
    public Task WriteAsync(string path, string text, CancellationToken token) => File.WriteAllTextAsync(path, text, this.Encoding, token);

    private static Encoding EncodingOf(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xEF, 0xBB, 0xBF, ..] => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        [0xFF, 0xFE, ..] => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
        [0xFE, 0xFF, ..] => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),

        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };
}