using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

using AIStudio.Tools.CodeHighlighting.Languages;

namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// The languages AI Studio highlights itself, beyond those MudBlazor.Markdown highlights.
/// </summary>
/// <remarks>
/// MudBlazor.Markdown highlights C#, Go, Kotlin, Razor, Rust, JSON, XML, and YAML. Its highlighter is
/// internal, so the languages models write just as often, such as Python, get rules of their own here.
/// A language is found by the name SourceCodeLanguages gives it, which already knows all the aliases
/// models write behind the fence, such as py or Python3.
/// </remarks>
public static class CodeHighlighters
{
    private static readonly FrozenDictionary<string, CodeLanguageRules> RULES_BY_LANGUAGE = new Dictionary<string, CodeLanguageRules>(StringComparer.Ordinal)
    {
        ["CSS"] = CssCodeRules.RULES,
        ["JavaScript"] = JavaScriptCodeRules.RULES,
        ["JSX"] = JavaScriptCodeRules.RULES,
        ["Lua"] = LuaCodeRules.RULES,
        ["PowerShell"] = PowerShellCodeRules.RULES,
        ["Python"] = PythonCodeRules.RULES,
        ["Shell"] = ShellCodeRules.RULES,
        ["TSX"] = TypeScriptCodeRules.RULES,
        ["TypeScript"] = TypeScriptCodeRules.RULES,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The names of the languages AI Studio highlights itself, as SourceCodeLanguages names them.
    /// </summary>
    internal static IEnumerable<string> LanguageNames => RULES_BY_LANGUAGE.Keys;

    /// <summary>
    /// Finds the rules for the language a model names behind the opening fence of a code block.
    /// </summary>
    /// <param name="fenceLanguage">The language behind the opening fence, as Markdig reads it.</param>
    /// <param name="rules">The rules of the language, when AI Studio highlights it itself.</param>
    /// <returns>Whether AI Studio highlights the language itself. Otherwise, MudBlazor.Markdown renders the
    /// block, highlighted when it knows the language.</returns>
    public static bool TryGetRules(string? fenceLanguage, [NotNullWhen(true)] out CodeLanguageRules? rules)
    {
        rules = null;
        if (string.IsNullOrWhiteSpace(fenceLanguage))
            return false;

        return RULES_BY_LANGUAGE.TryGetValue(SourceCodeLanguages.FromCodeFenceLanguage(fenceLanguage).Name, out rules);
    }
}