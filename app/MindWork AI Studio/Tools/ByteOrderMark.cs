using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace AIStudio.Tools;

/// <summary>
/// Finds the encoding which the byte order mark at the beginning of a text announces.
/// </summary>
/// <remarks>
/// Every encoding found here throws on bytes or characters it cannot handle, instead of replacing them with a
/// placeholder: a text read with a placeholder would be written back with it, and the original characters would be
/// lost. UTF-32 is checked before UTF-16, because the mark of UTF-32 in little endian starts with the one of UTF-16.
/// </remarks>
public static class ByteOrderMark
{
    private static readonly byte[] UTF8_MARK = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] UTF32_LE_MARK = [0xFF, 0xFE, 0x00, 0x00];
    private static readonly byte[] UTF32_BE_MARK = [0x00, 0x00, 0xFE, 0xFF];
    private static readonly byte[] UTF16_LE_MARK = [0xFF, 0xFE];
    private static readonly byte[] UTF16_BE_MARK = [0xFE, 0xFF];

    /// <summary>
    /// UTF-8 without a byte order mark, which throws on invalid bytes.
    /// </summary>
    public static readonly Encoding STRICT_UTF8_WITHOUT_MARK = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Finds the encoding which the byte order mark at the beginning of the bytes announces.
    /// </summary>
    /// <param name="bytes">The beginning of the text.</param>
    /// <param name="encoding">The encoding, which throws on invalid bytes, and whose preamble is the mark; null without a mark.</param>
    /// <param name="markLength">How many bytes the mark takes; 0 without a mark.</param>
    /// <returns>Whether the bytes start with a byte order mark.</returns>
    public static bool TryFind(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out Encoding? encoding, out int markLength)
    {
        if (bytes.StartsWith(UTF8_MARK))
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);
        else if (bytes.StartsWith(UTF32_LE_MARK))
            encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true, throwOnInvalidCharacters: true);
        else if (bytes.StartsWith(UTF32_BE_MARK))
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true, throwOnInvalidCharacters: true);
        else if (bytes.StartsWith(UTF16_LE_MARK))
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true);
        else if (bytes.StartsWith(UTF16_BE_MARK))
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true, throwOnInvalidBytes: true);
        else
            encoding = null;

        markLength = encoding?.Preamble.Length ?? 0;
        return encoding is not null;
    }
}