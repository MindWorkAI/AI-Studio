namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How JavaScript code looks to the tokenizer, JSX included, whose tags stay plain.
/// </summary>
/// <remarks>
/// A template literal stays one string, including the expressions within it. A regular expression
/// literal stays plain: telling it from a division needs the grammar of the language.
/// </remarks>
public static class JavaScriptCodeRules
{
    /// <summary>
    /// The keywords of JavaScript, which TypeScript has, too.
    /// </summary>
    internal static readonly string[] KEYWORDS =
    [
        "async", "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default", "delete", "do",
        "else", "export", "extends", "finally", "for", "from", "function", "if", "import", "in", "instanceof", "let",
        "new", "of", "return", "static", "super", "switch", "this", "throw", "try", "typeof", "var", "void", "while",
        "with", "yield",
    ];

    /// <summary>
    /// The words of JavaScript which stand for a value, which TypeScript has, too.
    /// </summary>
    internal static readonly string[] LITERALS = ["true", "false", "null", "undefined", "NaN", "Infinity"];

    /// <summary>
    /// The strings of JavaScript, which TypeScript has, too.
    /// </summary>
    internal static readonly IReadOnlyList<CodeStringRule> STRINGS =
    [
        new("`", "`", IsMultiline: true),
        new("\"", "\""),
        new("'", "'"),
    ];

    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.Words(KEYWORDS),
        Literals = CodeLanguageRules.Words(LITERALS),
        FunctionDeclarations = CodeLanguageRules.Words("function"),
        TypeDeclarations = CodeLanguageRules.Words("class"),
        LineComments = ["//"],
        BlockComments = [new("/*", "*/")],
        Strings = STRINGS,
        AnnotationPrefix = '@',
        WordStartCharacters = "$",
        WordCharacters = "$",
    };
}