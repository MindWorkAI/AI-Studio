using DocumentFormat.OpenXml;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// One element of a paragraph which goes back into it as it is, e.g., a tab, a picture, or a whole field instruction.
/// </summary>
/// <param name="Element">The original element, which the writer moves rather than copies, so that changes to paragraphs inside it, e.g., in a text box, stay.</param>
/// <param name="RunProperties">The properties of the run the element sat in; null for an element outside of a run.</param>
/// <param name="InRun">Whether the element sat in a run, so that the writer puts it into a run of its own.</param>
internal readonly record struct WordPiece(OpenXmlElement Element, W.RunProperties? RunProperties, bool InRun);