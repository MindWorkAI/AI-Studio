namespace AIStudio.Provider;

/// <summary>
/// Tells whoever read a stream of server-sent events whether the provider closed it.
/// </summary>
/// <remarks>
/// The shared stream reader ends quietly in four ways: the provider closed the stream, the user
/// stopped the answer, a timeout struck, or the connection broke. Only the first one is the end of
/// an answer. In the others, the reader has already told the user what went wrong, and a stop is no
/// error at all. Whoever puts a result together from the events, such as a round of the tool calling
/// loop, has to tell these apart, and the events alone cannot: some gateways close a complete answer
/// without any closing line.<br/><br/>
/// An async iterator returns nothing besides its items, so the caller hands this object to the
/// reader, which marks it, and reads it once the enumeration is over.
/// </remarks>
public sealed class ServerSentEventStreamEnd
{
    /// <summary>
    /// Whether the provider closed the stream. False while the stream runs, and for good once a stop,
    /// a timeout, or a broken connection cut it off.
    /// </summary>
    public bool IsReached { get; private set; }

    /// <summary>
    /// Notes that the provider closed the stream.
    /// </summary>
    public void MarkReached() => this.IsReached = true;
}