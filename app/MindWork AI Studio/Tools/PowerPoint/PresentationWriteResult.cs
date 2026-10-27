namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// What happened while writing a presentation, beyond writing it.
/// </summary>
/// <param name="SkippedPictures">The numbers of the pictures which could not be placed: unreadable, or in a format PowerPoint does not show. Their slots stay empty.</param>
internal sealed record PresentationWriteResult(IReadOnlyList<int> SkippedPictures);