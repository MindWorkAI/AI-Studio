namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How a style sheet in CSS looks to the tokenizer.
/// </summary>
/// <remarks>
/// A style sheet has no statements: it has selectors, such as .card or #main, and blocks of properties
/// with their values. The tokenizer tells them apart by the braces. At-rules, such as @media, and
/// !important get the color of keywords, and a function, such as var(), that of functions.
/// </remarks>
public static class CssCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        IsStyleSheet = true,
        BlockComments = [new("/*", "*/")],
        Strings =
        [
            new("\"", "\""),
            new("'", "'"),
        ],

        WordStartCharacters = "-",
        WordCharacters = "-",
    };
}