namespace SharedTools;

/// <summary>
/// Computes the hash which turns a text of T() or TB() into its I18N key.
/// </summary>
/// <remarks>
/// The I18N collector and the app both use it: the collector for the text it reads from the source, the app for the
/// string it gets at runtime. A multi-line text holds the line endings of the checkout it was compiled from, so the
/// hash makes them equal first; otherwise, a build from a Windows checkout would find no translation for such a
/// text. Texts without line breaks keep the hash they always had.
/// </remarks>
public static class I18NKeyHash
{
    /// <summary>
    /// Computes the hash of a text.
    /// </summary>
    /// <param name="text">The text, as T() or TB() gets it.</param>
    /// <returns>The 32-bit FNV-1a hash of the text with LF line endings.</returns>
    public static uint Of(string text) => NormalizeLineEndings(text).ToFNV32();

    /// <summary>
    /// Turns CRLF and CR line endings into LF.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with LF line endings, or the text itself when it holds no CR.</returns>
    public static string NormalizeLineEndings(string text) => text.Contains('\r') ? text.Replace("\r\n", "\n").Replace('\r', '\n') : text;
}