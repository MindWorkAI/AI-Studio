namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// What hints at a language in a code block which names none, see CodeLanguageDetector.
/// </summary>
/// <param name="FenceLanguage">The language as a model would write it behind the fence, for example csharp.</param>
/// <param name="Markers">What hints at the language.</param>
/// <param name="Refinement">A language which extends this one, as TypeScript extends JavaScript, or null.</param>
/// <param name="RefinementMarkers">What hints at the extended language. They count for this language,
/// too, and when they reach a weight of 2, the code is in the extended one.</param>
public sealed record CodeLanguageProfile(string FenceLanguage, IReadOnlyList<CodeLanguageMarker> Markers, string? Refinement = null, IReadOnlyList<CodeLanguageMarker>? RefinementMarkers = null);