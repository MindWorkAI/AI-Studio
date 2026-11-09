using DocumentFormat.OpenXml;

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
/// A field whose result shows words, e.g., an entry of a table of contents or a cross reference. The words
/// are its result, which the model translates, while the start of the field with its instruction and the end
/// of the field stay as they are, and in their order.
/// </summary>
/// <param name="Opening">The start of the field, its instruction, and the mark which separates the result.</param>
/// <param name="Closing">The end of the field.</param>
internal sealed record WordFieldPair(WordAtom Opening, WordAtom Closing) : WordPair;