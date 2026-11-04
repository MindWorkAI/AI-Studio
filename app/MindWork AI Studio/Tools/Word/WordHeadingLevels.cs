namespace AIStudio.Tools.Word;

/// <summary>
/// The heading levels of an answer which become the first heading level of a template.
/// </summary>
/// <remarks>
/// Models mostly start their answers with headings of the second level, keeping the first for a
/// title. A template, however, often styles the first level only, or numbers its chapters from
/// there, so that a document starting with the second level would get chapters such as "0.1".
/// So the top level of the answer becomes the first level of the template. When the heading the
/// answer starts with becomes the title of the document instead, see WordShapes.TitleBoxOf, the
/// top level of the headings after it does. Both are read from the answer alone, see
/// MarkdownWordConverter.HeadingLevelsOf, because the sources AI Studio adds below it always start
/// with a heading of the first level.
/// </remarks>
/// <param name="TopLevel">The top level of the headings of the answer; 1 when it has none.</param>
/// <param name="TopLevelBelowTitle">The top level of the headings after the heading the answer starts
/// with; TopLevel when it starts with none, or none follows.</param>
internal readonly record struct WordHeadingLevels(int TopLevel, int TopLevelBelowTitle)
{
    /// <summary>
    /// The levels of a text whose headings keep their levels in the template.
    /// </summary>
    public static readonly WordHeadingLevels AS_THEY_ARE = new(1, 1);
}