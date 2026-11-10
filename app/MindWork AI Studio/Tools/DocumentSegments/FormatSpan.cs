namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Words of a document which follow each other in the same formatting.
/// </summary>
/// <typeparam name="TFormat">How the format describes formatting, e.g., the properties of a run.</typeparam>
/// <param name="FormatKey">What tells this formatting apart from others; empty for none.</param>
/// <param name="Format">The formatting, as the first of these words had it.</param>
/// <param name="Text">The words.</param>
public sealed record FormatSpan<TFormat>(string FormatKey, TFormat Format, string Text);