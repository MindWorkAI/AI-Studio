namespace AIStudio.Tools;

/// <summary>
/// A source together with the number it is listed under.
/// </summary>
/// <remarks>
/// The number runs through the whole list rather than starting over per group, because that is how
/// an answer refers to a source. It is assigned once, where the groups are formed, so the chat and
/// an exported document cannot end up numbering the same list differently.
/// </remarks>
/// <param name="Number">The number this source is listed under, counted from one.</param>
/// <param name="Source">The source itself.</param>
public readonly record struct NumberedSource(int Number, Source Source);