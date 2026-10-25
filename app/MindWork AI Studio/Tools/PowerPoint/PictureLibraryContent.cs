namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What a picture library offers for one request.
/// </summary>
/// <param name="Pictures">The pictures the LLM may choose from, sorted by name: all of them, or a random sample of a large library.</param>
/// <param name="PictureCount">How many pictures the folder holds in all.</param>
/// <param name="IconCount">How many of them are icons.</param>
/// <param name="SkippedFileCount">How many files the folder holds which are neither PNG nor JPEG, e.g., SVG icons.</param>
internal sealed record PictureLibraryContent(IReadOnlyList<LibraryPicture> Pictures, int PictureCount, int IconCount, int SkippedFileCount)
{
    /// <summary>
    /// No library at all.
    /// </summary>
    public static readonly PictureLibraryContent NONE = new([], 0, 0, 0);

    /// <summary>
    /// Whether the folder holds more pictures than the LLM reads about, so that it gets a random sample.
    /// </summary>
    public bool IsSampled => this.PictureCount > this.Pictures.Count;
}
