namespace AIStudio.Chat;

/// <summary>
/// How the chat renders the text of a message: in parts, Markdown and the formulas between them, see
/// MarkdownRenderPlanner.
/// </summary>
/// <param name="Source">The text as the chat renders it. The parts refer to it.</param>
/// <param name="Segments">The parts, in the order of the text.</param>
internal sealed record MarkdownRenderPlan(string Source, IReadOnlyList<MarkdownRenderSegment> Segments)
{
    public static readonly MarkdownRenderPlan EMPTY = new(string.Empty, []);
}