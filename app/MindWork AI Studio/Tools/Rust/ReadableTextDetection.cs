using System.Text;

namespace AIStudio.Tools.Rust;

/// <summary>
/// Tells from the beginning of a file whether it holds text which the runtime's text reader can read.
/// </summary>
/// <remarks>
/// FileTypes asks it for READABLE_TEXT, i.e., for text whose file name or extension is not known in
/// advance, such as LICENSE, .gitignore, or main.tf. That applies across the app: chat attachments,
/// data sources, and the selectors of file patterns. Only the first PROBE_SIZE bytes are read, so a
/// file whose text ends there passes as well. The runtime's text reader has the final say when it
/// extracts the text.
/// </remarks>
internal static class ReadableTextDetection
{
    /// <summary>
    /// How many bytes at the beginning of a file are read at most.
    /// </summary>
    public const int PROBE_SIZE = 8_192;

    /// <summary>
    /// Text holds at most one control character in this many characters, e.g., an escape sequence
    /// in a log. Binary data holds far more.
    /// </summary>
    private const int MIN_CHARACTERS_PER_CONTROL_CHARACTER = 100;

    private const byte DELETE_BYTE = 0x7F;

    private static readonly byte[] UTF8_BOM = [0xEF, 0xBB, 0xBF];
    private static readonly byte[] UTF16_BE_BOM = [0xFE, 0xFF];
    private static readonly byte[] UTF16_LE_BOM = [0xFF, 0xFE];

    /// <summary>
    /// Determines from the first PROBE_SIZE bytes whether a file uses a recognizable text encoding
    /// and does not resemble binary data.
    /// </summary>
    /// <param name="filePath">The path of the file.</param>
    /// <returns>True, when the file looks like readable text. False, when it is empty, binary, or cannot be read.</returns>
    public static bool IsReadableTextFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                PROBE_SIZE,
                FileOptions.SequentialScan);

            var buffer = new byte[PROBE_SIZE];
            var length = 0;
            while (length < buffer.Length)
            {
                var read = stream.Read(buffer, length, buffer.Length - length);
                if (read == 0)
                    break;

                length += read;
            }

            if (length == 0)
                return false;

            return IsReadableText(buffer.AsSpan(0, length));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static bool IsReadableText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(UTF8_BOM))
            return IsReadableText(bytes[UTF8_BOM.Length..], new UTF8Encoding(true, true));

        if (bytes.StartsWith(UTF16_BE_BOM))
            return IsReadableText(bytes[UTF16_BE_BOM.Length..], new UnicodeEncoding(true, true, true));

        if (bytes.StartsWith(UTF16_LE_BOM))
            return IsReadableText(bytes[UTF16_LE_BOM.Length..], new UnicodeEncoding(false, true, true));

        if (bytes.Contains((byte)0))
            return false;

        try
        {
            return IsReadableText(bytes, new UTF8Encoding(false, true));
        }
        catch (DecoderFallbackException)
        {
            // Legacy single-byte encodings can represent every byte. Control bytes are therefore
            // the useful distinction between an encoded text file and arbitrary binary content.
            return HasAcceptableControlByteRatio(bytes);
        }
    }

    private static bool IsReadableText(ReadOnlySpan<byte> bytes, Encoding encoding)
    {
        //
        // The probe may end in the middle of a character. Without flushing, the decoder keeps
        // such a rest to itself instead of rejecting it:
        //
        var characters = new char[encoding.GetMaxCharCount(bytes.Length)];
        var decoder = encoding.GetDecoder();
        decoder.Convert(bytes, characters, false, out _, out var charactersUsed, out _);

        return charactersUsed > 0 && HasAcceptableControlCharacterRatio(characters.AsSpan(0, charactersUsed));
    }

    private static bool HasAcceptableControlByteRatio(ReadOnlySpan<byte> bytes)
    {
        var controls = 0;
        foreach (var value in bytes)
        {
            if ((value < (byte)' ' && value != (byte)'\t' && value != (byte)'\n' && value != (byte)'\r' && value != (byte)'\f') || value == DELETE_BYTE)
                controls++;
        }

        return controls * MIN_CHARACTERS_PER_CONTROL_CHARACTER <= bytes.Length;
    }

    private static bool HasAcceptableControlCharacterRatio(ReadOnlySpan<char> characters)
    {
        var controls = 0;
        foreach (var character in characters)
        {
            if (char.IsControl(character) && character != '\t' && character != '\n' && character != '\r' && character != '\f')
                controls++;
        }

        return controls * MIN_CHARACTERS_PER_CONTROL_CHARACTER <= characters.Length;
    }
}