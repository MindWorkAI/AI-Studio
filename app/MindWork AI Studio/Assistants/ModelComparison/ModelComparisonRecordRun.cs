namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// One run of a stored comparison, in the terms of the models rather than the columns: the columns were drawn fresh
/// for every run and mean nothing once the run is over.
/// </summary>
public sealed record ModelComparisonRecordRun
{
    /// <summary>
    /// The model the user preferred, a tie, or null when the user did not vote on the run.
    /// </summary>
    public ModelComparisonModelChoice? Vote { get; set; }

    /// <summary>
    /// The model the judge preferred, a tie, or null without a usable verdict of a judge.
    /// </summary>
    public ModelComparisonModelChoice? JudgeChoice { get; set; }

    /// <summary>
    /// How the first model did.
    /// </summary>
    public ModelComparisonRecordAnswer First { get; set; } = new();

    /// <summary>
    /// How the second model did.
    /// </summary>
    public ModelComparisonRecordAnswer Second { get; set; } = new();
}