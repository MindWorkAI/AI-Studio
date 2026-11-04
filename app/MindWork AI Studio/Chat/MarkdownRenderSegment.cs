namespace AIStudio.Chat;

/// <summary>
/// A part of a message which the chat renders on its own, see MarkdownRenderPlan.
/// </summary>
/// <param name="type">What the part is.</param>
/// <param name="start">Where the part starts in the source of its plan.</param>
/// <param name="length">How long the part is.</param>
/// <param name="fileStart">Where the block of the part starts in the source, its markers included,
/// such as the $$ before a formula. A formula finds its file by it, see PlainFileExport.ExtractFiles.</param>
internal sealed class MarkdownRenderSegment(MarkdownRenderSegmentType type, int start, int length, int fileStart)
{
    private string? cachedContent;

    public MarkdownRenderSegmentType Type { get; } = type;

    public int Start { get; } = start;

    public int Length { get; } = length;

    public int FileStart { get; } = fileStart;

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