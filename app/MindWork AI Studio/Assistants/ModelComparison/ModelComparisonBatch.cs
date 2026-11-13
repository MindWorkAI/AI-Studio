namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// The runs of one batch as the user goes through them: which arrived, which the user voted on, and which run the user
/// looks at right now.
/// </summary>
/// <remarks>
/// The runs are kept in the order they arrived, not the order they were started in: every run asks the same two models
/// the same question, and nothing tells "run 3" from "run 7" beyond how long each happened to take. They arrive one at
/// a time, so the user can vote on the first one without waiting for the slowest.
///
/// Not thread-safe. The assistant changes and reads it on the dispatcher of its page only.
/// </remarks>
/// <param name="runCount">How many runs the batch makes.</param>
public sealed class ModelComparisonBatch(int runCount)
{
    private readonly List<ModelComparisonBatchEntry> entries = [];

    /// <summary>
    /// The vote of the user for each run, at the same index as its entry. Null while the vote is still to come, or when
    /// there is none.
    /// </summary>
    private readonly List<ModelComparisonVote?> votes = [];

    /// <summary>
    /// How many runs the batch makes.
    /// </summary>
    public int RunCount { get; } = runCount;

    /// <summary>
    /// The runs which arrived so far, in the order they arrived.
    /// </summary>
    public IReadOnlyList<ModelComparisonBatchEntry> Entries => this.entries;

    /// <summary>
    /// Which run the user looks at right now, by its position. Moves on only once the vote is in, or once the user chose
    /// to skip the run, so it never passes a run the user has not seen; only SkipRemainingVotes and Settle jump to the end.
    /// </summary>
    public int CurrentIndex { get; private set; }

    /// <summary>
    /// Whether the user chose to stop voting on the runs of this batch. Every run still arrives, and the judge, if there
    /// is one, still judges it, but none is shown on its own anymore: the summary appears once the batch has settled.
    /// </summary>
    public bool SkipsRemainingVotes { get; private set; }

    /// <summary>
    /// The run the user looks at right now, or null before it arrived and once the user is through.
    /// </summary>
    public ModelComparisonBatchEntry? CurrentEntry => this.CurrentIndex < this.entries.Count ? this.entries[this.CurrentIndex] : null;

    /// <summary>
    /// The vote of the user for CurrentEntry, or null while it is still to come.
    /// </summary>
    public ModelComparisonVote? CurrentVote => this.CurrentIndex < this.votes.Count ? this.votes[this.CurrentIndex] : null;

    /// <summary>
    /// Whether the user is through with every run of the batch.
    /// </summary>
    public bool IsFinished => this.CurrentIndex >= this.RunCount;

    /// <summary>
    /// Whether CurrentEntry is the last run of the batch, so that the button which moves on says "Finish" instead of
    /// "Next run". Both do the same: after the last run, the summary follows.
    /// </summary>
    public bool IsLastEntry => this.CurrentIndex + 1 >= this.RunCount;

    /// <summary>
    /// Adds a run which arrived.
    /// </summary>
    /// <param name="entry">The run.</param>
    /// <returns>Whether the run is the one the user looks at now, so what is shown of it has to be built.</returns>
    public bool Add(ModelComparisonBatchEntry entry)
    {
        this.entries.Add(entry);
        this.votes.Add(null);
        return this.entries.Count - 1 == this.CurrentIndex;
    }

    /// <summary>
    /// Takes the vote of the user for CurrentEntry.
    /// </summary>
    /// <remarks>
    /// The vote is final. A second click, or a click on another button, changes nothing: once the names are shown, the
    /// answers can no longer be judged without knowing them. A run without both answers has nothing to vote on.
    /// </remarks>
    /// <param name="vote">The column the user preferred, or a tie.</param>
    /// <returns>Whether the vote was taken.</returns>
    public bool Vote(ModelComparisonVote vote)
    {
        if (this.CurrentVote is not null || this.CurrentEntry is not { Result.BothCompleted: true })
            return false;

        this.votes[this.CurrentIndex] = vote;
        return true;
    }

    /// <summary>
    /// Moves on to the next run, once the user voted on the current one, or right away when the current run has
    /// nothing to vote on.
    /// </summary>
    /// <returns>Whether the batch moved on.</returns>
    public bool GoToNextEntry()
    {
        if (this.CurrentVote is null && this.CurrentEntry is { Result.BothCompleted: true })
            return false;

        this.CurrentIndex++;
        return true;
    }

    /// <summary>
    /// Moves on to the next run without a vote on the current one, although it has something to vote on: the choice
    /// of the user, unlike the silent move in GoToNextEntry past a run with nothing to vote on.
    /// </summary>
    public void SkipThisRun() => this.CurrentIndex++;

    /// <summary>
    /// Stops asking for votes on the runs which are left.
    /// </summary>
    /// <param name="runsStillArriving">Whether runs are still on their way. Then Settle jumps to the summary once the last one arrived; otherwise, nothing is left to wait for.</param>
    public void SkipRemainingVotes(bool runsStillArriving)
    {
        this.SkipsRemainingVotes = true;
        if (!runsStillArriving)
            this.CurrentIndex = this.RunCount;
    }

    /// <summary>
    /// Ends the batch once no run is left on its way.
    /// </summary>
    /// <remarks>
    /// A stopped batch goes straight to the summary, the same as one whose remaining votes the user skipped: there is
    /// no point in asking for votes on runs the user chose to stop waiting for.
    /// </remarks>
    /// <param name="canceled">Whether the user stopped the batch.</param>
    public void Settle(bool canceled)
    {
        if (this.SkipsRemainingVotes || canceled)
            this.CurrentIndex = this.RunCount;
    }

    /// <summary>
    /// Where a run stands, for its badge in the progress of the batch.
    /// </summary>
    /// <remarks>
    /// Nothing tracks which run is which once all are started: they are interchangeable, and only how many arrived is
    /// known. So the status of a run which did not arrive yet counts positions, not identities: the runs nearest to
    /// arriving are shown as the ones asking the models right now, which is true in substance even when it is not true
    /// of that exact run.
    /// </remarks>
    /// <param name="index">The position of the run, zero-based.</param>
    /// <returns>The status.</returns>
    public ModelComparisonRunStatus StatusOf(int index)
    {
        if (index >= this.entries.Count)
            return index < this.entries.Count + ModelComparisonBatchRunner.MAX_CONCURRENT_RUNS
                ? ModelComparisonRunStatus.RUNNING
                : ModelComparisonRunStatus.QUEUED;

        if (!this.entries[index].Result.BothCompleted)
            return ModelComparisonRunStatus.NO_USABLE_ANSWER;

        if (this.votes[index] is not null)
            return ModelComparisonRunStatus.VOTED;

        return index < this.CurrentIndex
            ? ModelComparisonRunStatus.VOTE_SKIPPED
            : ModelComparisonRunStatus.AWAITING_VOTE;
    }

    /// <summary>
    /// Whether the run at this position arrived with a usable verdict of the judge.
    /// </summary>
    /// <param name="index">The position of the run, zero-based.</param>
    public bool HasJudgeVerdict(int index) => index < this.entries.Count && this.entries[index].Result.Judge is { Completed: true };

    /// <summary>
    /// Counts the votes of the user across the runs which arrived.
    /// </summary>
    public ModelComparisonTally TallyVotes() => ModelComparisonTally.Of(
    [
        .. this.entries.Select((entry, index) => this.votes[index] is { } vote
            ? entry.PresentationOrder.ToModelChoice(vote)
            : (ModelComparisonModelChoice?)null),
    ]);

    /// <summary>
    /// Counts the verdicts of the judge across the runs which arrived.
    /// </summary>
    public ModelComparisonTally TallyJudgeVerdicts() => ModelComparisonTally.Of(
    [
        .. this.entries.Select(entry => entry.Result.Judge is { Completed: true, Preferred: { } preferred }
            ? entry.PresentationOrder.ToModelChoice(preferred)
            : (ModelComparisonModelChoice?)null),
    ]);

    /// <summary>
    /// Copies the batch, for a snapshot of the assistant session: a snapshot must not change with the batch it was
    /// taken of. The entries themselves are shared, since nothing changes them.
    /// </summary>
    /// <returns>The copy.</returns>
    public ModelComparisonBatch Copy()
    {
        var copy = new ModelComparisonBatch(this.RunCount)
        {
            CurrentIndex = this.CurrentIndex,
            SkipsRemainingVotes = this.SkipsRemainingVotes,
        };

        copy.entries.AddRange(this.entries);
        copy.votes.AddRange(this.votes);
        return copy;
    }
}