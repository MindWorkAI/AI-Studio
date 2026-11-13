namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What became of creating a presentation file.
/// </summary>
/// <param name="IsWritten">Whether the presentation was written. It is not when the template changed since the slides were planned, and they no longer fit it; the target stays untouched then.</param>
/// <param name="SkippedPictures">The numbers of the pictures which could not be placed: unreadable, or in a format PowerPoint does not show. Their slots stay empty.</param>
internal sealed record PresentationFileResult(bool IsWritten, IReadOnlyList<int> SkippedPictures)
{
    /// <summary>
    /// The template changed since the slides were planned, and they no longer fit it. Nothing was written.
    /// </summary>
    public static readonly PresentationFileResult TEMPLATE_CHANGED = new(false, []);
}