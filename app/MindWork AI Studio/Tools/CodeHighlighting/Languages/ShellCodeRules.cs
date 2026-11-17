namespace AIStudio.Tools.CodeHighlighting.Languages;

/// <summary>
/// How a script of a Unix shell, such as Bash, looks to the tokenizer.
/// </summary>
/// <remarks>
/// A # starts a comment only at the start of a word, so $# and ${#list[@]} stay variables. Strings may
/// run over several lines, and the variables within double quotation marks stay a part of the string.
/// </remarks>
public static class ShellCodeRules
{
    public static readonly CodeLanguageRules RULES = new()
    {
        Keywords = CodeLanguageRules.Words(
            "break", "case", "continue", "declare", "do", "done", "elif", "else", "esac", "exit", "export", "fi", "for",
            "function", "if", "in", "local", "readonly", "return", "select", "source", "then", "time", "until", "unset",
            "while"),

        FunctionDeclarations = CodeLanguageRules.Words("function"),
        LineComments = ["#"],
        LineCommentsNeedSeparator = true,
        Strings =
        [
            new("\"", "\"", IsMultiline: true),
            new("'", "'", Escape: null, IsMultiline: true),
        ],

        VariablePrefix = '$',
    };
}