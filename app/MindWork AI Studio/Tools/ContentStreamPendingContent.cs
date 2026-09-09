namespace AIStudio.Tools;

/// <summary>
/// Content which a reader held back, together with the token count of exactly that content.
/// </summary>
/// <remarks>
/// Readers which assemble a page or a slide from several stream events cannot pass their content
/// on right away. Its token count has to travel with it: the count describes the content, not the
/// event which happened to arrive at the moment the content was released. Keeping the two together
/// is what stops a page from being sized by the text of the page after it.
/// </remarks>
/// <param name="Content">The assembled content.</param>
/// <param name="TokenCount">The number of tokens of that content, or null when it is unknown.</param>
public readonly record struct ContentStreamPendingContent(string Content, int? TokenCount)
{
    /// <summary>
    /// Adds up two token counts, where an unknown count makes the sum unknown as well.
    /// </summary>
    /// <remarks>
    /// A partial sum would understate the whole and would let the chunking size a chunk by a part
    /// of what it holds. Reporting the count as unknown is the honest answer, because the caller
    /// can still count the content itself.
    /// </remarks>
    /// <param name="left">The first count, or null when it is unknown.</param>
    /// <param name="right">The second count, or null when it is unknown.</param>
    /// <returns>The sum, or null when either count is unknown.</returns>
    public static int? AddTokenCounts(int? left, int? right) => left is null || right is null ? null : left + right;
}