namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// Whether the runs decided so far say which of two models is better, see ModelComparisonWinShare.
/// </summary>
public enum ModelComparisonSignificance
{
    /// <summary>
    /// Too few runs, or too even a result, to tell yet.
    /// </summary>
    NOT_YET_CLEAR,

    /// <summary>
    /// The model wins clearly more often than it loses.
    /// </summary>
    CLEARLY_BETTER,

    /// <summary>
    /// The model loses clearly more often than it wins.
    /// </summary>
    CLEARLY_WORSE,
}