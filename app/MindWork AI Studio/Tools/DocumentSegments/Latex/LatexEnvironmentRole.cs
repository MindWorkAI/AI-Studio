namespace AIStudio.Tools.DocumentSegments.Latex;

/// <summary>
/// What an environment means for the segments of a document.
/// </summary>
internal enum LatexEnvironmentRole
{
    /// <summary>
    /// Holds paragraphs, e.g., itemize or figure; the arguments of its start are layout.
    /// </summary>
    CONTAINER,

    /// <summary>
    /// Holds paragraphs and has a title as its arguments in braces, e.g., a frame of Beamer.
    /// </summary>
    CONTAINER_WITH_TITLE,

    /// <summary>
    /// Holds paragraphs and may have a name as its argument in brackets, e.g., a theorem.
    /// </summary>
    CONTAINER_WITH_NAME,

    /// <summary>
    /// A table, whose cells are segments of their own: an ampersand or a line break ends one.
    /// </summary>
    TABLE,

    /// <summary>
    /// A formula, e.g., equation: a tag of its own within the words around it.
    /// </summary>
    MATH,

    /// <summary>
    /// Code, a drawing, or anything else which stays out entirely, e.g., verbatim or tikzpicture.
    /// </summary>
    VERBATIM,
}