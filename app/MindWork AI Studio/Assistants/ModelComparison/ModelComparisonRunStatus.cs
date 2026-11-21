namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Where a run of a batch stands, as its badge in the progress of the batch shows it.
/// </summary>
public enum ModelComparisonRunStatus
{
    /// <summary>
    /// Started, but waiting for a free slot among the runs which ask the models at the same time.
    /// </summary>
    QUEUED,

    /// <summary>
    /// Asking the models right now.
    /// </summary>
    RUNNING,

    /// <summary>
    /// Arrived, but at least one model gave no usable answer, so there is nothing to vote on.
    /// </summary>
    NO_USABLE_ANSWER,

    /// <summary>
    /// Arrived with both answers; the vote of the user is still to come.
    /// </summary>
    AWAITING_VOTE,

    /// <summary>
    /// The user voted on it.
    /// </summary>
    VOTED,

    /// <summary>
    /// Arrived with both answers, but the user moved past it without voting.
    /// </summary>
    VOTE_SKIPPED,
}