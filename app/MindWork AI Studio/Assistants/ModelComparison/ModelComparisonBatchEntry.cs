namespace AIStudio.Assistants.ModelComparison;

/// <summary>
/// One run within a batch: its own random presentation order, and what came of it.
/// </summary>
/// <remarks>
/// The presentation order travels with its result because it takes both to translate a
/// <see cref="ModelComparisonVote"/> -- the user's own vote, or the judge's -- back into a model: the
/// same column means a different model in every run.
/// </remarks>
public sealed record ModelComparisonBatchEntry
{
    public required ModelComparisonPresentationOrder PresentationOrder { get; init; }

    public required ModelComparisonRunResult Result { get; init; }
}
