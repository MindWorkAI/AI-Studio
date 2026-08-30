namespace AIStudio.Chat;

public static class IContentExtensions
{
    /// <summary>
    /// Detaches whoever listens to the stream of this content.
    /// </summary>
    /// <remarks>
    /// The streaming handlers are closures over the component which registered them. A content
    /// object belongs to the chat thread and therefore outlives every component which renders it,
    /// so handlers left behind would keep those components alive for as long as the thread exists.
    /// Whoever registers a handler calls this when it is no longer needed.
    /// </remarks>
    /// <param name="content">The content whose streaming handlers you want to detach.</param>
    public static void ResetStreamingHandlers(this IContent content)
    {
        content.StreamingEvent = IContent.NO_STREAMING_HANDLER;
        content.StreamingDone = IContent.NO_STREAMING_HANDLER;
    }
}