namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How a PowerShell script looks to the tokenizer.
/// </summary>
/// <remarks>
/// PowerShell ignores the case of its keywords and operators. A command such as Get-ChildItem has the
/// form Verb-Noun, an operator such as -eq is a word after a hyphen, and a type stands in square
/// brackets, as in [string]. The backtick escapes within double quotation marks; two apostrophes stand
/// for one within single ones.
/// </remarks>
public static class PowerShellCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.WordsInAnyCase(
            "begin", "break", "catch", "class", "continue", "do", "dynamicparam", "else", "elseif", "end", "enum", "exit",
            "filter", "finally", "for", "foreach", "function", "if", "in", "param", "process", "return", "switch", "throw",
            "trap", "try", "until", "using", "while"),

        FunctionDeclarations = CodeLanguageRules.WordsInAnyCase("function", "filter"),
        TypeDeclarations = CodeLanguageRules.WordsInAnyCase("class", "enum"),
        LineComments = ["#"],
        LineCommentsNeedSeparator = true,
        BlockComments = [new("<#", "#>")],
        Strings =
        [
            new("@\"", "\"@", Escape: '`', IsMultiline: true),
            new("@'", "'@", Escape: null, IsMultiline: true),
            new("\"", "\"", Escape: '`', IsMultiline: true),
            new("'", "'", Escape: null, IsMultiline: true, DoubledCloseIsLiteral: true),
        ],

        VariablePrefix = '$',
        VariableNameCharacters = ":",
        WordCharacters = "-",
        HighlightCommandNames = true,
        HyphenOperators = CodeLanguageRules.WordsInAnyCase(
            "and", "as", "band", "bnot", "bor", "bxor", "ceq", "clike", "cmatch", "cne", "contains", "creplace", "eq", "f",
            "ge", "gt", "ieq", "ilike", "imatch", "in", "ine", "ireplace", "is", "isnot", "join", "le", "like", "lt",
            "match", "ne", "not", "notcontains", "notin", "notlike", "notmatch", "or", "replace", "shl", "shr", "split",
            "xor"),

        HasBracketTypes = true,
    };
}