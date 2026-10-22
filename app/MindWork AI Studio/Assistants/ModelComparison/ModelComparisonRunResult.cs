namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// What a comparison run brought back: the answers of the two models.
/// </summary>
public sealed record ModelComparisonRunResult
{
    /// <summary>
    /// When the run ended. Both answers are shown with this one time: a time of their own would
    /// tell the faster model from the slower one.
    /// </summary>
    public DateTimeOffset Time { get; init; } = DateTimeOffset.Now;

    public required ModelComparisonAnswer First { get; init; }

    public required ModelComparisonAnswer Second { get; init; }

    /// <summary>
    /// What an optional judge said about the two answers, or null when no judge was asked, or when
    /// there was nothing for it to judge because at least one model did not answer.
    /// </summary>
    public ModelComparisonJudgeVerdict? Judge { get; init; }

    /// <summary>
    /// Whether both models answered. Only then there is anything to vote on: a vote between an
    /// answer and a failure would say nothing about the models.
    /// </summary>
    public bool BothCompleted => this.First.Completed && this.Second.Completed;
}