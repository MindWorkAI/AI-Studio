namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Starts the same comparison several times in a row, to see how consistently the two models -- and
/// an optional judge -- come out the same way.
/// </summary>
/// <remarks>
/// Each run is independent: its own pair of answers, its own random presentation order, and its own
/// judge verdict, because that variation between runs is exactly what a batch is meant to surface. A
/// single, shared draw would defeat the point.
///
/// This starts every run at once and hands back one task per run, rather than awaiting them itself:
/// the caller decides whether to wait for all of them together (<c>Task.WhenAll</c>) or to react to
/// each one as it finishes (<c>Task.WhenEach</c>), for example to reveal runs to the user as they
/// become ready instead of only once the slowest of them has answered. "Started" only means the task
/// exists and is queued, though: <see cref="MAX_CONCURRENT_RUNS"/> of them are actually asking a
/// model at any one time, never more, because a whole batch's worth of requests landing on a model at
/// once is exactly what queues up and slows every one of them down on a provider with limited
/// concurrent capacity -- a self-hosted one most of all. A model comparison is not something run many
/// times a day, so trading wall-clock time for going easier on the model is the right default.
///
/// <see cref="Start"/> is called from a Blazor Server page, on its own synchronisation context, so
/// <see cref="RunOneAsync"/> uses <c>ConfigureAwait(false)</c> throughout, the same as
/// <see cref="ModelComparisonRunner"/> does further down: none of this is UI work, and resuming on
/// that context regardless would only queue up behind whatever the page itself is waiting to do,
/// a click included.
/// </remarks>
/// <param name="runner">Runs one comparison. Reused for every run of a batch, since the two compared
/// models and the judge are stateless requests, not a resource a run could use up.</param>
public sealed class ModelComparisonBatchRunner(ModelComparisonRunner runner)
{
    /// <summary>
    /// How many runs may be actually asking a model at once. Fixed, not a setting: this exists to
    /// protect whatever the batch is running against, not to be tuned per comparison. Public so the
    /// UI can show which of the runs still outstanding are actually in flight right now, rather than
    /// still queued behind this same limit.
    /// </summary>
    public const int MAX_CONCURRENT_RUNS = 3;

    private readonly SemaphoreSlim concurrencyLimit = new(MAX_CONCURRENT_RUNS, MAX_CONCURRENT_RUNS);

    /// <param name="first">The first model, asked fresh in every run.</param>
    /// <param name="second">The second model, asked fresh in every run.</param>
    /// <param name="request">What both models are asked, the same in every run.</param>
    /// <param name="runCount">How many independent runs to start. At least one.</param>
    /// <param name="token">Cancels every run of the batch, in flight or still queued.</param>
    /// <param name="judge">The optional judge, asked in every run once its two answers are in.</param>
    /// <param name="judgeInstructions">What the user wants the judge to pay attention to. Ignored without a <paramref name="judge"/>.</param>
    /// <returns>One task per run, already started, in no particular order of completion.</returns>
    public IReadOnlyList<Task<ModelComparisonBatchEntry>> Start(
        ModelComparisonParticipant first,
        ModelComparisonParticipant second,
        ModelComparisonRequest request,
        int runCount,
        CancellationToken token,
        ModelComparisonParticipant? judge = null,
        string judgeInstructions = "")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(runCount, 1);

        var tasks = new Task<ModelComparisonBatchEntry>[runCount];
        for (var i = 0; i < runCount; i++)
            tasks[i] = this.RunOneAsync(first, second, request, token, judge, judgeInstructions);

        return tasks;
    }

    /// <summary>
    /// Waits for a free slot among <see cref="MAX_CONCURRENT_RUNS"/>, then runs one comparison.
    /// </summary>
    /// <remarks>
    /// Never throws, a queued run included: cancellation while still waiting for a slot is reported
    /// the same way <see cref="ModelComparisonRunner"/> reports a model which did not answer, not as
    /// a fault on the task -- every run in a batch is meant to end up as an entry to show, whatever
    /// happened to it.
    /// </remarks>
    private async Task<ModelComparisonBatchEntry> RunOneAsync(
        ModelComparisonParticipant first,
        ModelComparisonParticipant second,
        ModelComparisonRequest request,
        CancellationToken token,
        ModelComparisonParticipant? judge,
        string judgeInstructions)
    {
        var presentationOrder = Random.Shared.Next(2) is 0
            ? ModelComparisonPresentationOrder.FIRST_MODEL_FIRST
            : ModelComparisonPresentationOrder.SECOND_MODEL_FIRST;

        try
        {
            await this.concurrencyLimit.WaitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new ModelComparisonBatchEntry
            {
                PresentationOrder = presentationOrder,
                Result = new ModelComparisonRunResult
                {
                    First = NotAnswered(first.Label),
                    Second = NotAnswered(second.Label),
                },
            };
        }

        try
        {
            var result = await runner.RunAsync(first, second, request, token, presentationOrder, judge, judgeInstructions).ConfigureAwait(false);
            return new ModelComparisonBatchEntry
            {
                PresentationOrder = presentationOrder,
                Result = result,
            };
        }
        finally
        {
            this.concurrencyLimit.Release();
        }
    }

    private static ModelComparisonAnswer NotAnswered(string label) => new()
    {
        Label = label,
        Text = string.Empty,
        Completed = false,
    };
}
