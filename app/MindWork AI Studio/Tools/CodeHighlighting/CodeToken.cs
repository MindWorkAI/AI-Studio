namespace AIStudio.Tools.CodeHighlighting;

/// <summary>
/// A part of a code block which gets a color of its own.
/// </summary>
/// <param name="Start">Where the part starts in the code.</param>
/// <param name="Length">How many characters the part spans.</param>
/// <param name="Kind">What the part is.</param>
public readonly record struct CodeToken(int Start, int Length, CodeTokenKind Kind);