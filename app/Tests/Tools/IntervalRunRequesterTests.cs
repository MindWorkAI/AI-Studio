using AIStudio.Settings.DataModel;
using AIStudio.Tools.Services;
using AIStudio.Tools.Services.Indexing;

using Microsoft.Extensions.Logging.Abstractions;

namespace AIStudio.Tests.Tools;

/// <summary>
/// Checks the runs asked for at an interval, the way mailboxes are synced.
/// </summary>
/// <remarks>
/// A round is started by hand here, with the timer far off, except for the one test which waits for
/// the timer itself.
/// </remarks>
[TestFixture]
public sealed class IntervalRunRequesterTests
{
    private static readonly TimeSpan FAR_OFF = TimeSpan.FromHours(1);

    private static readonly DataSourceMailbox WORK = new() { Id = "4f2a6c1e-8b3d-4e5f-9a7c-1d2e3f4a5b6c", Name = "Work" };

    private static readonly DataSourceMailbox PRIVATE = new() { Id = "7c9e1a3b-5d2f-4a6e-8b0c-2e4f6a8c0d1e", Name = "Private" };

    [Test]
    public async Task ARoundAsksForEveryTrackedDataSource()
    {
        var requests = new List<(string Id, DataSourceEmbeddingRefreshMode Mode)>();
        using var requester = CreateRequester();

        requester.Track([WORK, PRIVATE], Record(requests));
        await requester.RequestRunsAsync();

        Assert.That(requests, Is.EquivalentTo(new[] { (WORK.Id, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK), (PRIVATE.Id, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK) }));
    }

    [Test]
    public async Task TrackingAgainReplacesWhatWasTracked()
    {
        var requests = new List<(string Id, DataSourceEmbeddingRefreshMode Mode)>();
        using var requester = CreateRequester();

        requester.Track([WORK, PRIVATE], Record(requests));
        requester.Track([PRIVATE], Record(requests));
        await requester.RequestRunsAsync();

        Assert.That(requests.Select(request => request.Id), Is.EqualTo(new[] { PRIVATE.Id }), "A deleted mailbox is still synced.");
    }

    [Test]
    public async Task AStoppedDataSourceIsNoLongerAskedFor()
    {
        var requests = new List<(string Id, DataSourceEmbeddingRefreshMode Mode)>();
        using var requester = CreateRequester();

        requester.Track([WORK, PRIVATE], Record(requests));
        requester.Stop(WORK.Id);
        await requester.RequestRunsAsync();

        requester.StopAll();
        await requester.RequestRunsAsync();

        Assert.That(requests.Select(request => request.Id), Is.EqualTo(new[] { PRIVATE.Id }));
    }

    [Test]
    public async Task AFailedRequestCostsOnlyItsOwnDataSourceTheRound()
    {
        var requestedIds = new List<string>();
        using var requester = CreateRequester();

        requester.Track([WORK, PRIVATE], (id, _) =>
        {
            requestedIds.Add(id);
            return id == WORK.Id ? Task.FromException(new InvalidOperationException("The queue is gone.")) : Task.CompletedTask;
        });

        await requester.RequestRunsAsync();
        Assert.That(requestedIds, Is.EquivalentTo(new[] { WORK.Id, PRIVATE.Id }));
    }

    [Test]
    public async Task TheFirstRoundComesWithoutWaitingForAWholeInterval()
    {
        var firstRequest = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var requester = new IntervalRunRequester(FAR_OFF, TimeSpan.FromMilliseconds(10), DataSourceEmbeddingRefreshMode.INTERVAL_CHECK, NullLogger.Instance);

        requester.Track([WORK], (id, _) =>
        {
            firstRequest.TrySetResult(id);
            return Task.CompletedTask;
        });

        Assert.That(await firstRequest.Task.WaitAsync(TimeSpan.FromSeconds(10)), Is.EqualTo(WORK.Id));
    }

    [Test]
    public void OnlyAFailedSignInKeepsTheRoundsAway()
    {
        var status = new DataSourceEmbeddingStatus(WORK.Id, WORK.Name, DataSourceType.MAILBOX, DataSourceEmbeddingState.FAILED, 0, 0, 0, string.Empty, "Signing in failed.", []);
        Assert.Multiple(() =>
        {
            Assert.That(DataSourceEmbeddingService.IsWaitingForSignIn(status with { Attention = DataSourceAttention.AUTH_FAILED }), Is.True);
            Assert.That(DataSourceEmbeddingService.IsWaitingForSignIn(status with { Attention = DataSourceAttention.MASS_REMOVAL_PENDING }), Is.False, "New mails have to keep coming while a removal waits for the user.");
            Assert.That(DataSourceEmbeddingService.IsWaitingForSignIn(status), Is.False, "A server out of reach is no reason to stop syncing.");
        });
    }

    private static IntervalRunRequester CreateRequester() => new(FAR_OFF, FAR_OFF, DataSourceEmbeddingRefreshMode.INTERVAL_CHECK, NullLogger.Instance);

    private static Func<string, DataSourceEmbeddingRefreshMode, Task> Record(List<(string Id, DataSourceEmbeddingRefreshMode Mode)> requests) => (id, mode) =>
    {
        requests.Add((id, mode));
        return Task.CompletedTask;
    };
}