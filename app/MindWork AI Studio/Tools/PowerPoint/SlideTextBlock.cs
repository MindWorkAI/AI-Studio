namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// The content of one text slot.
/// </summary>
/// <param name="Paragraphs">The paragraphs, in order.</param>
internal sealed record SlideTextBlock(IReadOnlyList<SlideParagraph> Paragraphs);