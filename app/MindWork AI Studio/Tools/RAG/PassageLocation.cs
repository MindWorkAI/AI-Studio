namespace AIStudio.Tools.RAG;

/// <summary>
/// Where passages are in their document: their pages or parts, and the words around them.
/// </summary>
/// <remarks>
/// Kept in parts rather than as one text, so that each page can be shown as a link of its own,
/// while the words around the numbers still come from one translation, e.g., "Pages " and nothing
/// for "Pages 1, 11". Written out as a whole, see ToString, it reads the same as the parts.
/// </remarks>
/// <param name="Prefix">What stands before the numbers, e.g., "Pages ".</param>
/// <param name="Ranges">The pages or parts, in the order of the document, each once.</param>
/// <param name="Suffix">What stands after the numbers, often nothing.</param>
/// <param name="ArePages">Whether the numbers are pages, at which a document can be opened, or the parts of a document without pages.</param>
public sealed record PassageLocation(string Prefix, IReadOnlyList<LocationRange> Ranges, string Suffix, bool ArePages)
{
    /// <summary>
    /// What stands between two ranges.
    /// </summary>
    public const string RANGE_SEPARATOR = ", ";

    /// <summary>
    /// The location as one text, e.g., "Pages 1, 11" or "Part 3".
    /// </summary>
    public override string ToString() => $"{this.Prefix}{string.Join(RANGE_SEPARATOR, this.Ranges)}{this.Suffix}";
}