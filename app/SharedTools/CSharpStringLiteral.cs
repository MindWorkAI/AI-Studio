using System.Globalization;
using System.Text;

namespace SharedTools;

/// <summary>
/// Reads a C# string literal from source code and gives the string the compiler makes of it.
/// </summary>
/// <remarks>
/// The I18N collector reads the texts of T() and TB() from the source files, and the app looks up their
/// translations by a hash of the string it gets at runtime. Both have to agree on every character, so this reader
/// follows the compiler: escape sequences of regular literals, doubled quotes of verbatim literals, and the removed
/// indentation of multi-line raw literals. Interpolated literals are not read, since their text is known only at
/// runtime. A line break in the source stays as it is; I18NKeyHash makes line endings equal.
/// </remarks>
public static class CSharpStringLiteral
{
    /// <summary>
    /// Reads the literal at the start of the source.
    /// </summary>
    /// <param name="source">The source, starting with the first quote of the literal, or with the @ of a verbatim literal.</param>
    /// <param name="value">The string the compiler makes of the literal.</param>
    /// <param name="length">The number of characters the literal takes in the source.</param>
    /// <returns>Whether the source starts with a complete literal this reader understands.</returns>
    public static bool TryRead(ReadOnlySpan<char> source, out string value, out int length)
    {
        value = string.Empty;
        length = 0;
        if (source.IsEmpty)
            return false;

        if (source[0] == '@')
            return TryReadVerbatim(source, out value, out length);

        if (source[0] != '"')
            return false;

        var quotes = CountQuotes(source);
        if (quotes >= 3)
            return TryReadRaw(source, quotes, out value, out length);

        if (quotes == 2)
        {
            length = 2;
            return true;
        }

        return TryReadRegular(source, out value, out length);
    }

    private static bool TryReadRegular(ReadOnlySpan<char> source, out string value, out int length)
    {
        value = string.Empty;
        length = 0;

        var builder = new StringBuilder();
        for (var index = 1; index < source.Length; index++)
        {
            var character = source[index];
            switch (character)
            {
                case '"':
                    value = builder.ToString();
                    length = index + 1;
                    return true;

                case '\r' or '\n':
                    return false;

                case '\\':
                    if (!TryReadEscape(source, ref index, builder))
                        return false;

                    break;

                default:
                    builder.Append(character);
                    break;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the escape sequence whose backslash is at the index, and leaves the index at its last character.
    /// </summary>
    private static bool TryReadEscape(ReadOnlySpan<char> source, ref int index, StringBuilder builder)
    {
        if (++index >= source.Length)
            return false;

        switch (source[index])
        {
            case '\'': builder.Append('\''); return true;
            case '"': builder.Append('"'); return true;
            case '\\': builder.Append('\\'); return true;
            case '0': builder.Append('\0'); return true;
            case 'a': builder.Append('\a'); return true;
            case 'b': builder.Append('\b'); return true;
            case 'e': builder.Append('\u001b'); return true;
            case 'f': builder.Append('\f'); return true;
            case 'n': builder.Append('\n'); return true;
            case 'r': builder.Append('\r'); return true;
            case 't': builder.Append('\t'); return true;
            case 'v': builder.Append('\v'); return true;

            case 'u':
                if (!TryReadHex(source, index + 1, 4, 4, out var utf16, out var utf16Digits))
                    return false;

                builder.Append((char)utf16);
                index += utf16Digits;
                return true;

            case 'U':
                if (!TryReadHex(source, index + 1, 8, 8, out var utf32, out var utf32Digits) || utf32 > 0x10FFFF)
                    return false;

                builder.Append(char.ConvertFromUtf32(utf32));
                index += utf32Digits;
                return true;

            case 'x':
                if (!TryReadHex(source, index + 1, 1, 4, out var variable, out var variableDigits))
                    return false;

                builder.Append((char)variable);
                index += variableDigits;
                return true;

            default:
                return false;
        }
    }

    private static bool TryReadHex(ReadOnlySpan<char> source, int start, int minDigits, int maxDigits, out int value, out int digits)
    {
        value = 0;
        digits = 0;
        while (digits < maxDigits && start + digits < source.Length && char.IsAsciiHexDigit(source[start + digits]))
            digits++;

        return digits >= minDigits && int.TryParse(source.Slice(start, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadVerbatim(ReadOnlySpan<char> source, out string value, out int length)
    {
        value = string.Empty;
        length = 0;
        if (source.Length < 2 || source[1] != '"')
            return false;

        var builder = new StringBuilder();
        for (var index = 2; index < source.Length; index++)
        {
            if (source[index] != '"')
            {
                builder.Append(source[index]);
                continue;
            }

            // Two quotes stand for one:
            if (index + 1 < source.Length && source[index + 1] == '"')
            {
                builder.Append('"');
                index++;
                continue;
            }

            value = builder.ToString();
            length = index + 1;
            return true;
        }

        return false;
    }

    private static bool TryReadRaw(ReadOnlySpan<char> source, int quotes, out string value, out int length)
    {
        value = string.Empty;
        length = 0;

        // A raw literal spans several lines when nothing but whitespace follows its opening quotes:
        var afterOpening = quotes;
        while (afterOpening < source.Length && IsWhitespace(source[afterOpening]))
            afterOpening++;

        return afterOpening < source.Length && IsLineBreak(source[afterOpening])
            ? TryReadMultiLineRaw(source, quotes, afterOpening, out value, out length)
            : TryReadSingleLineRaw(source, quotes, out value, out length);
    }

    private static bool TryReadSingleLineRaw(ReadOnlySpan<char> source, int quotes, out string value, out int length)
    {
        value = string.Empty;
        length = 0;
        for (var index = quotes; index < source.Length; index++)
        {
            if (IsLineBreak(source[index]))
                return false;

            if (source[index] != '"')
                continue;

            var run = CountQuotes(source[index..]);
            if (run < quotes)
            {
                index += run - 1;
                continue;
            }

            if (run > quotes || index == quotes)
                return false;

            value = source[quotes..index].ToString();
            length = index + quotes;
            return true;
        }

        return false;
    }

    private static bool TryReadMultiLineRaw(ReadOnlySpan<char> source, int quotes, int lineBreakAfterOpening, out string value, out int length)
    {
        value = string.Empty;
        length = 0;

        var lines = new List<(int Start, int End, int LineBreakLength)>();
        var lineStart = lineBreakAfterOpening + LineBreakLength(source, lineBreakAfterOpening);
        while (lineStart <= source.Length)
        {
            var lineEnd = lineStart;
            while (lineEnd < source.Length && !IsLineBreak(source[lineEnd]))
                lineEnd++;

            // Is this the line which closes the literal?
            var indentationEnd = lineStart;
            while (indentationEnd < lineEnd && IsWhitespace(source[indentationEnd]))
                indentationEnd++;

            if (indentationEnd < lineEnd && source[indentationEnd] == '"')
            {
                var run = CountQuotes(source[indentationEnd..lineEnd]);
                if (run >= quotes)
                {
                    if (run > quotes || lines.Count == 0)
                        return false;

                    var indentation = source[lineStart..indentationEnd];
                    if (!TryRemoveIndentation(source, lines, indentation, out value))
                        return false;

                    length = indentationEnd + quotes;
                    return true;
                }
            }

            if (lineEnd >= source.Length)
                return false;

            var lineBreakLength = LineBreakLength(source, lineEnd);
            lines.Add((lineStart, lineEnd, lineBreakLength));
            lineStart = lineEnd + lineBreakLength;
        }

        return false;
    }

    /// <summary>
    /// Joins the content lines of a multi-line raw literal without the indentation of its closing line, which every
    /// line has to start with. A line of whitespace only may be shorter, and becomes empty then.
    /// </summary>
    private static bool TryRemoveIndentation(ReadOnlySpan<char> source, List<(int Start, int End, int LineBreakLength)> lines, ReadOnlySpan<char> indentation, out string value)
    {
        value = string.Empty;
        var builder = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            var (start, end, lineBreakLength) = lines[index];
            var line = source[start..end];
            if (line.StartsWith(indentation, StringComparison.Ordinal))
                builder.Append(line[indentation.Length..]);
            else if (!IsWhitespaceOnly(line) || line.Length > indentation.Length)
                return false;

            // The line break after the last content line belongs to the closing line:
            if (index < lines.Count - 1)
                builder.Append(source.Slice(end, lineBreakLength));
        }

        value = builder.ToString();
        return true;
    }

    private static int CountQuotes(ReadOnlySpan<char> source)
    {
        var count = 0;
        while (count < source.Length && source[count] == '"')
            count++;

        return count;
    }

    private static int LineBreakLength(ReadOnlySpan<char> source, int index) =>
        source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n' ? 2 : 1;

    private static bool IsLineBreak(char character) => character is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029';

    private static bool IsWhitespace(char character) => character is not ('\r' or '\n' or '\u0085' or '\u2028' or '\u2029') && char.IsWhiteSpace(character);

    private static bool IsWhitespaceOnly(ReadOnlySpan<char> line)
    {
        foreach (var character in line)
            if (!IsWhitespace(character))
                return false;

        return true;
    }
}