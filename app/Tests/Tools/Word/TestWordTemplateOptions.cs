namespace AIStudio.Tests.Tools.Word;

/// <summary>
/// What a test template holds besides what every one of them does.
/// </summary>
/// <param name="AsTemplate">Whether the file is a template (.dotx) rather than a document (.docx).</param>
/// <param name="CoverPage">Whether the template follows the pattern of corporate templates: the first
/// section, a cover page, states the headers and footers, with a header of its own for the first
/// page and a larger top margin below it, while the last section states none and inherits them.</param>
/// <param name="ListNumbering">Whether the template brings the numbering of its lists along: dashes
/// as the bullets of "List Paragraph", which pulls them out to the margin with an indentation of its
/// own, and numbers followed by a parenthesis for "List Number".</param>
/// <param name="NormalSpacingAfter">The space below a paragraph of the default style, in twentieths of
/// a point; by default, what Word itself leaves.</param>
/// <param name="LeadingEmptyParagraphs">How many empty paragraphs of the first heading style the first
/// page starts with, the way templates move their text below a picture.</param>
/// <param name="EmptyCoverPage">Whether the first page holds no text of its own, like a cover page
/// with nothing but pictures and shapes on it.</param>
/// <param name="SidebarLayout">Whether the text runs next to a wide margin for pictures, which is on
/// the left on the first page and on the right on the pages after it.</param>
/// <param name="Shapes">Whether shapes float around the text: on the first page a picture with a
/// caption in the left margin, a picture behind the whole page, and a sample picture within the text,
/// and on the first page of the last section a picture in the right margin.</param>
internal sealed record TestWordTemplateOptions(bool AsTemplate = false, bool CoverPage = false, bool ListNumbering = false, int NormalSpacingAfter = 160, int LeadingEmptyParagraphs = 0, bool EmptyCoverPage = false, bool SidebarLayout = false, bool Shapes = false);