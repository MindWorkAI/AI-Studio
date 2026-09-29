namespace AIStudio.Provider;

/// <summary>
/// One event of a server-sent event stream, as it came off the wire.
/// </summary>
/// <remarks>
/// The raw line travels next to its payload because not every decision can be made from the
/// payload alone: the Responses API, for one, ends its stream with an "event:" line which carries
/// no payload at all.
/// </remarks>
/// <param name="Line">The line as it arrived, including its "data:" prefix when it had one.</param>
/// <param name="Data">The payload of a data line, empty for every other kind of line.</param>
public readonly record struct ServerSentEvent(string Line, string Data);