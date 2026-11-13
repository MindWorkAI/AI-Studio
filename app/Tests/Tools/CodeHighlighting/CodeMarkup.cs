using System.Text;

using AIStudio.Tools.CodeHighlighting;

namespace AIStudio.Tests.Tools.CodeHighlighting;

/// <summary>
/// Writes code with its highlighted parts marked, such as {keyword:def}, so that a test reads like the
/// code it checks.
/// </summary>
internal static class CodeMarkup
{
    public static string Of(string code, CodeLanguageRules rules)
    {
        var markup = new StringBuilder();
        var position = 0;
        foreach (var token in CodeTokenizer.Tokenize(code, rules))
        {
            markup.Append(code, position, token.Start - position);
            markup.Append('{').Append(token.Kind.ToString().ToLowerInvariant()).Append(':').Append(code, token.Start, token.Length).Append('}');
            position = token.Start + token.Length;
        }

        markup.Append(code, position, code.Length - position);
        return markup.ToString();
    }
}