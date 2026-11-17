namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// How a language writes one kind of string, such as one in double quotation marks.
/// </summary>
/// <param name="Open">What opens the string, for example a quotation mark or three of them.</param>
/// <param name="Close">What closes it.</param>
/// <param name="Escape">The character which makes the next one a part of the string, even when it would
/// close the string, or null when the string knows no escapes.</param>
/// <param name="IsMultiline">Whether the string may run over several lines. Otherwise, it ends with its
/// line, closed or not.</param>
/// <param name="DoubledCloseIsLiteral">Whether the closing mark written twice stands for itself, as two
/// apostrophes do within a string of SQL.</param>
public sealed record CodeStringRule(string Open, string Close, char? Escape = '\\', bool IsMultiline = false, bool DoubledCloseIsLiteral = false);