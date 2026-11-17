namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// Something which hints at a language when it appears in a code block, such as using System; for C#.
/// </summary>
/// <param name="Text">What has to appear.</param>
/// <param name="Place">Where it has to appear.</param>
/// <param name="Weight">How strongly it hints at the language: 4 alone suffices, as only this language
/// writes it so; 1 hints only together with others.</param>
/// <param name="IgnoreCase">Whether it matches in any case, as the keywords of SQL do.</param>
public sealed record CodeLanguageMarker(string Text, CodeMarkerPlace Place, int Weight, bool IgnoreCase = false);