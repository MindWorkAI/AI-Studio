namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// The pictures of a picture library which a slide deck may use, by their numbers, which follow those of the pictures for the slides.
/// </summary>
/// <param name="Count">How many pictures of the library the LLM may choose from.</param>
/// <param name="IconNumbers">The numbers of those which are icons.</param>
internal sealed record LibraryPictures(int Count, IReadOnlySet<int> IconNumbers)
{
    /// <summary>
    /// No library at all.
    /// </summary>
    public static readonly LibraryPictures NONE = new(0, new HashSet<int>());
}
