namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// One paragraph of a text slot: a bullet point, or a sub-point of one.
/// </summary>
/// <param name="Text">The text, without a bullet character; the template draws the bullets.</param>
/// <param name="Level">The list level, 0 for a top-level point.</param>
internal readonly record struct SlideParagraph(string Text, int Level);