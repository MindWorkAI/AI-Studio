namespace AIStudio.Tools;

/// <summary>
/// One group of a source list: a heading and the sources below it.
/// </summary>
/// <param name="Heading">The heading above the group.</param>
/// <param name="Sources">The sources of the group, in the order they are shown.</param>
public readonly record struct SourceGroup(string Heading, IReadOnlyList<NumberedSource> Sources);