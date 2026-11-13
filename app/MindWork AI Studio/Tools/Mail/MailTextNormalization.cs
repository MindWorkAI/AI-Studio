using System.Text;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Cleans up the text of a mail, its header fields included, before anybody reads it.
/// </summary>
/// <remarks>
/// Characters nobody sees go first. They are the ones the prompt injection filter of the runtime
/// treats as invisible (normalize::is_invisible), plus the soft hyphen and the combining grapheme
/// joiner. Newsletters pad their preview text with long runs of them, and the filter would report
/// every such newsletter as an attack for carrying invisible characters. Removing them costs the
/// filter nothing, as it reads past them anyway: "ig&lt;zero-width space&gt;nore" is "ignore" to it.
/// The two extra characters are ones the filter does not read past, so without them a hidden word
/// gets whole again for it.
/// </remarks>
public static class MailTextNormalization
{
    private const char NO_BREAK_SPACE = (char)0x00A0;

    /// <summary>
    /// Cleans up the body: unified line breaks, no trailing whitespace, at most one empty line in a row.
    /// </summary>
    /// <remarks>
    /// A line which starts with a no-break space loses its whole indentation. That is padding, and
    /// four spaces of it would make the line a code block in Markdown. Other indentation stays, as
    /// it is what nests the lists of the converted HTML.
    /// </remarks>
    /// <param name="text">The body as it was converted.</param>
    /// <returns>The cleaned-up body.</returns>
    public static string NormalizeBody(string text)
    {
        var lines = WithoutInvisibleCharacters(text)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Length > 0 && line[0] == NO_BREAK_SPACE ? line.TrimStart(NO_BREAK_SPACE, ' ') : line)
            .Select(line => line.Replace(NO_BREAK_SPACE, ' ').TrimEnd());

        var body = new StringBuilder();
        var emptyLines = 0;
        foreach (var line in lines)
        {
            if (line.Length is 0)
            {
                emptyLines++;
                continue;
            }

            if (body.Length > 0)
                body.Append('\n', emptyLines > 0 ? 2 : 1);

            body.Append(line);
            emptyLines = 0;
        }

        return body.ToString();
    }

    /// <summary>
    /// Cleans up a header field, e.g. a subject, a display name, or a file name, into one line.
    /// </summary>
    /// <remarks>
    /// An encoded header may decode to line breaks. Kept, they would let a subject start lines of
    /// its own, e.g. a forged "From:" line in the header block AI Studio writes above the text.
    /// </remarks>
    /// <param name="value">The decoded value.</param>
    /// <returns>The value on one line, with every run of whitespace and control characters as one space.</returns>
    public static string NormalizeHeaderValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var cleaned = WithoutInvisibleCharacters(value).Select(character => char.IsControl(character) || character == NO_BREAK_SPACE ? ' ' : character);
        return string.Join(' ', string.Concat(cleaned).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string WithoutInvisibleCharacters(string text) => text.Any(IsInvisible)
        ? string.Concat(text.Where(character => !IsInvisible(character)))
        : text;

    private static bool IsInvisible(char character) => (int)character is
        0x00AD or 0x034F or 0xFEFF or
        (>= 0x200B and <= 0x200F) or
        (>= 0x2060 and <= 0x2064) or
        (>= 0x2066 and <= 0x2069);
}