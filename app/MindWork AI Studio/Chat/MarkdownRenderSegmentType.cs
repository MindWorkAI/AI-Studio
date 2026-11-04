namespace AIStudio.Chat;

/// <summary>
/// What a part of a message is, see MarkdownRenderPlan.
/// </summary>
internal enum MarkdownRenderSegmentType
{
    /// <summary>
    /// Markdown, which the chat renders with ExportableMarkdown.
    /// </summary>
    MARKDOWN,

    /// <summary>
    /// A formula of its own, which the chat renders with MathJaxBlock.
    /// </summary>
    MATH_BLOCK,
}