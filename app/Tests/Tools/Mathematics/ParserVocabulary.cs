using System.Globalization;
using System.Text.RegularExpressions;

using AngouriMath;

using Antlr4.Runtime;

namespace AIStudio.Tests.Tools.Mathematics;

/// <summary>
/// The tokens of AngouriMath's parser, read from the lexer of the version loaded.
/// </summary>
public static partial class ParserVocabulary
{
    /// <summary>
    /// Every literal token of the parser without its quotes, such as sin( or !!.
    /// </summary>
    public static IReadOnlyList<string> Tokens()
    {
        var vocabulary = (IVocabulary)typeof(Entity).Assembly.GetType("AngouriMath.Core.Antlr.AngouriMathLexer", throwOnError: true)!.GetField("DefaultVocabulary")!.GetValue(null)!;
        return [..Enumerable.Range(0, 1000).Select(vocabulary.GetLiteralName).Where(name => name is not null).Select(Unquote)];
    }

    /// <summary>
    /// Whether a token opens the arguments of a function, as sin( does.
    /// </summary>
    public static bool IsFunction(string token) => token.Length > 1 && token.EndsWith('(');

    /// <summary>
    /// The names of the functions of the parser, such as sin.
    /// </summary>
    public static IReadOnlyList<string> FunctionNames() => [..Tokens().Where(IsFunction).Select(token => token[..^1])];

    /// <summary>
    /// A literal name of the parser without its quotes, with its escapes, such as \u2026, decoded.
    /// </summary>
    private static string Unquote(string literalName) => UnicodeEscape().Replace(literalName[1..^1], match => ((char)int.Parse(match.Groups["code"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToString());

    [GeneratedRegex(@"\\u(?<code>[0-9A-Fa-f]{4})")]
    private static partial Regex UnicodeEscape();
}