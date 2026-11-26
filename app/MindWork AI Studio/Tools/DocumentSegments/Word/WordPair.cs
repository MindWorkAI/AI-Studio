using DocumentFormat.OpenXml;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// What a pair of tags in a paragraph stands for.
/// </summary>
internal abstract record WordPair;

/// <summary>
/// An element which encloses runs, e.g., a link, a content control, or an insertion of the change tracking.
/// The writer creates it anew around the new words, with the attributes and properties of the original.
/// </summary>
/// <param name="Container">The original element.</param>
internal sealed record WordContainerPair(OpenXmlElement Container) : WordPair;

/// <summary>
/// Words between a start and an end which are elements of their own and stay as they are, and in their order:
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>A field whose result shows words, e.g., an entry of a table of contents or a cross reference. The words
/// are its result, while the start of the field with its instruction and the end of the field stay.</item>
/// <item>A bookmark around words, e.g., the one a cross reference to a caption points to. It encloses the same
/// words afterward, so that the cross reference shows the new label and number, not the whole caption.</item>
/// </list>
/// </remarks>
/// <param name="Opening">The start: of a field, with its instruction and the mark which separates the result, or of a bookmark.</param>
/// <param name="Closing">The end of the field or of the bookmark.</param>
internal sealed record WordBoundaryPair(WordAtom Opening, WordAtom Closing) : WordPair;

/// <summary>
/// Words in a formatting other than the most common one of their surroundings, e.g., a word in bold. The
/// writer gives the new words within the pair this formatting.
/// </summary>
/// <param name="Format">The properties of the runs of these words.</param>
internal sealed record WordFormatPair(W.RunProperties? Format) : WordPair;