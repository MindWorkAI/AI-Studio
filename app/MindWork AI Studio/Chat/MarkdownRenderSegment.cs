namespace AIStudio.Chat;

/// <summary>
/// A part of a message which the chat renders on its own, see MarkdownRenderPlan.
/// </summary>
/// <param name="type">What the part is.</param>
/// <param name="start">Where the part starts in the source of its plan.</param>
/// <param name="length">How long the part is.</param>
internal sealed class MarkdownRenderSegment(MarkdownRenderSegmentType type, int start, int length)
{
    private string? cachedContent;

    public MarkdownRenderSegmentType Type { get; } = type;

    public int Start { get; } = start;

    public int Length { get; } = length;

    public int RenderKey { get; } = HashCode.Combine(type, start, length);

    public string GetContent(string source)
    {
        if (this.cachedContent is not null)
            return this.cachedContent;

        this.cachedContent = this.Start == 0 && this.Length == source.Length
            ? source
            : source.Substring(this.Start, this.Length);

        return this.cachedContent;
    }
}