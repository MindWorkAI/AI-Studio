using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

using AIStudio.Tools;
using AIStudio.Tools.Rust;
using AIStudio.Tools.Services;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks how the log events of the .NET server reach the runtime.
/// </summary>
/// <remarks>
/// The runtime holds back its first answer here until the test lets it go, so the events pile up behind the
/// first batch, as they do during a burst.
/// </remarks>
[TestFixture]
public sealed partial class RustLogForwarderTests
{
    [Test]
    public async Task EventsArriveInBatchesAndInOrder()
    {
        const int EVENTS = 1_234;
        var runtime = new LogRuntime();
        var forwarder = CreateForwarder(runtime);

        forwarder.Enqueue(CreateEvent(0));
        await runtime.FirstRequestArrived;
        for (var number = 1; number < EVENTS; number++)
            forwarder.Enqueue(CreateEvent(number));

        runtime.Answer();
        await forwarder.StopAsync();

        var messages = runtime.Batches.SelectMany(batch => batch.Events).Select(logEvent => logEvent.Message).ToList();
        Assert.That(messages, Is.EqualTo(Enumerable.Range(0, EVENTS).Select(number => $"Event {number}")), "Events were lost or arrived out of order.");
        Assert.That(runtime.Batches.Max(batch => batch.Events.Count), Is.LessThanOrEqualTo(RustLogForwarder.MAX_BATCH_SIZE));
        Assert.That(runtime.Batches, Has.Count.EqualTo(1 + (EVENTS - 1 + RustLogForwarder.MAX_BATCH_SIZE - 1) / RustLogForwarder.MAX_BATCH_SIZE), "The waiting events were not sent in full batches.");
    }

    [Test]
    public async Task EventsBeyondTheCapacityAreDroppedAndReported()
    {
        const int EXTRA_EVENTS = 42;
        var runtime = new LogRuntime();
        var forwarder = CreateForwarder(runtime);

        forwarder.Enqueue(CreateEvent(0));
        await runtime.FirstRequestArrived;
        for (var number = 1; number <= RustLogForwarder.CAPACITY + EXTRA_EVENTS; number++)
            forwarder.Enqueue(CreateEvent(number));

        runtime.Answer();
        await forwarder.StopAsync();

        var events = runtime.Batches.SelectMany(batch => batch.Events).ToList();
        var notices = events.Where(logEvent => logEvent.Category == typeof(RustLogForwarder).FullName).ToList();
        Assert.That(notices, Has.Count.EqualTo(1), "The dropped events were not reported.");
        Assert.That(notices[0].Level, Is.EqualTo("Warning"));
        Assert.That(LostEvents(notices[0]), Is.EqualTo(EXTRA_EVENTS));
        Assert.That(events, Has.Count.EqualTo(1 + RustLogForwarder.CAPACITY + 1), "More or fewer events than the queue holds arrived.");
    }

    [Test]
    public async Task TheEventsOfAFailedBatchAreReportedAsLost()
    {
        var runtime = new LogRuntime(HttpStatusCode.InternalServerError);
        var forwarder = CreateForwarder(runtime);

        forwarder.Enqueue(CreateEvent(0));
        await runtime.FirstRequestArrived;
        forwarder.Enqueue(CreateEvent(1));

        runtime.Answer();
        await forwarder.StopAsync();

        var events = runtime.Batches.Single().Events;
        Assert.That(events[0].Message, Is.EqualTo("Event 1"));
        Assert.That(LostEvents(events[1]), Is.EqualTo(1), "The event of the failed batch was not reported as lost.");
    }

    private static RustLogForwarder CreateForwarder(LogRuntime runtime) => new(new HttpClient(runtime) { BaseAddress = new Uri("https://127.0.0.1") }, TimeSpan.Zero);

    private static LogEventRequest CreateEvent(int number) => new(TerminalLogger.FormatTransportTimestamp(DateTimeOffset.UtcNow), "Information", "Tests", $"Event {number}", null, null);

    private static int LostEvents(LogEventRequest notice)
    {
        var match = LostEventsPattern().Match(notice.Message);
        Assert.That(match.Success, Is.True, $"'{notice.Message}' is no notice about dropped events.");
        return int.Parse(match.Groups[1].Value);
    }

    [GeneratedRegex(@"^Dropped (\d+) log events ")]
    private static partial Regex LostEventsPattern();

    /// <summary>
    /// Plays the runtime: it holds back its first answer until the test lets it go, and records every batch
    /// it accepted.
    /// </summary>
    /// <param name="firstAnswer">How the runtime answers the first batch; every later batch succeeds.</param>
    private sealed class LogRuntime(HttpStatusCode firstAnswer = HttpStatusCode.OK) : HttpMessageHandler
    {
        private readonly TaskCompletionSource firstRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentQueue<LogEventsRequest> batches = new();
        private int requests;

        /// <summary>
        /// Ends once the first batch arrived.
        /// </summary>
        public Task FirstRequestArrived => this.firstRequest.Task;

        /// <summary>
        /// The batches the runtime accepted, in order.
        /// </summary>
        public IReadOnlyList<LogEventsRequest> Batches => [..this.batches];

        /// <summary>
        /// Lets the runtime answer the first batch, and every later one at once.
        /// </summary>
        public void Answer() => this.answer.TrySetResult();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath is not "/log/events")
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            var batch = await request.Content!.ReadFromJsonAsync<LogEventsRequest>(RustService.JSON_OPTIONS, cancellationToken);
            var isFirst = Interlocked.Increment(ref this.requests) == 1;
            this.firstRequest.TrySetResult();
            await this.answer.Task;

            var status = isFirst ? firstAnswer : HttpStatusCode.OK;
            if (status is HttpStatusCode.OK)
                this.batches.Enqueue(batch);

            return new HttpResponseMessage(status);
        }
    }
}