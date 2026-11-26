namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// What a command means for the segments of a document.
/// </summary>
internal enum LatexCommandRole
{
    /// <summary>
    /// Marks up words, e.g., \textbf or \footnote: its text argument becomes a pair of tags.
    /// </summary>
    WORDS,

    /// <summary>
    /// A heading or a caption, e.g., \section: its text argument and its optional arguments are segments of their own.
    /// </summary>
    HEADING,

    /// <summary>
    /// An item of a list, which starts a segment; its optional argument is its label, a segment of its own.
    /// </summary>
    ITEM,

    /// <summary>
    /// Ends a paragraph, as \par does.
    /// </summary>
    PARAGRAPH,

    /// <summary>
    /// Breaks a line, as \\ does: within a table, it ends a row and so a segment; elsewhere, it is layout.
    /// </summary>
    LINE_BREAK,

    /// <summary>
    /// Layout without words, e.g., \label or \centering: a tag of its own, which stays outside of a segment at its edges.
    /// </summary>
    LAYOUT,

    /// <summary>
    /// Anything else, e.g., \ref or \cite: a tag of its own within the words, since it stands for something a reader sees.
    /// </summary>
    INLINE,
}