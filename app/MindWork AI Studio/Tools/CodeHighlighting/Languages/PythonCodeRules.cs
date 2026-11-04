namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How Python code looks to the tokenizer.
/// </summary>
/// <remarks>
/// The soft keywords match, case, and type stay out: they are names everywhere else, as in re.match.
/// </remarks>
public static class PythonCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.Words(
            "and", "as", "assert", "async", "await", "break", "class", "continue", "def", "del", "elif", "else", "except",
            "finally", "for", "from", "global", "if", "import", "in", "is", "lambda", "nonlocal", "not", "or", "pass",
            "raise", "return", "try", "while", "with", "yield"),

        Types = CodeLanguageRules.Words("bool", "bytearray", "bytes", "complex", "dict", "float", "frozenset", "int", "list", "object", "set", "str", "tuple"),
        Literals = CodeLanguageRules.Words("True", "False", "None"),
        FunctionDeclarations = CodeLanguageRules.Words("def"),
        TypeDeclarations = CodeLanguageRules.Words("class"),
        LineComments = ["#"],
        Strings =
        [
            new("\"\"\"", "\"\"\"", IsMultiline: true),
            new("'''", "'''", IsMultiline: true),
            new("\"", "\""),
            new("'", "'"),
        ],

        StringPrefixes = CodeLanguageRules.WordsInAnyCase("r", "u", "b", "f", "t", "rb", "br", "fr", "rf", "tr", "rt"),
        AnnotationPrefix = '@',
    };
}