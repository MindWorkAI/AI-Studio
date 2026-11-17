using AIStudio.Tools.DocumentSegments;

namespace AIStudio.Assistants;

/// <summary>
/// A document an assistant worked on, e.g., translated, which the user may save as often as they like.
/// </summary>
/// <remarks>
/// Only the new texts are kept, never the new file: saving writes them into a copy of the source,
/// which the format reads again then.
/// </remarks>
/// <param name="SourcePath">The document the assistant worked on.</param>
/// <param name="Results">What became of its segments.</param>
/// <param name="LanguageTag">The IETF tag of the language of the new texts, e.g., "de-DE"; empty to leave the marks of the language as they are.</param>
/// <param name="Label">What sets the new document apart, e.g., its target language, for the user and for the suggested file name.</param>
public sealed record EditedDocument(string SourcePath, SegmentResults Results, string LanguageTag, string Label);