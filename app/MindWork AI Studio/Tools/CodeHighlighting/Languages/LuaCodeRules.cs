namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How Lua code looks to the tokenizer, the language of the plugins of AI Studio, too.
/// </summary>
/// <remarks>
/// Lua writes long strings and comments in brackets, such as [[ … ]] and --[==[ … ]==].
/// </remarks>
public static class LuaCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.Words(
            "and", "break", "do", "else", "elseif", "end", "for", "function", "goto", "if", "in", "local", "not", "or",
            "repeat", "return", "then", "until", "while"),

        Literals = CodeLanguageRules.Words("nil", "true", "false"),
        FunctionDeclarations = CodeLanguageRules.Words("function"),
        LineComments = ["--"],
        HasLongBrackets = true,
        Strings =
        [
            new("\"", "\""),
            new("'", "'"),
        ],
    };
}