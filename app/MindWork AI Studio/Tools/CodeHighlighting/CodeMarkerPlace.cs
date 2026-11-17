namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// Where a marker of a language has to appear in a code block, see CodeLanguageMarker.
/// </summary>
public enum CodeMarkerPlace
{
    /// <summary>
    /// Anywhere in the code, also across a line break.
    /// </summary>
    ANYWHERE,

    /// <summary>
    /// At the start of a line, after its indentation.
    /// </summary>
    LINE_START,

    /// <summary>
    /// At the end of a line, before any space behind it.
    /// </summary>
    LINE_END,

    /// <summary>
    /// As a line of its own, apart from its indentation, such as the fi of a shell script.
    /// </summary>
    WHOLE_LINE,

    /// <summary>
    /// At the start of the first line which is not empty, such as a shebang.
    /// </summary>
    FIRST_LINE,
}