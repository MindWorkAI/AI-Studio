namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// What a part of a LaTeX document is, as LatexParser reads it.
/// </summary>
internal enum LatexNodeKind
{
    /// <summary>
    /// Words and the white space between them, without any character which LaTeX treats specially.
    /// </summary>
    TEXT,

    /// <summary>
    /// One blank line or more, which end a paragraph.
    /// </summary>
    PARAGRAPH_BREAK,

    /// <summary>
    /// A comment, from its percent sign to the end of its line.
    /// </summary>
    COMMENT,

    /// <summary>
    /// Markup which stands for words, e.g., \% for a percent sign or \"a for an umlaut; LatexNode.Words holds them.
    /// </summary>
    ESCAPED_WORDS,

    /// <summary>
    /// A command with its arguments.
    /// </summary>
    COMMAND,

    /// <summary>
    /// A group in braces.
    /// </summary>
    GROUP,

    /// <summary>
    /// A formula within the words, e.g., $x$ or \[x\].
    /// </summary>
    MATH,

    /// <summary>
    /// Code which LaTeX prints as it stands, e.g., \verb|x|.
    /// </summary>
    VERBATIM,

    /// <summary>
    /// An environment, from its \begin to its \end.
    /// </summary>
    ENVIRONMENT,

    /// <summary>
    /// A character which LaTeX treats specially, e.g., ~ or &amp;; LatexNode.Name holds it.
    /// </summary>
    SPECIAL,

    /// <summary>
    /// Markup the parser cannot follow, e.g., a closing brace without an opening one, or a formula without its end.
    /// </summary>
    INVALID,
}