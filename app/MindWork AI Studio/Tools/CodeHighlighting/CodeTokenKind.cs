namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// What a part of a code block is, which decides its color.
/// </summary>
public enum CodeTokenKind
{
    KEYWORD,
    TYPE,
    LITERAL,
    STRING,
    COMMENT,
    NUMBER,
    FUNCTION,
    META,
    VARIABLE,
    ATTRIBUTE,
    SELECTOR,
}