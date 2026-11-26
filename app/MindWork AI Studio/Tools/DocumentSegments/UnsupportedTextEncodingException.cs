namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// A document which is text itself, e.g., a web page, is in an encoding whose new text the format cannot write
/// without damaging characters.
/// </summary>
/// <remarks>
/// The format writes new words in the encoding of the source, which is UTF-8 unless a byte order mark announces
/// another Unicode encoding. A source in another encoding, e.g., Windows-1252, would be damaged twice: its own
/// characters beyond ASCII cannot be read, and the new words would come out in bytes which its readers take for
/// other characters. So the format refuses such a document instead of working on it.
/// </remarks>
public sealed class UnsupportedTextEncodingException : Exception
{
    /// <summary>
    /// Creates the exception for a document whose bytes are no valid UTF-8.
    /// </summary>
    /// <param name="path">The document.</param>
    /// <param name="innerException">Why the bytes could not be read.</param>
    public UnsupportedTextEncodingException(string path, Exception innerException) : base($"The document '{path}' is not valid UTF-8.", innerException)
    {
        this.DeclaredEncoding = string.Empty;
    }

    /// <summary>
    /// Creates the exception for a document which names another encoding than UTF-8 for itself.
    /// </summary>
    /// <param name="path">The document.</param>
    /// <param name="declaredEncoding">The encoding the document names, e.g., "iso-8859-1".</param>
    public UnsupportedTextEncodingException(string path, string declaredEncoding) : base($"The document '{path}' declares the encoding '{declaredEncoding}'.")
    {
        this.DeclaredEncoding = declaredEncoding;
    }

    /// <summary>
    /// The encoding the document names for itself, e.g., the charset of a web page; empty when its bytes are no valid UTF-8.
    /// </summary>
    public string DeclaredEncoding { get; }
}