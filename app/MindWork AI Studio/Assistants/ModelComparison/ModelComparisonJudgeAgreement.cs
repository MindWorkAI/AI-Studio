namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// How often a judge chose the same as the user, across the runs which hold both a vote and a verdict of this judge.
/// </summary>
public sealed record ModelComparisonJudgeAgreement
{
    /// <summary>
    /// The judge, with the name it had in its latest comparison.
    /// </summary>
    public required ModelComparisonModelIdentity Judge { get; init; }

    /// <summary>
    /// How many runs the judge chose the same as the user: the same model, or a tie both times.
    /// </summary>
    public int Agreements { get; init; }

    /// <summary>
    /// How many runs hold both a vote of the user and a verdict of this judge.
    /// </summary>
    public int ComparedRuns { get; init; }

    /// <summary>
    /// The share of the compared runs in which the judge chose the same as the user, or null without such runs.
    /// </summary>
    public double? Share => this.ComparedRuns == 0 ? null : (double)this.Agreements / this.ComparedRuns;
}