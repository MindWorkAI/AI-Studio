using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// Turns the markup of LaTeX which stands for words into those words, and new words into markup.
/// </summary>
/// <remarks>
/// A model gets the words as a reader of the document sees them, e.g., "ü" for \"u, so that it never has to keep
/// markup within a word. New words go into the document as UTF-8, which LaTeX reads by default since 2018; only
/// the characters which LaTeX treats specially get escaped.
/// </remarks>
internal static class LatexWords
{
    /// <summary>
    /// What a tilde stands for.
    /// </summary>
    public const string NO_BREAK_SPACE = " ";

    /// <summary>
    /// What \, stands for, e.g., in "z.\,B.".
    /// </summary>
    public const string THIN_SPACE = " ";

    /// <summary>
    /// Commands with letters which stand for a character, e.g., \ss or \glqq.
    /// </summary>
    private static readonly FrozenDictionary<string, string> LETTER_COMMANDS = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ss"] = "ß", ["ae"] = "æ", ["AE"] = "Æ", ["oe"] = "œ", ["OE"] = "Œ", ["aa"] = "å", ["AA"] = "Å", ["o"] = "ø",
        ["O"] = "Ø", ["l"] = "ł", ["L"] = "Ł", ["i"] = "ı", ["j"] = "ȷ",
        ["glqq"] = "„", ["grqq"] = "“", ["glq"] = "‚", ["grq"] = "‘", ["flqq"] = "«", ["frqq"] = "»", ["flq"] = "‹",
        ["frq"] = "›", ["quotedblbase"] = "„", ["quotesinglbase"] = "‚", ["textquotedblleft"] = "“",
        ["textquotedblright"] = "”", ["textquoteleft"] = "‘", ["textquoteright"] = "’", ["guillemotleft"] = "«",
        ["guillemotright"] = "»", ["guillemetleft"] = "«", ["guillemetright"] = "»", ["guilsinglleft"] = "‹",
        ["guilsinglright"] = "›", ["ldots"] = "…", ["dots"] = "…", ["textellipsis"] = "…", ["textendash"] = "–",
        ["textemdash"] = "—", ["S"] = "§",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The accents of LaTeX with the combining characters of Unicode they stand for.
    /// </summary>
    private static readonly FrozenDictionary<string, char> ACCENTS = new Dictionary<string, char>(StringComparer.Ordinal)
    {
        ["\""] = '̈', ["'"] = '́', ["`"] = '̀', ["^"] = '̂', ["~"] = '̃', ["="] = '̄',
        ["."] = '̇', ["c"] = '̧', ["v"] = '̌', ["u"] = '̆', ["H"] = '̋', ["r"] = '̊',
        ["k"] = '̨', ["d"] = '̣', ["b"] = '̱',
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Finds the character a command with letters stands for, e.g., "ß" for \ss.
    /// </summary>
    /// <param name="name">The name of the command, without its backslash.</param>
    /// <param name="words">The character.</param>
    public static bool TryLetterCommand(string name, [NotNullWhen(true)] out string? words) => LETTER_COMMANDS.TryGetValue(name, out words);

    /// <summary>
    /// Finds the combining character an accent stands for, e.g., U+0308 for \".
    /// </summary>
    /// <param name="accent">The name of the accent, without its backslash, e.g., "\"" or "c".</param>
    /// <param name="combining">The combining character.</param>
    public static bool TryAccent(string accent, out char combining) => ACCENTS.TryGetValue(accent, out combining);

    /// <summary>
    /// Whether a character after a backslash is an accent, e.g., the quote of \"a.
    /// </summary>
    public static bool IsSymbolAccent(char character) => character is '"' or '\'' or '`' or '^' or '~' or '=' or '.';

    /// <summary>
    /// Puts a letter and an accent together, e.g., "ü" for u and \".
    /// </summary>
    public static string WithAccent(char letter, char combining) => string.Concat(letter.ToString(), combining.ToString()).Normalize(NormalizationForm.FormC);

    /// <summary>
    /// Makes new words fit into the text of a document.
    /// </summary>
    public static string Encoded(string words) => Encode(words, isInBrackets: false);

    /// <summary>
    /// Makes new words fit into an optional argument, e.g., the short title of a section, which a closing bracket would end.
    /// </summary>
    public static string EncodedInBrackets(string words) => Encode(words, isInBrackets: true);

    private static string Encode(string words, bool isInBrackets)
    {
        var builder = new StringBuilder(words.Length + 16);
        for (var index = 0; index < words.Length; index++)
        {
            var character = words[index];
            switch (character)
            {
                case '\\':
                    builder.Append(@"\textbackslash{}");
                    break;

                case '{' or '}' or '$' or '&' or '%' or '#' or '_':
                    builder.Append('\\').Append(character);
                    break;

                case '^':
                    builder.Append(@"\^{}");
                    break;

                case '~':
                    builder.Append(@"\textasciitilde{}");
                    break;

                case ' ':
                    builder.Append('~');
                    break;

                // pdfLaTeX knows no thin spaces as characters:
                case ' ' or ' ':
                    builder.Append(@"\,");
                    break;

                // A blank line would end the paragraph, a line break within it changes nothing:
                case '\r' or '\n':
                    if (character == '\r' && index + 1 < words.Length && words[index + 1] == '\n')
                        index++;

                    builder.Append(' ');
                    break;

                // After a line break or an item, a bracket would open an optional argument:
                case '[' when index == 0:
                    builder.Append("{[}");
                    break;

                case ']' when isInBrackets:
                    builder.Append("{]}");
                    break;

                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }
}