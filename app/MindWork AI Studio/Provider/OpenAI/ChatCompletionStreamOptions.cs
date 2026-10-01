namespace AIStudio.Provider.OpenAI;

/// <summary>
/// What a streamed chat completion should report beyond its content.
/// </summary>
/// <remarks>
/// An OpenAI-compatible provider says nothing about what a streamed request cost unless it is asked
/// to. Without this block, the stream simply ends and the only token number anybody ever sees is
/// the one AI Studio estimated for itself.
/// </remarks>
/// <param name="IncludeUsage">Whether the stream should end with a line stating the token usage.</param>
public sealed record ChatCompletionStreamOptions(bool IncludeUsage)
{
    /// <summary>
    /// Asks for the usage line.
    /// </summary>
    public static readonly ChatCompletionStreamOptions INCLUDE_USAGE = new(true);
}