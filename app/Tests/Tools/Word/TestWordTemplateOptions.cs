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
/// <param name="TitleBox">Whether a text box above the text of the first page holds a sample title of
/// two lines in white letters, in the style "Title", and a subtitle.</param>
/// <param name="FooterShapes">Whether the footer of the cover page floats an address in a text box
/// across the text at the foot of the page, and a logo higher up in the right margin; only together
/// with CoverPage, which brings the footer along.</param>
/// <param name="TableOfContents">Whether a table of contents follows the first section, as a field on
/// a page of its own, and a heading of the first level starts the sample text, like in a report.</param>
/// <param name="AppendixPage">Whether a heading of the first level starts the sample text, and a page
/// of instructions follows it in a section with a header of its own, to remove before sending the document.</param>
internal sealed record TestWordTemplateOptions(bool AsTemplate = false, bool CoverPage = false, bool ListNumbering = false, int NormalSpacingAfter = 160, int LeadingEmptyParagraphs = 0, bool EmptyCoverPage = false, bool SidebarLayout = false, bool Shapes = false, bool TitleBox = false, bool FooterShapes = false, bool TableOfContents = false, bool AppendixPage = false);