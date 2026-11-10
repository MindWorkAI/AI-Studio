using AIStudio.Tools.DocumentSegments;

namespace AIStudio.Assistants.Translation;

/// <summary>
/// A document the translator translated, which the user may save as often as they like.
/// </summary>
/// <remarks>
/// Only the translations are kept, never the new file: saving writes them into a copy of the source,
/// which the format reads again then.
/// </remarks>
/// <param name="SourcePath">The document which was translated.</param>
/// <param name="Results">What became of its segments.</param>
/// <param name="LanguageTag">The IETF tag of the target language, e.g., "de-DE"; empty for a language of the user's own.</param>
/// <param name="LanguageName">The name of the target language, for the user and for the suggested file name.</param>
public sealed record TranslatedDocument(string SourcePath, SegmentResults Results, string LanguageTag, string LanguageName);