namespace AIStudio.Provider;

/// <summary>
/// A contract for a streamed response line that may contain content and annotations.
/// </summary>
public interface IResponseStreamLine : IAnnotationStreamLine
{
    /// <summary>
    /// Checks if the response line contains any content.
    /// </summary>
    /// <returns>True when the response line contains content, false otherwise.</returns>
    public bool ContainsContent();
    
    /// <summary>
    /// Gets the content of the response line.
    /// </summary>
    /// <returns>The content of the response line.</returns>
    public ContentStreamChunk GetContent();

    /// <summary>
    /// Gets what the provider said the request cost.
    /// </summary>
    /// <remarks>
    /// Answered here for every wire format which says nothing about it, which is most of them: a
    /// provider who reports no usage is the normal case, not a gap somebody has to fill in.
    ///
    /// Unlike content and sources, there is no separate check for whether a line carries it. This
    /// never fails on a line without one, and whether the answer means anything is what IsKnown of
    /// the returned usage says.
    /// </remarks>
    /// <returns>The usage, or TokenUsage.UNKNOWN when the line carries none.</returns>
    public TokenUsage GetUsage() => TokenUsage.UNKNOWN;
}