using System.Threading.Channels;

using AIStudio.Tools.Rust;

namespace AIStudio.Tools.Services;

/// <summary>
/// Forwards the log events of the .NET server to the runtime, in batches and in the order they happened.
/// </summary>
/// <remarks>
/// With one request per event, a burst of events, such as the changes of a build in a watched folder, put
/// thousands of requests on the HTTP/2 connection to the runtime at once. The runtime fell minutes behind,
/// and once the server gave up on more than 20 of them, the runtime closed the connection which all calls
/// of the server share. Therefore, a single sender takes the waiting events off a bounded queue and sends up
/// to MAX_BATCH_SIZE of them per request, one request at a time, over a client of its own. When the runtime
/// cannot keep up, the queue fills up, further events are dropped, and the next batch says how many.
/// </remarks>
public sealed class RustLogForwarder : IDisposable
{
    /// <summary>
    /// How many events wait for the runtime at most.
    /// </summary>
    public const int CAPACITY = 10_000;

    /// <summary>
    /// How many events one request carries at most.
    /// </summary>
    public const int MAX_BATCH_SIZE = 500;

    /// <summary>
    /// How long the sender waits after a batch failed, so a runtime which is gone is not asked again and again.
    /// </summary>
    public static readonly TimeSpan PAUSE_AFTER_FAILURE = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How long disposing waits for the waiting events to reach the runtime.
    /// </summary>
    private static readonly TimeSpan DRAIN_TIMEOUT = TimeSpan.FromSeconds(2);

    private readonly Channel<LogEventRequest> queue = Channel.CreateBounded<LogEventRequest>(new BoundedChannelOptions(CAPACITY) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly HttpClient http;
    private readonly TimeSpan pauseAfterFailure;
    private readonly Task sender;

    private int lostEvents;

    /// <param name="http">The client which sends the batches. It needs a connection of its own, so the log events never wait in front of other calls.</param>
    /// <param name="pauseAfterFailure">How long the sender waits after a batch failed.</param>
    public RustLogForwarder(HttpClient http, TimeSpan pauseAfterFailure)
    {
        this.http = http;
        this.pauseAfterFailure = pauseAfterFailure;
        this.sender = Task.Run(this.SendAsync);
    }

    /// <summary>
    /// Queues a log event for the runtime. Never waits: when the queue is full, the event is dropped and counted.
    /// </summary>
    /// <param name="logEvent">The log event.</param>
    public void Enqueue(LogEventRequest logEvent)
    {
        if (!this.queue.Writer.TryWrite(logEvent))
            Interlocked.Increment(ref this.lostEvents);
    }

    /// <summary>
    /// Accepts no further events and sends the waiting ones.
    /// </summary>
    /// <returns>A task which ends once every waiting event was sent or lost.</returns>
    public Task StopAsync()
    {
        this.queue.Writer.TryComplete();
        return this.sender;
    }

    public void Dispose()
    {
        // Gives the waiting events a moment to reach the runtime before the server ends:
        this.StopAsync().Wait(DRAIN_TIMEOUT);
    }

    private async Task SendAsync()
    {
        var reader = this.queue.Reader;
        var batch = new List<LogEventRequest>(MAX_BATCH_SIZE + 1);
        while (await reader.WaitToReadAsync())
        {
            batch.Clear();
            while (batch.Count < MAX_BATCH_SIZE && reader.TryRead(out var logEvent))
                batch.Add(logEvent);

            var eventsInBatch = batch.Count;
            var lostBefore = Interlocked.Exchange(ref this.lostEvents, 0);
            if (lostBefore > 0)
                batch.Add(CreateLostEventsNotice(lostBefore));

            if (await this.TrySendAsync(batch))
                continue;

            // The events of this batch are lost as well, and the next batch reports them together:
            Interlocked.Add(ref this.lostEvents, lostBefore + eventsInBatch);
            await Task.Delay(this.pauseAfterFailure);
        }
    }

    private async Task<bool> TrySendAsync(List<LogEventRequest> batch)
    {
        //
        // Nothing here may use a logger: logging means sending another event through this very
        // forwarder, and a broken connection to the runtime would feed itself. The console is the
        // only way left, which the developer sees while the server runs from the IDE.
        //
        try
        {
            using var response = await this.http.PostAsJsonAsync("/log/events", new LogEventsRequest(batch), RustService.JSON_OPTIONS);
            if (response.IsSuccessStatusCode)
                return true;

            Console.WriteLine($"Failed to send {batch.Count} log events to the runtime: '{response.StatusCode}'.");
            return false;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to send {batch.Count} log events to the runtime: {TerminalLogger.DescribeException(e)}");
            return false;
        }
    }

    private static LogEventRequest CreateLostEventsNotice(int lostEvents) => new(
        TerminalLogger.FormatTransportTimestamp(DateTimeOffset.UtcNow),
        nameof(LogLevel.Warning),
        typeof(RustLogForwarder).FullName!,
        $"Dropped {lostEvents} log events because the runtime could not take them in time.",
        null,
        null);
}